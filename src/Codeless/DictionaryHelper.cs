using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace Codeless {
  /// <summary>
  /// Provides extension methods to objects implementing the <see cref="IDictionary"/> interface.
  /// </summary>
  public static class DictionaryHelper {
    /// <summary>
    /// Checks if an dictionary contains the specified key, otherwise an object of type <typeparamref name="TValue"/> instantiated by 
    /// its parameterless constructor is added to the dictionary with the specified key.
    /// </summary>
    /// <typeparam name="TValue">Type of the object to be added.</typeparam>
    /// <param name="dictionary">An dictionary.</param>
    /// <param name="key">A key that is accepted by the target dictionary.</param>
    /// <returns>An existing value in the dictionary if the key is found and its associated value is of type <typeparamref name="TValue"/>; othewise a new instance of type <typeparamref name="TValue"/>.</returns>
    [DebuggerStepThrough]
    public static TValue EnsureKeyValue<TValue>(this IDictionary dictionary, object key) where TValue : new() {
      CommonHelper.ConfirmNotNull(dictionary, "dictionary");
      CommonHelper.ConfirmNotNull(key, "key");
      object value = dictionary[key];
      if (value is TValue) {
        return (TValue)value;
      }
      TValue newValue = ReflectionHelper.CreateInstance<TValue>();
      dictionary[key] = newValue;
      return newValue;
    }

    /// <summary>
    /// Checks if an dictionary contains the specified key, otherwise an object of type <typeparamref name="TValue"/> returned by 
    /// a value factory delegate is added to the dictionary with the specified key.
    /// </summary>
    /// <typeparam name="TValue">Type of the object to be added.</typeparam>
    /// <param name="dictionary">An dictionary.</param>
    /// <param name="key">A key that is accepted by the target dictionary.</param>
    /// <param name="valueFactory">A value factory delegate that will be called if a new value is needed to be added to the dictionary.</param>
    /// <returns>An existing value in the dictionary if the key is found and its associated value is of type <typeparamref name="TValue"/>; othewise a new instance of type <typeparamref name="TValue"/> returned by the value factory.</returns>
    [DebuggerStepThrough]
    public static TValue EnsureKeyValue<TValue>(this IDictionary dictionary, object key, Func<TValue> valueFactory) {
      CommonHelper.ConfirmNotNull(dictionary, "dictionary");
      CommonHelper.ConfirmNotNull(key, "key");
      CommonHelper.ConfirmNotNull(valueFactory, "valueFactory");
      object value = dictionary[key];
      if (value is TValue) {
        return (TValue)value;
      }
      TValue newValue = valueFactory();
      if (Object.ReferenceEquals(newValue, null)) {
        throw new InvalidOperationException("valueFactory returned null");
      }
      dictionary[key] = newValue;
      return newValue;
    }

    /// <summary>
    /// Checks if a typed dictionary contains the specified key, otherwise an object of type <typeparamref name="TValue"/> instantiated by 
    /// its parameterless constructor is added to the dictionary with the specified key.
    /// </summary>
    /// <typeparam name="TKey">Type of keys accepted by the dictionary.</typeparam>
    /// <typeparam name="TValue">Type of the objects to be added.</typeparam>
    /// <param name="dictionary">An dictionary.</param>
    /// <param name="key">A key that is accepted by the target dictionary.</param>
    /// <returns>An existing value in the dictionary if the key is found and its associated value is of type <typeparamref name="TValue"/>; othewise a new instance of type <typeparamref name="TValue"/>.</returns>
    [DebuggerStepThrough]
    public static TValue EnsureKeyValue<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key) where TValue : new() {
      CommonHelper.ConfirmNotNull(dictionary, "dictionary");
      CommonHelper.ConfirmNotNull(key, "key");
      ConcurrentDictionary<TKey, TValue> concurrent = dictionary as ConcurrentDictionary<TKey, TValue>;
      if (concurrent != null) {
        return concurrent.EnsureKeyValue(key);
      }
      TValue value;
      if (dictionary.TryGetValue(key, out value)) {
        return value;
      }
      TValue newValue = ReflectionHelper.CreateInstance<TValue>();
      dictionary.Add(key, newValue);
      return newValue;
    }

    /// <summary>
    /// Checks if a typed dictionary contains the specified key, otherwise an object of type <typeparamref name="TValue"/> returned by 
    /// a value factory delegate is added to the dictionary with the specified key.
    /// </summary>
    /// <typeparam name="TKey">Type of keys accepted by the dictionary.</typeparam>
    /// <typeparam name="TValue">Type of the objects to be added.</typeparam>
    /// <param name="dictionary">An dictionary.</param>
    /// <param name="key">A key that is accepted by the target dictionary.</param>
    /// <param name="valueFactory">A value factory delegate that will be called if a new value is needed to be added to the dictionary.</param>
    /// <returns>An existing value in the dictionary if the key is found and its associated value is of type <typeparamref name="TValue"/>; othewise a new instance of type <typeparamref name="TValue"/> returned by the value factory.</returns>
    [DebuggerStepThrough]
    public static TValue EnsureKeyValue<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, Func<TValue> valueFactory) {
      CommonHelper.ConfirmNotNull(dictionary, "dictionary");
      CommonHelper.ConfirmNotNull(valueFactory, "valueFactory");
      return EnsureKeyValue(dictionary, key, v => valueFactory());
    }

    /// <summary>
    /// Checks if a typed dictionary contains the specified key, otherwise an object of type <typeparamref name="TValue"/> returned by 
    /// a value factory delegate is added to the dictionary with the specified key.
    /// </summary>
    /// <typeparam name="TKey">Type of keys accepted by the dictionary.</typeparam>
    /// <typeparam name="TValue">Type of the objects to be added.</typeparam>
    /// <param name="dictionary">An dictionary.</param>
    /// <param name="key">A key that is accepted by the target dictionary.</param>
    /// <param name="valueFactory">A value factory delegate that will be called if a new value is needed to be added to the dictionary.</param>
    /// <returns>An existing value in the dictionary if the key is found and its associated value is of type <typeparamref name="TValue"/>; othewise a new instance of type <typeparamref name="TValue"/> returned by the value factory.</returns>
    [DebuggerStepThrough]
    public static TValue EnsureKeyValue<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, Func<TKey, TValue> valueFactory) {
      CommonHelper.ConfirmNotNull(dictionary, "dictionary");
      CommonHelper.ConfirmNotNull(key, "key");
      CommonHelper.ConfirmNotNull(valueFactory, "valueFactory");
      ConcurrentDictionary<TKey, TValue> concurrent = dictionary as ConcurrentDictionary<TKey, TValue>;
      if (concurrent != null) {
        return concurrent.EnsureKeyValue(key, valueFactory);
      }
      TValue value;
      if (dictionary.TryGetValue(key, out value)) {
        return value;
      }
      TValue newValue = valueFactory(key);
      if (Object.ReferenceEquals(newValue, null)) {
        throw new InvalidOperationException("valueFactory returned null");
      }
      dictionary.Add(key, newValue);
      return newValue;
    }

    /// <summary>
    /// Checks if a thread-safe dictionary contains the specified key, otherwise an object of type <typeparamref name="TValue"/> instantiated by 
    /// its parameterless constructor is added to the dictionary with the specified key.
    /// </summary>
    /// <typeparam name="TKey">Type of keys accepted by the dictionary.</typeparam>
    /// <typeparam name="TValue">Type of the objects to be added.</typeparam>
    /// <param name="dictionary">An dictionary.</param>
    /// <param name="key">A key that is accepted by the target dictionary.</param>
    /// <returns>An existing value in the dictionary if the key is found and its associated value is of type <typeparamref name="TValue"/>; othewise a new instance of type <typeparamref name="TValue"/>.</returns>
    [DebuggerStepThrough]
    public static TValue EnsureKeyValue<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> dictionary, TKey key) where TValue : new() {
      return EnsureKeyValue(dictionary, key, ReflectionHelper.CreateInstance<TValue>);
    }

    /// <summary>
    /// Checks if a thread-safe dictionary contains the specified key, otherwise an object of type <typeparamref name="TValue"/> returned by 
    /// a value factory delegate is added to the dictionary with the specified key.
    /// </summary>
    /// <typeparam name="TKey">Type of keys accepted by the dictionary.</typeparam>
    /// <typeparam name="TValue">Type of the objects to be added.</typeparam>
    /// <param name="dictionary">An dictionary.</param>
    /// <param name="key">A key that is accepted by the target dictionary.</param>
    /// <param name="valueFactory">A value factory delegate that will be called if a new value is needed to be added to the dictionary.</param>
    /// <returns>An existing value in the dictionary if the key is found and its associated value is of type <typeparamref name="TValue"/>; othewise a new instance of type <typeparamref name="TValue"/> returned by the value factory.</returns>
    [DebuggerStepThrough]
    public static TValue EnsureKeyValue<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> dictionary, TKey key, Func<TValue> valueFactory) {
      CommonHelper.ConfirmNotNull(dictionary, "dictionary");
      CommonHelper.ConfirmNotNull(valueFactory, "valueFactory");
      return dictionary.GetOrAdd(key, v => valueFactory());
    }

    /// <summary>
    /// Checks if a thread-safe dictionary contains the specified key, otherwise an object of type <typeparamref name="TValue"/> returned by 
    /// a value factory delegate is added to the dictionary with the specified key.
    /// </summary>
    /// <typeparam name="TKey">Type of keys accepted by the dictionary.</typeparam>
    /// <typeparam name="TValue">Type of the objects to be added.</typeparam>
    /// <param name="dictionary">An dictionary.</param>
    /// <param name="key">A key that is accepted by the target dictionary.</param>
    /// <param name="valueFactory">A value factory delegate that will be called if a new value is needed to be added to the dictionary.</param>
    /// <returns>An existing value in the dictionary if the key is found and its associated value is of type <typeparamref name="TValue"/>; othewise a new instance of type <typeparamref name="TValue"/> returned by the value factory.</returns>
    [DebuggerStepThrough]
    public static TValue EnsureKeyValue<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> dictionary, TKey key, Func<TKey, TValue> valueFactory) {
      CommonHelper.ConfirmNotNull(dictionary, "dictionary");
      CommonHelper.ConfirmNotNull(key, "key");
      CommonHelper.ConfirmNotNull(valueFactory, "valueFactory");
      return dictionary.GetOrAdd(key, valueFactory);
    }

    /// <summary>
    /// Creates a read-only dictionary from the supplied dictionary.
    /// </summary>
    /// <typeparam name="TKey">Type of keys of the source dictionary.</typeparam>
    /// <typeparam name="TValue">Type of values of the source dictionary.</typeparam>
    /// <param name="dictionary">Source dictionary.</param>
    /// <returns></returns>
    [DebuggerStepThrough]
    public static ReadOnlyDictionary<TKey, TValue> AsReadOnly<TKey, TValue>(this IDictionary<TKey, TValue> dictionary) {
      CommonHelper.ConfirmNotNull(dictionary, "dictionary");
      return new ReadOnlyDictionary<TKey, TValue>(dictionary);
    }
  }
}
