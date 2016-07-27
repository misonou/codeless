using Microsoft.SharePoint;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Codeless.SharePoint {
  /// <summary>
  /// Provides caching and uniqueness to database persisted SharePoint objects.
  /// </summary>
  public sealed class SPObjectCache {
    #region Helper Class
    private interface ILookupKey<T> { }

    private struct SPWebLookupKey : IEquatable<SPWebLookupKey>, ILookupKey<SPWeb> {
      public Guid WebId { get; private set; }

      public SPWebLookupKey(SPWeb web)
        : this(web.ID) { }

      public SPWebLookupKey(Guid webId)
        : this() {
        this.WebId = webId;
      }

      public bool Equals(SPWebLookupKey other) {
        return WebId == other.WebId;
      }

      public override bool Equals(object obj) {
        if (obj is SPWebLookupKey) {
          return Equals((SPWebLookupKey)obj);
        }
        return base.Equals(obj);
      }

      public override int GetHashCode() {
        return WebId.GetHashCode();
      }
    }

    private struct SPListLookupKey : IEquatable<SPListLookupKey>, ILookupKey<SPList> {
      public Guid WebId { get; private set; }
      public Guid ListId { get; private set; }

      public SPListLookupKey(SPList list)
        : this(list.ParentWeb.ID, list.ID) { }

      public SPListLookupKey(Guid webId, Guid listId)
        : this() {
        this.WebId = webId;
        this.ListId = listId;
      }

      public bool Equals(SPListLookupKey other) {
        return ListId == other.ListId && WebId == other.WebId;
      }

      public override bool Equals(object obj) {
        if (obj is SPListLookupKey) {
          return Equals((SPListLookupKey)obj);
        }
        return base.Equals(obj);
      }

      public override int GetHashCode() {
        return WebId.GetHashCode() ^ ListId.GetHashCode();
      }
    }

    private struct SPFieldLookupKey : IEquatable<SPFieldLookupKey>, ILookupKey<SPField> {
      public Guid ListId { get; private set; }
      public Guid FieldId { get; private set; }

      public SPFieldLookupKey(SPField field)
        : this(field.ParentList == null ? Guid.Empty : field.ParentList.ID, field.Id) { }

      public SPFieldLookupKey(Guid listId, Guid fieldId)
        : this() {
        this.ListId = listId;
        this.FieldId = fieldId;
      }

      public bool Equals(SPFieldLookupKey other) {
        return FieldId == other.FieldId && ListId == other.ListId;
      }

      public override bool Equals(object obj) {
        if (obj is SPFieldLookupKey) {
          return Equals((SPFieldLookupKey)obj);
        }
        return base.Equals(obj);
      }

      public override int GetHashCode() {
        return ListId.GetHashCode() ^ FieldId.GetHashCode();
      }
    }

    private struct SPContentTypeLookupKey : IEquatable<SPContentTypeLookupKey>, ILookupKey<SPContentType> {
      public Guid ListId { get; private set; }
      public SPContentTypeId ContentTypeId { get; private set; }

      public SPContentTypeLookupKey(SPContentType contentType)
        : this(contentType.ParentList == null ? Guid.Empty : contentType.ParentList.ID, contentType.Id) { }

      public SPContentTypeLookupKey(Guid listId, SPContentTypeId contentTypeId):this() {
        this.ListId = listId;
        this.ContentTypeId = contentTypeId;
      }

      public bool Equals(SPContentTypeLookupKey other) {
        return ContentTypeId == other.ContentTypeId && ListId == other.ListId;
      }

      public override bool Equals(object obj) {
        if (obj is SPContentTypeLookupKey) {
          return Equals((SPContentTypeLookupKey)obj);
        }
        return base.Equals(obj);
      }

      public override int GetHashCode() {
        return ListId.GetHashCode() ^ ContentTypeId.GetHashCode();
      }
    }

    private struct SPListItemLookupKey : IEquatable<SPListItemLookupKey>, ILookupKey<SPListItem> {
      public Guid ListId { get; private set; }
      public int ListItemId { get; private set; }

      public SPListItemLookupKey(SPListItem listItem)
        : this(listItem.ParentList.ID, listItem.ID) { }

      public SPListItemLookupKey(Guid listId, int listItemId)
        : this() {
        this.ListId = listId;
        this.ListItemId = listItemId;
      }

      public bool Equals(SPListItemLookupKey other) {
        return ListItemId == other.ListItemId && ListId == other.ListId;
      }

      public override bool Equals(object obj) {
        if (obj is SPListItemLookupKey) {
          return Equals((SPListItemLookupKey)obj);
        }
        return base.Equals(obj);
      }

      public override int GetHashCode() {
        return ListId.GetHashCode() ^ ListItemId.GetHashCode();
      }
    }

    private struct SPViewLookupKey : IEquatable<SPViewLookupKey>, ILookupKey<SPView> {
      public Guid WebId { get; private set; }
      public string ServerRelativeUrl { get; private set; }

      public SPViewLookupKey(SPView view)
        : this(view.ParentList.ParentWeb.ID, view.ServerRelativeUrl) { }

      public SPViewLookupKey(Guid webId, string serverRelativeUrl)
        : this() {
        this.WebId = webId;
        this.ServerRelativeUrl = serverRelativeUrl;
      }

      public bool Equals(SPViewLookupKey other) {
        return WebId == other.WebId && ServerRelativeUrl == other.ServerRelativeUrl;
      }

      public override bool Equals(object obj) {
        if (obj is SPViewLookupKey) {
          return Equals((SPViewLookupKey)obj);
        }
        return base.Equals(obj);
      }

      public override int GetHashCode() {
        return WebId.GetHashCode() ^ ServerRelativeUrl.GetHashCode();
      }
    }
    #endregion

    private readonly SPSite contextSite;
    private readonly Hashtable hashtable = new Hashtable();
    private readonly Dictionary<string, SPListLookupKey> listUrls = new Dictionary<string, SPListLookupKey>();
    private readonly Dictionary<string, SPFieldLookupKey> fieldInternalNames = new Dictionary<string, SPFieldLookupKey>();

    /// <summary>
    /// Creates an <see cref="SPObjectCache"/> instance with the specific site collection.
    /// </summary>
    /// <param name="contextSite">Site collection. All objects will be fetched on this site collection instance.</param>
    /// <exception cref="System.ArgumentNullException">Throws when input parameter <paramref name="contextSite"/> is null.</exception>
    public SPObjectCache(SPSite contextSite) {
      CommonHelper.ConfirmNotNull(contextSite, "contextSite");
      this.contextSite = contextSite;
    }

    /// <summary>
    /// Gets a enumerable collection of cached objects of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">Type of cached objects to be enumerated.</typeparam>
    /// <returns></returns>
    public IEnumerable<T> GetObjects<T>() {
      return hashtable.Values.OfType<T>();
    }

    /// <summary>
    /// Adds the given <see cref="Microsoft.SharePoint.SPWeb"/> object to the cache. 
    /// If a <see cref="Microsoft.SharePoint.SPWeb"/> with the same site GUID already exists in cache, the given one is ignored. 
    /// </summary>
    /// <param name="web">Site object.</param>
    /// <returns>An <see cref="Microsoft.SharePoint.SPWeb"/> object in cache. Returned object is not necessary the same instance as the given one.</returns>
    /// <exception cref="System.ArgumentNullException">Throws when input parameter <paramref name="web"/> is null.</exception>
    public SPWeb AddWeb(SPWeb web) {
      CommonHelper.ConfirmNotNull(web, "web");
      SPWebLookupKey lookupKey = new SPWebLookupKey(web);
      return GetOrAdd(lookupKey, web);
    }

    /// <summary>
    /// Gets an <see cref="Microsoft.SharePoint.SPWeb"/> object with the given site GUID.
    /// </summary>
    /// <param name="webId">Site GUID.</param>
    /// <returns>An <see cref="Microsoft.SharePoint.SPWeb"/> object in cache. NULL if site of given GUID does not exist.</returns>
    public SPWeb GetWeb(Guid webId) {
      SPWebLookupKey lookupKey = new SPWebLookupKey(webId);
      return GetOrAdd(lookupKey, () => contextSite.TryGetWebForCurrentUser(webId));
    }

    /// <summary>
    /// Adds the given <see cref="Microsoft.SharePoint.SPList"/> object to the cache. 
    /// If a <see cref="Microsoft.SharePoint.SPList"/> with the same list ID already exists in cache, the given one is ignored. 
    /// </summary>
    /// <param name="list">List object.</param>
    /// <returns>An <see cref="Microsoft.SharePoint.SPList"/> object in cache. Returned object is not necessary the same instance as the given one.</returns>
    /// <exception cref="System.ArgumentNullException">Throws when input parameter <paramref name="list"/> is null.</exception>
    public SPList AddList(SPList list) {
      CommonHelper.ConfirmNotNull(list, "list");
      SPListLookupKey lookupKey = new SPListLookupKey(list);
      listUrls.EnsureKeyValue(list.RootFolder.ServerRelativeUrl, () => lookupKey);
      return GetOrAdd(lookupKey, list);
    }

    /// <summary>
    /// Gets an <see cref="Microsoft.SharePoint.SPList"/> objectof the spcified list GUID, under specified site.
    /// </summary>
    /// <param name="webId">Site GUID.</param>
    /// <param name="listId">List GUID.</param>
    /// <returns>An <see cref="Microsoft.SharePoint.SPList"/> object in cache. NULL if site or list of given GUID does not exist.</returns>
    public SPList GetList(Guid webId, Guid listId) {
      SPListLookupKey lookupKey = new SPListLookupKey(webId, listId);
      SPList list = GetOrAdd(lookupKey, () => GetWeb(webId).Lists[listId]);
      if (list != null) {
        listUrls.EnsureKeyValue(list.RootFolder.ServerRelativeUrl, () => lookupKey);
      }
      return list;
    }

    /// <summary>
    /// Gets an <see cref="Microsoft.SharePoint.SPList"/> object with the given server-relative URL.
    /// </summary>
    /// <param name="listUrl">Server-relative URL of list.</param>
    /// <returns>An <see cref="Microsoft.SharePoint.SPList"/> object in cache. NULL if site or list of given GUID does not exist.</returns>
    /// <exception cref="System.ArgumentNullException">Throws when input parameter <paramref name="listUrl"/> is null.</exception>
    public SPList TryGetList(string listUrl) {
      CommonHelper.ConfirmNotNull(listUrl, "listUrl");
      try {
        SPListLookupKey listInfo = listUrls.EnsureKeyValue(listUrl, GetListInfoFromUrl);
        return GetList(listInfo.WebId, listInfo.ListId);
      } catch (ArgumentException) {
        return null;
      }
    }

    /// <summary>
    /// Adds the given <see cref="Microsoft.SharePoint.SPListItem"/> object to the cache.
    /// </summary>
    /// <param name="listItem">List item object.</param>
    /// <returns>An <see cref="Microsoft.SharePoint.SPListItem"/> object in cache. Returned object is not necessary the same instance as the given one.</returns>
    /// <exception cref="System.ArgumentNullException">Throws when input parameter <paramref name="listItem"/> is null.</exception>
    public SPListItem AddListItem(SPListItem listItem) {
      CommonHelper.ConfirmNotNull(listItem, "listItem");
      SPListItemLookupKey lookupKey = new SPListItemLookupKey(listItem);
      return GetOrAdd(lookupKey, listItem);
    }

    /// <summary>
    /// Gets an <see cref="Microsoft.SharePoint.SPListItem"/> object of the specified list item ID, under specified site and list.
    /// </summary>
    /// <param name="webId">Site GUID.</param>
    /// <param name="listId">List GUID.</param>
    /// <param name="listItemId">List item ID.</param>
    /// <returns>An <see cref="Microsoft.SharePoint.SPList"/> object in cache. NULL if site or list of given GUID does not exist, or list item of given ID does not exist in list.</returns>
    public SPListItem GetListItem(Guid webId, Guid listId, int listItemId) {
      SPListItemLookupKey lookupKey = new SPListItemLookupKey(listId, listItemId);
      return GetOrAdd(lookupKey, () => GetList(webId, listId).GetItemById(listItemId));
    }

    /// <summary>
    /// Adds the given <see cref="Microsoft.SharePoint.SPField"/> object to the cache.
    /// </summary>
    /// <param name="field">Field object.</param>
    /// <returns>An <see cref="Microsoft.SharePoint.SPField"/> object in cache. Returned object is not necessary the same instance as the given one.</returns>
    /// <exception cref="System.ArgumentNullException">Throws when input parameter <paramref name="field"/> is null.</exception>
    public SPField AddField(SPField field) {
      CommonHelper.ConfirmNotNull(field, "field");
      SPFieldLookupKey lookupKey = new SPFieldLookupKey(field);
      if (field.ParentList == null) {
        fieldInternalNames.EnsureKeyValue(field.InternalName, () => new SPFieldLookupKey(Guid.Empty, field.Id));
      }
      return GetOrAdd(lookupKey, field);
    }

    /// <summary>
    /// Gets an <see cref="Microsoft.SharePoint.SPField"/> object representing site column of the specified GUID.
    /// </summary>
    /// <param name="fieldId">Field GUID.</param>
    /// <returns>An <see cref="Microsoft.SharePoint.SPField"/> object in cache. NULL if site column of given GUID does not exist.</returns>
    public SPField GetField(Guid fieldId) {
      SPFieldLookupKey lookupKey = new SPFieldLookupKey(Guid.Empty, fieldId);
      SPField field = GetOrAdd(lookupKey, () => contextSite.RootWeb.Fields[fieldId]);
      if (field != null && field.ParentList == null) {
        fieldInternalNames.EnsureKeyValue(field.InternalName, () => new SPFieldLookupKey(Guid.Empty, field.Id));
      }
      return field;
    }

    /// <summary>
    /// Gets an <see cref="Microsoft.SharePoint.SPField"/> object representing list column of the specified GUID, under the specified list.
    /// </summary>
    /// <param name="webId">Site GUID.</param>
    /// <param name="listId">List GUID.</param>
    /// <param name="fieldId">Field GUID.</param>
    /// <returns>An <see cref="Microsoft.SharePoint.SPField"/> object in cache. NULL if list column of given GUID does not exist, or specified list does not exist.</returns>
    public SPField GetField(Guid webId, Guid listId, Guid fieldId) {
      SPFieldLookupKey lookupKey = new SPFieldLookupKey(listId, fieldId);
      SPField field = GetOrAdd(lookupKey, () => GetList(webId, listId).Fields[fieldId]);
      if (field != null && field.ParentList == null) {
        fieldInternalNames.EnsureKeyValue(field.InternalName, () => new SPFieldLookupKey(Guid.Empty, field.Id));
      }
      return field;
    }

    /// <summary>
    /// Get an <see cref="Microsoft.SharePoint.SPField"/> object representing site column of the specified internal name.
    /// </summary>
    /// <param name="internalName">Internal name.</param>
    /// <returns>An <see cref="Microsoft.SharePoint.SPField"/> object in cache. NULL if site column of given internal name does not exist.</returns>
    /// <exception cref="System.ArgumentNullException">Throws when input parameter <paramref name="internalName"/> is null.</exception>
    public SPField TryGetField(string internalName) {
      CommonHelper.ConfirmNotNull(internalName, "internalName");
      SPFieldLookupKey lookupKey;
      if (fieldInternalNames.TryGetValue(internalName, out lookupKey)) {
        return (SPField)hashtable[lookupKey];
      }
      try {
        SPField field = contextSite.RootWeb.Fields.GetFieldByInternalName(internalName);
        return AddField(field);
      } catch (ArgumentException) { }
      return null;
    }

    /// <summary>
    /// Adds the given <see cref="Microsoft.SharePoint.SPContentType"/> object to the cache.
    /// </summary>
    /// <param name="contentType">Content type object.</param>
    /// <returns>>An <see cref="Microsoft.SharePoint.SPContentType"/> object in cache. Returned object is not necessary the same instance as the given one.</returns>
    /// <exception cref="System.ArgumentNullException">Throws when input parameter <paramref name="contentType"/> is null.</exception>
    public SPContentType AddContentType(SPContentType contentType) {
      CommonHelper.ConfirmNotNull(contentType, "contentType");
      SPContentTypeLookupKey lookupKey = new SPContentTypeLookupKey(contentType);
      return GetOrAdd(lookupKey, contentType);
    }

    /// <summary>
    /// Gets an <see cref="Microsoft.SharePoint.SPContentType"/> object representing site content type of the specified content type ID.
    /// </summary>
    /// <param name="contentTypeId">Content type ID.</param>
    /// <returns>An <see cref="Microsoft.SharePoint.SPContentType"/> object in cache. NULL if site content type of given content type ID does not exist.</returns>
    public SPContentType GetContentType(SPContentTypeId contentTypeId) {
      SPContentTypeLookupKey lookupKey = new SPContentTypeLookupKey(Guid.Empty, contentTypeId);
      return GetOrAdd(lookupKey, () => contextSite.RootWeb.ContentTypes[contentTypeId]);
    }

    /// <summary>
    /// Gets an <see cref="Microsoft.SharePoint.SPContentType"/> object representing list content type of the specified content type ID under the specified list.
    /// </summary>
    /// <param name="listUrl">Server-relative URL of the list.</param>
    /// <param name="contentTypeId">List content type ID.</param>
    /// <returns>An <see cref="Microsoft.SharePoint.SPContentType"/> object in cache. NULL if list content type of given content type ID does not exist, or the specified list does not exist.</returns>
    /// <exception cref="System.ArgumentNullException">Throws when input parameter <paramref name="listUrl"/> is null.</exception>
    public SPContentType GetContentType(string listUrl, SPContentTypeId contentTypeId) {
      CommonHelper.ConfirmNotNull(listUrl, "listUrl");
      SPListLookupKey listInfo = listUrls.EnsureKeyValue(listUrl, GetListInfoFromUrl);
      SPContentTypeLookupKey lookupKey = new SPContentTypeLookupKey(listInfo.ListId, contentTypeId);
      return GetOrAdd(lookupKey, () => GetList(listInfo.WebId, listInfo.ListId).ContentTypes[contentTypeId]);
    }
    
    /// <summary>
    /// Adds the given <see cref="Microsoft.SharePoint.SPView"/> object to the cache
    /// </summary>
    /// <param name="view">View object.</param>
    /// <returns>An <see cref="Microsoft.SharePoint.SPView"/> object in cache. Returned object is not necessary the same instance as the given one.</returns>
    /// <exception cref="System.ArgumentNullException">Throws when input parameter <paramref name="view"/> is null.</exception>
    public SPView AddView(SPView view) {
      CommonHelper.ConfirmNotNull(view, "view");
      SPViewLookupKey lookupKey = new SPViewLookupKey(view);
      return GetOrAdd(lookupKey, view);
    }

    private T GetOrAdd<T>(ILookupKey<T> lookupKey, Func<T> factory) {
      T cachedItem = (T)hashtable[lookupKey];
      if (cachedItem == null) {
        try {
          cachedItem = factory();
        } catch (Exception) { }
        hashtable[lookupKey] = cachedItem;
      }
      return cachedItem;
    }

    private T GetOrAdd<T>(ILookupKey<T> lookupKey, T value) {
      T cachedItem = (T)hashtable[lookupKey];
      if (cachedItem == null) {
        cachedItem = value;
        hashtable[lookupKey] = cachedItem;
      }
      return cachedItem;
    }

    private SPListLookupKey GetListInfoFromUrl(string listUrl) {
      SPFolder folder = (SPFolder)contextSite.GetFileOrFolder(listUrl);
      if (folder == null) {
        throw new ArgumentException("listUrl");
      }
      return new SPListLookupKey(folder.ParentWeb.ID, folder.ParentListId);
    }
  }
}
