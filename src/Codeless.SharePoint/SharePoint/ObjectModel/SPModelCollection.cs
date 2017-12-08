using Microsoft.SharePoint;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Codeless.SharePoint.ObjectModel {
  /// <summary>
  /// Provides a base class for a collection of <see cref="SPModel"/> queried from a site collection.
  /// </summary>
  [DebuggerDisplay("Count = {Count}")]
  public abstract class SPModelCollection : ICollection, IEnumerable {
    /// <summary>
    /// Provides a key for fetched model class instance lookup.
    /// </summary>
    protected struct LookupKey : IEquatable<LookupKey> {
      private readonly Guid listId;
      private readonly int listItemId;

      /// <summary>
      /// Creates a key.
      /// </summary>
      /// <param name="listId">List ID.</param>
      /// <param name="listItemId">List item ID.</param>
      public LookupKey(Guid listId, int listItemId) {
        this.listId = listId;
        this.listItemId = listItemId;
      }

      /// <summary>
      /// Determines the equality of this instance to the given instance.
      /// Two date ranges are considered equal if and only if both list ID and list item ID are equal.
      /// </summary>
      /// <param name="other"></param>
      /// <returns></returns>
      public bool Equals(LookupKey other) {
        return listItemId == other.listItemId && listId == other.listId;
      }

      /// <summary>
      /// Overriden. When <paramref name="obj"/> is a <see cref="LookupKey"/> instance, the custom equality comparison is performed.
      /// </summary>
      /// <param name="obj"></param>
      /// <returns></returns>
      public override bool Equals(object obj) {
        if (obj is LookupKey) {
          return Equals((LookupKey)obj);
        }
        return base.Equals(obj);
      }

      /// <summary>
      /// Overriden.
      /// </summary>
      /// <returns></returns>
      public override int GetHashCode() {
        return listId.GetHashCode() ^ listItemId.GetHashCode();
      }
    }

    [NonSerialized]
    private object syncRoot;
    private Hashtable hashtable;
    private readonly ISPModelManagerInternal manager;
    private readonly bool readOnly;

    internal SPModelCollection(ISPModelManagerInternal manager, bool readOnly) {
      CommonHelper.ConfirmNotNull(manager, "manager");
      this.manager = manager;
      this.readOnly = readOnly;
    }

    /// <summary>
    /// Gets the number of items in this collection.
    /// </summary>
    public abstract int Count { get; }

    internal ISPModelManagerInternal Manager {
      get { return manager; }
    }

    internal bool IsReadOnly {
      get { return readOnly; }
    }

    internal IEnumerable<T> TryGetCachedModel<T>(ISPListItemAdapter source, string fieldName, params int[] lookupIds) {
      List<T> collection = new List<T>();
      SPObjectCache cache = this.Manager.ObjectCache;
      SPFieldLookup lookupField = cache.GetField(source.WebId, source.ListId, fieldName) as SPFieldLookup;
      
      if (lookupField != null) {
        if (hashtable == null) {
          hashtable = new Hashtable();
        }
        Guid listId = lookupField.LookupList == "Self" ? source.ListId : new Guid(lookupField.LookupList);
        List<int> lookupIdsToQuery = new List<int>();

        foreach (int id in lookupIds) {
          LookupKey key = new LookupKey(listId, id);
          if (hashtable.ContainsKey(key)) {
            object cachedItem = hashtable[key];
            if (cachedItem is T) {
              collection.Add((T)cachedItem);
            }
          } else {
            lookupIdsToQuery.Add(id);
          }
        }
        if (lookupIdsToQuery.Count > 0) {
          ISPModelManagerInternal manager = hashtable.EnsureKeyValue(typeof(T), () => (ISPModelManagerInternal)SPModel.GetDefaultManager(typeof(T), this.manager.Site.RootWeb, cache));
          SPList list = cache.GetList(lookupField.LookupWebId, listId);
          SPQuery query = new SPQuery { Query = Caml.EqualsAny(SPBuiltInFieldName.ID, lookupIdsToQuery).ToString() };
          
          foreach (SPListItem item in list.GetItems(query)) {
            object model = manager.TryCreateModel(new SPListItemAdapter(item, cache), false);
            hashtable[new LookupKey(listId, item.ID)] = model;
            if (model is T) {
              collection.Add((T)model);
            }
            cache.AddListItem(item);
          }
        }
      }
      return collection;
    }

    /// <summary>
    /// Copies items in this collection to the specified array at an arbitrary position.
    /// </summary>
    /// <param name="array">Destination array.</param>
    /// <param name="arrayIndex">Position at the destination array where the first item in this collection is copied to.</param>
    public abstract void CopyTo(Array array, int arrayIndex);

    /// <summary>
    /// When overriden, gets a typeless enumerator of that iterates through all items in this collection.
    /// </summary>
    /// <returns>An enumerator.</returns>
    protected abstract IEnumerator BaseGetEnumerator();

    #region Explicit Interface Implementation
    bool ICollection.IsSynchronized {
      get { return false; }
    }

    object ICollection.SyncRoot {
      get { return Interlocked.CompareExchange<object>(ref syncRoot, new object(), null); }
    }

    IEnumerator IEnumerable.GetEnumerator() {
      return BaseGetEnumerator();
    }
    #endregion
  }

  /// <summary>
  /// Provides a typed collection of <see cref="SPModel"/> queried from a site collection or search service.
  /// </summary>
  /// <typeparam name="T">Type of the items in this collection.</typeparam>
  public class SPModelCollection<T> : SPModelCollection, ICollection<T>, IList<T>, IList, IEnumerable<T> {
    private readonly Dictionary<LookupKey, SPModel> lookupDictionary = new Dictionary<LookupKey, SPModel>();
    private readonly IList<T> list = new List<T>();

    private SPModelCollection(ISPModelManagerInternal manager, bool readOnly)
      : base(manager, readOnly) { }

    /// <summary>
    /// Gets the number of items in this collection.
    /// </summary>
    public override int Count {
      get { return list.Count; }
    }

    /// <summary>
    /// Gets an item at the specified position in this collection.
    /// </summary>
    /// <param name="index">Position in this collection.</param>
    /// <exception cref="ArgumentOutOfRangeException">Throws when the given position is less than zero or greater than the largest possible value.</exception>
    /// <returns>An item at the specified position in this collection.</returns>
    public T this[int index] {
      get { return list[index]; }
    }

    /// <summary>
    /// Determines if this collection contains the specified item.
    /// </summary>
    /// <param name="item">Item to search.</param>
    /// <returns>*true* if this collection contains the specified item; otherwise *false*.</returns>
    public bool Contains(T item) {
      return list.Contains(item);
    }

    /// <summary>
    /// Copies items in this collection to the specified array at an arbitrary position.
    /// </summary>
    /// <param name="array">Destination array.</param>
    /// <param name="arrayIndex">Position at the destination array where the first item in this collection is copied to.</param>
    public override void CopyTo(Array array, int arrayIndex) {
      CommonHelper.ConfirmNotNull(array, "array");
      T[] typedArray = CommonHelper.TryCastOrDefault<T[]>(array);
      if (typedArray == null) {
        throw new ArgumentException("array");
      }
      this.CopyTo(typedArray, arrayIndex);
    }

    /// <summary>
    /// Copies items in this collection to the specified array at an arbitrary position.
    /// </summary>
    /// <param name="array">Destination array.</param>
    /// <param name="arrayIndex">Position at the destination array where the first item in this collection is copied to.</param>
    public void CopyTo(T[] array, int arrayIndex) {
      list.CopyTo(array, arrayIndex);
    }

    /// <summary>
    /// Gets an enumerator of type <typeparamref name="T"/> that iterates through all items in this collection.
    /// </summary>
    /// <returns>An enumerator.</returns>
    public IEnumerator<T> GetEnumerator() {
      return list.GetEnumerator();
    }

    /// <summary>
    /// Gets the position of the specified item in this collection.
    /// </summary>
    /// <param name="item">Item to search.</param>
    /// <returns>The position the item if this collection contains the specified item; othewise -1.</returns>
    public int IndexOf(T item) {
      return list.IndexOf(item);
    }

    internal static SPModelCollection<T> Create(ISPModelManagerInternal manager, IEnumerable<ISPListItemAdapter> queryResultSet, bool readOnly) {
      CommonHelper.ConfirmNotNull(queryResultSet, "queryResultSet");
      SPModelCollection<T> collection = new SPModelCollection<T>(manager, readOnly);
      foreach (ISPListItemAdapter item in queryResultSet) {
        SPModel modelItem = SPModel.TryCreate(item, collection);
        if (modelItem != null) {
          modelItem = collection.lookupDictionary.EnsureKeyValue(new LookupKey(item.ListId, item.ListItemId), () => modelItem);
          if (modelItem is T) {
            collection.list.Add((T)(object)modelItem);
          }
        }
      }
      return collection;
    }

    /// <summary>
    /// Gets a typeless enumerator of that iterates through all items in this collection.
    /// </summary>
    /// <returns>An enumerator.</returns>
    protected override IEnumerator BaseGetEnumerator() {
      return GetEnumerator();
    }

    #region Explicit Interface Implementation
    bool IList.IsFixedSize {
      get { return true; }
    }

    bool IList.IsReadOnly {
      get { return true; }
    }

    object IList.this[int index] {
      get { return this[index]; }
      set { throw new NotSupportedException(); }
    }

    T IList<T>.this[int index] {
      get { return this[index]; }
      set { throw new NotSupportedException(); }
    }

    bool ICollection<T>.IsReadOnly {
      get { return true; }
    }

    bool IList.Contains(object value) {
      if (value is T) {
        return this.Contains((T)value);
      }
      return false;
    }

    int IList.IndexOf(object value) {
      if (value is T) {
        return this.IndexOf((T)value);
      }
      return -1;
    }
    #endregion

    #region Not Supported
    int IList.Add(object value) {
      throw new NotSupportedException();
    }

    void IList.Clear() {
      throw new NotSupportedException();
    }

    void IList.Insert(int index, object value) {
      throw new NotSupportedException();
    }

    void IList.Remove(object value) {
      throw new NotSupportedException();
    }

    void IList.RemoveAt(int index) {
      throw new NotSupportedException();
    }

    void IList<T>.Insert(int index, T item) {
      throw new NotSupportedException();
    }

    void IList<T>.RemoveAt(int index) {
      throw new NotSupportedException();
    }

    void ICollection<T>.Add(T item) {
      throw new NotSupportedException();
    }

    void ICollection<T>.Clear() {
      throw new NotSupportedException();
    }

    bool ICollection<T>.Remove(T item) {
      throw new NotSupportedException();
    }
    #endregion
  }
}
