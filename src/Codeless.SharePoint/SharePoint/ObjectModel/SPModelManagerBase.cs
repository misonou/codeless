using Codeless.SharePoint.Internal;
using Codeless.SharePoint.Publishing;
using Microsoft.Office.DocumentManagement.DocumentSets;
using Microsoft.Office.Server.Search.Query;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Administration;
using Microsoft.SharePoint.Publishing;
using Microsoft.SharePoint.Taxonomy;
using Microsoft.SharePoint.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Codeless.SharePoint.ObjectModel {
  #region Exceptions
  /// <summary>
  /// Throws when error has occurred when executing query against SharePoint or Office search service.
  /// </summary>
  public class SPModelQueryException : Exception {
    internal SPModelQueryException(SPWeb web, Exception ex, string queryText)
      : base(String.Format("{2}. {0}. {1}", web.Url, queryText, ex.Message.TrimEnd('.')), ex) {
      this.QueryText = queryText;
      this.WebUrl = web.Url;
    }

    /// <summary>
    /// Gets the query expression that caused the exception.
    /// </summary>
    public string QueryText { get; private set; }

    /// <summary>
    /// Gets the site URL where the query is executed against.
    /// </summary>
    public string WebUrl { get; private set; }
  }
  #endregion

  #region Enums
  /// <summary>
  /// Represents which API will be used when performing item queries if no search keywords are specified.
  /// </summary>
  public enum SPModelImplicitQueryMode {
    /// <summary>
    /// No actual queries will be performed.
    /// </summary>
    None,
    /// <summary>
    /// Queries will be performed using <see cref="SPList.GetItems(SPQuery)"/>.
    /// </summary>
    ListQuery,
    /// <summary>
    /// Queries will be performed using <see cref="SPWeb.GetSiteData"/>.
    /// </summary>
    SiteQuery,
    /// <summary>
    /// Queries will be performed using FAST search infrastructure.
    /// </summary>
    KeywordSearch
  }

  /// <summary>
  /// Specifies how a list item is saved when committing changes.
  /// </summary>
  public enum SPModelCommitMode {
    /// <summary>
    /// The list item is updated by creating a new version of the item.
    /// </summary>
    Default,
    /// <summary>
    /// The list item is updated without effecting changes in the Modified or Modified By fields.
    /// </summary>
    SystemUpdate,
    /// <summary>
    /// The list item is updated without effecting changes in the Modified or Modified By fields, and without creating another version of the item.
    /// </summary>
    SystemUpdateOverwriteVersion,
    /// <summary>
    /// The list item is updated without creating another version of the item.
    /// </summary>
    OverwriteVersion
  }

  /// <summary>
  /// Specifies operation to be done on a file in a SharePoint web site.
  /// </summary>
  public enum SPModelFileOperation {
    /// <summary>
    /// Checks in the file to a document library and increments as a minor version.
    /// </summary>
    MinorCheckIn,
    /// <summary>
    /// Checks in the file to a document library and increments as a major version.
    /// </summary>
    MajorCheckIn,
    /// <summary>
    /// Checks in the file to a document library and overwrite the file.
    /// </summary>
    OverwriteCheckIn,
    /// <summary>
    /// Checks the file out of a document library.
    /// </summary>
    CheckOut,
    /// <summary>
    /// Undoes the file checkout.
    /// </summary>
    UndoCheckOut,
    /// <summary>
    /// Submits the file for content approval.
    /// </summary>
    Publish,
    /// <summary>
    /// Removes the file from content approval.
    /// </summary>
    UnPublish,
    /// <summary>
    /// Approves the file submitted for content approval.
    /// </summary>
    Approve,
    /// <summary>
    /// Denies approval for a file that was submitted for content approval.
    /// </summary>
    Deny,
    /// <summary>
    /// Takes the most current approved or major version of the file offline.
    /// </summary>
    TakeOffline
  }
  #endregion

  #region EventArgs
  /// <summary>
  /// Provides data when an ExecutingListQuery event is triggered from <see cref="SPModelManagerBase{T}"/>.
  /// See <see cref="SPModelManagerBase{T}.OnExecutingListQuery"/>.
  /// </summary>
  public class SPModelListQueryEventArgs : EventArgs {
    /// <summary>
    /// Gets an <see cref="SPQuery"/> instance that will be executed against a list.
    /// </summary>
    public SPQuery Query { get; internal set; }
  }

  /// <summary>
  /// Provides data when an ExecutingSiteQuery event is triggered from <see cref="SPModelManagerBase{T}"/>.
  /// See <see cref="SPModelManagerBase{T}.OnExecutingSiteQuery"/>.
  /// </summary>
  public class SPModelSiteQueryEventArgs : EventArgs {
    /// <summary>
    /// Gets an <see cref="SPSiteDataQuery"/> instance that will be executed against a site.
    /// </summary>
    public SPSiteDataQuery Query { get; internal set; }
  }

  /// <summary>
  /// Provides data when an ExecutingKeywordSearch event is triggered from <see cref="SPModelManagerBase{T}"/>.
  /// See <see cref="SPModelManagerBase{T}.OnExecutingKeywordSearch"/>.
  /// </summary>
  public class SPModelKeywordSearchEventArgs : EventArgs {
    /// <summary>
    /// Gets an <see cref="KeywordQuery"/> instance that will be executed against Office search service.
    /// </summary>
    public KeywordQuery Query { get; internal set; }
  }
  #endregion

  /// <summary>
  /// Provides a base class that handles querying, creating, deleting and persisting list items in a SharePoint site collection using model classes.
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public abstract class SPModelManagerBase<T> : ISPModelManager, ISPModelManagerInternal {
    private readonly SPWeb currentWeb;
    private readonly SPModelDescriptor descriptor;
    private readonly ICollection<SPModelUsage> currentLists = new HashSet<SPModelUsage>(SPModelUsageEqualityComparer.Default);
    private readonly HashSet<SPModel> itemsToSave = new HashSet<SPModel>();
    private readonly SPModelImplicitQueryMode queryMode;
    private readonly uint throttlingLimit;
    private readonly bool explicitListScope;
    private SPObjectCache objectCache;
    private TermStore termStore;
    private CultureInfo workingCulture;

    /// <summary>
    /// Initializes an instance of the <see cref="SPModelManagerBase{T}"/> class that queries list items under the specified site collection and its sub-sites.
    /// </summary>
    /// <param name="site">The site collection object to query against.</param>
    public SPModelManagerBase(SPSite site)
      : this(site.RootWeb, null) { }

    /// <summary>
    /// Initializes an instance of the <see cref="SPModelManagerBase{T}"/> class that queries list items under the specified site and its sub-sites.
    /// </summary>
    /// <param name="web">The site object to query against.</param>
    public SPModelManagerBase(SPWeb web)
      : this(web, null, false) { }

    /// <summary>
    /// Initializes an instance of the <see cref="SPModelManagerBase{T}"/> class that queries list items under the specified site and optionally its sub-sites.
    /// </summary>
    /// <param name="web">The site object to query against.</param>
    /// <param name="currentWebOnly">A boolean value specifies whether lists in sub-sites should also be queried.</param>
    public SPModelManagerBase(SPWeb web, bool currentWebOnly)
      : this(web, null, currentWebOnly) { }

    /// <summary>
    /// Initializes an instance of the <see cref="SPModelManagerBase{T}"/> class that queries list items under the specified list.
    /// </summary>
    /// <param name="list">The list object to query against.</param>
    public SPModelManagerBase(SPList list)
      : this(CommonHelper.ConfirmNotNull(list, "list").ParentWeb, new[] { list }) { }

    /// <summary>
    /// Initializes an instance of the <see cref="SPModelManagerBase{T}"/> class that queries list items under the specified list(s).
    /// </summary>
    /// <param name="currentWeb">The site object.</param>
    /// <param name="contextLists">A List of lists to query against.</param>
    public SPModelManagerBase(SPWeb currentWeb, IList<SPList> contextLists)
      : this(currentWeb, contextLists, false) { }

    private SPModelManagerBase(SPWeb currentWeb, IList<SPList> contextLists, bool currentWebOnly) {
      CommonHelper.ConfirmNotNull(currentWeb, "currentWeb");

      using (new SPSecurity.SuppressAccessDeniedRedirectInScope()) {
        this.currentWeb = currentWeb;
        this.throttlingLimit = currentWeb.Site.WebApplication.MaxItemsPerThrottledOperation;

        this.descriptor = SPModelDescriptor.Resolve(typeof(T));
        descriptor.Provision(currentWeb, SPModelProvisionOptions.Asynchronous | SPModelProvisionOptions.SuppressListCreation, SPModelListProvisionOptions.Default);

        if (contextLists != null) {
          contextLists.SelectMany(descriptor.GetUsages).ForEach(currentLists.Add);
          explicitListScope = true;
        }
        if (contextLists == null) {
          descriptor.GetUsages(currentWeb, currentWebOnly).ForEach(currentLists.Add);
        }
        if (currentLists.Count > 1 && descriptor.BaseType == SPBaseType.UnspecifiedBaseType) {
          this.queryMode = SPModelImplicitQueryMode.KeywordSearch;
        } else if (currentLists.Count > 1) {
          this.queryMode = SPModelImplicitQueryMode.SiteQuery;
        } else if (currentLists.Count == 1) {
          this.queryMode = SPModelImplicitQueryMode.ListQuery;
        } else {
          this.queryMode = SPModelImplicitQueryMode.None;
        }
      }
    }

    /// <summary>
    /// Gets the site collection associated with the site that initialized this instance of the <see cref="SPModelManagerBase{T}"/> class.
    /// </summary>
    public SPSite Site {
      get { return currentWeb.Site; }
    }

    /// <summary>
    /// Gets the term store connected with the site that initialized this instance of the <see cref="SPModelManagerBase{T}"/> class.
    /// </summary>
    public TermStore TermStore {
      get {
        if (termStore != null) {
          return termStore;
        }
        this.termStore = GetTermStoreForContext(currentWeb);
        if (termStore != null) {
          termStore.WorkingLanguage = this.Culture.LCID;
          return termStore;
        }
        return CommonHelper.AccessNotNull(termStore, "TermStore");
      }
    }

    /// <summary>
    /// Gets the <see cref="SPObjectCache"/> object. This object cache instance is used in <see cref="ISPListItemAdapter"/> objects created by this manager.
    /// </summary>
    protected internal SPObjectCache ObjectCache {
      get {
        if (objectCache == null) {
          objectCache = new SPObjectCache(this.Site);
        }
        return objectCache;
      }
    }

    /// <summary>
    /// Gets the query mode when calling overloads of <see cref="GetItems{TItem}()"/> or <see cref="GetItems{GetCount}()"/> which does not perform keyword search explicitly.
    /// </summary>
    protected internal SPModelImplicitQueryMode ImplicitQueryMode {
      get { return queryMode; }
    }

    internal CultureInfo Culture {
      get {
        if (workingCulture == null) {
          VariationContext variationContext = new VariationContext(currentWeb);
          this.workingCulture = variationContext.Culture;
        }
        return workingCulture;
      }
    }

    internal IEnumerable<SPModelUsage> ContextLists {
      get { return Enumerable.AsEnumerable(currentLists); }
    }

    /// <summary>
    /// Gets items of the associated content type(s).
    /// </summary>
    /// <typeparam name="TItem">Item type.</typeparam>
    /// <returns>A collection containing the returned items.</returns>
    protected internal SPModelCollection<TItem> GetItems<TItem>() {
      return GetItems<TItem>(null);
    }

    /// <summary>
    /// Gets items of the associated content type(s) that satisfy the condition.
    /// </summary>
    /// <typeparam name="TItem">Item type.</typeparam>
    /// <param name="query">CAML query expression.</param>
    /// <returns>A collection containing the returned items.</returns>
    protected internal SPModelCollection<TItem> GetItems<TItem>(CamlExpression query) {
      return GetItems<TItem>(query, throttlingLimit);
    }

    /// <summary>
    /// Gets items of the associated content type(s) that satisfy the condition.
    /// </summary>
    /// <typeparam name="TItem">Item type.</typeparam>
    /// <param name="query">CAML query expression.</param>
    /// <param name="limit">Maximum number of items to be returned.</param>
    /// <returns>A collection containing the returned items.</returns>
    protected internal SPModelCollection<TItem> GetItems<TItem>(CamlExpression query, uint limit) {
      return GetItems<TItem>(query, limit, 0);
    }

    /// <summary>
    /// Gets items of the associated content type(s) that satisfy the condition.
    /// </summary>
    /// <typeparam name="TItem">Item type.</typeparam>
    /// <param name="query">CAML query expression.</param>
    /// <param name="limit">Maximum number of items to be returned.</param>
    /// <param name="startRow">Number of items to skip from start.</param>
    /// <returns>A collection containing the returned items.</returns>
    protected internal SPModelCollection<TItem> GetItems<TItem>(CamlExpression query, uint limit, uint startRow) {
      int dummy;
      if (query != Caml.False && queryMode != SPModelImplicitQueryMode.None) {
        SPModelDescriptor typeInfo = SPModelDescriptor.Resolve(typeof(TItem));
        if (descriptor.Contains(typeInfo)) {
          CamlExpression contentTypedQuery = query + typeInfo.GetContentTypeExpression(descriptor);
          IEnumerable<ISPListItemAdapter> queryResultSet = null;
          switch (queryMode) {
            case SPModelImplicitQueryMode.KeywordSearch:
              queryResultSet = ExecuteKeywordSearchAsAdapter(typeInfo, contentTypedQuery, (int)limit, (int)startRow, null, null, KeywordInclusion.AllKeywords, out dummy);
              break;
            case SPModelImplicitQueryMode.SiteQuery:
              queryResultSet = ExecuteSiteQueryAsAdapter(typeInfo, contentTypedQuery, limit, startRow);
              break;
            case SPModelImplicitQueryMode.ListQuery:
              queryResultSet = ExecuteListQueryAsAdapter(typeInfo, contentTypedQuery, limit, startRow);
              break;
          }
          return SPModelCollection<TItem>.Create(this, queryResultSet, false);
        }
      }
      return SPModelCollection<TItem>.Create(this, new ISPListItemAdapter[0], false);
    }

    /// <summary>
    /// Performs a keyword search against the items of the associated content type(s).
    /// </summary>
    /// <typeparam name="TItem">Item type.</typeparam>
    /// <param name="query">CAML query expression.</param>
    /// <param name="limit">Maximum number of items to be returned.</param>
    /// <param name="keywords">A list of keywords to be searched against.</param>
    /// <param name="keywordInclusion">See <see cref="KeywordInclusion"/>.</param>
    /// <returns>A collection containing the returned items.</returns>
    protected internal SPModelCollection<TItem> GetItems<TItem>(CamlExpression query, uint limit, string[] keywords, KeywordInclusion keywordInclusion) {
      int dummy;
      return GetItems<TItem>(query, limit, 0, keywords, null, keywordInclusion, out dummy);
    }

    /// <summary>
    /// Performs a keyword search against the items of the associated content type(s).
    /// </summary>
    /// <typeparam name="TItem">Item type.</typeparam>
    /// <param name="query">CAML query expression.</param>
    /// <param name="limit">Maximum number of items to be returned.</param>
    /// <param name="startRow">Number of items to skip from start.</param>
    /// <param name="keywords">A list of keywords to be searched against.</param>
    /// <param name="refiners">A list of <see cref="SearchRefiner"/> instances. Refinement results are populated to the supplied instances.</param>
    /// <param name="keywordInclusion">See <see cref="KeywordInclusion"/>.</param>
    /// <param name="totalCount">Total number of items.</param>
    /// <returns>A collection containing the returned items.</returns>
    protected internal SPModelCollection<TItem> GetItems<TItem>(CamlExpression query, uint limit, uint startRow, string[] keywords, SearchRefiner[] refiners, KeywordInclusion keywordInclusion, out int totalCount) {
      if (query != Caml.False) {
        SPModelDescriptor typeInfo = SPModelDescriptor.Resolve(typeof(TItem));
        if (descriptor.Contains(typeInfo)) {
          CamlExpression contentTypedQuery = query + typeInfo.GetContentTypeExpression(descriptor);
          IEnumerable<ISPListItemAdapter> queryResultSet = ExecuteKeywordSearchAsAdapter(typeInfo, contentTypedQuery, (int)limit, (int)startRow, keywords, refiners, keywordInclusion, out totalCount);
          return SPModelCollection<TItem>.Create(this, queryResultSet, false);
        }
      }
      totalCount = 0;
      return SPModelCollection<TItem>.Create(this, new ISPListItemAdapter[0], false);
    }

    /// <summary>
    /// Gets the number of items of the associated content type(s).
    /// </summary>
    /// <typeparam name="TItem">Item type.</typeparam>
    /// <returns>Number of items.</returns>
    protected internal int GetCount<TItem>() {
      return GetCount<TItem>(null);
    }

    /// <summary>
    /// Gets the number of items of the associated content type(s) that satisfy the condition.
    /// </summary>
    /// <typeparam name="TItem">Item type.</typeparam>
    /// <param name="query">CAML query expression.</param>with the associated content type(s)
    /// <returns>Number of items.</returns>
    protected internal int GetCount<TItem>(CamlExpression query) {
      if (query != Caml.False && queryMode != SPModelImplicitQueryMode.None) {
        SPModelDescriptor typeInfo = SPModelDescriptor.Resolve(typeof(TItem));
        if (descriptor.Contains(typeInfo)) {
          CamlExpression contentTypedQuery = query + typeInfo.GetContentTypeExpression(descriptor);
          switch (queryMode) {
            case SPModelImplicitQueryMode.KeywordSearch:
              return GetCount<TItem>(query, null, KeywordInclusion.AllKeywords);
            case SPModelImplicitQueryMode.SiteQuery:
              DataTable dt = ExecuteSiteQuery(typeInfo, contentTypedQuery, throttlingLimit, false);
              return dt.Rows.Count;
            case SPModelImplicitQueryMode.ListQuery:
              IEnumerable<SPListItem> collection = ExecuteListQuery(typeInfo, contentTypedQuery, throttlingLimit, 0, false);
              return collection.Count();
          }
        }
      }
      return 0;
    }

    /// <summary>
    /// Performs a keyword search against the items of the associated content type(s) and returns the number of items.
    /// </summary>
    /// <typeparam name="TItem">Item type.</typeparam>
    /// <param name="query">CAML query expression.</param>
    /// <param name="keywords">A list of keywords to be searched against.</param>
    /// <param name="keywordInclusion">See <see cref="KeywordInclusion"/>.</param>
    /// <returns>Number of items.</returns>
    protected internal int GetCount<TItem>(CamlExpression query, string[] keywords, KeywordInclusion keywordInclusion) {
      return GetCount<TItem>(query, keywords, null, keywordInclusion);
    }

    /// <summary>
    /// Performs a keyword search against the items of the associated content type(s) and returns the number of items.
    /// </summary>
    /// <typeparam name="TItem">Item type.</typeparam>
    /// <param name="query">CAML query expression.</param>
    /// <param name="keywords">A list of keywords to be searched against.</param>
    /// <param name="refiners">A list of <see cref="SearchRefiner"/> instances. Refinement results are populated to the supplied instances.</param>
    /// <param name="keywordInclusion">See <see cref="KeywordInclusion"/>.</param>
    /// <returns>Number of items.</returns>
    protected internal int GetCount<TItem>(CamlExpression query, string[] keywords, SearchRefiner[] refiners, KeywordInclusion keywordInclusion) {
      int dummy;
      if (query != Caml.False) {
        SPModelDescriptor typeInfo = SPModelDescriptor.Resolve(typeof(TItem));
        if (descriptor.Contains(typeInfo)) {
          CamlExpression contentTypedQuery = query + typeInfo.GetContentTypeExpression(descriptor);
          ResultTable resultTable = ExecuteKeywordSearch(typeInfo, contentTypedQuery, (int)throttlingLimit, 0, keywords, refiners, keywordInclusion, false, out dummy);
          return resultTable.RowCount;
        }
      }
      return 0;
    }

    /// <summary>
    /// Creates an item of the associated content type. If the content type derives from File or Folder, a random name is used.
    /// See also <see cref="Create(Type,string)"/>
    /// </summary>
    /// <typeparam name="TItem">Item type.</typeparam>
    /// <returns>An item of the specified content type.</returns>
    protected TItem Create<TItem>() where TItem : T {
      return Create<TItem>(Path.GetRandomFileName());
    }

    /// <summary>
    /// Creates an item of the associated content type with the given file or folder name.
    /// See also <see cref="Create(Type,string)"/>
    /// </summary>
    /// <typeparam name="TItem">Item type.</typeparam>
    /// <param name="name">File or folder name.</param>
    /// <returns>An item of the specified content type.</returns>
    protected TItem Create<TItem>(string name) where TItem : T {
      return (TItem)Create(typeof(TItem), name);
    }

    /// <summary>
    /// Creates an item of the associated content type with the given file or folder name.
    /// </summary>
    /// <param name="modelType">Item type.</param>
    /// <param name="name">File or folder name.</param>
    /// <returns>An item of the specified content type.</returns>
    protected T Create(Type modelType, string name) {
      CommonHelper.ConfirmNotNull(modelType, "modelType");
      if (!modelType.IsOf(typeof(T))) {
        throw new InvalidOperationException(String.Format("Type '{0}' does not derive from or implement type '{1}'.", modelType.FullName, typeof(T).FullName));
      }

      SPModelDescriptor exactType = SPModelDescriptor.Resolve(modelType);
      if (exactType is SPModelInterfaceTypeDescriptor) {
        throw new InvalidOperationException(String.Format("Cannot create item of type '{0}'.", modelType.FullName));
      }
      if (exactType.ItemType != SPModelItemType.GenericItem && String.IsNullOrEmpty(name)) {
        throw new ArgumentNullException("File or folder name cannot be null.");
      }
      if (currentLists.Count > 1) {
        throw new InvalidOperationException("Ambiguous target list found. Try instanstite SPModelManager with SPList constructor to specify target list.");
      }
      if (currentLists.Count == 0) {
        exactType.Provision(currentWeb).ForEach(currentLists.Add);
        if (currentLists.Count == 0) {
          throw new InvalidOperationException("No target list is specified to create item.");
        }
      }

      SPList targetList = currentLists.First().EnsureList(this.ObjectCache).List;
      if (targetList == null) {
        throw new InvalidOperationException("No target list is specified to create item. User may not have sufficient permission to access the list.");
      }
      if (!exactType.UsedInList(targetList)) {
        currentLists.Clear();
        exactType.Provision(targetList.ParentWeb, new SPModelListProvisionOptions(targetList)).ForEach(currentLists.Add);
        targetList = currentLists.First().EnsureList(this.ObjectCache).List;
      }

      SPContentTypeId contentTypeId = exactType.ContentTypeIds.First();
      SPListItem createdItem;

      switch (exactType.ItemType) {
        case SPModelItemType.PublishingPage:
          PublishingWeb parentWeb = PublishingWeb.GetPublishingWeb(targetList.ParentWeb);
          PublishingPage page = parentWeb.CreatePublishingPage(contentTypeId, name);
          createdItem = page.ListItem;
          break;
        case SPModelItemType.DocumentSet:
          createdItem = CreateDocumentSet(targetList, name, contentTypeId);
          break;
        case SPModelItemType.File:
          SPFile file = targetList.RootFolder.Files.Add(name, new byte[1], new Hashtable { { SPBuiltInFieldName.ContentTypeId, contentTypeId.ToString() } }, false);
          createdItem = file.Item;
          break;
        case SPModelItemType.Folder:
          SPFolder folder = targetList.RootFolder.SubFolders.Add(name);
          folder.Item[SPBuiltInFieldId.ContentTypeId] = contentTypeId;
          folder.Item.Update();
          createdItem = folder.Item;
          break;
        default:
          createdItem = targetList.AddItem();
          createdItem[SPBuiltInFieldId.ContentTypeId] = contentTypeId;
          break;
      }
      return TryCreateModel(new SPListItemAdapter(createdItem, this.ObjectCache), false);
    }

    /// <summary>
    /// Moves the specified item to recycle bin.
    /// </summary>
    /// <param name="item">An item to be recycled.</param>
    protected void Recycle(T item) {
      CommonHelper.ConfirmNotNull(item, "item");
      SPModel model = (SPModel)(object)item;
      if (model.ParentCollection.Manager != this) {
        throw new ArgumentException("Supplied item does not belongs to this manager", "item");
      }
      SPListItem targetItem = model.Adapter.ListItem;
      if (targetItem.ID > 0) {
        using (targetItem.Web.GetAllowUnsafeUpdatesScope()) {
          targetItem.Recycle();
        }
      }
    }

    /// <summary>
    /// Deletes the specified item from a list. 
    /// </summary>
    /// <param name="item">An item to be deleted.</param>
    protected void Delete(T item) {
      CommonHelper.ConfirmNotNull(item, "item");
      SPModel model = (SPModel)(object)item;
      if (model.ParentCollection.Manager != this) {
        throw new ArgumentException("Supplied item does not belongs to this manager", "item");
      }
      SPListItem targetItem = model.Adapter.ListItem;
      if (targetItem.ID > 0) {
        using (targetItem.Web.GetAllowUnsafeUpdatesScope()) {
          targetItem.Delete();
        }
      }
    }

    /// <summary>
    /// Commits changes made to model class instances fetched from this manager.
    /// </summary>
    protected void CommitChanges() {
      CommitChanges(SPModelCommitMode.Default);
    }

    /// <summary>
    /// Commits changes made to the specified model class instances.
    /// </summary>
    /// <param name="item">An item with changes to be persisted.</param>
    protected void CommitChanges(T item) {
      CommitChanges(item, SPModelCommitMode.Default);
    }

    /// <summary>
    /// Commits changes made to model class instances fetched from this manager with the specified commit option.
    /// </summary>
    /// <param name="mode">An value of <see cref="Codeless.SharePoint.ObjectModel.SPModelCommitMode"/> representing how a list item is updated.</param>
    protected void CommitChanges(SPModelCommitMode mode) {
      List<SPModel> itemsToSaveCopy = new List<SPModel>(itemsToSave);
      foreach (SPModel item in itemsToSaveCopy) {
        UpdateItem(item.Adapter.ListItem, mode);
        itemsToSave.Remove(item);
      }
    }

    /// <summary>
    /// Commits changes made to the specified model class instances with the specified commit option.
    /// </summary>
    /// <param name="item">An item with changes to be persisted.</param>
    /// <param name="mode">An value of <see cref="Codeless.SharePoint.ObjectModel.SPModelCommitMode"/> representing how a list item is updated.</param>
    /// <exception cref="System.ArgumentException">Supplied item does not belongs to this manager - item</exception>
    protected void CommitChanges(T item, SPModelCommitMode mode) {
      SPModel model = ValidateModel(item, false);
      if (itemsToSave.Contains(model)) {
        UpdateItem(model.Adapter.ListItem, mode);
        itemsToSave.Remove(model);
      }
    }

    /// <summary>
    /// Executes specified operation on the file represented by the model object with no comment.
    /// </summary>
    /// <param name="item">An item which operation is being performed on.</param>
    /// <param name="operation">The operation to be performed.</param>
    protected void ExecuteFileOperation(T item, SPModelFileOperation operation) {
      ExecuteFileOperation(item, operation, String.Empty);
    }

    /// <summary>
    /// Executes specified operation on the file represented by the model object with the specified comment.
    /// </summary>
    /// <param name="item">An item which operation is being performed on.</param>
    /// <param name="operation">The operation to be performed.</param>
    /// <param name="comment">A string that contains a comment about the operation. It is ignored for some oeprations.</param>
    protected void ExecuteFileOperation(T item, SPModelFileOperation operation, string comment) {
      SPModel model = ValidateModel(item, true);
      SPListItem listItem = model.Adapter.ListItem;
      using (listItem.Web.GetAllowUnsafeUpdatesScope()) {
        switch (operation) {
          case SPModelFileOperation.MajorCheckIn:
            listItem.File.CheckIn(comment, SPCheckinType.MajorCheckIn);
            break;
          case SPModelFileOperation.MinorCheckIn:
            listItem.File.CheckIn(comment, SPCheckinType.MinorCheckIn);
            break;
          case SPModelFileOperation.OverwriteCheckIn:
            listItem.File.CheckIn(comment, SPCheckinType.OverwriteCheckIn);
            break;
          case SPModelFileOperation.CheckOut:
            listItem.File.CheckOut();
            break;
          case SPModelFileOperation.UndoCheckOut:
            listItem.File.UndoCheckOut();
            break;
          case SPModelFileOperation.Publish:
            listItem.File.Publish(comment);
            break;
          case SPModelFileOperation.UnPublish:
            listItem.File.UnPublish(comment);
            break;
          case SPModelFileOperation.Approve:
            listItem.File.Approve(comment);
            break;
          case SPModelFileOperation.Deny:
            listItem.File.Deny(comment);
            break;
          case SPModelFileOperation.TakeOffline:
            listItem.File.TakeOffline();
            break;
        }
      }
    }

    /// <summary>
    /// Attempts to create a model class instance of type <typeparamref name="T"/> from the list item reprensented by the specified data access adapter.
    /// If the list item does not correspond to a model type equal or derived from type <typeparamref name="T"/>, *null* is returned.
    /// </summary>
    /// <param name="adapter">A data access adapter.</param>
    /// <param name="readOnly">Whether to mark the model instance created as read-only.</param>
    /// <returns>An typed model item instance.</returns>
    protected T TryCreateModel(ISPListItemAdapter adapter, bool readOnly) {
      CommonHelper.ConfirmNotNull(adapter, "adapter");
      SPModelCollection<T> collection = SPModelCollection<T>.Create(this, new[] { adapter }, readOnly);
      return collection.FirstOrDefault();
    }

    /// <summary>
    /// Called when a list query is being executed.
    /// </summary>
    /// <param name="e">Event data.</param>
    protected virtual void OnExecutingListQuery(SPModelListQueryEventArgs e) { }

    /// <summary>
    /// Called when a cross-list query is being executed.
    /// </summary>
    /// <param name="e">Event data.</param>
    protected virtual void OnExecutingSiteQuery(SPModelSiteQueryEventArgs e) { }

    /// <summary>
    /// Called when a keyword search is being executed against Office search service.
    /// </summary>
    /// <param name="e"></param>
    protected virtual void OnExecutingKeywordSearch(SPModelKeywordSearchEventArgs e) { }

    /// <summary>
    /// Returns the default term store connected with the site that initialized this instance of the <see cref="SPModelManagerBase{T}"/> class.
    /// </summary>
    /// <param name="web">The site that initialized this instance of the <see cref="SPModelManagerBase{T}"/> class.</param>
    /// <returns>An instance of the <see cref="TermStore"/> class representing a connected term store.</returns>
    protected virtual TermStore GetTermStoreForContext(SPWeb web) {
      TaxonomySession session = new TaxonomySession(web.Site);
      return session.DefaultKeywordsTermStore;
    }

    private IEnumerable<ISPListItemAdapter> ExecuteListQueryAsAdapter(SPModelDescriptor typeInfo, CamlExpression query, uint limit, uint startRow) {
      IEnumerable<SPListItem> collection = ExecuteListQuery(typeInfo, query, limit, startRow, true);
      foreach (SPListItem item in collection) {
        yield return new SPListItemAdapter(item, this.ObjectCache);
      }
    }

    private IEnumerable<ISPListItemAdapter> ExecuteSiteQueryAsAdapter(SPModelDescriptor typeInfo, CamlExpression query, uint limit, uint startRow) {
      DataTable dt = ExecuteSiteQuery(typeInfo, query, limit + startRow, true);
      for (int i = (int)startRow, count = dt.Rows.Count; i < count; i++) {
        DataRowAdapter adapter = new DataRowAdapter(currentWeb.Site, dt.Rows[i], this.ObjectCache);
        this.ObjectCache.RequestReusableAcl(new Guid(adapter.GetLookupFieldValue(SPBuiltInFieldName.ScopeId)));
        yield return adapter;
      }
    }

    private IEnumerable<ISPListItemAdapter> ExecuteKeywordSearchAsAdapter(SPModelDescriptor typeInfo, CamlExpression query, int limit, int startRow, string[] keywords, SearchRefiner[] refiners, KeywordInclusion keywordInclusion, out int totalCount) {
      ResultTable queryResultsTable = ExecuteKeywordSearch(typeInfo, query, limit, startRow, keywords, refiners, keywordInclusion, true, out totalCount);
      DataTable queryDataTable = new DataTable();
      queryDataTable.Load(queryResultsTable, LoadOption.OverwriteChanges);
      return queryDataTable.Rows.OfType<DataRow>().Select(v => {
        ISPListItemAdapter adapter = new KeywordQueryResultAdapter(currentWeb.Site, v, this.ObjectCache);
        this.ObjectCache.RequestReusableAcl(new Guid(adapter.GetLookupFieldValue(SPBuiltInFieldName.ScopeId)));
        return adapter;
      });
    }

    private IEnumerable<SPListItem> ExecuteListQuery(SPModelDescriptor typeInfo, CamlExpression query, uint limit, uint startRow, bool selectProperties) {
      SPList list = currentLists.First().EnsureList(this.ObjectCache).List;
      if (list == null || list.ItemCount == 0) {
        return new SPListItem[0];
      }

      SPQuery listQuery = new SPQuery();
      listQuery.ViewFields = selectProperties ? (Caml.ViewFields(typeInfo.RequiredViewFields) + Caml.ViewFields(SPModel.RequiredViewFields)).ToString() : String.Empty;
      listQuery.Query = query.ToString();
      listQuery.RowLimit = limit;
      listQuery.ViewAttributes = "Scope=\"RecursiveAll\"";
      OnExecutingListQuery(new SPModelListQueryEventArgs { Query = listQuery });

      try {
        if (startRow > 0) {
          SPQuery skipQuery = new SPQuery();
          skipQuery.ExpandRecurrence = listQuery.ExpandRecurrence;
          skipQuery.Query = listQuery.Query;
          skipQuery.ViewFields = String.Empty;
          skipQuery.ViewAttributes = listQuery.ViewAttributes;
          skipQuery.RowLimit = startRow;
          SPListItemCollection skipResult = list.GetItems(skipQuery);
          if (skipResult.Count < startRow) {
            return Enumerable.Empty<SPListItem>();
          }
          listQuery.ListItemCollectionPosition = skipResult.ListItemCollectionPosition;
        }

        SPListItemCollection result = list.GetItems(listQuery);
        int count = result.Count;
        return result.OfType<SPListItem>();
      } catch (Exception ex) {
        SPDiagnosticsService.Local.WriteTrace(TraceCategory.ModelQuery, ex);
        throw new SPModelQueryException(currentWeb, ex, listQuery.Query);
      }
    }

    private DataTable ExecuteSiteQuery(SPModelDescriptor typeInfo, CamlExpression query, uint limit, bool selectProperties) {
      SPSiteDataQuery siteQuery = new SPSiteDataQuery();
      siteQuery.Webs = Caml.WebsScope.Recursive;
      siteQuery.Lists = Caml.ListsScope(currentLists.Select(v => v.ListId).ToArray()).ToString();
      siteQuery.ViewFields = (query.GetViewFieldsExpression() + (selectProperties ? (Caml.ViewFields(typeInfo.RequiredViewFields) + Caml.ViewFields(SPModel.RequiredViewFields)) : Caml.Empty)).ToString();
      siteQuery.Query = query.ToString();
      siteQuery.RowLimit = limit;
      OnExecutingSiteQuery(new SPModelSiteQueryEventArgs { Query = siteQuery });

      using (SPWeb targetWeb = currentWeb.Site.OpenWeb(currentWeb.ID)) {
        try {
          return targetWeb.GetSiteData(siteQuery);
        } catch (Exception ex) {
          if (ex.InnerException is COMException && (ex.InnerException.Message.IndexOf("0x80131904") >= 0 || ex.InnerException.Message.IndexOf("0x80020009") >= 0)) {
            try {
              foreach (SPModelUsage v in currentLists) {
                SPList list = v.EnsureList(this.ObjectCache).List;
                if (list != null) {
                  typeInfo.CheckMissingFields(list);
                }
              }
            } catch (Exception ex2) {
              SPDiagnosticsService.Local.WriteTrace(TraceCategory.ModelQuery, ex2);
              throw new SPModelQueryException(currentWeb, ex2, siteQuery.Query);
            }
          }
          SPDiagnosticsService.Local.WriteTrace(TraceCategory.ModelQuery, ex);
          throw new SPModelQueryException(currentWeb, ex, siteQuery.Query);
        }
      }
    }

    private ResultTable ExecuteKeywordSearch(SPModelDescriptor typeInfo, CamlExpression query, int limit, int startRow, string[] keywords, SearchRefiner[] refiners, KeywordInclusion inclusion, bool selectProperties, out int totalCount) {
      CamlExpression listScopedQuery = Caml.Empty;
      if (explicitListScope) {
        foreach (SPModelUsage list in currentLists) {
          listScopedQuery |= Caml.BeginsWith(BuiltInManagedPropertyName.Path, SPUtility.GetFullUrl(currentWeb.Site, list.ServerRelativeUrl));
        }
      } else {
        listScopedQuery = Caml.BeginsWith(BuiltInManagedPropertyName.Path, currentWeb.Url);
      }

      KeywordQuery keywordQuery = SearchServiceHelper.CreateQuery(currentWeb.Site, query & listScopedQuery, limit, startRow, keywords, inclusion, SearchServiceHelper.GetManagedPropertyNames(currentWeb.Site, typeInfo.RequiredViewFields));
      keywordQuery.Culture = this.Culture;
      OnExecutingKeywordSearch(new SPModelKeywordSearchEventArgs { Query = keywordQuery });

      try {
        ResultTable relevantResults = SearchServiceHelper.ExecuteQuery(keywordQuery, refiners);
        totalCount = relevantResults.TotalRows;
        return relevantResults;
      } catch (Exception ex) {
        SPDiagnosticsService.Local.WriteTrace(TraceCategory.ModelQuery, ex);
        throw new SPModelQueryException(currentWeb, ex, keywordQuery.QueryText);
      }
    }

    private SPModel ValidateModel(T item, bool requireFile) {
      CommonHelper.ConfirmNotNull(item, "item");
      SPModel model = item as SPModel;
      if (model == null) {
        throw new ArgumentException("Supplied item is not an SPModel instance", "item");
      }
      if (model.ParentCollection.Manager != this) {
        throw new ArgumentException("Supplied item does not belongs to this manager", "item");
      }
      if (requireFile && !model.Adapter.ContentTypeId.IsChildOf(SPBuiltInContentTypeId.Document)) {
        throw new ArgumentException("Supplied item is not a file", "item");
      }
      return model;
    }

    private T ValidateModel(object item) {
      CommonHelper.ConfirmNotNull(item, "item");
      if (!(item is SPModel) || ((SPModel)item).Manager != this) {
        throw new ArgumentException("item");
      }
      return (T)item;
    }

    private void UpdateItem(SPListItem item, SPModelCommitMode mode) {
      bool systemCheckIn = false;
      using (item.Web.GetAllowUnsafeUpdatesScope()) {
        if (item.ParentList.ForceCheckout && item.FileSystemObjectType == SPFileSystemObjectType.File && item.File.CheckOutType == SPFile.SPCheckOutType.None) {
          item.File.CheckOut();
          systemCheckIn = true;
        }
        switch (mode) {
          case SPModelCommitMode.Default:
            item.Update();
            break;
          case SPModelCommitMode.OverwriteVersion:
            item.UpdateOverwriteVersion();
            break;
          case SPModelCommitMode.SystemUpdate:
            item.SystemUpdate();
            break;
          case SPModelCommitMode.SystemUpdateOverwriteVersion:
            item.SystemUpdate(false);
            break;
        }
        if (systemCheckIn) {
          item.File.CheckIn(String.Empty);
        }
      }
    }

    private static SPListItem CreateDocumentSet(SPList targetList, string name, SPContentTypeId contentTypeId) {
      DocumentSet documentSet = DocumentSet.Create(targetList.RootFolder, name, contentTypeId, new Hashtable());
      return targetList.GetItemById(documentSet.Item.ID);
    }

    #region ISPModelManagerInternal
    SPModelDescriptor ISPModelManagerInternal.Descriptor {
      get { return descriptor; }
    }

    SPObjectCache ISPModelManagerInternal.ObjectCache {
      get {
        if (objectCache == null) {
          objectCache = new SPObjectCache(this.Site);
        }
        return objectCache;
      }
      set {
        if (objectCache != null) {
          throw new InvalidOperationException();
        }
        objectCache = value;
      }
    }

    IEnumerable<SPModelUsage> ISPModelManagerInternal.ContextLists {
      get { return Enumerable.AsEnumerable(currentLists); }
    }

    SPModel ISPModelManagerInternal.TryCreateModel(ISPListItemAdapter adapter, bool readOnly) {
      return CommonHelper.TryCastOrDefault<SPModel>(TryCreateModel(adapter, readOnly));
    }

    void ISPModelManagerInternal.SaveOnCommit(SPModel item) {
      CommonHelper.ConfirmNotNull(item, "item");
      itemsToSave.Add(item);
    }
    #endregion

    #region ISPModelManager
    SPModelCollection ISPModelManager.GetItems() {
      return this.GetItems<T>();
    }

    SPModelCollection ISPModelManager.GetItems(CamlExpression query) {
      return this.GetItems<T>(query);
    }

    SPModelCollection ISPModelManager.GetItems(CamlExpression query, uint limit) {
      return this.GetItems<T>(query, limit);
    }

    SPModelCollection ISPModelManager.GetItems(CamlExpression query, uint limit, uint startRow) {
      return this.GetItems<T>(query, limit, startRow);
    }

    SPModelCollection ISPModelManager.GetItems(CamlExpression query, uint limit, string[] keywords, KeywordInclusion keywordInclusion) {
      return this.GetItems<T>(query, limit, keywords, keywordInclusion);
    }

    SPModelCollection ISPModelManager.GetItems(CamlExpression query, uint limit, uint startRow, string[] keywords, SearchRefiner[] refiners, KeywordInclusion keywordInclusion, out int totalCount) {
      return this.GetItems<T>(query, limit, startRow, keywords, refiners, keywordInclusion, out totalCount);
    }

    int ISPModelManager.GetCount() {
      return this.GetCount<T>();
    }

    int ISPModelManager.GetCount(CamlExpression query) {
      return this.GetCount<T>(query);
    }

    int ISPModelManager.GetCount(CamlExpression query, string[] keywords, KeywordInclusion keywordInclusion) {
      return this.GetCount<T>(query, keywords, keywordInclusion);
    }

    object ISPModelManager.Create(Type modelType) {
      return Create(modelType, Path.GetRandomFileName());
    }

    object ISPModelManager.Create(Type modelType, string filename) {
      return Create(modelType, filename);
    }

    void ISPModelManager.Recycle(object item) {
      Recycle(ValidateModel(item));
    }

    void ISPModelManager.Delete(object item) {
      Delete(ValidateModel(item));
    }

    void ISPModelManager.CommitChanges() {
      this.CommitChanges();
    }

    void ISPModelManager.CommitChanges(object item) {
      CommitChanges(ValidateModel(item));
    }

    void ISPModelManager.CommitChanges(SPModelCommitMode mode) {
      this.CommitChanges(mode);
    }

    void ISPModelManager.CommitChanges(object item, SPModelCommitMode mode) {
      CommitChanges(ValidateModel(item), mode);
    }
    #endregion

    #region Helper class
    private class SPModelUsageEqualityComparer : EqualityComparer<SPModelUsage> {
      public new static readonly SPModelUsageEqualityComparer Default = new SPModelUsageEqualityComparer();

      public override bool Equals(SPModelUsage x, SPModelUsage y) {
        return x.ListId == y.ListId;
      }

      public override int GetHashCode(SPModelUsage obj) {
        return obj.ListId.GetHashCode();
      }
    }
    #endregion
  }
}
