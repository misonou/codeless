using Microsoft.Office.Server.Search.Query;
using Microsoft.SharePoint;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Codeless.SharePoint.ObjectModel {
  /// <summary>
  /// Provides a generic implementation of <see cref="SPModelManagerBase{T}"/>.
  /// </summary>
  /// <typeparam name="T">Type of model class.</typeparam>
  public class SPModelManager<T> : SPModelManagerBase<T> {
    private object syncLock = new object();
    private T currentItem;
    private bool currentItemInitialized;

    /// <summary>
    /// Initializes an instance of the <see cref="SPModelManager{T}"/> class that queries list items under the specified site collection and its sub-sites.
    /// </summary>
    /// <param name="site">The site collection object to query against.</param>
    public SPModelManager(SPSite site)
      : base(site) { }

    /// <summary>
    /// Initializes an instance of the <see cref="SPModelManager{T}"/> class that queries list items under the specified site and its sub-sites.
    /// </summary>
    /// <param name="web">Site object.</param>
    public SPModelManager(SPWeb web)
      : base(web) { }

    /// <summary>
    /// Initializes an instance of the <see cref="SPModelManager{T}"/> class that queries list items under the specified site and optionally its sub-sites.
    /// </summary>
    /// <param name="web">The site object to query against.</param>
    /// <param name="currentWebOnly">A boolean value specifies whether lists in sub-sites should also be queried.</param>
    public SPModelManager(SPWeb web, bool currentWebOnly)
      : base(web, currentWebOnly) { }

    /// <summary>
    /// Initializes an instance of the <see cref="SPModelManager{T}"/> class that queries list items under the specified list.
    /// </summary>
    /// <param name="list">The list object to query against.</param>
    public SPModelManager(SPList list)
      : base(list) { }

    /// <summary>
    /// Initializes an instance of the <see cref="SPModelManager{T}"/> class that queries list items under the specified list(s).
    /// </summary>
    /// <param name="web">The site object.</param>
    /// <param name="lists">A List of lists to query against.</param>
    public SPModelManager(SPWeb web, IList<SPList> lists)
      : base(web, lists) { }

    /// <summary>
    /// Gets a singleton manager in the current HTTP request.
    /// </summary>
    public static SPModelManager<T> Current {
      get {
        if (SPContext.Current != null) {
          return CommonHelper.HttpContextSingleton(() => (SPModelManager<T>)SPModel.GetDefaultManager(typeof(T), SPContext.Current.Web));
        }
        return null;
      }
    }

    /// <summary>
    /// Gets the list item associated with the current SharePoint request context.
    /// </summary>
    public T CurrentItem {
      get { return LazyInitializer.EnsureInitialized(ref currentItem, ref currentItemInitialized, ref syncLock, EnsureCurrentItem); }
    }

    private T EnsureCurrentItem() {
      if (SPContext.Current != null && SPContext.Current.ListItem != null) {
        return TryCreateModel(new SPListItemAdapter(SPContext.Current.ListItem, this.ObjectCache), false);
      }
      return default(T);
    }

    #region Override protected methods explicitly as public methods
    /// <summary>
    /// Gets items of the associated content type(s).
    /// </summary>
    /// <returns>A collection containing the returned items.</returns>
    public SPModelCollection<T> GetItems() {
      return base.GetItems<T>();
    }

    /// <summary>
    /// Gets items of the associated content type(s) that satisfy the condition.
    /// </summary>
    /// <param name="query">CAML query expression.</param>
    /// <returns>A collection containing the returned items.</returns>
    public SPModelCollection<T> GetItems(CamlExpression query) {
      return base.GetItems<T>(query);
    }

    /// <summary>
    /// Gets items of the associated content type(s) that satisfy the condition.
    /// </summary>
    /// <param name="query">CAML query expression.</param>
    /// <param name="limit">Maximum number of items to be returned.</param>
    /// <returns>A collection containing the returned items.</returns>
    public SPModelCollection<T> GetItems(CamlExpression query, uint limit) {
      return base.GetItems<T>(query, limit);
    }

    /// <summary>
    /// Performs a keyword search against the items of the associated content type(s).
    /// </summary>
    /// <param name="query">CAML query expression.</param>
    /// <param name="limit">Maximum number of items to be returned.</param>
    /// <param name="keywords">A list of keywords to be searched against.</param>
    /// <param name="keywordInclusion">See <see cref="KeywordInclusion"/>.</param>
    /// <returns>A collection containing the returned items.</returns>
    public SPModelCollection<T> GetItems(CamlExpression query, uint limit, string[] keywords, KeywordInclusion keywordInclusion) {
      return base.GetItems<T>(query, limit, keywords, keywordInclusion);
    }

    /// <summary>
    /// Gets items of the associated content type(s).
    /// </summary>
    /// <typeparam name="TItem">Item type.</typeparam>
    /// <returns>A collection containing the returned items.</returns>
    public new SPModelCollection<TItem> GetItems<TItem>() {
      return base.GetItems<TItem>();
    }

    /// <summary>
    /// Gets items of the associated content type(s) that satisfy the condition.
    /// </summary>
    /// <typeparam name="TItem">Item type.</typeparam>
    /// <param name="query">CAML query expression.</param>
    /// <returns>A collection containing the returned items.</returns>
    public new SPModelCollection<TItem> GetItems<TItem>(CamlExpression query) {
      return base.GetItems<TItem>(query);
    }

    /// <summary>
    /// Gets items of the associated content type(s) that satisfy the condition.
    /// </summary>
    /// <typeparam name="TItem">Item type.</typeparam>
    /// <param name="query">CAML query expression.</param>
    /// <param name="limit">Maximum number of items to be returned.</param>
    /// <returns>A collection containing the returned items.</returns>
    public new SPModelCollection<TItem> GetItems<TItem>(CamlExpression query, uint limit) {
      return base.GetItems<TItem>(query, limit);
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
    public new SPModelCollection<TItem> GetItems<TItem>(CamlExpression query, uint limit, string[] keywords, KeywordInclusion keywordInclusion) {
      return base.GetItems<TItem>(query, limit, keywords, keywordInclusion);
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
    public new SPModelCollection<TItem> GetItems<TItem>(CamlExpression query, uint limit, uint startRow, string[] keywords, SearchRefiner[] refiners, KeywordInclusion keywordInclusion, out int totalCount) {
      return base.GetItems<TItem>(query, limit, startRow, keywords, refiners, keywordInclusion, out totalCount);
    }

    /// <summary>
    /// Gets the number of items of the associated content type(s).
    /// </summary>
    /// <returns>Number of items.</returns>
    public int GetCount() {
      return base.GetCount<T>();
    }

    /// <summary>
    /// Gets the number of items of the associated content type(s) that satisfy the condition.
    /// </summary>
    /// <param name="query">CAML query expression.</param>with the associated content type(s)
    /// <returns>Number of items.</returns>
    public int GetCount(CamlExpression query) {
      return base.GetCount<T>(query);
    }

    /// <summary>
    /// Performs a keyword search against the items of the associated content type(s) and returns the number of items.
    /// </summary>
    /// <param name="query">CAML query expression.</param>
    /// <param name="keywords">A list of keywords to be searched against.</param>
    /// <param name="keywordInclusion">See <see cref="KeywordInclusion"/>.</param>
    /// <returns>Number of items.</returns>
    public int GetCount(CamlExpression query, string[] keywords, KeywordInclusion keywordInclusion) {
      return base.GetCount<T>(query, keywords, keywordInclusion);
    }

    /// <summary>
    /// Gets the number of items of the associated content type(s).
    /// </summary>
    /// <typeparam name="TItem">Item type.</typeparam>
    /// <returns>Number of items.</returns>
    public new int GetCount<TItem>() {
      return base.GetCount<TItem>();
    }

    /// <summary>
    /// Gets the number of items of the associated content type(s) that satisfy the condition.
    /// </summary>
    /// <typeparam name="TItem">Item type.</typeparam>
    /// <param name="query">CAML query expression.</param>with the associated content type(s)
    /// <returns>Number of items.</returns>
    public new int GetCount<TItem>(CamlExpression query) {
      return base.GetCount<TItem>(query);
    }

    /// <summary>
    /// Performs a keyword search against the items of the associated content type(s) and returns the number of items.
    /// </summary>
    /// <typeparam name="TItem">Item type.</typeparam>
    /// <param name="query">CAML query expression.</param>
    /// <param name="keywords">A list of keywords to be searched against.</param>
    /// <param name="keywordInclusion">See <see cref="KeywordInclusion"/>.</param>
    /// <returns>Number of items.</returns>
    public new int GetCount<TItem>(CamlExpression query, string[] keywords, KeywordInclusion keywordInclusion) {
      return base.GetCount<TItem>(query, keywords, keywordInclusion);
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
    public new int GetCount<TItem>(CamlExpression query, string[] keywords, SearchRefiner[] refiners, KeywordInclusion keywordInclusion) {
      return base.GetCount<TItem>(query, keywords, refiners, keywordInclusion);
    }

    /// <summary>
    /// Creates an item of the associated content type.
    /// If the content type derives from File or Folder, a random name is used. See <see cref="Create(string)"/>.
    /// </summary>
    /// <typeparam name="TItem">Item type.</typeparam>
    /// <returns>An item of the specified content type.</returns>
    public new TItem Create<TItem>() where TItem : T {
      return base.Create<TItem>();
    }

    /// <summary>
    /// Creates an item of the associated content type with the given file or folder name.
    /// </summary>
    /// <typeparam name="TItem">Item type.</typeparam>
    /// <param name="name">File or folder name.</param>
    /// <returns>An item of the specified content type.</returns>
    public new TItem Create<TItem>(string name) where TItem : T {
      return base.Create<TItem>(name);
    }

    /// <summary>
    /// Creates an item of the associated content type.
    /// If the content type derives from File or Folder, a random name is used. See <see cref="Create(string)"/>.
    /// </summary>
    /// <param name="modelType">Item type.</param>
    /// <returns>An item of the specified content type.</returns>
    public T Create(System.Type modelType) {
      return Create(modelType, Path.GetRandomFileName());
    }

    /// <summary>
    /// Creates an item of the associated content type with the given file or folder name.
    /// </summary>
    /// <param name="modelType">Item type.</param>
    /// <param name="name">File or folder name.</param>
    /// <returns>An item of the specified content type.</returns>
    public new T Create(System.Type modelType, string name) {
      return base.Create(modelType, name);
    }

    /// <summary>
    /// Moves the specified item to recycle bin.
    /// </summary>
    /// <param name="item">An item to be recycled.</param>
    public new void Recycle(T item) {
      base.Recycle(item);
    }

    /// <summary>
    /// Deletes the specified item from a list. 
    /// </summary>
    /// <param name="item">An item to be deleted.</param>
    public new void Delete(T item) {
      base.Delete(item);
    }

    /// <summary>
    /// Commits changes made to model class instances fetched from this manager.
    /// </summary>
    public new void CommitChanges() {
      base.CommitChanges();
    }

    /// <summary>
    /// Commits changes made to the specified model class instances.
    /// </summary>
    /// <param name="item">An item with changes to be persisted.</param>
    public new void CommitChanges(T item) {
      base.CommitChanges(item);
    }

    /// <summary>
    /// Commits changes made to model class instances fetched from this manager with the specified commit option.
    /// </summary>
    /// <param name="mode">An value of <see cref="Codeless.SharePoint.ObjectModel.SPModelCommitMode" /> representing how a list item is updated.</param>
    public new void CommitChanges(SPModelCommitMode mode) {
      base.CommitChanges(mode);
    }

    /// <summary>
    /// Commits changes made to the specified model class instances with the specified commit option.
    /// </summary>
    /// <param name="item">An item with changes to be persisted.</param>
    /// <param name="mode">An value of <see cref="Codeless.SharePoint.ObjectModel.SPModelCommitMode" /> representing how a list item is updated.</param>
    public new void CommitChanges(T item, SPModelCommitMode mode) {
      base.CommitChanges(item, mode);
    }

    /// <summary>
    /// Executes specified operation on the file represented by the model object with no comment.
    /// </summary>
    /// <param name="item">An item which operation is being performed on.</param>
    /// <param name="operation">The operation to be performed.</param>
    public new void ExecuteFileOperation(T item, SPModelFileOperation operation) {
      base.ExecuteFileOperation(item, operation);
    }

    /// <summary>
    /// Executes specified operation on the file represented by the model object with the specified comment.
    /// </summary>
    /// <param name="item">An item which operation is being performed on.</param>
    /// <param name="operation">The operation to be performed.</param>
    /// <param name="comment">A string that contains a comment about the operation. It is ignored for some oeprations.</param>
    public new void ExecuteFileOperation(T item, SPModelFileOperation operation, string comment) {
      base.ExecuteFileOperation(item, operation, comment);
    }
    #endregion
  }
}
