using Codeless.SharePoint.ObjectModel;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Administration;
using Microsoft.SharePoint.Taxonomy;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Web.Hosting;

namespace Codeless.SharePoint {
  /// <summary>
  /// Provides a base class to implement value accessors to different classes representing a list item.
  /// </summary>
  public abstract class SPListItemAdapterBase : MarshalByRefObject, ISPListItemAdapter {
    private SPObjectCache objectCache;

    /// <summary>
    /// Creates an adapter.
    /// </summary>
    public SPListItemAdapterBase() { }

    /// <summary>
    /// Creates an adapter with the given object cache.
    /// </summary>
    /// <param name="objectCache">Object cache.</param>
    public SPListItemAdapterBase(SPObjectCache objectCache) {
      this.objectCache = objectCache;
    }

    /// <summary>
    /// When overriden in derived classes, gets or sets values to the specified column.
    /// </summary>
    /// <param name="name">Field name.</param>
    /// <returns>Value of the specified column.</returns>
    protected abstract object this[string name] { get; set; }

    /// <summary>
    /// Gets the object cache set for the adapter. If the adapter is instantiated using default constructor, a new object cache instance is created.
    /// </summary>
    protected SPObjectCache ObjectCache {
      get { return LazyInitializer.EnsureInitialized(ref objectCache, () => new SPObjectCache(this.Site)); }
    }

    /// <summary>
    /// Gets the title of the list item represented by the adapter.
    /// </summary>
    public virtual string Title {
      get { return GetString(SPBuiltInFieldName.Title); }
    }

    /// <summary>
    /// Gets the filename of the list item represented by the adapter.
    /// </summary>
    public virtual string Filename {
      get {
        string value = GetString(SPBuiltInFieldName.FileLeafRef);
        int pos = value.IndexOf(";#");
        if (pos >= 0) {
          return value.Substring(pos + 2);
        }
        return value;
      }
    }

    /// <summary>
    /// Gets the server-relative URL of the list item represented by the adapter.
    /// </summary>
    public virtual string ServerRelativeUrl {
      get {
        string value = GetString(SPBuiltInFieldName.FileRef);
        int pos = value.IndexOf(";#");
        if (pos >= 0) {
          return String.Concat("/", value.Substring(pos + 2));
        }
        return value;
      }
    }

    /// <summary>
    /// When overriden in derived classes, gets the site collection associated with the list item represented by the adapter.
    /// </summary>
    public abstract SPSite Site { get; }

    /// <summary>
    /// When overriden in derived classes, gets the parent site of the list item represented by the adapter.
    /// </summary>
    public abstract SPWeb Web { get; }

    /// <summary>
    /// Gets the unique ID of the list item represented by the adapter.
    /// </summary>
    public virtual Guid UniqueId {
      get {
        object value = this[SPBuiltInFieldName.UniqueId];
        if (value == null) {
          throw new MemberAccessException("UniqueId");
        }
        if (value is Guid) {
          return (Guid)value;
        }
        SPFieldLookupValue uniqueIdValue = new SPFieldLookupValue(value.ToString());
        return new Guid(uniqueIdValue.LookupValue);
      }
    }

    /// <summary>
    /// When overriden in derived classes, gets the parent site ID of the list item represented by the adapter.
    /// </summary>
    public abstract Guid WebId { get; }

    /// <summary>
    /// When overriden in derived classes, gets the parent list ID of the list item represented by the adapter.
    /// </summary>
    public abstract Guid ListId { get; }

    /// <summary>
    /// When overriden in derived classes, gets the list item ID of the list item represented by the adapter.
    /// </summary>
    public abstract int ListItemId { get; }

    /// <summary>
    /// When overriden in derived classes, gets the list item represented by the adapter.
    /// </summary>
    public abstract SPListItem ListItem { get; }

    /// <summary>
    /// Gets the content type ID of the list item represented by the adapter.
    /// </summary>
    public virtual SPContentTypeId ContentTypeId {
      get {
        object value = this[SPBuiltInFieldName.ContentTypeId];
        if (value == null) {
          throw new MemberAccessException("ContentTypeId");
        }
        if (value is SPContentTypeId) {
          return (SPContentTypeId)value;
        }
        return new SPContentTypeId(value.ToString());
      }
    }

    /// <summary>
    /// Gets the last modified time of the list item represented by the adapter.
    /// </summary>
    public virtual DateTime LastModified {
      get { return GetDateTime(SPBuiltInFieldName.Modified).GetValueOrDefault(); }
    }

    /// <summary>
    /// Gets the permissions of the list item represented by the adapter which is granted to the current user.
    /// </summary>
    public virtual SPBasePermissions EffectivePermissions {
      get {
        string value = GetString(SPBuiltInFieldName.PermMask);
        return (SPBasePermissions)UInt64.Parse(value.Substring(2), NumberStyles.HexNumber);
      }
    }

    /// <summary>
    /// Gets the version number of the list item.
    /// </summary>
    public virtual SPItemVersion Version {
      get {
        string value = GetString(SPBuiltInFieldName._UIVersionString);
        return new SPItemVersion(value);
      }
    }

    /// <summary>
    /// When overidden, determines whether the specified field is included in the data set.
    /// </summary>
    /// <param name="fieldName">Field name.</param>
    /// <returns>Returns *true* if the specified field is included in the data set.</returns>
    public abstract bool HasField(string fieldName);

    /// <summary>
    /// Gets value from a text field, such as Text, Note and Publishing HTML field.
    /// If the field does not contain value, an empty string is returned.
    /// If the field is not a text column, a string representation of the value is returned.
    /// </summary>
    /// <param name="fieldName">Field name.</param>
    /// <returns>Value in the specified field as a string.</returns>
    public virtual string GetString(string fieldName) {
      object value = this[fieldName];
      if (value != null) {
        return value.ToString();
      }
      return String.Empty;
    }

    /// <summary>
    /// Gets value from an integer field.
    /// If the field does not contain value or the string representation of the value does not form an integer value, default value is returned.
    /// </summary>
    /// <param name="fieldName">Field name.</param>
    /// <returns>Value in the specified field as an integer.</returns>
    public virtual int GetInteger(string fieldName) {
      return GetSystemValueType(fieldName, Int32.Parse);
    }

    /// <summary>
    /// Gets value from a numeric field, such as Integer, Number and Currency field.
    /// If the field does not contain value or the string representation of the value does not form a double-precision value, default value is returned.
    /// </summary>
    /// <param name="fieldName">Field name.</param>
    /// <returns>Value in the specified field as a double.</returns>
    public virtual double GetNumber(string fieldName) {
      return GetSystemValueType(fieldName, Double.Parse);
    }

    /// <summary>
    /// Gets value from a boolean field.
    /// If the field does not contain value or the string representation of the value does not form a boolean value, default value is returned.
    /// </summary>
    /// <param name="fieldName">Field name.</param>
    /// <returns>Value in the specified field as a boolean.</returns>
    public virtual bool GetBoolean(string fieldName) {
      return GetSystemValueType(fieldName, Boolean.Parse);
    }

    /// <summary>
    /// Gets value from a GUID field.
    /// If the field does not contain value or the string representation of the value does not form a GUID value, default value is returned.
    /// </summary>
    /// <param name="fieldName">Field name.</param>
    /// <returns>Value in the specified field as a GUID.</returns>
    public virtual Guid GetGuid(string fieldName) {
      return GetSystemValueType(fieldName, (v) => new Guid(v));
    }

    /// <summary>
    /// Gets value from a DateTime field.
    /// If the field does not contain value, *null* is returned.
    /// </summary>
    /// <param name="fieldName">Field name.</param>
    /// <returns>Value in the specified field as a <see cref="DateTime"/> object.</returns>
    public virtual DateTime? GetDateTime(string fieldName) {
      object value = this[fieldName];
      if (value != null) {
        if (value is DateTime) {
          return DateTime.SpecifyKind((DateTime)value, DateTimeKind.Local);
        }
        DateTime dateTimeValue;
        if (DateTime.TryParseExact(value.ToString(), "yyyy-MM-ddTHH:mm:ssZ", null, DateTimeStyles.None, out dateTimeValue) ||
            DateTime.TryParseExact(value.ToString(), "yyyy-MM-dd HH:mm:ss", null, DateTimeStyles.AssumeLocal, out dateTimeValue)) {
          return dateTimeValue;
        }
      }
      return null;
    }

    /// <summary>
    /// Gets value from an Integer, Text, Choice or MultiChoice field and returns as the equivalent value of the enum type.
    /// For a MultiChoice field, the returned value is the bitwise OR result of the enum values represented by each selected choice.
    /// </summary>
    /// <typeparam name="TEnum">Enum type.</typeparam>
    /// <param name="fieldName">Field name.</param>
    /// <returns>Value in the specified field as an enum.</returns>
    public virtual TEnum GetEnum<TEnum>(string fieldName) where TEnum : struct {
      object value = this[fieldName];
      if (value != null) {
        string stringValue = value.ToString();
        TEnum enumValue;
        int intValue;

        if (stringValue.StartsWith(SPFieldMultiChoiceValue.Delimiter)) {
          if (stringValue.Length > 4) {
            int bitmask = 0;
            foreach (string entry in stringValue.Split(new[] { SPFieldMultiChoiceValue.Delimiter }, StringSplitOptions.RemoveEmptyEntries)) {
              if (Enum<TEnum>.TryParse(entry, true, out enumValue)) {
                bitmask |= (int)((object)enumValue);
              }
            }
            return (TEnum)((object)bitmask);
          }
        }
        if (Int32.TryParse(stringValue, out intValue)) {
          return (TEnum)((object)intValue);
        }
        if (Enum<TEnum>.TryParse(stringValue, true, out enumValue)) {
          return enumValue;
        }
      }
      return default(TEnum);
    }

    /// <summary>
    /// Gets a <see cref="Term"/> object from a Taxonomy field.
    /// </summary>
    /// <param name="fieldName">Field name.</param>
    /// <param name="termStore">Term store object.</param>
    /// <returns>Value in the specified field as a <see cref="Term"/> object.</returns>
    public virtual Term GetTaxonomy(string fieldName, TermStore termStore) {
      object value = this[fieldName];
      if (value != null) {
        try {
          Guid termId = new Guid(value.ToString().Split(TaxonomyField.TaxonomyGuidLabelDelimiter).Last());
          if (termId != Guid.Empty) {
            return termStore.GetTerm(termId);
          }
        } catch (FormatException) { }
      }
      return null;
    }

    /// <summary>
    /// Gets a collection of <see cref="Term"/> objects from a multiple Taxonomy field.
    /// </summary>
    /// <param name="fieldName">Field name.</param>
    /// <param name="termStore">Term store object.</param>
    /// <returns>A collection of <see cref="Term"/> objects.</returns>
    protected virtual IList<Term> GetTaxonomyMultiInternal(string fieldName, TermStore termStore) {
      Collection<Term> collection = new Collection<Term>();
      object value = this[fieldName];
      if (value != null) {
        foreach (string s in value.ToString().Split(TaxonomyField.TaxonomyMultipleTermDelimiter)) {
          try {
            Guid termId = new Guid(s.Split(TaxonomyField.TaxonomyGuidLabelDelimiter).Last());
            if (termId != Guid.Empty) {
              Term term = termStore.GetTerm(termId);
              if (term != null) {
                collection.Add(term);
              }
            }
          } catch (FormatException) { }
        }
      }
      return collection;
    }

    /// <summary>
    /// Gets value from a URL field where URL returned can be absolute or relative.
    /// The URL is normalized to a server-relative path if it points to the same SharePoint web application.
    /// </summary>
    /// <param name="fieldName">Field name.</param>
    /// <returns>Value in the specified field.</returns>
    public virtual SPFieldUrlValue GetUrlFieldValue(string fieldName) {
      object value = this[fieldName];
      if (value != null) {
        SPFieldUrlValue typedValue = new SPFieldUrlValue(value.ToString());
        if (typedValue.Url != null) {
          bool sameWebApplication;
          string normalizedUrl = NormalizeUrl(typedValue.Url, out sameWebApplication);
          if (HostingEnvironment.IsHosted && sameWebApplication) {
            normalizedUrl = new Uri(normalizedUrl).PathAndQuery;
          }
          return new SPFieldUrlValue { Url = normalizedUrl, Description = typedValue.Description };
        }
      }
      return new SPFieldUrlValue();
    }

    /// <summary>
    /// Gets a lookup value from a Lookup field.
    /// </summary>
    /// <param name="fieldName">Field name.</param>
    /// <returns>Value in the specified field.</returns>
    public virtual string GetLookupFieldValue(string fieldName) {
      object value = this[fieldName];
      if (value != null) {
        try {
          SPFieldLookupValue u = new SPFieldLookupValue(value.ToString());
          return u.LookupValue;
        } catch { }
      }
      return String.Empty;
    }

    /// <summary>
    /// Gets a collection of lookup values from a multiple Lookup field.
    /// </summary>
    /// <param name="fieldName">Field name.</param>
    /// <returns>A collection of lookup values.</returns>
    protected virtual IList<string> GetMultiLookupFieldValueInternal(string fieldName) {
      Collection<string> collection = new Collection<string>();
      object value = this[fieldName];
      if (value != null) {
        try {
          SPFieldLookupValueCollection values = CommonHelper.TryCastOrDefault<SPFieldLookupValueCollection>(value) ?? new SPFieldLookupValueCollection(value.ToString());
          foreach (SPFieldLookupValue u in values) {
            collection.Add(u.LookupValue);
          }
        } catch { }
      }
      return collection;
    }

    /// <summary>
    /// Gets an <see cref="SPPrincipal"/> object from a User field.
    /// </summary>
    /// <param name="fieldName">Field name.</param>
    /// <returns>An <see cref="SPPrincipal"/> object.</returns>
    public virtual SPPrincipal GetUserFieldValue(string fieldName) {
      object value = this[fieldName];
      if (value != null) {
        try {
          SPFieldUserValue u = new SPFieldUserValue(this.Web, value.ToString());
          if (u.LookupId == -1) {
            using (this.Web.GetAllowUnsafeUpdatesScope()) {
              return this.Web.EnsureUser(u.LookupValue);
            }
          }
          if (u.User != null) {
            return u.User;
          }
          if (u.LookupId > 0) {
            return this.Web.SiteGroups.GetByID(u.LookupId);
          }
        } catch { }
      }
      return null;
    }

    /// <summary>
    /// Gets a collection of <see cref="SPPrincipal"/> objects from a multiple User field.
    /// </summary>
    /// <param name="fieldName">Field name.</param>
    /// <returns>A collection of <see cref="SPPrincipal"/> objects.</returns>
    protected virtual IList<SPPrincipal> GetMultiUserFieldValueInternal(string fieldName) {
      Collection<SPPrincipal> collection = new Collection<SPPrincipal>();
      object value = this[fieldName];
      if (value != null) {
        try {
          SPFieldUserValueCollection values = CommonHelper.TryCastOrDefault<SPFieldUserValueCollection>(value) ?? new SPFieldUserValueCollection(this.Web, value.ToString());
          foreach (SPFieldUserValue u in values) {
            if (u.LookupId == -1) {
              using (this.Web.GetAllowUnsafeUpdatesScope()) {
                collection.Add(this.Web.EnsureUser(u.LookupValue));
              }
            } else if (u.User != null) {
              collection.Add(u.User);
            } else if (u.LookupId > 0) {
              collection.Add(this.Web.SiteGroups.GetByID(u.LookupId));
            }
          }
        } catch { }
      }
      return collection;
    }

    /// <summary>
    /// Gets a model object from a Lookup field.
    /// The same instance representing the same foreign list item is returned when the same <paramref name="parentCollection"/> is supplied.
    /// </summary>
    /// <typeparam name="T">Type of model object.</typeparam>
    /// <param name="fieldName">Field name.</param>
    /// <param name="parentCollection">An <see cref="SPModelCollection"/> object where the returned model object is cached in the collection.</param>
    /// <returns>Value in the specified field represented by a model object.</returns>
    public virtual T GetModel<T>(string fieldName, SPModelCollection parentCollection) {
      object value = this[fieldName];
      if (value != null) {
        try {
          SPField lookupField = GetLookupField(fieldName);
          SPFieldLookupValue u = new SPFieldLookupValue(value.ToString());
          return GetModel<T>(parentCollection, lookupField.ParentList, u.LookupId);
        } catch { }
      }
      return default(T);
    }

    /// <summary>
    /// Gets a collection of model objects from a Lookup field.
    /// The same instance representing the same foreign list item is returned when the same <paramref name="parentCollection"/> is supplied.
    /// </summary>
    /// <typeparam name="T">Type of model object.</typeparam>
    /// <param name="fieldName">Field name.</param>
    /// <param name="parentCollection">An <see cref="SPModelCollection"/> object where the returned model object is cached in the collection.</param>
    /// <returns>A collection of model objects.</returns>
    protected virtual IList<T> GetModelCollectionInternal<T>(string fieldName, SPModelCollection parentCollection) {
      Collection<T> collection = new Collection<T>();
      object value = this[fieldName];
      if (value != null) {
        try {
          SPField lookupField = GetLookupField(fieldName);
          SPFieldLookupValueCollection values = CommonHelper.TryCastOrDefault<SPFieldLookupValueCollection>(value) ?? new SPFieldLookupValueCollection(value.ToString());
          foreach (SPFieldLookupValue u in values) {
            T typedItem = GetModel<T>(parentCollection, lookupField.ParentList, u.LookupId);
            if (typedItem != null) {
              collection.Add(typedItem);
            }
          }
        } catch { }
      }
      return collection;
    }

    protected virtual IList<string> GetMultiChoiceFieldValueInternal(string fieldName) {
      Collection<string> collection = new Collection<string>();
      object value = this[fieldName];
      if (value != null) {
        SPFieldMultiChoiceValue values = new SPFieldMultiChoiceValue(value.ToString());
        for (int i = 0; i < values.Count; i++) {
          collection.Add(values[i]);
        }
      }
      return collection;
    }

    /// <summary>
    /// Sets value to a boolean field.
    /// </summary>
    /// <param name="fieldName">Field name.</param>
    /// <param name="value">A boolean value to set.</param>
    public virtual void SetBoolean(string fieldName, bool value) {
      this[fieldName] = value;
    }

    /// <summary>
    /// Sets value to an integer field.
    /// </summary>
    /// <param name="fieldName">Field name.</param>
    /// <param name="value">An integer value to set.</param>
    public virtual void SetInteger(string fieldName, int value) {
      this[fieldName] = value;
    }

    /// <summary>
    /// Sets value to a numeric field, such as Integer, Number and Currency field.
    /// </summary>
    /// <param name="fieldName">Field name.</param>
    /// <param name="value">A double value to set.</param>
    public virtual void SetNumber(string fieldName, double value) {
      this[fieldName] = value;
    }

    /// <summary>
    /// Sets value to a text field, such as Text, Note and Publishing HTML field.
    /// </summary>
    /// <param name="fieldName">Field name.</param>
    /// <param name="value">A string value to set.</param>
    public virtual void SetString(string fieldName, string value) {
      this[fieldName] = value;
    }

    /// <summary>
    /// Sets value to a GUID field.
    /// </summary>
    /// <param name="fieldName">Field name.</param>
    /// <param name="value">A GUID value to set.</param>
    public virtual void SetGuid(string fieldName, Guid value) {
      this[fieldName] = value.ToString("B");
    }

    /// <summary>
    /// Sets value to a DateTime field.
    /// </summary>
    /// <param name="fieldName">Field name.</param>
    /// <param name="value">A <see cref="DateTime"/> object representing the date to set; or *null* to empty the field.</param>
    public virtual void SetDateTime(string fieldName, DateTime? value) {
      if (value.HasValue) {
        this[fieldName] = value.Value.ToLocalTime();
      } else {
        this[fieldName] = null;
      }
    }

    /// <summary>
    /// Sets value to an Integer, Text, Choice or MultiChoice field with the equivalent representation of the enum value.
    /// For a MultiChoice field, the value set is the collection of choices representating each bit that is set to 1.
    /// </summary>
    /// <typeparam name="T">Enum type.</typeparam>
    /// <param name="fieldName">Field name.</param>
    /// <param name="value">A enum value to set.</param>
    public virtual void SetEnum<T>(string fieldName, T value) where T : struct {
      SPField field = this.Web.AvailableFields.GetFieldByInternalName(fieldName);
      if (field.Type == SPFieldType.Number || field.Type == SPFieldType.Integer) {
        this[fieldName] = Convert.ToInt32(value);
      } else if (field.Type == SPFieldType.MultiChoice) {
        this[fieldName] = String.Concat(SPFieldMultiChoiceValue.Delimiter, String.Join(SPFieldMultiChoiceValue.Delimiter, value.ToString().Split(new[] { ", " }, StringSplitOptions.None)), SPFieldMultiChoiceValue.Delimiter);
      } else {
        this[fieldName] = value.ToString();
      }
    }

    /// <summary>
    /// Sets value to a Taxonomy field.
    /// </summary>
    /// <param name="fieldName">Field name.</param>
    /// <param name="value">A <see cref="Term"/> object to set.</param>
    public virtual void SetTaxonomy(string fieldName, Term value) {
      TaxonomyFieldValue taxonomyFieldValue = new TaxonomyFieldValue("");
      taxonomyFieldValue.TermGuid = value.Id.ToString();
      taxonomyFieldValue.Label = value.Name;
      taxonomyFieldValue.WssId = value.EnsureWssId(this.Site, fieldName.Equals("TaxKeyword"));
      this[fieldName] = taxonomyFieldValue;
    }

    public virtual void SetUrlFieldValue(string fieldName, SPFieldUrlValue value) {
      if (value != null) {
        bool sameWebApplication = false;
        string normalizedUrl = value.Url;
        if (!String.IsNullOrEmpty(normalizedUrl)) {
          normalizedUrl = NormalizeUrl(normalizedUrl, out sameWebApplication);
        }
        if (value.Description == value.Url && sameWebApplication) {
          if (!String.IsNullOrEmpty(normalizedUrl)) {
            value.Description = new Uri(normalizedUrl).PathAndQuery;
          } else {
            value.Description = String.Empty;
          }
        }
        this[fieldName] = new SPFieldUrlValue { Url = normalizedUrl, Description = value.Description };
      } else {
        this[fieldName] = new SPFieldUrlValue();
      }
    }

    public virtual void SetLookupFieldValue(string fieldName, string value) {
      this[fieldName] = GetLookupIdByValue(fieldName, value);
    }

    public virtual void SetUserFieldValue(string fieldName, SPPrincipal user) {
      this[fieldName] = user.ID;
    }

    public virtual void SetModel<T>(string fieldName, T item) {
      if (Object.ReferenceEquals(item, null)) {
        this[fieldName] = null;
      } else {
        this[fieldName] = ((ISPModelMetaData)item).ID;
      }
    }

    public IList<Term> GetTaxonomyMulti(string fieldName, TermStore termStore) {
      return CreateNotifyingCollection(fieldName, GetTaxonomyMultiInternal(fieldName, termStore));
    }

    public IList<string> GetMultiLookupFieldValue(string fieldName) {
      return CreateNotifyingCollection(fieldName, GetMultiLookupFieldValueInternal(fieldName));
    }

    public IList<SPPrincipal> GetMultiUserFieldValue(string fieldName) {
      return CreateNotifyingCollection(fieldName, GetMultiUserFieldValueInternal(fieldName));
    }

    public IList<T> GetModelCollection<T>(string fieldName, SPModelCollection parentCollection) {
      return CreateNotifyingCollection(fieldName, GetModelCollectionInternal<T>(fieldName, parentCollection));
    }

    public IList<string> GetMultiChoiceFieldValue(string fieldName) {
      return CreateNotifyingCollection(fieldName, GetMultiChoiceFieldValueInternal(fieldName));
    }

    public ReadOnlyCollection<Term> GetTaxonomyMultiReadOnly(string fieldName, TermStore termStore) {
      return new ReadOnlyCollection<Term>(GetTaxonomyMultiInternal(fieldName, termStore));
    }

    public ReadOnlyCollection<string> GetMultiLookupFieldValueReadOnly(string fieldName) {
      return new ReadOnlyCollection<string>(GetMultiLookupFieldValueInternal(fieldName));
    }

    public ReadOnlyCollection<SPPrincipal> GetMultiUserFieldValueReadOnly(string fieldName) {
      return new ReadOnlyCollection<SPPrincipal>(GetMultiUserFieldValueInternal(fieldName));
    }

    public ReadOnlyCollection<T> GetModelCollectionReadOnly<T>(string fieldName, SPModelCollection parentCollection) {
      return new ReadOnlyCollection<T>(GetModelCollectionInternal<T>(fieldName, parentCollection));
    }

    public ReadOnlyCollection<string> GetMultiChoiceFieldValueReadOnly(string fieldName) {
      return new ReadOnlyCollection<string>(GetMultiChoiceFieldValueInternal(fieldName));
    }

    private IList<T> CreateNotifyingCollection<T>(string fieldName, IList<T> values) {
      if (values is ObservableCollection<T>) {
        return values;
      }
      ObservableCollection<T> collection = new ObservableCollection<T>(values);
      collection.CollectionChanged += ((sender, e) => OnCollectionChanged(sender, fieldName));
      return collection;
    }

    private TValue GetSystemValueType<TValue>(string fieldName, Func<string, TValue> parser) where TValue : struct {
      object value = this[fieldName];
      if (value is TValue) {
        return (TValue)value;
      }
      if (value != null) {
        try {
          return parser(value.ToString());
        } catch (FormatException) { }
      }
      return default(TValue);
    }

    private int? GetLookupIdByValue(string fieldName, object value) {
      if (value != null) {
        SPField lookupField = GetLookupField(fieldName);
        SPQuery query = new SPQuery();
        query.ViewFields = Caml.ViewFields(SPBuiltInFieldName.ID).ToString();
        query.Query = Caml.Equals(lookupField.InternalName, CamlParameterBinding.GetValueBinding(this.Site, lookupField, value)).ToString();
        query.RowLimit = 1;

        SPListItemCollection collection = lookupField.ParentList.GetItems(query);
        if (collection.Count > 0) {
          return collection[0].ID;
        }
        throw new ArgumentOutOfRangeException("value");
      }
      return null;
    }

    private SPField GetLookupField(string fieldName) {
      using (new SPSecurity.SuppressAccessDeniedRedirectInScope()) {
        SPField field = this.ListItem.Fields.GetFieldByInternalName(fieldName);
        SPList lookupList = null;
        string lookupField = SPBuiltInFieldName.Title;

        if (field.Type == SPFieldType.Integer) {
          lookupList = this.ListItem.ParentList;
        } else if (field.Type == SPFieldType.Lookup) {
          SPFieldLookup typedField = (SPFieldLookup)field;
          lookupField = typedField.LookupField;
          if (typedField.LookupList == "Self") {
            lookupList = this.ListItem.ParentList;
          } else {
            lookupList = this.ObjectCache.GetList(typedField.LookupWebId, new Guid(typedField.LookupList));
            if (lookupList == null) {
              throw new InvalidOperationException("Lookup list does not exists");
            }
          }
        }
        if (lookupList != null) {
          return lookupList.Fields.GetFieldByInternalName(lookupField);
        }
      }
      throw new InvalidOperationException("Invalid lookup field");
    }

    private string NormalizeUrl(string url, out bool sameWebApplication) {
      if (url.IndexOf(':') > 0) {
        if (url.StartsWith(this.Site.MakeFullUrl("/") + "/", StringComparison.OrdinalIgnoreCase)) {
          sameWebApplication = true;
          return url;
        }
        Uri uri = new Uri(url);
        foreach (SPAlternateUrl zone in this.Site.WebApplication.AlternateUrls) {
          if (zone.Uri.Scheme == uri.Scheme && zone.Uri.Host == uri.Host && zone.Uri.Port == uri.Port) {
            sameWebApplication = true;
            return this.Site.MakeFullUrl(uri.PathAndQuery);
          }
        }
        sameWebApplication = false;
        return url;
      }
      sameWebApplication = true;
      return this.Site.MakeFullUrl(url);
    }

    private T GetModel<T>(SPModelCollection parentCollection, SPList lookupList, int lookupId) {
      SPModel item;
      if (parentCollection.TryGetCachedModel(lookupList, lookupId, out item)) {
        if (item is T) {
          return (T)(object)item;
        }
      }
      return default(T);
    }

    private void OnCollectionChanged(object sender, string fieldName) {
      Type elementType = sender.GetType().GetEnumeratedType();
      if (elementType == typeof(SPPrincipal)) {
        SPFieldUserValueCollection collection = new SPFieldUserValueCollection();
        foreach (SPPrincipal user in (IEnumerable<SPPrincipal>)sender) {
          collection.Add(new SPFieldUserValue(user.ParentWeb, user.ID, user.Name));
        }
        this[fieldName] = collection.ToString();
      } else if (elementType == typeof(SPListItem)) {
        SPFieldLookupValueCollection collection = new SPFieldLookupValueCollection();
        foreach (SPListItem item in (IEnumerable<SPListItem>)sender) {
          collection.Add(new SPFieldLookupValue(item.ID, item.Title));
        }
        this[fieldName] = collection.ToString();
      } else if (elementType == typeof(Term)) {
        TaxonomyFieldValueCollection collection = new TaxonomyFieldValueCollection("");
        foreach (Term term in (IEnumerable<Term>)sender) {
          TaxonomyFieldValue value = new TaxonomyFieldValue("");
          value.Label = term.Name;
          value.TermGuid = term.Id.ToString();
          value.WssId = term.EnsureWssId(this.Site, fieldName.Equals("TaxKeyword"));
          collection.Add(value);
        }
        this[fieldName] = collection.ToString();
      } else if (elementType == typeof(string)) {
        SPFieldMultiChoiceValue collection = new SPFieldMultiChoiceValue();
        foreach (string item in (IEnumerable<string>)sender) {
          collection.Add(item);
        }
        this[fieldName] = collection.ToString();
      } else {
        SPFieldLookupValueCollection collection = new SPFieldLookupValueCollection();
        foreach (SPModel item in (IEnumerable)sender) {
          collection.Add(new SPFieldLookupValue(item.Adapter.ListItemId, item.Adapter.Title));
        }
        this[fieldName] = collection.ToString();
      }
    }
  }
}
