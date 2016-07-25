using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Codeless.SharePoint.ObjectModel.Linq {
  internal class SPModelFieldAssociationCollection : ICollection<SPModelFieldAssociation>, IEnumerable<SPModelFieldAssociation>, IEnumerable {
    private static readonly object syncLock = new object();
    private static readonly ConcurrentDictionary<MemberInfo, SPModelFieldAssociationCollection> QueryableFields = new ConcurrentDictionary<MemberInfo, SPModelFieldAssociationCollection>();

    private readonly HashSet<SPModelFieldAssociation> dictionary = new HashSet<SPModelFieldAssociation>();
    private readonly HashSet<SPModelDescriptor> typeDictionary = new HashSet<SPModelDescriptor>();
    private readonly HashSet<SPFieldAttribute> fieldDictionary = new HashSet<SPFieldAttribute>();
    private bool queryable;

    public int Count {
      get { return dictionary.Count; }
    }

    public bool Queryable {
      get { return queryable; }
    }

    public ICollection<SPFieldAttribute> Fields {
      get { return fieldDictionary; }
    }

    public void Add(SPModelFieldAssociation entry) {
      if (dictionary.Count == 0) {
        queryable = true;
      }
      if (dictionary.Add(entry)) {
        if (entry.Attribute.IncludeInQuery) {
          fieldDictionary.Add(entry.Attribute);
        }
        if (queryable) {
          if (typeDictionary.Add(entry.Descriptor)) {
            queryable &= entry.Attribute.IncludeInQuery;
          } else if (entry.Attribute.IncludeInQuery) {
            queryable &= !dictionary.Any(v => v.Descriptor == entry.Descriptor && v.Attribute.IncludeInQuery);
          }
        }
      }
    }

    #region ICollection<SPModelFieldAssociation>
    public IEnumerator<SPModelFieldAssociation> GetEnumerator() {
      return dictionary.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() {
      return dictionary.GetEnumerator();
    }

    void ICollection<SPModelFieldAssociation>.Clear() {
      throw new InvalidOperationException();
    }

    bool ICollection<SPModelFieldAssociation>.Contains(SPModelFieldAssociation item) {
      return dictionary.Contains(item);
    }

    void ICollection<SPModelFieldAssociation>.CopyTo(SPModelFieldAssociation[] array, int arrayIndex) {
      throw new NotImplementedException();
    }

    bool ICollection<SPModelFieldAssociation>.IsReadOnly {
      get { return false; }
    }

    bool ICollection<SPModelFieldAssociation>.Remove(SPModelFieldAssociation item) {
      throw new InvalidOperationException();
    }
    #endregion

    public static SPModelFieldAssociationCollection GetByMember(MemberInfo member) {
      return QueryableFields.EnsureKeyValue(member);
    }

    public static IEnumerable<SPFieldAttribute> EnumerateFieldAttributes(SPModelDescriptor descriptor, Type sourceType) {
      CommonHelper.ConfirmNotNull(descriptor, "descriptor");
      CommonHelper.ConfirmNotNull(sourceType, "sourceType");
      lock (syncLock) {
        foreach (SPFieldAttribute v in EnumerateFieldAttributes(descriptor, sourceType, sourceType)) {
          yield return v;
        }
        foreach (Type interfaceType in sourceType.GetInterfaces()) {
          foreach (SPFieldAttribute v in EnumerateFieldAttributes(descriptor, interfaceType, sourceType)) {
            yield return v;
          }
        }
      }
    }

    private static IEnumerable<SPFieldAttribute> EnumerateFieldAttributes(SPModelDescriptor descriptor, Type sourceType, Type implementedType) {
      InterfaceMapping mapping = default(InterfaceMapping);
      if (sourceType.IsInterface) {
        mapping = implementedType.GetInterfaceMap(sourceType);
      }
      foreach (MemberInfo member in sourceType.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) {
        SPModelFieldAssociationCollection collection = QueryableFields.EnsureKeyValue(member);
        SPModelFieldAssociationCollection otherCollection = null;
        if (sourceType.IsInterface) {
          MethodInfo lookupMethod = null;
          if (member.MemberType == MemberTypes.Property) {
            lookupMethod = ((PropertyInfo)member).GetGetMethod() ?? ((PropertyInfo)member).GetSetMethod();
          } else if (member.MemberType == MemberTypes.Method) {
            lookupMethod = (MethodInfo)member;
          }
          if (lookupMethod != null) {
            int pos = Array.IndexOf(mapping.InterfaceMethods, lookupMethod);
            if (pos >= 0) {
              MemberInfo mappedMember = mapping.TargetMethods[pos];
              if (member.MemberType == MemberTypes.Property) {
                mappedMember = implementedType.GetProperty(mappedMember.Name.Substring(4));
              }
              if (mappedMember != null) {
                otherCollection = QueryableFields.EnsureKeyValue(mappedMember);
                if (otherCollection.Count > 0) {
                  foreach (SPModelFieldAssociation value in otherCollection) {
                    collection.Add(value);
                  }
                }
              }
            }
          }
        }

        SPFieldProvisionMode provisionMode = SPFieldProvisionMode.Default;
        SPModelFieldAssociationCollection basePropertyCollection = null;
        IEnumerable<SPFieldAttribute> attributes = member.GetCustomAttributes<SPFieldAttribute>(true);

        if (member.MemberType == MemberTypes.Property) {
          PropertyInfo baseProperty = ((PropertyInfo)member).GetBaseDefinition();
          if (baseProperty != member) {
            if (!attributes.Any()) {
              attributes = baseProperty.GetCustomAttributes<SPFieldAttribute>(false);
              provisionMode = SPFieldProvisionMode.None;
            } else {
              basePropertyCollection = QueryableFields.EnsureKeyValue(baseProperty);
              provisionMode = SPFieldProvisionMode.FieldLink;
            }
          }
        }
        if (member.DeclaringType != sourceType) {
          provisionMode = SPFieldProvisionMode.FieldLink;
        }
        foreach (SPFieldAttribute attribute in attributes) {
          if (attribute.IncludeInQuery) {
            PropertyInfo property = null;
            SPModelQueryPropertyAttribute queryPropertyAttribute = member.GetCustomAttribute<SPModelQueryPropertyAttribute>(false);
            if (queryPropertyAttribute != null) {
              property = queryPropertyAttribute.QueryProperty;
            }
            SPModelFieldAssociation value = new SPModelFieldAssociation(descriptor, attribute, property);
            collection.Add(value);
            if (otherCollection != null) {
              otherCollection.Add(value);
            }
            if (basePropertyCollection != null) {
              basePropertyCollection.Add(value);
            }
            if (property != null) {
              SPModelFieldAssociationCollection foreignPropertyCollection = QueryableFields.EnsureKeyValue(property);
              foreignPropertyCollection.Add(new SPModelFieldAssociation(descriptor, attribute, null));
            }
          }
          yield return attribute.Clone(provisionMode);
        }
      }
    }
  }
}
