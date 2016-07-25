using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;

namespace Codeless.DynamicType {
  [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
  public class DynamicMemberAttribute : Attribute {
    public DynamicMemberAttribute(string name) {
      this.Name = name;
    }

    public string Name { get; private set; }
  }

  public class DynamicObject : ICustomDynamicObject {
    private static readonly ConcurrentDictionary<Type, ReadOnlyDictionary<DynamicKey, MemberInfo>> memberDictionary = new ConcurrentDictionary<Type, ReadOnlyDictionary<DynamicKey, MemberInfo>>();
    private readonly ReadOnlyDictionary<DynamicKey, MemberInfo> memberDictionaryMyType;
    private readonly Hashtable hashtable = new Hashtable();
    private readonly HashSet<DynamicKey> keys = new HashSet<DynamicKey>();

    public DynamicObject() {
      if (!memberDictionary.TryGetValue(this.GetType(), out memberDictionaryMyType)) {
        memberDictionary.TryAdd(this.GetType(), GetDynamicMembers(this.GetType()));
        memberDictionary.TryGetValue(this.GetType(), out memberDictionaryMyType);
      }
      foreach (DynamicKey key in memberDictionaryMyType.Keys) {
        keys.Add(key);
      }
    }

    public virtual string TypeName {
      get { return "Object"; }
    }

    public virtual IEnumerable<DynamicKey> GetKeys() {
      return keys.AsEnumerable();
    }

    public virtual bool GetValue(string key, out object value) {
      if (hashtable.ContainsKey(key)) {
        value = hashtable[key];
        return true;
      }
      MemberInfo member;
      if (memberDictionaryMyType.TryGetValue(new DynamicKey(key), out member)) {
        if (member.MemberType == MemberTypes.Method) {
          value = new MethodInfo[] { (MethodInfo)member };
          return true;
        } else if (member.MemberType == MemberTypes.Property) {
          value = ((PropertyInfo)member).GetValue(this);
          return true;
        }
      }
      value = null;
      return false;
    }

    public virtual bool SetValue(string key, object value) {
      keys.Add(new DynamicKey(key));
      hashtable[key] = value;
      return true;
    }

    public virtual void DeleteKey(string key) {
      keys.RemoveWhere(v => v.Name == key);
      hashtable.Remove(key);
    }

    [DynamicMember("toString")]
    public override string ToString() {
      return new DynamicValue(this).AsString();
    }

    [DynamicMember("valueOf")]
    public virtual object ValueOf() {
      return this;
    }

    private static ReadOnlyDictionary<DynamicKey, MemberInfo> GetDynamicMembers(Type t) {
      Dictionary<DynamicKey, MemberInfo> dictionary = new Dictionary<DynamicKey, MemberInfo>();
      foreach (MemberInfo member in t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) {
        DynamicMemberAttribute attribute = member.GetCustomAttribute<DynamicMemberAttribute>();
        if (attribute != null) {
          dictionary.Add(new DynamicKey(attribute.Name), member);
        }
      }
      return new ReadOnlyDictionary<DynamicKey, MemberInfo>(dictionary);
    }

    void ICustomDynamicObject.SetValue(string key, object value) {
      if (!SetValue(key, value)) {
        throw new DynamicValueIndexingException("Unable to set value");
      }
    }
  }
}
