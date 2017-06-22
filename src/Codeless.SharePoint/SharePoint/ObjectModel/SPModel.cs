using Codeless.SharePoint.ObjectModel.Linq;
using Microsoft.Practices.Unity.InterceptionExtension;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Taxonomy;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.SymbolStore;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;

namespace Codeless.SharePoint.ObjectModel {
  /// <summary>
  /// Provides the base class of model objects representing a list item in site collection.
  /// </summary>
  public abstract class SPModel : ISPModelMetaData {
    private static readonly ModuleBuilder ModuleBuilder;
    [ThreadStatic]
    private static bool calledByInternal;

    internal static readonly string[] RequiredViewFields = new[] {
      SPBuiltInFieldName.ContentTypeId,
      SPBuiltInFieldName.Title,
      SPBuiltInFieldName.UniqueId,
      SPBuiltInFieldName.FileRef,
      SPBuiltInFieldName.FileLeafRef,
      SPBuiltInFieldName.PermMask,
      SPBuiltInFieldName.ID,
      SPBuiltInFieldName._UIVersionString,
      SPBuiltInFieldName.CheckoutUser,
      SPBuiltInFieldName.Modified
    };

    internal static readonly string[] RequiredSearchProperties = new[] {
      SPBuiltInFieldName.ContentTypeId,
      SPBuiltInFieldName.Title,
      BuiltInManagedPropertyName.UniqueID,
      BuiltInManagedPropertyName.WebId,
      BuiltInManagedPropertyName.ListID,
      BuiltInManagedPropertyName.ListItemID,
      BuiltInManagedPropertyName.Path,
      BuiltInManagedPropertyName.HitHighlightedSummary,
      BuiltInManagedPropertyName.LastModifiedTime,
      "UIVersionStringOWSTEXT"
    };

    static SPModel() {
      AssemblyBuilder assemblyBuilder = Thread.GetDomain().DefineDynamicAssembly(new AssemblyName("__DynamicSPModel"), AssemblyBuilderAccess.Run);
      ModuleBuilder = assemblyBuilder.DefineDynamicModule("__DynamicSPModel");
    }

    /// <summary>
    /// Default constructor.
    /// </summary>
    public SPModel() {
      if (!calledByInternal) {
        throw new InvalidOperationException("Class derived from SPModel must be instantiated internally");
      }
    }

    /// <summary>
    /// Gets the data access adapter of the underlying list item.
    /// </summary>
    protected internal ISPListItemAdapter Adapter { get; private set; }

    /// <summary>
    /// Get the <see cref="SPModelCollection"/> object this object belongs to.
    /// </summary>
    protected internal SPModelCollection ParentCollection { get; private set; }

    /// <summary>
    /// Get the <see cref="ISPModelManager"/> object this object belongs to.
    /// </summary>
    protected internal ISPModelManager Manager { get { return this.ParentCollection.Manager; } }

    /// <summary>
    /// Invoked when the underlying list item is being added to a list.
    /// </summary>
    /// <param name="e">Event data.</param>
    protected virtual void OnAdding(SPModelEventArgs e) { }

    /// <summary>
    /// Invoked when the underlying list item is added to a list.
    /// </summary>
    /// <param name="e">Event data.</param>
    protected virtual void OnAdded(SPModelEventArgs e) { }

    /// <summary>
    /// Invoked asynchronously when the underlying list item is added to a list.
    /// </summary>
    /// <param name="e">Event data.</param>
    protected virtual void OnAddedAsync(SPModelEventArgs e) { }

    /// <summary>
    /// Invoked when the underlying list item is being updated.
    /// </summary>
    /// <param name="e">Event data.</param>
    protected virtual void OnUpdating(SPModelEventArgs e) { }

    /// <summary>
    /// Invoked when the underlying list item is updated.
    /// </summary>
    /// <param name="e">Event data.</param>
    protected virtual void OnUpdated(SPModelEventArgs e) { }

    /// <summary>
    /// Invoked asynchronously when the underlying list item is updated.
    /// </summary>
    /// <param name="e">Event data.</param>
    protected virtual void OnUpdatedAsync(SPModelEventArgs e) { }

    /// <summary>
    /// Invoked when the underlying list item is being added to a list or being updated.
    /// </summary>
    /// <param name="e">Event data.</param>
    protected virtual void OnAddingOrUpdating(SPModelEventArgs e) { }

    /// <summary>
    /// Invoked when the underlying list item is added to a list or updated.
    /// </summary>
    /// <param name="e">Event data.</param>
    protected virtual void OnAddedOrUpdated(SPModelEventArgs e) { }

    /// <summary>
    /// Invoked asynchronously when the underlying list item is added to a list or updated.
    /// </summary>
    /// <param name="e">Event data.</param>
    protected virtual void OnAddedOrUpdatedAsync(SPModelEventArgs e) { }

    /// <summary>
    /// Invoked when the underlying list item is being deleted.
    /// </summary>
    /// <param name="e">Event data.</param>
    protected virtual void OnDeleting(SPModelEventArgs e) { }

    /// <summary>
    /// Invoked when the underlying list item is deleted.
    /// </summary>
    /// <param name="e">Event data.</param>
    protected virtual void OnDeleted(SPModelEventArgs e) { }

    /// <summary>
    /// Invoked when the underlying list item is being published.
    /// </summary>
    /// <param name="e">Event data.</param>
    protected virtual void OnPublishing(SPModelEventArgs e) { }

    /// <summary>
    /// Invoked when the underlying list item is published.
    /// </summary>
    /// <param name="e">Event data.</param>
    protected virtual void OnPublished(SPModelEventArgs e) { }

    internal void HandleEvent(SPModelEventArgs e) {
      SPModelDescriptor descriptor = SPModelDescriptor.Resolve(this.GetType());
      foreach (SPModelInterfaceTypeDescriptor d in descriptor.Interfaces) {
        if (d.EventHandlerType != null) {
          ISPModelEventHandler handler = (ISPModelEventHandler)d.EventHandlerType.CreateInstance();
          handler.HandleEvent(this, e);
        }
      }
      switch (e.EventType) {
        case SPModelEventType.Adding:
          OnAdding(e);
          OnAddingOrUpdating(e);
          return;
        case SPModelEventType.Added:
          OnAdded(e);
          OnAddedOrUpdated(e);
          return;
        case SPModelEventType.AddedAsync:
          OnAddedAsync(e);
          OnAddedOrUpdatedAsync(e);
          return;
        case SPModelEventType.Updating:
          OnUpdating(e);
          OnAddingOrUpdating(e);
          return;
        case SPModelEventType.Updated:
          OnUpdated(e);
          OnAddedOrUpdated(e);
          return;
        case SPModelEventType.UpdatedAsync:
          OnUpdatedAsync(e);
          OnAddedOrUpdatedAsync(e);
          return;
        case SPModelEventType.Deleting:
          OnDeleting(e);
          return;
        case SPModelEventType.Deleted:
          OnDeleted(e);
          return;
        case SPModelEventType.Publishing:
          OnPublishing(e);
          return;
        case SPModelEventType.Published:
          OnPublished(e);
          return;
      }
    }
    
    public static void Watch<T>(SPSite site, EventHandler<SPChangeMonitorEventArgs> listener) {
      CommonHelper.ConfirmNotNull(listener, "listener");
      SPModelMonitor<T>.GetMonitor(site).ObjectChanged += listener;
    }

    public static void Unwatch<T>(SPSite site, EventHandler<SPChangeMonitorEventArgs> listener) {
      CommonHelper.ConfirmNotNull(listener, "listener");
      SPModelMonitor<T>.GetMonitor(site).ObjectChanged -= listener;
    }

    /// <summary>
    /// Gets a list of content type ID associated with the specified model type.
    /// </summary>
    /// <param name="type">A type that derives from <see cref="SPModel"/>.</param>
    /// <returns>A list of content type ID.</returns>
    public static SPContentTypeId[] ResolveContentTypeId(Type type) {
      SPModelDescriptor descriptor = SPModelDescriptor.Resolve(type);
      return descriptor.ContentTypeIds.ToArray();
    }

    /// <summary>
    /// Gets a list of field internal names needed when querying list item represented by the specified model type.
    /// </summary>
    /// <param name="type">A type that derives from <see cref="SPModel"/>.</param>
    /// <returns>A list of internal names.</returns>
    public static string[] GetRequiredViewFields(Type type) {
      SPModelDescriptor descriptor = SPModelDescriptor.Resolve(type);
      return descriptor.RequiredViewFields;
    }

    /// <summary>
    /// Resolves inherited <see cref="SPModel"/> type associated with the specified content type ID.
    /// For details of associating Content type ID for a given type, see <see cref="SPContentTypeAttribute"/>.
    /// </summary>
    /// <param name="contentTypeId">Content type ID.</param>
    /// <exception cref="System.ArgumentException">No types are found with the specified content type ID.</exception>
    /// <returns>A resolved type.</returns>
    public static Type ResolveType(SPContentTypeId contentTypeId) {
      SPModelDescriptor descriptor = SPModelDescriptor.Resolve(contentTypeId);
      return descriptor.ModelType;
    }

    /// <summary>
    /// Creates content type(s) associated with the specified type to the given site, and create list if any.
    /// </summary>
    /// <remarks>
    /// Children content types associated with derived types are also created. Content types and site columns are prosivioned on the root site.
    /// If a <see cref="SPListAttribute"/> is attributed to the specified type, a list with URL specified by <see cref="SPListAttribute.Url"/> will be created on the given site.
    /// However, if another <see cref="SPListAttribute"/> is attributed to derived types, lists will *not* be created.</remarks>
    /// <param name="type">A type that derives from <see cref="SPModel"/>.</param>
    /// <param name="targetWeb">Site object.</param>
    /// <returns>A collection of lists affected.</returns>
    public static ICollection<SPList> Provision(Type type, SPWeb targetWeb) {
      SPModelDescriptor descriptor = SPModelDescriptor.Resolve(type);
      SPModelUsageCollection collection = descriptor.Provision(targetWeb);
      return collection.GetListCollection();
    }

    /// <summary>
    /// Creates content type(s) associated with the specified type to the given site, and create list with the specified URL if any.
    /// </summary>
    /// <remarks>
    /// Children content types associated with derived types are also created. Content types and site columns are prosivioned on the root site.
    /// If a <see cref="SPListAttribute"/> is attributed to the specified type, a list with URL specified by <paramref name="webRelativeUrl"/> will be created on the given site.
    /// However, if another <see cref="SPListAttribute"/> is attributed to derived types, lists will *not* be created.
    /// </remarks>
    /// <param name="type">A type that derives from <see cref="SPModel"/>.</param>
    /// <param name="targetWeb">Site object.</param>
    /// <param name="webRelativeUrl">List URL.</param>
    /// <returns>A collection of lists affected.</returns>
    public static ICollection<SPList> Provision(Type type, SPWeb targetWeb, string webRelativeUrl) {
      SPModelDescriptor descriptor = SPModelDescriptor.Resolve(type);
      SPModelUsageCollection collection = descriptor.Provision(targetWeb, new SPModelListProvisionOptions(webRelativeUrl));
      return collection.GetListCollection();
    }

    /// <summary>
    /// Creates content type(s) associated with the specified type to the given site, and create list with the specified URL and title if any.
    /// </summary>
    /// <param name="type">A type that derives from <see cref="SPModel"/>.</param>
    /// <param name="targetWeb">Site object.</param>
    /// <param name="webRelativeUrl">List URL.</param>
    /// <param name="title">List title.</param>
    /// <returns>A collection of lists affected.</returns>
    public static ICollection<SPList> Provision(Type type, SPWeb targetWeb, string webRelativeUrl, string title) {
      SPModelDescriptor descriptor = SPModelDescriptor.Resolve(type);
      SPModelUsageCollection collection = descriptor.Provision(targetWeb, new SPModelListProvisionOptions(webRelativeUrl, title));
      return collection.GetListCollection();
    }

    /// <summary>
    /// Creates content type(s) associated with the specified type to the given list.
    /// </summary>
    /// <remarks>
    /// Children content types associated with derived types are also created. Content types and site columns are prosivioned on the root site.
    /// If a <see cref="SPListAttribute"/> is attributed to the specified type, values specified on the attribute are copied to the given list.
    /// </remarks>
    /// <param name="type">A type that derives from <see cref="SPModel"/>.</param>
    /// <param name="targetList">A list object.</param>
    /// <returns>A collection of lists affected.</returns>
    public static ICollection<SPList> Provision(Type type, SPList targetList) {
      SPModelDescriptor descriptor = SPModelDescriptor.Resolve(type);
      SPModelUsageCollection collection = descriptor.Provision(targetList.ParentWeb, new SPModelListProvisionOptions(targetList));
      return collection.GetListCollection();
    }

    /// <summary>
    /// Creates a list template with the specified name. The list template created contains content type(s) associated with the specified type.
    /// </summary>
    /// <param name="type">A type that derives from <see cref="SPModel"/>.</param>
    /// <param name="targetWeb">Site object.</param>
    /// <param name="title">Name of list template.</param>
    public static void ProvisionAsTemplate(Type type, SPWeb targetWeb, string title) {
      SPList list = Provision(type, targetWeb, String.Concat("ListTemplate_", Path.GetRandomFileName().Replace(".", ""))).First();
      using (list.ParentWeb.GetAllowUnsafeUpdatesScope()) {
        string filename = String.Concat(title, ".stp");
        SPFile previousTemplate = list.ParentWeb.Site.RootWeb.GetFile("_catalogs/lt/" + filename);
        if (previousTemplate.Exists) {
          previousTemplate.Delete();
        }
        list.SaveAsTemplate(filename, title, String.Empty, false);
      }
      try {
        Thread.Sleep(5000);
        list.Delete();
      } catch { }
    }

    /// <summary>
    /// Gets the default manager instantiated with the specified site.
    /// Actual type of the created manager can be set through <see cref="SPModelManagerDefaultTypeAttribute"/> on the model type.
    /// If there is no <see cref="SPModelManagerDefaultTypeAttribute"/> specified, an <see cref="SPModelManager{T}"/> object is instantiated with <paramref name="type"/>.
    /// </summary>
    /// <param name="type">Model type.</param>
    /// <param name="contextWeb">A site object.</param>
    /// <returns>A manager object.</returns>
    public static ISPModelManager GetDefaultManager(Type type, SPWeb contextWeb) {
      SPModelDescriptor descriptor = SPModelDescriptor.Resolve(type);
      return descriptor.CreateManager(contextWeb);
    }

    /// <summary>
    /// Gets all lists under the specified site and all its descendant sites which contains the content type associated with the model type.
    /// </summary>
    /// <param name="type">Model type.</param>
    /// <param name="contextWeb">A site object.</param>
    /// <exception cref="ArgumentException">The type <paramref name="type"/> does not associate with any content type.</exception>
    /// <returns>An enumerable of list objects.</returns>
    public static IEnumerable<SPList> EnumerateLists(Type type, SPWeb contextWeb) {
      SPModelDescriptor descriptor = SPModelDescriptor.Resolve(type);
      return descriptor.GetUsages(contextWeb).Select(v => v.EnsureList(contextWeb.Site).List).Where(v => v != null).ToArray();
    }

    /// <summary>
    /// Determines whether the specified list contains the content type associated with the model type.
    /// </summary>
    /// <param name="list">A list object.</param>
    /// <param name="type">Model type.</param>
    /// <exception cref="ArgumentException">The type <paramref name="type"/> does not associate with any content type.</exception>
    /// <returns>*true* if the specified list contains content type that is associated with the model type.</returns>
    public static bool DoesListContainsType(SPList list, Type type) {
      SPModelDescriptor descriptor = SPModelDescriptor.Resolve(type);
      return descriptor.UsedInList(list);
    }

    /// <summary>
    /// Creates a model object representing the list item.
    /// </summary>
    /// <param name="listItem">A list item.</param>
    /// <returns>A model object or *null* if there is no types associated with the content type of the list item.</returns>
    public static SPModel TryCreate(SPListItem listItem) {
      CommonHelper.ConfirmNotNull(listItem, "listItem");
      return TryCreate(new SPListItemAdapter(listItem));
    }

    /// <summary>
    /// Creates a model object representing the list item.
    /// </summary>
    /// <param name="adapter">A data access adapter of the list item.</param>
    /// <returns>A model object or *null* if there is no types associated with the content type of the list item.</returns>
    public static SPModel TryCreate(ISPListItemAdapter adapter) {
      CommonHelper.ConfirmNotNull(adapter, "adapter");
      SPContentTypeId contentTypeId;
      try {
        contentTypeId = adapter.ContentTypeId;
      } catch (MemberAccessException) {
        return null;
      }
      if (adapter.Web.AvailableContentTypes[contentTypeId] == null) {
        contentTypeId = contentTypeId.Parent;
      }
      SPModelDescriptor descriptor;
      try {
        descriptor = SPModelDescriptor.Resolve(contentTypeId, adapter.Site);
      } catch (ArgumentException) {
        return null;
      }
      ISPModelManagerInternal manager = descriptor.CreateManager(adapter.Web);
      return manager.TryCreateModel(adapter, false);
    }

    internal static SPModel TryCreate(ISPListItemAdapter adapter, SPModelCollection parentCollection) {
      CommonHelper.ConfirmNotNull(adapter, "adapter");
      CommonHelper.ConfirmNotNull(parentCollection, "parentCollection");

      SPModelDescriptor exactType;
      try {
        exactType = SPModelDescriptor.Resolve(adapter.ContentTypeId);
      } catch (ArgumentException) {
        return null;
      }
      if (exactType.ModelType.IsGenericType) {
        throw new InvalidOperationException(String.Format("Cannot create object from generic type '{0}'. Consider adding SPModelManagerDefaultTypeAttribute to the model class.", exactType.ModelType.FullName));
      }
      try {
        calledByInternal = true;
        SPModel item = (SPModel)exactType.ModelInstanceType.CreateInstance();
        item.Adapter = Intercept.ThroughProxy(adapter, new TransparentProxyInterceptor(), new[] { new SPListItemAdapterInterceptionBehavior(adapter, parentCollection) });
        item.ParentCollection = parentCollection;
        return item;
      } finally {
        calledByInternal = false;
      }
    }

    internal static bool IsDynamicConstructedType(Type t) {
      return ModuleBuilder.Assembly.ManifestModule == t.Assembly.ManifestModule;
    }

    internal static Type BuildTypeFromAbstractBaseType(Type baseType) {
      Random random = new Random();
      string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
      string randomTypeName = String.Concat(baseType.Name, "__", new String(Enumerable.Repeat(chars, 8).Select(s => s[random.Next(s.Length)]).ToArray()));
      TypeBuilder typeBuilder = ModuleBuilder.DefineType(randomTypeName, TypeAttributes.Public | TypeAttributes.AutoClass | TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit, baseType);

      MethodInfo spModelGetAdapterMethod = typeof(SPModel).GetMethod("get_Adapter", true);
      MethodInfo spModelGetManagerMethod = typeof(SPModel).GetMethod("get_Manager", true);
      MethodInfo spModelGetParentCollectionMethod = typeof(SPModel).GetMethod("get_ParentCollection", true);
      MethodInfo ispModelManagerGetTermStoreMethod = typeof(ISPModelManager).GetMethod("get_TermStore");

      foreach (PropertyInfo sourceProperty in baseType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) {
        MethodInfo sourceGetter = sourceProperty.GetGetMethod(true);
        MethodInfo sourceSetter = sourceProperty.GetSetMethod(true);
        if ((sourceGetter != null && sourceGetter.IsAbstract) || (sourceSetter != null && sourceSetter.IsAbstract)) {
          SPModelFieldAssociationCollection association = SPModelFieldAssociationCollection.GetByMember(sourceProperty);
          if (!association.Queryable) {
            continue;
          }
          SPFieldAttribute field = association.Fields.First();
          MethodInfo getterMethod = null;
          MethodInfo setterMethod = null;
          MethodInfo postGetterMethod = null;
          MethodInfo preSetterMethod = null;
          Type secondParameterType = null;

          if (sourceProperty.PropertyType == typeof(bool)) {
            getterMethod = typeof(ISPListItemAdapter).GetMethod("GetBoolean");
            setterMethod = typeof(ISPListItemAdapter).GetMethod("SetBoolean");
          } else if (sourceProperty.PropertyType == typeof(int)) {
            getterMethod = typeof(ISPListItemAdapter).GetMethod("GetInteger");
            setterMethod = typeof(ISPListItemAdapter).GetMethod("SetInteger");
          } else if (sourceProperty.PropertyType == typeof(double)) {
            getterMethod = typeof(ISPListItemAdapter).GetMethod("GetNumber");
            setterMethod = typeof(ISPListItemAdapter).GetMethod("SetNumber");
          } else if (sourceProperty.PropertyType == typeof(string)) {
            if (field.Type == SPFieldType.Lookup) {
              getterMethod = typeof(ISPListItemAdapter).GetMethod("GetLookupFieldValue");
              setterMethod = typeof(ISPListItemAdapter).GetMethod("SetLookupFieldValue");
            } else if (field.Type == SPFieldType.URL) {
              getterMethod = typeof(ISPListItemAdapter).GetMethod("GetUrlFieldValue");
              setterMethod = typeof(SPExtension).GetMethod("SetUrlFieldValue", new[] { typeof(ISPListItemAdapter), typeof(string), typeof(string) });
              postGetterMethod = typeof(SPFieldUrlValue).GetProperty("Url").GetGetMethod();
            } else {
              getterMethod = typeof(ISPListItemAdapter).GetMethod("GetString");
              setterMethod = typeof(ISPListItemAdapter).GetMethod("SetString");
            }
          } else if (sourceProperty.PropertyType == typeof(Guid)) {
            getterMethod = typeof(ISPListItemAdapter).GetMethod("GetGuid");
            setterMethod = typeof(ISPListItemAdapter).GetMethod("SetGuid");
          } else if (sourceProperty.PropertyType == typeof(DateTime?)) {
            getterMethod = typeof(ISPListItemAdapter).GetMethod("GetDateTime");
            setterMethod = typeof(ISPListItemAdapter).GetMethod("SetDateTime");
          } else if (sourceProperty.PropertyType == typeof(DateTime)) {
            getterMethod = typeof(SPExtension).GetMethod("GetDateTimeOrMin");
            setterMethod = typeof(ISPListItemAdapter).GetMethod("SetDateTime");
            preSetterMethod = typeof(DateTime?).GetMethod("op_Implicit");
          } else if (sourceProperty.PropertyType == typeof(Term)) {
            getterMethod = typeof(ISPListItemAdapter).GetMethod("GetTaxonomy");
            setterMethod = typeof(ISPListItemAdapter).GetMethod("SetTaxonomy");
            secondParameterType = typeof(TermStore);
          } else if (sourceProperty.PropertyType == typeof(SPFieldUrlValue)) {
            getterMethod = typeof(ISPListItemAdapter).GetMethod("GetUrlFieldValue");
            setterMethod = typeof(ISPListItemAdapter).GetMethod("SetUrlFieldValue");
          } else if (sourceProperty.PropertyType == typeof(SPPrincipal)) {
            getterMethod = typeof(ISPListItemAdapter).GetMethod("GetUserFieldValue");
            setterMethod = typeof(ISPListItemAdapter).GetMethod("SetUserFieldValue");
          } else if (sourceProperty.PropertyType.IsOf<Enum>()) {
            getterMethod = typeof(ISPListItemAdapter).GetMethod("GetEnum").MakeGenericMethod(sourceProperty.PropertyType);
            setterMethod = typeof(ISPListItemAdapter).GetMethod("SetEnum").MakeGenericMethod(sourceProperty.PropertyType);
          } else if (sourceProperty.PropertyType.IsOf<SPModel>()) {
            getterMethod = typeof(ISPListItemAdapter).GetMethod("GetModel").MakeGenericMethod(sourceProperty.PropertyType);
            setterMethod = typeof(ISPListItemAdapter).GetMethod("SetModel").MakeGenericMethod(sourceProperty.PropertyType);
            secondParameterType = typeof(SPModelCollection);
          } else {
            Type elementType;
            if (sourceProperty.PropertyType.IsOf(typeof(IEnumerable<>), out elementType)) {
              bool isReadOnly = sourceProperty.PropertyType.IsOf(typeof(ReadOnlyCollection<>));
              if (elementType == typeof(Term)) {
                getterMethod = typeof(ISPListItemAdapter).GetMethod(isReadOnly ? "GetTaxonomyMultiReadOnly" : "GetTaxonomyMulti");
                secondParameterType = typeof(TermStore);
              } else if (elementType == typeof(SPPrincipal)) {
                getterMethod = typeof(ISPListItemAdapter).GetMethod(isReadOnly ? "GetMultiUserFieldValueReadOnly" : "GetMultiUserFieldValue");
              } else if (elementType == typeof(string)) {
                if (field.Type == SPFieldType.MultiChoice) {
                  getterMethod = typeof(ISPListItemAdapter).GetMethod(isReadOnly ? "GetMultiChoiceFieldValueReadOnly" : "GetMultiChoiceFieldValue");
                } else {
                  getterMethod = typeof(ISPListItemAdapter).GetMethod(isReadOnly ? "GetMultiLookupFieldValueReadOnly" : "GetMultiLookupFieldValue");
                }
              } else {
                try {
                  SPModelDescriptor.Resolve(elementType);
                  getterMethod = typeof(ISPListItemAdapter).GetMethod(isReadOnly ? "GetModelCollectionReadOnly" : "GetModelCollection").MakeGenericMethod(elementType);
                  secondParameterType = typeof(SPModelCollection);
                } catch (ArgumentException) { }
              }
              if (sourceSetter != null) {
                throw new InvalidOperationException("Collection property cannot have setter.");
              }
            }
          }
          if ((sourceGetter != null && getterMethod == null) || (sourceSetter != null && setterMethod == null)) {
            throw new InvalidOperationException(String.Format("Unable to find suitable method for '{0}.{1}'.", baseType.Name, sourceProperty.Name));
          }

          PropertyBuilder property = typeBuilder.DefineProperty(sourceProperty.Name, PropertyAttributes.HasDefault, sourceProperty.PropertyType, null);
          if (sourceGetter != null) {
            MethodBuilder propertyGetter = typeBuilder.DefineMethod(sourceGetter.Name, GetMethodVisibility(sourceGetter) | MethodAttributes.ReuseSlot | MethodAttributes.Virtual | MethodAttributes.SpecialName | MethodAttributes.HideBySig, sourceProperty.PropertyType, Type.EmptyTypes);
            ILGenerator propertyGetterIL = propertyGetter.GetILGenerator();
            propertyGetterIL.Emit(OpCodes.Ldarg_0);
            propertyGetterIL.Emit(OpCodes.Call, spModelGetAdapterMethod);
            propertyGetterIL.Emit(OpCodes.Ldstr, field.InternalName);
            if (secondParameterType != null) {
              if (secondParameterType == typeof(TermStore)) {
                propertyGetterIL.Emit(OpCodes.Ldarg_0);
                propertyGetterIL.Emit(OpCodes.Call, spModelGetManagerMethod);
                propertyGetterIL.Emit(OpCodes.Callvirt, ispModelManagerGetTermStoreMethod);
              } else if (secondParameterType == typeof(SPModelCollection)) {
                propertyGetterIL.Emit(OpCodes.Ldarg_0);
                propertyGetterIL.Emit(OpCodes.Call, spModelGetParentCollectionMethod);
              } else {
                throw new NotSupportedException();
              }
            }
            if (getterMethod.DeclaringType.IsInterface || getterMethod.IsAbstract) {
              propertyGetterIL.Emit(OpCodes.Callvirt, getterMethod);
            } else {
              propertyGetterIL.Emit(OpCodes.Call, getterMethod);
            }
            if (postGetterMethod != null) {
              propertyGetterIL.Emit(OpCodes.Call, postGetterMethod);
            }
            propertyGetterIL.Emit(OpCodes.Ret);
            property.SetGetMethod(propertyGetter);
          }
          if (sourceSetter != null) {
            MethodBuilder propertySetter = typeBuilder.DefineMethod(sourceSetter.Name, GetMethodVisibility(sourceSetter) | MethodAttributes.ReuseSlot | MethodAttributes.Virtual | MethodAttributes.SpecialName | MethodAttributes.HideBySig, null, new Type[] { sourceProperty.PropertyType });
            ILGenerator propertySetterIL = propertySetter.GetILGenerator();
            propertySetterIL.Emit(OpCodes.Ldarg_0);
            propertySetterIL.Emit(OpCodes.Call, spModelGetAdapterMethod);
            propertySetterIL.Emit(OpCodes.Ldstr, field.InternalName);
            propertySetterIL.Emit(OpCodes.Ldarg_1);
            if (preSetterMethod != null) {
              propertySetterIL.Emit(OpCodes.Call, preSetterMethod);
            }
            if (setterMethod.DeclaringType.IsInterface || setterMethod.IsAbstract) {
              propertySetterIL.Emit(OpCodes.Callvirt, setterMethod);
            } else {
              propertySetterIL.Emit(OpCodes.Call, setterMethod);
            }
            propertySetterIL.Emit(OpCodes.Nop);
            propertySetterIL.Emit(OpCodes.Ret);
            property.SetSetMethod(propertySetter);
          }
        }
      }
      return typeBuilder.CreateType();
    }

    private static MethodAttributes GetMethodVisibility(MethodInfo method) {
      if (method.IsPublic) {
        return MethodAttributes.Public;
      }
      if (method.IsFamilyAndAssembly) {
        return MethodAttributes.FamANDAssem;
      }
      if (method.IsFamilyOrAssembly) {
        return MethodAttributes.FamORAssem;
      }
      if (method.IsFamily) {
        return MethodAttributes.Family;
      }
      throw new ArgumentException("Unsupported method visiblity", "method");
    }

    #region ISPModelMetaData
    int ISPModelMetaData.ID {
      get { return this.Adapter.ListItemId; }
    }

    Guid ISPModelMetaData.UniqueId {
      get { return this.Adapter.UniqueId; }
    }

    string ISPModelMetaData.FileRef {
      get { return this.Adapter.ServerRelativeUrl.TrimStart('/'); }
    }

    Guid ISPModelMetaData.SiteId {
      get { return this.Adapter.Site.ID; }
    }

    Guid ISPModelMetaData.WebId {
      get { return this.Adapter.WebId; }
    }

    Guid ISPModelMetaData.ListId {
      get { return this.Adapter.ListId; }
    }

    string ISPModelMetaData.FileLeafRef {
      get { return this.Adapter.Filename; }
    }

    DateTime ISPModelMetaData.LastModified {
      get { return this.Adapter.LastModified; }
    }

    SPBasePermissions ISPModelMetaData.EffectivePermissions {
      get { return this.Adapter.EffectivePermissions; }
    }

    SPContentTypeId ISPModelMetaData.ContentTypeId {
      get { return this.Adapter.ContentTypeId; }
    }

    SPItemVersion ISPModelMetaData.Version {
      get { return this.Adapter.Version; }
    }

    int ISPModelMetaData.CheckOutUserID {
      get { 
        SPPrincipal p = this.Adapter.GetUserFieldValue(SPBuiltInFieldName.CheckoutUser);
        return p == null ? 0 : p.ID;
      }
    }

    string ISPModelMetaData.HitHighlightSummary {
      get { return (this.Adapter.HasField(BuiltInManagedPropertyName.HitHighlightedSummary) ? this.Adapter.GetString(BuiltInManagedPropertyName.HitHighlightedSummary) : String.Empty); }
    }
    #endregion
  }
}
