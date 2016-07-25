using Codeless;
using Codeless;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Codeless.DynamicType {
  public class DynamicNativeObject : DynamicObject {
    private static readonly ConcurrentDictionary<Type, IList<DynamicKey>> memberDictionary = new ConcurrentDictionary<Type, IList<DynamicKey>>();
    private readonly ICollection keysFromEnumerable;
    private readonly IList<DynamicKey> nativeMembers;
    internal readonly object obj;

    #region Helper Classes
    private class CollectionKeyCollection : ICollection {
      private readonly ICollection collection;

      public CollectionKeyCollection(ICollection collection) {
        this.collection = collection;
      }

      public void CopyTo(Array array, int index) {
        throw new NotImplementedException();
      }

      public int Count {
        get { return collection.Count; }
      }

      public bool IsSynchronized {
        get { return false; }
      }

      public object SyncRoot {
        get { return new object(); }
      }

      public IEnumerator GetEnumerator() {
        return Enumerable.Range(0, collection.Count).GetEnumerator();
      }
    }

    private class EnumerableKeyCollection : ICollection {
      private readonly IEnumerable collection;

      public EnumerableKeyCollection(IEnumerable collection) {
        this.collection = collection;
      }

      public void CopyTo(Array array, int index) {
        throw new NotImplementedException();
      }

      public int Count {
        get { return collection.OfType<object>().Count(); }
      }

      public bool IsSynchronized {
        get { return false; }
      }

      public object SyncRoot {
        get { return new object(); }
      }

      public IEnumerator GetEnumerator() {
        return Enumerable.Range(0, collection.OfType<object>().Count()).GetEnumerator();
      }
    }
    #endregion

    public DynamicNativeObject(object obj) {
      if (obj == null) {
        throw new ArgumentNullException("obj");
      }
      this.obj = obj;
      if (obj is IDictionary) {
        this.keysFromEnumerable = ((IDictionary)obj).Keys;
      } else if (obj is ICollection) {
        this.keysFromEnumerable = new CollectionKeyCollection((ICollection)obj);
      } else if (obj is IEnumerable) {
        this.keysFromEnumerable = new EnumerableKeyCollection((IEnumerable)obj);
      }
      if (!memberDictionary.TryGetValue(obj.GetType(), out nativeMembers)) {
        memberDictionary.TryAdd(obj.GetType(), GetNativeMembers(obj.GetType()));
        memberDictionary.TryGetValue(obj.GetType(), out nativeMembers);
      }
    }

    public override string TypeName {
      get { return obj.GetType().Name; }
    }

    public override IEnumerable<DynamicKey> GetKeys() {
      if (keysFromEnumerable != null) {
        foreach (object key in keysFromEnumerable) {
          yield return new DynamicKey(key.ToString());
        }
      }
      foreach (DynamicKey key in nativeMembers) {
        yield return key;
      }
      foreach (DynamicKey key in base.GetKeys()) {
        yield return key;
      }
    }

    public override bool GetValue(string key, out object value) {
      if (base.GetValue(key, out value)) {
        return true;
      }
      Type keyType;
      Type objType = obj.GetType();

      if (objType.IsOf<IDictionary>()) {
        if (objType.IsOf(typeof(IDictionary<,>), out keyType) && Type.GetTypeCode(keyType) != TypeCode.Object) {
          try {
            value = ((IDictionary)obj)[Convert.ChangeType(key, keyType)];
            return true;
          } catch { }
        }
        try {
          value = ((IDictionary)obj)[key];
          return true;
        } catch { }
      }
      if (Type.GetTypeCode(objType) != TypeCode.String && objType.IsOf<IEnumerable>()) {
        int index;
        if (Int32.TryParse(key, out index)) {
          try {
            value = ((IEnumerable)obj).OfType<object>().ElementAt(index);
            return true;
          } catch { }
        }
      }
      PropertyInfo property = objType.GetProperty(key);
      if (property != null) {
        value = new DynamicValue(property.GetValue(obj));
        return true;
      }
      foreach (PropertyInfo indexer in objType.GetProperties().Where(v => v.Name == "Item" && v.GetIndexParameters().Length == 1)) {
        Type indexerType = indexer.GetIndexParameters()[0].ParameterType;
        try {
          value = indexer.GetValue(obj, new[] { Convert.ChangeType(key, indexerType) });
          return true;
        } catch { }
      }
      FieldInfo field = objType.GetField(key);
      if (field != null) {
        value = field.GetValue(obj);
        return true;
      }
      MethodInfo[] methods = objType.GetMethods().Where(v => v.Name == key && DynamicValue.IsMethodCallable(v)).ToArray();
      if (methods.Length > 0) {
        value = methods;
        return true;
      }
      return DynamicValue.Undefined;
    }

    private static IList<DynamicKey> GetNativeMembers(Type t) {
      HashSet<DynamicKey> members = new HashSet<DynamicKey>();
      foreach (MemberInfo member in t.GetMembers(BindingFlags.Public | BindingFlags.Instance)) {
        if (member.MemberType == MemberTypes.Property || (member.MemberType == MemberTypes.Method && !((MethodInfo)member).Attributes.HasFlag(MethodAttributes.SpecialName))) {
          members.Add(new DynamicKey(member.Name));
        }
      }
      return new List<DynamicKey>(members);
    }
  }
}
