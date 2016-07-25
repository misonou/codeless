using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Codeless {
  /// <summary>
  /// Provides a thread-safe keyed collection that for any key, the singleton value factory delegate is executed exactly one only.
  /// </summary>
  /// <typeparam name="TKey">Type of keys.</typeparam>
  /// <typeparam name="TItem">Type of values.</typeparam>
  public class ConcurrentFactory<TKey, TItem> : IDictionary<TKey, TItem>, IDictionary {
    private readonly object syncLock = new object();
    private readonly ConcurrentDictionary<TKey, Lazy<TItem>> dictionary = new ConcurrentDictionary<TKey, Lazy<TItem>>();

    /// <summary>
    /// Gets an instance of type <typeparamref name="TItem"/>.
    /// </summary>
    /// <param name="key">Key of an item.</param>
    /// <param name="valueFactory">A delegate that intakes a key and generate a value.</param>
    /// <returns>A singleton item corresponding to the specified key.</returns>
    public TItem GetInstance(TKey key, Func<TKey, TItem> valueFactory) {
      CommonHelper.ConfirmNotNull(key, "key");
      CommonHelper.ConfirmNotNull(valueFactory, "valueFactory");
      return GetInstance(key, () => valueFactory(key));
    }

    /// <summary>
    /// Gets an instance of type <typeparamref name="TItem"/>.
    /// </summary>
    /// <param name="key">Key of an item.</param>
    /// <param name="valueFactory">A delegate that generate a value.</param>
    /// <returns>A singleton item corresponding to the specified key.</returns>
    public TItem GetInstance(TKey key, Func<TItem> valueFactory) {
      CommonHelper.ConfirmNotNull(key, "key");
      CommonHelper.ConfirmNotNull(valueFactory, "valueFactory");
      Lazy<TItem> lazyInitializer = new Lazy<TItem>(valueFactory, LazyThreadSafetyMode.ExecutionAndPublication);
      lazyInitializer = dictionary.GetOrAdd(key, lazyInitializer);
      try {
        return lazyInitializer.Value;
      } catch {
        dictionary.TryRemove(key, out lazyInitializer);
        throw;
      }
    }

    /// <summary>
    /// Removes a singleton value for a specified key.
    /// </summary>
    /// <param name="key">Key of an item.</param>
    /// <returns>A boolean indicating whether the removal executes successfully or not.</returns>
    public bool Destroy(TKey key) {
      Lazy<TItem> dummy;
      return dictionary.TryRemove(key, out dummy);
    }

    /// <summary>
    /// Clears all entries in this collection.
    /// </summary>
    public void Clear() {
      dictionary.Clear();
    }

    /// <summary>
    /// Gets an enumerator of each entry in this collection.
    /// </summary>
    /// <returns>An enumerator.</returns>
    public IEnumerator<KeyValuePair<TKey, TItem>> GetEnumerator() {
      foreach (KeyValuePair<TKey, Lazy<TItem>> entry in dictionary) {
        yield return new KeyValuePair<TKey, TItem>(entry.Key, entry.Value.Value);
      }
    }

    #region IDictionary
    void IDictionary<TKey, TItem>.Add(TKey key, TItem value) {
      dictionary.GetOrAdd(key, new Lazy<TItem>(() => value));
    }

    bool IDictionary<TKey, TItem>.ContainsKey(TKey key) {
      return dictionary.ContainsKey(key);
    }

    ICollection<TKey> IDictionary<TKey, TItem>.Keys {
      get { return dictionary.Keys; }
    }

    bool IDictionary<TKey, TItem>.Remove(TKey key) {
      return Destroy(key);
    }

    bool IDictionary<TKey, TItem>.TryGetValue(TKey key, out TItem value) {
      Lazy<TItem> initializer;
      if (dictionary.TryGetValue(key, out initializer)) {
        value = initializer.Value;
        return true;
      }
      value = default(TItem);
      return false;
    }

    ICollection<TItem> IDictionary<TKey, TItem>.Values {
      get {
        lock (syncLock) {
          int n = 0;
          TItem[] values = new TItem[dictionary.Count];
          foreach (Lazy<TItem> initializer in dictionary.Values) {
            values[n++] = initializer.Value;
          }
          return values;
        }
      }
    }

    TItem IDictionary<TKey, TItem>.this[TKey key] {
      get {
        Lazy<TItem> initializer;
        if (dictionary.TryGetValue(key, out initializer)) {
          return initializer.Value;
        }
        throw new ArgumentOutOfRangeException("key");
      }
      set {
        Lazy<TItem> initializer = new Lazy<TItem>(() => value);
        dictionary.AddOrUpdate(key, initializer, (k, v) => initializer);
      }
    }

    void ICollection<KeyValuePair<TKey, TItem>>.Add(KeyValuePair<TKey, TItem> item) {
      if (!dictionary.TryAdd(item.Key, new Lazy<TItem>(() => item.Value))) {
        throw new ArgumentException("An element with the same key already exist");
      }
    }

    bool ICollection<KeyValuePair<TKey, TItem>>.Contains(KeyValuePair<TKey, TItem> item) {
      Lazy<TItem> initializer;
      if (dictionary.TryGetValue(item.Key, out initializer)) {
        return EqualityComparer<TItem>.Default.Equals(initializer.Value, item.Value);
      }
      return false;
    }

    void ICollection<KeyValuePair<TKey, TItem>>.CopyTo(KeyValuePair<TKey, TItem>[] array, int arrayIndex) {
      throw new NotImplementedException();
    }

    int ICollection<KeyValuePair<TKey, TItem>>.Count {
      get { return dictionary.Count; }
    }

    bool ICollection<KeyValuePair<TKey, TItem>>.IsReadOnly {
      get { return false; }
    }

    bool ICollection<KeyValuePair<TKey, TItem>>.Remove(KeyValuePair<TKey, TItem> item) {
      lock (syncLock) {
        Lazy<TItem> initializer;
        if (dictionary.TryGetValue(item.Key, out initializer)) {
          if (EqualityComparer<TItem>.Default.Equals(initializer.Value, item.Value)) {
            return dictionary.TryRemove(item.Key, out initializer);
          }
        }
        return false;
      }
    }

    void IDictionary.Add(object key, object value) {
      TItem typedItem = (TItem)value;
      dictionary.GetOrAdd((TKey)key, new Lazy<TItem>(() => typedItem));
    }

    void IDictionary.Clear() {
      dictionary.Clear();
    }

    bool IDictionary.Contains(object key) {
      return dictionary.ContainsKey((TKey)key);
    }

    IDictionaryEnumerator IDictionary.GetEnumerator() {
      return ((IDictionary)dictionary).GetEnumerator();
    }

    bool IDictionary.IsFixedSize {
      get { return false; }
    }

    bool IDictionary.IsReadOnly {
      get { return false; }
    }

    ICollection IDictionary.Keys {
      get { return ((IDictionary)dictionary).Keys; }
    }

    void IDictionary.Remove(object key) {
      Lazy<TItem> value;
      dictionary.TryRemove((TKey)key, out value);
    }

    ICollection IDictionary.Values {
      get {
        lock (syncLock) {
          int n = 0;
          TItem[] values = new TItem[dictionary.Count];
          foreach (Lazy<TItem> initializer in dictionary.Values) {
            values[n++] = initializer.Value;
          }
          return values;
        }
      }
    }

    object IDictionary.this[object key] {
      get {
        Lazy<TItem> initializer;
        if (dictionary.TryGetValue((TKey)key, out initializer)) {
          return initializer.Value;
        }
        throw new ArgumentOutOfRangeException("key");
      }
      set {
        TItem typedItem = (TItem)value;
        Lazy<TItem> initializer = new Lazy<TItem>(() => typedItem);
        dictionary.AddOrUpdate((TKey)key, initializer, (k, v) => initializer);
      }
    }

    void ICollection.CopyTo(Array array, int index) {
      throw new NotImplementedException();
    }

    int ICollection.Count {
      get { return dictionary.Count; }
    }

    bool ICollection.IsSynchronized {
      get { return true; }
    }

    object ICollection.SyncRoot {
      get { return syncLock; }
    }

    IEnumerator IEnumerable.GetEnumerator() {
      return GetEnumerator();
    }
    #endregion
  }
}
