﻿using Codeless.SharePoint.Internal;
using Codeless.SharePoint.ObjectModel.Linq;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Administration;
using Microsoft.SharePoint.Publishing;
using Microsoft.SharePoint.Utilities;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Codeless.SharePoint.ObjectModel {
  internal enum SPModelItemType {
    GenericItem,
    Folder,
    File,
    PublishingPage,
    DocumentSet
  }

  /// <summary>
  /// The exception that is thrown when error has occured when provisioning fields, content types, lists or views in a site collection.
  /// </summary>
  public class SPModelProvisionException : Exception {
    internal SPModelProvisionException(string message)
      : base(message) { }

    internal SPModelProvisionException(string message, Exception ex)
      : base(message, ex) { }
  }

  [Flags]
  internal enum SPModelProvisionOptions {
    None = 0,
    ForceProvisionContentType = 1,
    SuppressListCreation = 2,
    Asynchronous = 4,
    MismatchChecksumCTOnly = 8
  }

  internal sealed class SPModelListProvisionOptions {
    public static readonly SPModelListProvisionOptions Default = new SPModelListProvisionOptions();

    private SPModelListProvisionOptions() { }

    public SPModelListProvisionOptions(string targetListUrl) {
      CommonHelper.ConfirmNotNull(targetListUrl, "targetListUrl");
      this.TargetListUrl = targetListUrl;
    }

    public SPModelListProvisionOptions(string targetListUrl, string title) {
      CommonHelper.ConfirmNotNull(targetListUrl, "targetListUrl");
      this.TargetListUrl = targetListUrl;
      this.TargetListTitle = title;
    }

    public SPModelListProvisionOptions(SPList targetList) {
      CommonHelper.ConfirmNotNull(targetList, "targetList");
      this.TargetWebId = targetList.ParentWeb.ID;
      this.TargetListId = targetList.ID;
    }

    public SPModelListProvisionOptions(SPListAttribute attribute) {
      CommonHelper.ConfirmNotNull(attribute, "attribute");
      this.ListAttributeOverrides = attribute.Clone();
    }

    public SPListAttribute ListAttributeOverrides { get; private set; }
    public string TargetListUrl { get; private set; }
    public string TargetListTitle { get; private set; }
    public Guid TargetWebId { get; private set; }
    public Guid TargetListId { get; private set; }
  }

  [DebuggerDisplay("ModelType = {ModelType.FullName,nq}")]
  internal class SPModelDescriptor {
    private class TypeInheritanceComparer : Comparer<Type> {
      public override int Compare(Type x, Type y) {
        return GetDepth(x) - GetDepth(y);
      }

      private static int GetDepth(Type x) {
        int depth = 0;
        for (; x != typeof(SPModel); x = x.BaseType, depth++) ;
        return depth;
      }
    }

    private class ReverseComparer<T> : IComparer<T> {
      private static ReverseComparer<T> defaultInstance;
      private readonly Comparison<T> comparer;

      public ReverseComparer()
        : this(Comparer<T>.Default) { }

      public ReverseComparer(IComparer<T> comparer) {
        CommonHelper.ConfirmNotNull(comparer, "comparer");
        this.comparer = comparer.Compare;
      }

      public ReverseComparer(Comparison<T> comparer) {
        CommonHelper.ConfirmNotNull(comparer, "comparer");
        this.comparer = comparer;
      }

      public static ReverseComparer<T> Default {
        get { return LazyInitializer.EnsureInitialized(ref defaultInstance); }
      }

      public int Compare(T x, T y) {
        return comparer(y, x);
      }
    }

    private class ProvisionResult {
      public ProvisionResult() {
        this.StackTrace = new StackTrace(1);
        this.ProvisionedLists = new HashSet<SPModelUsage>();
      }

      public HashSet<SPModelUsage> ProvisionedLists { get; private set; }
      public Exception Exception { get; set; }
      public StackTrace StackTrace { get; private set; }
    }

    private static readonly object syncLock = new object();
    private static readonly ConstructorInfo SPContentTypeCtor = typeof(SPContentType).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(SPContentTypeId) }, null);
    private static readonly PropertyInfo SPContentTypePropertyWeb = typeof(SPContentType).GetProperty("Web", true);
    private static readonly AssemblyName SelfAssemblyName = new AssemblyName(typeof(SPModel).Assembly.FullName);
    private static readonly ConcurrentDictionary<Assembly, object> RegisteredAssembly = new ConcurrentDictionary<Assembly, object>();
    private static readonly ConcurrentDictionary<Type, SPModelDescriptor> TargetTypeDictionary = new ConcurrentDictionary<Type, SPModelDescriptor>();
    private static readonly SortedDictionary<SPContentTypeId, SPModelDescriptor> ContentTypeDictionary = new SortedDictionary<SPContentTypeId, SPModelDescriptor>(ReverseComparer<SPContentTypeId>.Default);
    [ThreadStatic]
    private static bool enteredLock;

    private readonly SPContentTypeAttribute contentTypeAttribute;
    private readonly SPFieldAttribute[] fieldAttributes;
    private readonly SPListAttribute listAttribute;
    private readonly Type defaultManagerType;
    private readonly Type provisionEventReceiverType;
    private readonly HashSet<SPFieldAttribute> hiddenFields = new HashSet<SPFieldAttribute>();
    private readonly HashSet<string> requiredViewFields = new HashSet<string>();
    private readonly ConcurrentDictionary<Guid, bool> provisionedSites = new ConcurrentDictionary<Guid, bool>();
    private readonly ConcurrentDictionary<Guid, EventWaitHandle> provisionedSitesLocks = new ConcurrentDictionary<Guid, EventWaitHandle>();
    private readonly bool hasExplicitListAttribute;

    public readonly SPModelDescriptor Parent;
    public readonly List<SPModelDescriptor> Children = new List<SPModelDescriptor>();
    public readonly List<SPModelDescriptor> Interfaces = new List<SPModelDescriptor>();

    private string checksum;
    protected SPBaseType? baseType;
    protected Lazy<Type> instanceType;

    static SPModelDescriptor() {
      // trigger type load of SPModel class before attaching AssemblyLoad event
      // because the static constructor will fire an AssemblyLoad event which may cause deadlock
      SPModel.RequiredViewFields.GetType();
      AppDomain.CurrentDomain.AssemblyLoad += (sender, e) => {
        RegisterAssembly(e.LoadedAssembly);
      };
      foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
        RegisterAssembly(assembly);
      }
    }

    protected SPModelDescriptor(Type targetType) {
      this.ModelType = targetType;
      this.defaultManagerType = GetDefaultManagerType(targetType);
      this.fieldAttributes = new SPFieldAttribute[0];
    }

    private SPModelDescriptor(Type targetType, SPModelDefaultsAttribute defaultsAttribute) {
      this.ModelType = targetType;
      TargetTypeDictionary.TryAdd(targetType, this);
      TargetTypeDictionary.TryGetValue(targetType.BaseType, out this.Parent);
      if (this.Parent is SPModelInterfaceTypeDescriptor) {
        this.Parent = null;
      }

      this.contentTypeAttribute = targetType.GetCustomAttribute<SPContentTypeAttribute>(false);
      ResolveContentTypeId(contentTypeAttribute, targetType);
      ContentTypeDictionary.Add(contentTypeAttribute.ContentTypeId, this);

      this.defaultManagerType = GetDefaultManagerType(targetType);
      this.provisionEventReceiverType = contentTypeAttribute.ProvisionEventReceiverType;
      this.hasExplicitListAttribute = targetType.GetCustomAttribute<SPListAttribute>(false) != null;
      this.listAttribute = targetType.GetCustomAttribute<SPListAttribute>(true) ?? new SPListAttribute();
      this.fieldAttributes = SPModelFieldAssociationCollection.EnumerateFieldAttributes(this, targetType).ToArray();

      if (contentTypeAttribute.Group == null && defaultsAttribute != null) {
        contentTypeAttribute.Group = defaultsAttribute.DefaultContentTypeGroup;
      }
      foreach (SPFieldAttribute attribute in fieldAttributes) {
        if (attribute.Group == null) {
          if (this.Parent != null) {
            SPFieldAttribute baseAttribute = this.Parent.fieldAttributes.FirstOrDefault(v => v.InternalName == attribute.InternalName);
            if (baseAttribute != null) {
              attribute.Group = baseAttribute.Group;
              continue;
            }
          }
          if (defaultsAttribute != null) {
            attribute.Group = defaultsAttribute.DefaultFieldGroup;
          }
        }
      }

      if (contentTypeAttribute.ContentTypeId.IsChildOf(ContentTypeId.Page)) {
        this.ItemType = SPModelItemType.PublishingPage;
      } else if (contentTypeAttribute.ContentTypeId.IsChildOf(SPBuiltInContentTypeId.DocumentSet)) {
        this.ItemType = SPModelItemType.DocumentSet;
      } else if (contentTypeAttribute.ContentTypeId.IsChildOf(SPBuiltInContentTypeId.Folder)) {
        this.ItemType = SPModelItemType.Folder;
      } else if (contentTypeAttribute.ContentTypeId.IsChildOf(SPBuiltInContentTypeId.Document)) {
        this.ItemType = SPModelItemType.File;
      }

      if (this.ItemType == SPModelItemType.GenericItem) {
        this.baseType = SPBaseType.GenericList;
      } else if (contentTypeAttribute.ContentTypeId.IsChildOf(SPBuiltInContentTypeId.Issue)) {
        this.baseType = SPBaseType.Issue;
      } else {
        this.baseType = SPBaseType.DocumentLibrary;
      }

      if (this.Parent != null) {
        this.Parent.Children.Add(this);
        this.fieldAttributes = fieldAttributes.Concat(this.Parent.fieldAttributes).Distinct().ToArray();
        if (provisionEventReceiverType == null) {
          this.provisionEventReceiverType = this.Parent.provisionEventReceiverType;
        }
      }

      foreach (SPFieldAttribute v in fieldAttributes) {
        AddRequiredViewField(v);
      }
      foreach (Type interfaceType in targetType.GetInterfaces()) {
        if (!interfaceType.IsDefined(typeof(SPModelIgnoreAttribute), true)) {
          SPModelInterfaceTypeDescriptor interfaceDescriptor = (SPModelInterfaceTypeDescriptor)TargetTypeDictionary.EnsureKeyValue(interfaceType, SPModelInterfaceTypeDescriptor.Create);
          interfaceDescriptor.AddImplementedType(this);
          this.Interfaces.Add(interfaceDescriptor);
        }
      }
      if (targetType.BaseType != typeof(SPModel) && targetType.BaseType.GetCustomAttribute<SPContentTypeAttribute>(false) == null) {
        SPModelInterfaceTypeDescriptor interfaceDescriptor = (SPModelInterfaceTypeDescriptor)TargetTypeDictionary.EnsureKeyValue(targetType.BaseType, SPModelInterfaceTypeDescriptor.Create);
        interfaceDescriptor.AddImplementedType(this);
        this.Interfaces.Add(interfaceDescriptor);
      }
      if (!targetType.IsAbstract) {
        instanceType = new Lazy<Type>(() => targetType);
      } else {
        instanceType = new Lazy<Type>(() => SPModel.BuildTypeFromAbstractBaseType(targetType), LazyThreadSafetyMode.ExecutionAndPublication);
      }
    }

    public Type ModelType { get; private set; }

    public Type ModelInstanceType { get { return instanceType.Value; } }

    public SPModelItemType ItemType { get; private set; }

    public SPBaseType BaseType {
      get { return baseType.GetValueOrDefault(SPBaseType.UnspecifiedBaseType); }
    }

    public string[] RequiredViewFields {
      get { return requiredViewFields.ToArray(); }
    }

    public IEnumerable<SPFieldAttribute> Fields {
      get { return Enumerable.AsEnumerable(fieldAttributes); }
    }

    public virtual IEnumerable<SPContentTypeId> ContentTypeIds {
      get { yield return contentTypeAttribute.ContentTypeId; }
    }

    public virtual SPModelUsageCollection GetUsages(SPWeb web) {
      CommonHelper.ConfirmNotNull(web, "web");
      List<SPModelUsage> collection = new List<SPModelUsage>();
      string startUrl = web.ServerRelativeUrl;
      if (listAttribute.RootWebOnly) {
        startUrl = web.Site.ServerRelativeUrl;
      }
      startUrl = startUrl.TrimEnd('/');
      foreach (SPContentTypeUsage usage in GetContentTypeUsages(web, contentTypeAttribute.ContentTypeId)) {
        if (usage.IsUrlToList && IsUrlInScope(startUrl, usage.Url)) {
          collection.Add(SPModelUsage.Create(web.Site, usage));
        }
      }
      return new SPModelUsageCollection(web.Site, collection);
    }

    public SPModelUsageCollection GetUsages(SPWeb web, bool currentWebOnly) {
      SPModelUsageCollection collection = GetUsages(web);
      if (currentWebOnly) {
        return new SPModelUsageCollection(web.Site, collection.Where(v => v.WebId == web.ID));
      }
      return collection;
    }

    public virtual SPModelUsageCollection GetUsages(SPList list) {
      CommonHelper.ConfirmNotNull(list, "list");
      List<SPModelUsage> collection = new List<SPModelUsage>();
      foreach (SPContentType ct in list.ContentTypes) {
        if (this.ContentTypeIds.Any(v => v.IsParentOf(ct.Id))) {
          collection.Add(SPModelUsage.Create(list, ct.Id));
        }
      }
      return new SPModelUsageCollection(list.ParentWeb.Site, collection);
    }

    public SPModelUsageCollection Provision(SPWeb targetWeb) {
      return Provision(targetWeb, SPModelProvisionOptions.None, SPModelListProvisionOptions.Default);
    }

    public SPModelUsageCollection Provision(SPWeb targetWeb, SPModelListProvisionOptions listOptions) {
      return Provision(targetWeb, SPModelProvisionOptions.None, listOptions);
    }

    public SPModelUsageCollection Provision(SPWeb targetWeb, SPModelProvisionOptions options, SPModelListProvisionOptions listOptions) {
      CommonHelper.ConfirmNotNull(targetWeb, "targetWeb");
      CommonHelper.ConfirmNotNull(listOptions, "listOptions");
      if (contentTypeAttribute == null || !contentTypeAttribute.ExternalContentType) {
        bool provisionContentType = options.HasFlag(SPModelProvisionOptions.ForceProvisionContentType) || provisionedSites.TryAdd(targetWeb.Site.ID, true);
        bool provisionList = !options.HasFlag(SPModelProvisionOptions.SuppressListCreation);
        if (provisionContentType || provisionList) {
          string siteUrl = targetWeb.Site.Url;
          Guid siteId = targetWeb.Site.ID;
          Guid webId = targetWeb.ID;
          bool matchChecksum = options.HasFlag(SPModelProvisionOptions.MismatchChecksumCTOnly);
          ProvisionResult result = new ProvisionResult();

          Thread thread = new Thread(() => Provision(siteUrl, siteId, webId, provisionContentType, provisionList, matchChecksum, listOptions, result));
          thread.Start();
          if (!options.HasFlag(SPModelProvisionOptions.Asynchronous)) {
            thread.Join();
            if (result.Exception != null) {
              throw result.Exception.Rethrow();
            }
            return new SPModelUsageCollection(targetWeb.Site, result.ProvisionedLists.ToArray());
          }
        }
      }
      return new SPModelUsageCollection(targetWeb.Site, new SPModelUsage[0]);
    }

    public ISPModelManagerInternal CreateManager(SPWeb context) {
      return (ISPModelManagerInternal)defaultManagerType.CreateInstance(context);
    }

    public CamlExpression GetContentTypeExpression(SPModelDescriptor other) {
      CommonHelper.ConfirmNotNull(other, "other");
      CamlExpression expression = Caml.False;
      foreach (SPContentTypeId contentTypeId in this.ContentTypeIds) {
        if (other == this || other.ContentTypeIds.Any(v => v.IsParentOf(contentTypeId))) {
          expression |= Caml.OfContentType(contentTypeId);
        }
      }
      return expression;
    }

    public bool Contains(SPContentTypeId contentTypeId) {
      return this.ContentTypeIds.Any(v => v.IsParentOf(contentTypeId));
    }

    public bool Contains(SPModelDescriptor other) {
      return (this == other || this.Interfaces.Contains(other) || this.Children.Any(v => v.Contains(other)));
    }

    public bool UsedInList(SPList list) {
      CommonHelper.ConfirmNotNull(list, "list");
      return this.ContentTypeIds.Any(v => list.ContainsContentType(v));
    }

    public void AddRequiredViewField(SPFieldAttribute field) {
      CommonHelper.ConfirmNotNull(field, "field");
      if (field.IncludeInQuery || field.IncludeInViewFields) {
        requiredViewFields.Add(field.ListFieldInternalName);
        AddInterfaceDepenedentField(field);
        if (this.Parent != null) {
          this.Parent.AddRequiredViewField(field);
        }
      }
    }

    public void AddInterfaceDepenedentField(SPFieldAttribute field) {
      CommonHelper.ConfirmNotNull(field, "field");
      if (IsTwoColumnField(field)) {
        foreach (SPModelDescriptor d in EnumerableHelper.AncestorsAndSelf(this, v => v.Parent)) {
          if (!d.fieldAttributes.Contains(field)) {
            d.hiddenFields.Add(field);
          }
        }
        foreach (SPModelDescriptor d in EnumerableHelper.Descendants(this, v => v.Children)) {
          if (!d.fieldAttributes.Contains(field)) {
            d.hiddenFields.Add(field);
          }
        }
      }
    }

    public void CheckMissingFields(SPList list) {
      CommonHelper.ConfirmNotNull(list, "list");
      foreach (SPFieldAttribute attribute in fieldAttributes.Concat(hiddenFields)) {
        if (IsTwoColumnField(attribute)) {
          try {
            SPField field = list.Fields.GetFieldByInternalName(attribute.ListFieldInternalName);
            if (!IsTwoColumnField(field)) {
              throw new Exception(String.Format("Field '{0}' has incorrect type in list {1}", attribute.InternalName, SPUrlUtility.CombineUrl(list.ParentWebUrl, list.RootFolder.Url)));
            }
          } catch (ArgumentException) {
            throw new Exception(String.Format("Missing field '{0}' in list {1}", attribute.InternalName, SPUrlUtility.CombineUrl(list.ParentWebUrl, list.RootFolder.Url)));
          }
        }
      }
    }

    public static SPModelDescriptor Resolve(string typeName) {
      CommonHelper.ConfirmNotNull(typeName, "typeName");
      foreach (Type type in TargetTypeDictionary.Keys) {
        if (type.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase) || type.FullName.Equals(typeName, StringComparison.OrdinalIgnoreCase)) {
          return TargetTypeDictionary[type];
        }
      }
      throw new ArgumentException("typeName");
    }

    public static SPModelDescriptor Resolve(Type type) {
      CommonHelper.ConfirmNotNull(type, "type");
      if (SPModel.IsDynamicConstructedType(type)) {
        type = type.BaseType;
      } else {
        RegisterAssembly(type.Assembly);
        if (type.IsGenericType) {
          type = type.GetGenericTypeDefinition();
        }
      }
      SPModelDescriptor result;
      if (TargetTypeDictionary.TryGetValue(type, out result)) {
        return result;
      }
      throw new ArgumentException("type", String.Format("Type '{0}' does not attributed with SPContentTypeAttribute", type.FullName));
    }

    public static SPModelDescriptor Resolve(SPContentTypeId contentTypeId) {
      lock (syncLock) {
        foreach (KeyValuePair<SPContentTypeId, SPModelDescriptor> entry in ContentTypeDictionary) {
          if (contentTypeId.IsChildOf(entry.Key)) {
            return entry.Value;
          }
        }
      }
      throw new ArgumentException("contentTypeId", String.Format("There is no type associated with content type ID '{0}'", contentTypeId));
    }

    public static SPModelDescriptor Resolve(SPContentTypeId contentTypeId, SPSite lookupSite) {
      CommonHelper.ConfirmNotNull(lookupSite, "lookupSite");
      try {
        SPModelDescriptor descriptor = Resolve(contentTypeId);
        if (descriptor.ContentTypeIds.Contains(contentTypeId)) {
          return descriptor;
        }
      } catch (ArgumentException) { }

      RegisterReferencedAssemblies(lookupSite);
      return Resolve(contentTypeId);
    }

    public static bool RegisterReferencedAssemblies(SPSite site) {
      CommonHelper.ConfirmNotNull(site, "site");
      HashSet<string> assemblyNames = new HashSet<string>();
      foreach (string key in site.RootWeb.AllProperties.Keys) {
        if (key.StartsWith("SPModel.") && key.EndsWith(".Assembly")) {
          assemblyNames.Add((string)site.RootWeb.AllProperties[key]);
        }
      }
      int beforeCount = TargetTypeDictionary.Count;
      foreach (string assemblyName in assemblyNames) {
        Assembly.Load(assemblyName);
      }
      return TargetTypeDictionary.Count != beforeCount;
    }

    protected virtual void CheckFieldConsistency() {
      if (this.Parent != null) {
        CheckFieldConsistency(this.Parent);
      }
    }

    protected void CheckFieldConsistency(SPModelDescriptor other) {
      foreach (SPFieldAttribute definition in fieldAttributes) {
        SPFieldAttribute parentDefinition = other.fieldAttributes.FirstOrDefault(v => v.InternalName == definition.InternalName);
        if (parentDefinition != null) {
          if (definition.GetType() != parentDefinition.GetType()) {
            throw new SPModelProvisionException(String.Format("Definition for field '{0}' in content type '{1}' conflicts with parent content type.", definition.InternalName, contentTypeAttribute.Name));
          }
          foreach (PropertyInfo property in definition.GetType().GetProperties()) {
            object myValue = property.GetValue(definition, null);
            object paValue = property.GetValue(parentDefinition, null);
            if (!Object.Equals(myValue, paValue)) {
              if (property.PropertyType == typeof(SPOption) && (SPOption)myValue == SPOption.Unspecified) {
                continue;
              }
              if (property.PropertyType == typeof(StringCollection)) {
                StringCollection sourceCollection = (StringCollection)myValue;
                StringCollection targetCollection = (StringCollection)paValue;
                if (sourceCollection.Count == targetCollection.Count && !sourceCollection.Cast<string>().Except(targetCollection.Cast<string>()).Any()) {
                  continue;
                }
              }
              throw new SPModelProvisionException(String.Format("Definition for field '{0}' in content type '{1}' conflicts with parent content type.", definition.InternalName, contentTypeAttribute.Name));
            }
          }
        }
      }
    }

    private string ComputeCheckSum() {
      using (MemoryStream ms = new MemoryStream()) {
        BinaryFormatter bf = new BinaryFormatter();
        bf.Serialize(ms, contentTypeAttribute);
        if (hasExplicitListAttribute) {
          bf.Serialize(ms, listAttribute);
        }
        foreach (SPFieldAttribute f in fieldAttributes) {
          bf.Serialize(ms, f);
        }
        foreach (SPFieldAttribute f in hiddenFields) {
          bf.Serialize(ms, f);
        }
        ms.Seek(0, SeekOrigin.Begin);
        using (SHA1Managed sha1 = new SHA1Managed()) {
          byte[] hash = sha1.ComputeHash(ms);
          StringBuilder formatted = new StringBuilder(2 * hash.Length);
          foreach (byte b in hash) {
            formatted.AppendFormat("{0:X2}", b);
          }
          return formatted.ToString();
        }
      }
    }

    private void Provision(string siteUrl, Guid siteId, Guid webId, bool provisionContentType, bool provisionList, bool matchChecksum, SPModelListProvisionOptions listOptions, ProvisionResult result) {
      try {
        if (provisionContentType) {
          CheckFieldConsistency();
          ProvisionContentType(siteUrl, siteId, true, true, matchChecksum, listOptions != SPModelListProvisionOptions.Default ? null : result.ProvisionedLists);
        }
        if (provisionList && (listOptions != SPModelListProvisionOptions.Default || !String.IsNullOrEmpty(listAttribute.Url))) {
          ProvisionList(siteUrl, siteId, webId, listOptions, result.ProvisionedLists);
        }
      } catch (Exception ex) {
        result.Exception = ex;
        SPDiagnosticsService.Local.WriteTrace(TraceCategory.ModelProvision, ex);
        SPDiagnosticsService.Local.WriteTrace(TraceCategory.ModelProvision, String.Concat("[Invocation site ", result.StackTrace, "]"));
      }
    }

    private void ProvisionContentType(string siteUrl, Guid siteId, bool provisionParent, bool provisionChildren, bool matchChecksum, HashSet<SPModelUsage> deferredListUrls) {
      if (provisionParent && this.Parent != null) {
        this.Parent.ProvisionContentType(siteUrl, siteId, true, false, matchChecksum, null);
      }
      if (contentTypeAttribute != null) {
        if (TryLockSite(siteId)) {
          provisionedSites.TryAdd(siteId, true);
          try {
            if (checksum == null) {
              checksum = ComputeCheckSum();
            }
            SPModelProvisionEventReceiver eventReceiver = GetProvisionEventReceiver(true);
            using (SPModelProvisionHelper helper = new SPModelProvisionHelper(siteId, eventReceiver)) {
              SPContentType contentType = helper.EnsureContentType(contentTypeAttribute);
              if (!matchChecksum || helper.GetContentTypeChecksum(contentType) != checksum) {
                helper.UpdateContentType(contentType, contentTypeAttribute, fieldAttributes, checksum);
              }
              SaveAssemblyName(helper.TargetSite, contentTypeAttribute.ContentTypeId, this.ModelType.Assembly);

              foreach (SPModelUsage usage in GetUsages(helper.TargetSite.RootWeb)) {
                if (usage.ContentTypeId.Parent == contentTypeAttribute.ContentTypeId) {
                  using (SPModelProvisionHelper helper2 = new SPModelProvisionHelper(siteId, eventReceiver)) {
                    SPList list = helper2.ObjectCache.GetList(usage.WebId, usage.ListId);
                    if (list == null) {
                      continue;
                    }
                    SPContentType listContentType = list.ContentTypes[usage.ContentTypeId];
                    if (listContentType == null) {
                      continue;
                    }
                    if (!matchChecksum || helper2.GetContentTypeChecksum(listContentType) != checksum) {
                      helper2.UpdateContentType(listContentType, contentTypeAttribute, fieldAttributes, checksum);
                    }
                    if (!matchChecksum || helper2.GetContentTypeChecksum(list, contentTypeAttribute.ContentTypeId) != checksum) {
                      SPListAttribute listAttributeClone = listAttribute.Clone(list);
                      helper2.UpdateList(list, listAttributeClone, contentTypeAttribute, fieldAttributes, hiddenFields.ToArray(), new SPContentTypeId[0], checksum);
                    }
                  }
                  if (deferredListUrls != null) {
                    deferredListUrls.Add(usage.GetWithoutList());
                  }
                }
              }
            }
          } catch (Exception ex) {
            bool dummy;
            provisionedSites.TryRemove(siteId, out dummy);
            throw new SPModelProvisionException(String.Format("Unable to provision for type '{0}'. {1}. {2}", this.ModelType.Name, siteUrl, ex.Message), ex);
          } finally {
            EventWaitHandle handle;
            if (provisionedSitesLocks.TryRemove(siteId, out handle)) {
              handle.Set();
              handle.Close();
            }
          }
        }
      }
      if (provisionChildren) {
        foreach (SPModelDescriptor child in this.Children) {
          if (!child.hasExplicitListAttribute) {
            child.ProvisionContentType(siteUrl, siteId, false, true, matchChecksum, deferredListUrls);
          }
        }
      }
    }

    private void ProvisionList(string siteUrl, Guid siteId, Guid webId, SPModelListProvisionOptions listOptions, HashSet<SPModelUsage> deferredListUrls) {
      if (checksum == null) {
        checksum = ComputeCheckSum();
      }
      using (SPModelProvisionHelper helper = new SPModelProvisionHelper(siteId, GetProvisionEventReceiver(true))) {
        SPList targetList = null;
        if (listOptions.TargetListId != Guid.Empty) {
          targetList = helper.ObjectCache.GetList(listOptions.TargetWebId, listOptions.TargetListId);
          if (targetList == null) {
            return;
          }
          helper.UpdateList(targetList, listAttribute.Clone(targetList), contentTypeAttribute, fieldAttributes, hiddenFields.ToArray(), new SPContentTypeId[0], checksum);
        } else {
          SPListAttribute implListAttribute = listOptions.ListAttributeOverrides ?? listAttribute;
          if (listOptions.TargetListUrl != null) {
            implListAttribute = implListAttribute.Clone(listOptions.TargetListUrl);
          } else {
            implListAttribute = implListAttribute.Clone();
          }
          if (listOptions.TargetListTitle != null) {
            implListAttribute.Title = listOptions.TargetListTitle;
          }
          List<SPContentTypeId> contentTypes;
          targetList = helper.EnsureList(helper.ObjectCache.GetWeb(webId), implListAttribute, out contentTypes);
          helper.UpdateList(targetList, implListAttribute, contentTypeAttribute, fieldAttributes, hiddenFields.ToArray(), contentTypes, checksum);
        }
        foreach (SPContentType ct in targetList.ContentTypes) {
          if (ct.Id.Parent == contentTypeAttribute.ContentTypeId) {
            deferredListUrls.Add(SPModelUsage.Create(targetList, ct.Id).GetWithoutList());
            break;
          }
        }
      }
    }

    private bool TryLockSite(Guid siteId) {
      EventWaitHandle handleOwn = new EventWaitHandle(false, EventResetMode.AutoReset);
      EventWaitHandle handle = provisionedSitesLocks.GetOrAdd(siteId, handleOwn);
      if (handle != handleOwn) {
        handleOwn.Close();
        if (!handle.WaitOne(10000)) {
          throw new SPModelProvisionException("Provision lock waiting time exceeded.");
        }
        return false;
      }
      return true;
    }

    private SPModelProvisionEventReceiver GetProvisionEventReceiver(bool includeInterfaces) {
      SPModelProvisionMulticastEventReceiver eventReceivers = new SPModelProvisionMulticastEventReceiver();
      if (this.Parent != null) {
        eventReceivers.Add(this.Parent.GetProvisionEventReceiver(false));
      }
      foreach (SPModelDescriptor descriptor in this.Interfaces) {
        eventReceivers.Add(descriptor.GetProvisionEventReceiver(false));
      }
      if (provisionEventReceiverType != null) {
        eventReceivers.Add((SPModelProvisionEventReceiver)provisionEventReceiverType.CreateInstance());
      }
      if (eventReceivers.Count > 1) {
        return eventReceivers;
      }
      if (eventReceivers.Count == 1) {
        return eventReceivers[0];
      }
      return SPModelProvisionEventReceiver.Default;
    }

    private static bool IsUrlInScope(string startUrl, string url) {
      return (url.Length > startUrl.Length && url.StartsWith(startUrl, StringComparison.OrdinalIgnoreCase) && url[startUrl.Length] == '/');
    }

    private static bool IsTwoColumnField(SPFieldAttribute field) {
      return (field.Type == SPFieldType.Lookup || field.Type == SPFieldType.User || field.Type == SPFieldType.URL);
    }

    private static bool IsTwoColumnField(SPField field) {
      return (field.Type == SPFieldType.Lookup || field.Type == SPFieldType.User || field.Type == SPFieldType.URL);
    }

    private static void SaveAssemblyName(SPSite site, SPContentTypeId contentTypeId, Assembly assembly) {
      using (site.RootWeb.GetAllowUnsafeUpdatesScope()) {
        site.RootWeb.AllProperties["SPModel." + contentTypeId.ToString().ToLower() + ".Assembly"] = assembly.FullName;
        site.RootWeb.Update();
      }
    }

    private static bool NeedProcess(Assembly assembly) {
      object dummy = new object();
      return dummy == RegisteredAssembly.GetOrAdd(assembly, dummy);
    }

    private static void RegisterAssembly(Assembly assembly) {
      AssemblyName[] refAsm = new AssemblyName[0];
      try {
        refAsm = assembly.GetReferencedAssemblies();
      } catch { }
      if (NeedProcess(assembly) && (assembly == typeof(SPModel).Assembly || refAsm.Any(v => AssemblyName.ReferenceMatchesDefinition(v, SelfAssemblyName)))) {
        bool requireLock = !enteredLock;
        if (requireLock) {
          Monitor.Enter(syncLock);
          enteredLock = true;
        }
        try {
          RegisterAssemblyRecursive(assembly);
        } finally {
          if (requireLock) {
            enteredLock = false;
            Monitor.Exit(syncLock);
          }
        }
      }
    }

    private static void RegisterAssemblyRecursive(Assembly assembly) {
      try {
        List<Type> modelTypes = new List<Type>(assembly.GetLoadedTypes().Where(v => v.IsSubclassOf(typeof(SPModel)) && v.GetCustomAttribute<SPContentTypeAttribute>(false) != null));
        foreach (Type type in modelTypes) {
          if (type.BaseType.Assembly != assembly && NeedProcess(type.BaseType.Assembly)) {
            RegisterAssemblyRecursive(type.BaseType.Assembly);
          }
        }
        modelTypes.Sort(new TypeInheritanceComparer());
        modelTypes.ForEach(v => new SPModelDescriptor(v, assembly.GetCustomAttribute<SPModelDefaultsAttribute>()));
      } catch {
        object dummy;
        RegisteredAssembly.TryRemove(assembly, out dummy);
        throw;
      }
    }

    private static void ResolveContentTypeId(SPContentTypeAttribute contentTypeAttribute, Type targetType) {
      string contentTypeIdString = contentTypeAttribute.ContentTypeIdString;
      if (!contentTypeIdString.StartsWith("0x01")) {
        SPModelDescriptor descriptor;
        if (TargetTypeDictionary.TryGetValue(targetType.BaseType, out descriptor) && !(descriptor is SPModelInterfaceTypeDescriptor)) {
          contentTypeIdString = String.Concat(descriptor.ContentTypeIds.First(), contentTypeIdString);
        }
        if (!contentTypeIdString.StartsWith("0x01")) {
          contentTypeIdString = String.Concat(SPBuiltInContentTypeIdString.Item, contentTypeIdString);
        }
      }

      SPContentTypeId contentTypeId;
      try {
        contentTypeId = new SPContentTypeId(contentTypeIdString);
      } catch (ArgumentException) {
        throw new SPModelProvisionException(String.Format("Invalid content type ID '{0}' for type '{1}'", contentTypeIdString, targetType.Name));
      }
      if (ContentTypeDictionary.ContainsKey(contentTypeId)) {
        throw new SPModelProvisionException(String.Format("Type '{0}' uses duplicated content type ID with another model class", targetType));
      }
      contentTypeAttribute.SetFullContentTypeId(contentTypeId);
    }

    private static Type GetDefaultManagerType(Type targetType) {
      SPModelManagerDefaultTypeAttribute defaultManagerTypeAttribute = targetType.GetCustomAttribute<SPModelManagerDefaultTypeAttribute>(true);
      if (defaultManagerTypeAttribute != null) {
        if (!defaultManagerTypeAttribute.DefaultType.IsOf<ISPModelManagerInternal>()) {
          throw new SPModelProvisionException(String.Format("Type '0' must inherit SPModelManager", defaultManagerTypeAttribute.DefaultType.FullName));
        }
        if (defaultManagerTypeAttribute.DefaultType.IsGenericTypeDefinition) {
          return defaultManagerTypeAttribute.DefaultType.MakeGenericType(targetType);
        }
        return defaultManagerTypeAttribute.DefaultType;
      }
      return typeof(SPModelManager<>).MakeGenericType(targetType);
    }

    private static IList<SPContentTypeUsage> GetContentTypeUsages(SPWeb web, SPContentTypeId contentTypeId) {
      try {
        if (SPContentTypeCtor != null && SPContentTypePropertyWeb != null) {
          SPContentType ct = (SPContentType)SPContentTypeCtor.Invoke(new object[] { contentTypeId });
          SPContentTypePropertyWeb.SetValue(ct, web);
          return SPContentTypeUsage.GetUsages(ct);
        }
      } catch {
        SPContentType ct = web.AvailableContentTypes[contentTypeId];
        if (ct != null) {
          return SPContentTypeUsage.GetUsages(ct);
        }
      }
      return new SPContentTypeUsage[0];
    }
  }

  internal class SPModelInterfaceTypeDescriptor : SPModelDescriptor {
    private SPModelInterfaceTypeDescriptor(Type interfaceType)
      : base(interfaceType) {
      SPModelInterfaceAttribute attribute = interfaceType.GetCustomAttribute<SPModelInterfaceAttribute>(false);
      if (attribute != null) {
        this.EventHandlerType = attribute.EventHandlerType;
      }
    }

    public Type EventHandlerType { get; private set; }

    public override IEnumerable<SPContentTypeId> ContentTypeIds {
      get { return base.Children.SelectMany(v => v.ContentTypeIds); }
    }

    public override SPModelUsageCollection GetUsages(SPWeb web) {
      CommonHelper.ConfirmNotNull(web, "web");
      return new SPModelUsageCollection(web.Site, base.Children.SelectMany(v => v.GetUsages(web)));
    }

    public override SPModelUsageCollection GetUsages(SPList list) {
      CommonHelper.ConfirmNotNull(list, "list");
      return new SPModelUsageCollection(list.ParentWeb.Site, base.Children.SelectMany(v => v.GetUsages(list)));
    }

    public void AddImplementedType(SPModelDescriptor descriptor) {
      CommonHelper.ConfirmNotNull(descriptor, "descriptor");
      foreach (SPModelDescriptor otherType in base.Children) {
        if (otherType != descriptor) {
          foreach (SPFieldAttribute attribute in otherType.Fields) {
            descriptor.AddInterfaceDepenedentField(attribute);
          }
          foreach (SPFieldAttribute attribute in descriptor.Fields) {
            otherType.AddInterfaceDepenedentField(attribute);
          }
        }
      }
      foreach (SPFieldAttribute attribute in descriptor.Fields) {
        AddRequiredViewField(attribute);
      }
      SPContentTypeId contentTypeId = descriptor.ContentTypeIds.First();
      foreach (SPModelDescriptor otherType in base.Children) {
        if (contentTypeId.IsChildOf(otherType.ContentTypeIds.First())) {
          return;
        }
      }
      if (!baseType.HasValue) {
        baseType = descriptor.BaseType;
      } else if (baseType != descriptor.BaseType) {
        baseType = SPBaseType.UnspecifiedBaseType;
      }
      base.Children.Add(descriptor);
    }

    public static SPModelDescriptor Create(Type type) {
      CommonHelper.ConfirmNotNull(type, "type");
      return new SPModelInterfaceTypeDescriptor(type);
    }

    protected override void CheckFieldConsistency() {
      base.CheckFieldConsistency();
      this.Children.ForEach(CheckFieldConsistency);
    }
  }
}
