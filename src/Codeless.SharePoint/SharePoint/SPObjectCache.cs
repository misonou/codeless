using Codeless.SharePoint.Internal;
using Microsoft.SharePoint;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Codeless.SharePoint {
  /// <summary>
  /// Provides caching and uniqueness to database persisted SharePoint objects.
  /// </summary>
  public sealed class SPObjectCache : IDisposable {
    #region Helper Class
    private class SPReusableAclLookupKey : IEquatable<SPReusableAclLookupKey> {
      public Guid ScopeId { get; private set; }

      public SPReusableAclLookupKey(Guid scopeId) {
        this.ScopeId = scopeId;
      }

      public bool Equals(SPReusableAclLookupKey other) {
        return ScopeId == other.ScopeId;
      }

      public override bool Equals(object obj) {
        SPReusableAclLookupKey other = obj as SPReusableAclLookupKey;
        return other != null && Equals(other);
      }

      public override int GetHashCode() {
        return ScopeId.GetHashCode();
      }
    }

    private class SPWebLookupKey : IEquatable<SPWebLookupKey> {
      public Guid WebId { get; private set; }

      public SPWebLookupKey(SPWeb web)
        : this(web.ID) { }

      public SPWebLookupKey(Guid webId) {
        this.WebId = webId;
      }

      public bool Equals(SPWebLookupKey other) {
        return WebId == other.WebId;
      }

      public override bool Equals(object obj) {
        SPWebLookupKey other = obj as SPWebLookupKey;
        return other != null && Equals(other);
      }

      public override int GetHashCode() {
        return WebId.GetHashCode();
      }
    }

    private class SPListLookupKey : IEquatable<SPListLookupKey> {
      public Guid WebId { get; private set; }
      public Guid ListId { get; private set; }

      public SPListLookupKey(SPList list)
        : this(list.ParentWeb.ID, list.ID) { }

      public SPListLookupKey(Guid webId, Guid listId) {
        this.WebId = webId;
        this.ListId = listId;
      }

      public bool Equals(SPListLookupKey other) {
        return ListId == other.ListId && WebId == other.WebId;
      }

      public override bool Equals(object obj) {
        SPListLookupKey other = obj as SPListLookupKey;
        return other != null && Equals(other);
      }

      public override int GetHashCode() {
        return WebId.GetHashCode() ^ ListId.GetHashCode();
      }
    }

    private class SPFieldLookupKey : IEquatable<SPFieldLookupKey> {
      public Guid ListId { get; private set; }
      public Guid FieldId { get; private set; }

      public SPFieldLookupKey(SPField field)
        : this(field.ParentList == null ? Guid.Empty : field.ParentList.ID, field.Id) { }

      public SPFieldLookupKey(Guid listId, Guid fieldId) {
        this.ListId = listId;
        this.FieldId = fieldId;
      }

      public bool Equals(SPFieldLookupKey other) {
        return FieldId == other.FieldId && ListId == other.ListId;
      }

      public override bool Equals(object obj) {
        SPFieldLookupKey other = obj as SPFieldLookupKey;
        return other != null && Equals(other);
      }

      public override int GetHashCode() {
        return ListId.GetHashCode() ^ FieldId.GetHashCode();
      }
    }

    private class SPContentTypeLookupKey : IEquatable<SPContentTypeLookupKey> {
      public Guid ListId { get; private set; }
      public SPContentTypeId ContentTypeId { get; private set; }

      public SPContentTypeLookupKey(SPContentType contentType)
        : this(contentType.ParentList == null ? Guid.Empty : contentType.ParentList.ID, contentType.Id) { }

      public SPContentTypeLookupKey(Guid listId, SPContentTypeId contentTypeId) {
        this.ListId = listId;
        this.ContentTypeId = contentTypeId;
      }

      public bool Equals(SPContentTypeLookupKey other) {
        return ContentTypeId == other.ContentTypeId && ListId == other.ListId;
      }

      public override bool Equals(object obj) {
        SPContentTypeLookupKey other = obj as SPContentTypeLookupKey;
        return other != null && Equals(other);
      }

      public override int GetHashCode() {
        return ListId.GetHashCode() ^ ContentTypeId.GetHashCode();
      }
    }

    private class SPListItemLookupKey : IEquatable<SPListItemLookupKey> {
      public Guid ListId { get; private set; }
      public int ListItemId { get; private set; }

      public SPListItemLookupKey(SPListItem listItem)
        : this(listItem.ParentList.ID, listItem.ID) { }

      public SPListItemLookupKey(Guid listId, int listItemId) {
        this.ListId = listId;
        this.ListItemId = listItemId;
      }

      public bool Equals(SPListItemLookupKey other) {
        return ListItemId == other.ListItemId && ListId == other.ListId;
      }

      public override bool Equals(object obj) {
        SPListItemLookupKey other = obj as SPListItemLookupKey;
        return other != null && Equals(other);
      }

      public override int GetHashCode() {
        return ListId.GetHashCode() ^ ListItemId.GetHashCode();
      }
    }

    private class SPViewLookupKey : IEquatable<SPViewLookupKey> {
      public Guid WebId { get; private set; }
      public string ServerRelativeUrl { get; private set; }

      public SPViewLookupKey(SPView view)
        : this(view.ParentList.ParentWeb.ID, view.ServerRelativeUrl) { }

      public SPViewLookupKey(Guid webId, string serverRelativeUrl) {
        this.WebId = webId;
        this.ServerRelativeUrl = serverRelativeUrl;
      }

      public bool Equals(SPViewLookupKey other) {
        return WebId == other.WebId && ServerRelativeUrl == other.ServerRelativeUrl;
      }

      public override bool Equals(object obj) {
        SPViewLookupKey other = obj as SPViewLookupKey;
        return other != null && Equals(other);
      }

      public override int GetHashCode() {
        return WebId.GetHashCode() ^ ServerRelativeUrl.GetHashCode();
      }
    }
    #endregion

    private static readonly PropertyInfo fieldNode = typeof(SPField).GetProperty("Node", true);

    private readonly SPSite contextSite;
    private readonly Hashtable hashtable = new Hashtable();
    private readonly HashSet<Guid> scopeIds = new HashSet<Guid>();
    private readonly List<IDisposable> disposables = new List<IDisposable>();
    private bool disposed;

    /// <summary>
    /// Creates an <see cref="SPObjectCache"/> instance with the specific site collection.
    /// </summary>
    /// <param name="contextSite">Site collection. All objects will be fetched on this site collection instance.</param>
    /// <exception cref="System.ArgumentNullException">Throws when input parameter <paramref name="contextSite"/> is null.</exception>
    public SPObjectCache(SPSite contextSite) {
      CommonHelper.ConfirmNotNull(contextSite, "contextSite");
      this.contextSite = contextSite;
    }

    private SPObjectCache(SPContext context) {
      CommonHelper.ConfirmNotNull(context, "context");
      this.contextSite = context.Site;
      using (new SPSecurity.SuppressAccessDeniedRedirectInScope()) {
        AddWeb(context.Web);
        if (context.List != null) {
          AddList(context.List);
        }
        if (context.ListItem != null) {
          AddListItem(context.ListItem);
        }
      }
    }

    internal static SPObjectCache GetInstanceForCurrentContext() {
      if (SPContext.Current == null) {
        throw new InvalidOperationException();
      }
      return CommonHelper.HttpContextSingleton(() => new SPObjectCache(SPContext.Current));
    }

    /// <summary>
    /// Gets or sets object associated with a specified key in the cache.
    /// </summary>
    /// <param name="key">A string representing the key associated with a cached object.</param>
    /// <returns>The cached object associated with the specified key; -or- *null* if the specified key does not exist in the cache.</returns>
    public object this[string key] {
      get { return hashtable[key]; }
      set { hashtable[key] = value; }
    }

    /// <summary>
    /// Releases resources held by this object cache.
    /// </summary>
    public void Dispose() {
      if (!disposed) {
        List<IDisposable> list = new List<IDisposable>(disposables);
        foreach (IDisposable item in list) {
          item.Dispose();
          disposables.Remove(item);
        }
        disposed = true;
      }
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
      hashtable.EnsureKeyValue(web.ServerRelativeUrl, () => new SPListLookupKey(web.ID, Guid.Empty));
      return GetOrAdd(lookupKey, web);
    }

    /// <summary>
    /// Gets an <see cref="Microsoft.SharePoint.SPWeb"/> object with the given site GUID.
    /// </summary>
    /// <param name="webId">Site GUID.</param>
    /// <returns>An <see cref="Microsoft.SharePoint.SPWeb"/> object in cache. NULL if site of given GUID does not exist.</returns>
    public SPWeb GetWeb(Guid webId) {
      SPWebLookupKey lookupKey = new SPWebLookupKey(webId);
      SPWeb web = GetOrAdd(lookupKey, () => OpenWeb(webId));
      if (web != null) {
        hashtable.EnsureKeyValue(web.ServerRelativeUrl, () => new SPListLookupKey(web.ID, Guid.Empty));
      }
      return web;
    }

    /// <summary>
    /// Gets an <see cref="Microsoft.SharePoint.SPWeb"/> object with the given URL.
    /// </summary>
    /// <param name="webUrl">Site URL.</param>
    /// <returns>An <see cref="Microsoft.SharePoint.SPWeb"/> object in cache. NULL if site of given URL does not exist.</returns>
    public SPWeb TryGetWeb(string webUrl) {
      CommonHelper.ConfirmNotNull(webUrl, "webUrl");
      SPListLookupKey lookupKey = hashtable[webUrl] as SPListLookupKey;
      if (lookupKey != null) {
        return GetWeb(lookupKey.WebId);
      }
      SPWeb web = SPExtensionHelper.OpenWebSafe(contextSite, webUrl, false);
      if (web != null) {
        SPWeb returnValue = AddWeb(web);
        if (returnValue != web) {
          web.Dispose();
        }
        return returnValue;
      }
      return null;
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
      hashtable.EnsureKeyValue(list.RootFolder.ServerRelativeUrl, () => lookupKey);
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
        hashtable.EnsureKeyValue(list.RootFolder.ServerRelativeUrl, () => lookupKey);
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
      SPListLookupKey listInfo = hashtable.EnsureKeyValue(listUrl, () => GetListInfoFromUrl(listUrl));
      return GetList(listInfo.WebId, listInfo.ListId);
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
      AddList(listItem.ParentList);
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
        hashtable.EnsureKeyValue(field.InternalName, () => new SPFieldLookupKey(Guid.Empty, field.Id));
      } else {
        AddList(field.ParentList);
      }
      EnsureLocalXmlNode(field);
      return GetOrAdd(lookupKey, field);
    }

    /// <summary>
    /// Gets an <see cref="Microsoft.SharePoint.SPField"/> object representing site column of the specified GUID.
    /// </summary>
    /// <param name="fieldId">Field GUID.</param>
    /// <returns>An <see cref="Microsoft.SharePoint.SPField"/> object in cache. NULL if site column of given GUID does not exist.</returns>
    public SPField GetField(Guid fieldId) {
      SPFieldLookupKey lookupKey = new SPFieldLookupKey(Guid.Empty, fieldId);
      SPField field = GetOrAdd(lookupKey, () => EnsureLocalXmlNode(contextSite.RootWeb.Fields[fieldId]));
      if (field != null) {
        hashtable.EnsureKeyValue(field.InternalName, () => new SPFieldLookupKey(Guid.Empty, field.Id));
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
      SPField field = GetOrAdd(lookupKey, () => EnsureLocalXmlNode(GetList(webId, listId).Fields[fieldId]));
      return field;
    }

    /// <summary>
    /// Gets an <see cref="Microsoft.SharePoint.SPField"/> object representing list column of the specified internal name, under the specified list.
    /// </summary>
    /// <param name="webId">Site GUID.</param>
    /// <param name="listId">List GUID.</param>
    /// <param name="internalName">Internal name of the list column</param>
    /// <returns>An <see cref="Microsoft.SharePoint.SPField"/> object in cache. NULL if list column of given internal name does not exist, or specified list does not exist.</returns>
    public SPField GetField(Guid webId, Guid listId, string internalName) {
      SPField field = TryGetField(internalName);
      if (field != null) {
        return GetField(webId, listId, field.Id);
      }
      return null;
    }

    /// <summary>
    /// Get an <see cref="Microsoft.SharePoint.SPField"/> object representing site column of the specified internal name.
    /// </summary>
    /// <param name="internalName">Internal name.</param>
    /// <returns>An <see cref="Microsoft.SharePoint.SPField"/> object in cache. NULL if site column of given internal name does not exist.</returns>
    /// <exception cref="System.ArgumentNullException">Throws when input parameter <paramref name="internalName"/> is null.</exception>
    public SPField TryGetField(string internalName) {
      CommonHelper.ConfirmNotNull(internalName, "internalName");
      SPFieldLookupKey lookupKey = hashtable[internalName] as SPFieldLookupKey;
      if (lookupKey != null) {
        return GetField(lookupKey.FieldId);
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
      SPListLookupKey listInfo = hashtable.EnsureKeyValue(listUrl, () => GetListInfoFromUrl(listUrl));
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

    /// <summary>
    /// Requests an <see cref="SPReusableAcl"/> object representing ACL information of the specified scope ID in advance.
    /// If the requested object has not been loaded, it will be loaded in batch in the next time <see cref="GetReusableAcl(Guid)"/> is called.
    /// </summary>
    /// <param name="scopeId">Scope ID.</param>
    public void RequestReusableAcl(Guid scopeId) {
      if (!hashtable.ContainsKey(new SPReusableAclLookupKey(scopeId))) {
        scopeIds.Add(scopeId);
      }
    }

    /// <summary>
    /// Gets an <see cref="SPReusableAcl"/> object representing ACL information of the specified scope ID.
    /// </summary>
    /// <param name="scopeId">Scope ID.</param>
    /// <returns>An <see cref="SPReusableAcl"/> object in cache. NULL if the specified scope ID does not exist in the site collection.</returns>
    public SPReusableAcl GetReusableAcl(Guid scopeId) {
      SPReusableAclLookupKey lookupKey = new SPReusableAclLookupKey(scopeId);
      return GetOrAdd(lookupKey, () => {
        contextSite.WithElevatedPrivileges(elevatedSite => {
          scopeIds.Add(scopeId);
          foreach (Guid id in scopeIds) {
            try {
              hashtable[new SPReusableAclLookupKey(id)] = elevatedSite.GetReusableAclForScope(id);
            } catch {
              hashtable[new SPReusableAclLookupKey(id)] = null;
            }
          }
          scopeIds.Clear();
        });
        return (SPReusableAcl)hashtable[lookupKey];
      });
    }

    private T GetOrAdd<T>(object lookupKey, Func<T> factory) {
      if (hashtable.ContainsKey(lookupKey)) {
        return (T)hashtable[lookupKey];
      }
      T value = default(T);
      try {
        value = factory();
      } catch {
        return value;
      }
      hashtable[lookupKey] = value;
      return value;
    }

    private T GetOrAdd<T>(object lookupKey, T value) {
      if (hashtable.ContainsKey(lookupKey)) {
        return (T)hashtable[lookupKey];
      }
      hashtable[lookupKey] = value;
      return value;
    }

    private SPWeb OpenWeb(Guid webId) {
      SPWeb web = SPExtensionHelper.OpenWebSafe(contextSite, webId);
      disposables.Add(web);
      return web;
    }

    private SPField EnsureLocalXmlNode(SPField field) {
      // SPField object points to XML data in an array shared by the field collection until becoming dirty
      // however adding new field to the collection and causing the shared array to mutate
      // clean SPField object may corrupt as the same array index would instead point to XML data for another field
      fieldNode.GetValue<object>(field);
      return field;
    }

    private SPListLookupKey GetListInfoFromUrl(string listUrl) {
      SPWeb web = TryGetWeb(listUrl);
      if (web == null) {
        return new SPListLookupKey(Guid.Empty, Guid.Empty);
      }
      object fileOrFolder = web.GetFileOrFolderObjectSafe(listUrl);
      SPFolder folder = fileOrFolder as SPFolder;
      if (folder != null) {
        return new SPListLookupKey(web.ID, folder.ParentListId);
      }
      SPFile file = fileOrFolder as SPFile;
      if (file != null) {
        return new SPListLookupKey(web.ID, file.ParentFolder.ParentListId);
      }
      return new SPListLookupKey(web.ID, Guid.Empty);
    }
  }
}
