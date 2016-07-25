using Codeless.SharePoint.ObjectModel;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Taxonomy;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Threading;

namespace Codeless.SharePoint {
  /// <summary>
  /// Provides field value accessors to a <see cref="DataRow"/> instance returned from a cross-list query.
  /// </summary>
  public class DataRowAdapter : SPListItemAdapterBase {
    private readonly DataRow instance;
    private readonly SPSite parentSite;
    private readonly Guid webId;
    private readonly Guid listId;
    private readonly int listItemId;
    private SPListItemAdapter listItemAdapter;

    /// <summary>
    /// Creates an adapter.
    /// </summary>
    /// <param name="site">Site collection object.</param>
    /// <param name="item">List item.</param>
    public DataRowAdapter(SPSite site, DataRow item)
      : this(site, item, null) { }

    /// <summary>
    /// Creates an adapter with the given object cache.
    /// </summary>
    /// <param name="site">Site collection object.</param>
    /// <param name="item">List item.</param>
    /// <param name="objectCache">Object cache.</param>
    public DataRowAdapter(SPSite site, DataRow item, SPObjectCache objectCache)
      : base(objectCache) {
      CommonHelper.ConfirmNotNull(site, "site");
      CommonHelper.ConfirmNotNull(item, "item");
      this.parentSite = site;
      this.instance = item;
      this.webId = GetGuid("WebId");
      this.listId = GetGuid("ListId");
      this.listItemId = GetInteger("ID");
    }

    /// <summary>
    /// Gets or sets values to the specified column.
    /// </summary>
    /// <param name="name">Field name.</param>
    /// <returns>Value of the specified column.</returns>
    protected override object this[string name] {
      get { return instance[name]; }
      set { throw new InvalidOperationException(); }
    }

    /// <summary>
    /// Gets an <see cref="SPListItemAdapter"/> instance that references an <see cref="SPListItem"/> object.
    /// </summary>
    protected SPListItemAdapter ListItemAdapater {
      get { return listItemAdapter; }
    }

    /// <summary>
    /// Gets the site collection associated with the list item represented by the adapter.
    /// </summary>
    public override SPSite Site {
      get { return parentSite; }
    }

    /// <summary>
    /// Gets the parent site of the list item represented by the adapter.
    /// </summary>
    public override SPWeb Web {
      get { return CommonHelper.AccessNotNull(this.ObjectCache.GetWeb(this.WebId), "Web"); }
    }

    /// <summary>
    /// Gets the parent site ID of the list item represented by the adapter.
    /// </summary>
    public override Guid WebId {
      get { return webId; }
    }

    /// <summary>
    /// Gets the parent list ID of the list item represented by the adapter.
    /// </summary>
    public override Guid ListId {
      get { return listId; }
    }

    /// <summary>
    /// Gets the list item ID of the list item represented by the adapter.
    /// </summary>
    public override int ListItemId {
      get { return listItemId; }
    }

    /// <summary>
    /// Gets the list item represented by the adapter.
    /// </summary>
    public override SPListItem ListItem {
      get { return CommonHelper.AccessNotNull(this.ObjectCache.GetListItem(this.WebId, this.ListId, this.ListItemId), "ListItem"); }
    }

    /// <summary>
    /// Determines whether the specified field is included in the data set.
    /// </summary>
    /// <param name="fieldName">Field name.</param>
    /// <returns>Returns *true* if the specified field is included in the data set.</returns>
    public override bool HasField(string fieldName) {
      return instance.Table.Columns.Contains(fieldName);
    }

    /// <summary>
    /// Gets value from a boolean field.
    /// If the field does not contain value or the string representation of the value does not form a boolean value, default value is returned.
    /// </summary>
    /// <param name="fieldName">Field name.</param>
    /// <returns>Value in the specified field.</returns>
    public override bool GetBoolean(string fieldName) {
      if (this.ListItemAdapater != null) {
        return this.ListItemAdapater.GetBoolean(fieldName);
      }
      return "1".Equals((string)this[fieldName]);
    }

    /// <summary>
    /// Gets value from a DateTime field.
    /// If the field does not contain value, *null* is returned.
    /// </summary>
    /// <param name="fieldName">Field name.</param>
    /// <returns>Value in the specified field.</returns>
    public override DateTime? GetDateTime(string fieldName) {
      if (this.ListItemAdapater != null) {
        return this.ListItemAdapater.GetDateTime(fieldName);
      }
      return base.GetDateTime(fieldName);
    }

    /// <summary>
    /// Gets value from an Integer, Text, Choice or MultiChoice field and returns as the equivalent value of the enum type.
    /// For a MultiChoice field, the returned value is the bitwise OR result of the enum values represented by each selected choice.
    /// </summary>
    /// <typeparam name="TEnum">Enum type.</typeparam>
    /// <param name="fieldName">Field name.</param>
    /// <returns>Value in the specified field.</returns>
    public override TEnum GetEnum<TEnum>(string fieldName) {
      if (this.ListItemAdapater != null) {
        return this.ListItemAdapater.GetEnum<TEnum>(fieldName);
      }
      return base.GetEnum<TEnum>(fieldName);
    }

    /// <summary>
    /// Gets value from a GUID field.
    /// If the field does not contain value or the string representation of the value does not form a GUID value, default value is returned.
    /// </summary>
    /// <param name="fieldName">Field name.</param>
    /// <returns>Value in the specified field.</returns>
    public override Guid GetGuid(string fieldName) {
      if (this.ListItemAdapater != null) {
        return this.ListItemAdapater.GetGuid(fieldName);
      }
      return base.GetGuid(fieldName);
    }

    /// <summary>
    /// Gets value from an integer field.
    /// If the field does not contain value or the string representation of the value does not form an integer value, default value is returned.
    /// </summary>
    /// <param name="fieldName">Field name.</param>
    /// <returns>Value in the specified field.</returns>
    public override int GetInteger(string fieldName) {
      if (this.ListItemAdapater != null) {
        return this.ListItemAdapater.GetInteger(fieldName);
      }
      return base.GetInteger(fieldName);
    }

    /// <summary>
    /// Gets value from a Lookup field.
    /// </summary>
    /// <param name="fieldName">Field name.</param>
    /// <returns>Value in the specified field.</returns>
    public override string GetLookupFieldValue(string fieldName) {
      if (this.ListItemAdapater != null) {
        return this.ListItemAdapater.GetLookupFieldValue(fieldName);
      }
      return base.GetLookupFieldValue(fieldName);
    }

    public override T GetModel<T>(string fieldName, SPModelCollection parentCollection) {
      if (this.ListItemAdapater != null) {
        return this.ListItemAdapater.GetModel<T>(fieldName, parentCollection);
      }
      return base.GetModel<T>(fieldName, parentCollection);
    }

    /// <summary>
    /// Gets value from a numeric field, such as Integer, Number and Currency field.
    /// If the field does not contain value or the string representation of the value does not form a double-precision value, default value is returned.
    /// </summary>
    /// <param name="fieldName">Field name.</param>
    /// <returns>Value in the specified field.</returns>
    public override double GetNumber(string fieldName) {
      if (this.ListItemAdapater != null) {
        return this.ListItemAdapater.GetNumber(fieldName);
      }
      return base.GetNumber(fieldName);
    }

    /// <summary>
    /// Gets value from text field, such as Text, Note and Publishing HTML field.
    /// If the field does not contain value, an empty string is returned.
    /// If the field is not a text column, a string representation of the value is returned.
    /// </summary>
    /// <param name="fieldName">Field name.</param>
    /// <returns>Value in the specified field.</returns>
    public override string GetString(string fieldName) {
      if (this.ListItemAdapater != null) {
        return this.ListItemAdapater.GetString(fieldName);
      }
      return base.GetString(fieldName);
    }

    /// <summary>
    /// Gets value from a Taxonomy field.
    /// </summary>
    /// <param name="fieldName">Field name.</param>
    /// <param name="termStore">Term store object.</param>
    /// <returns>Value in the specified field.</returns>
    public override Term GetTaxonomy(string fieldName, TermStore termStore) {
      if (this.ListItemAdapater != null) {
        return this.ListItemAdapater.GetTaxonomy(fieldName, termStore);
      }
      object value = this[fieldName];
      if (value != null) {
        SPFieldLookupValue typedValue = new SPFieldLookupValue(value.ToString());
        return termStore.GetTermByWssId(this.Site, typedValue.LookupId);
      }
      return null;
    }

    /// <summary>
    /// Gets value from a URL field where URL returned can be absolute or relative.
    /// The URL is normalized to a server-relative path if it points to the same SharePoint web application.
    /// </summary>
    /// <param name="fieldName">Field name.</param>
    /// <returns>Value in the specified field.</returns>
    public override SPFieldUrlValue GetUrlFieldValue(string fieldName) {
      if (this.ListItemAdapater != null) {
        return this.ListItemAdapater.GetUrlFieldValue(fieldName);
      }
      return base.GetUrlFieldValue(fieldName);
    }

    public override SPPrincipal GetUserFieldValue(string fieldName) {
      if (this.ListItemAdapater != null) {
        return this.ListItemAdapater.GetUserFieldValue(fieldName);
      }
      return base.GetUserFieldValue(fieldName);
    }

    protected override IList<T> GetModelCollectionInternal<T>(string fieldName, SPModelCollection parentCollection) {
      if (this.ListItemAdapater != null) {
        return this.ListItemAdapater.GetModelCollection<T>(fieldName, parentCollection);
      }
      return base.GetModelCollectionInternal<T>(fieldName, parentCollection);
    }

    protected override IList<string> GetMultiLookupFieldValueInternal(string fieldName) {
      if (this.ListItemAdapater != null) {
        return this.ListItemAdapater.GetMultiLookupFieldValue(fieldName);
      }
      return base.GetMultiLookupFieldValueInternal(fieldName);
    }

    protected override IList<SPPrincipal> GetMultiUserFieldValueInternal(string fieldName) {
      if (this.ListItemAdapater != null) {
        return this.ListItemAdapater.GetMultiUserFieldValue(fieldName);
      }
      return base.GetMultiUserFieldValueInternal(fieldName);
    }

    protected override IList<Term> GetTaxonomyMultiInternal(string fieldName, TermStore termStore) {
      if (this.ListItemAdapater != null) {
        return this.ListItemAdapater.GetTaxonomyMulti(fieldName, termStore);
      }
      Collection<Term> collection = new Collection<Term>();
      object value = this[fieldName];
      if (value != null) {
        try {
          SPFieldLookupValueCollection values = new SPFieldLookupValueCollection(value.ToString());
          foreach (SPFieldLookupValue u in values) {
            Term term = termStore.GetTermByWssId(this.Site, u.LookupId);
            if (term != null) {
              collection.Add(term);
            }
          }
        } catch { }
      }
      return collection;
    }

    public override void SetBoolean(string fieldName, bool value) {
      EnsureListItemAdapter();
      this.ListItemAdapater.SetBoolean(fieldName, value);
    }

    public override void SetDateTime(string fieldName, DateTime? value) {
      EnsureListItemAdapter();
      this.ListItemAdapater.SetDateTime(fieldName, value);
    }

    public override void SetEnum<T>(string fieldName, T value) {
      EnsureListItemAdapter();
      this.ListItemAdapater.SetEnum(fieldName, value);
    }

    public override void SetGuid(string fieldName, Guid value) {
      EnsureListItemAdapter();
      this.ListItemAdapater.SetGuid(fieldName, value);
    }

    public override void SetInteger(string fieldName, int value) {
      EnsureListItemAdapter();
      this.ListItemAdapater.SetInteger(fieldName, value);
    }

    public override void SetLookupFieldValue(string fieldName, string value) {
      EnsureListItemAdapter();
      this.ListItemAdapater.SetLookupFieldValue(fieldName, value);
    }

    public override void SetModel<T>(string fieldName, T item) {
      EnsureListItemAdapter();
      this.ListItemAdapater.SetModel(fieldName, item);
    }

    public override void SetNumber(string fieldName, double value) {
      EnsureListItemAdapter();
      this.ListItemAdapater.SetNumber(fieldName, value);
    }

    public override void SetString(string fieldName, string value) {
      EnsureListItemAdapter();
      this.ListItemAdapater.SetString(fieldName, value);
    }

    public override void SetTaxonomy(string fieldName, Term value) {
      EnsureListItemAdapter();
      this.ListItemAdapater.SetTaxonomy(fieldName, value);
    }

    public override void SetUrlFieldValue(string fieldName, SPFieldUrlValue value) {
      EnsureListItemAdapter();
      this.ListItemAdapater.SetUrlFieldValue(fieldName, value);
    }

    public override void SetUserFieldValue(string fieldName, SPPrincipal user) {
      EnsureListItemAdapter();
      this.ListItemAdapater.SetUserFieldValue(fieldName, user);
    }

    protected SPListItemAdapter EnsureListItemAdapter() {
      return LazyInitializer.EnsureInitialized(ref listItemAdapter, () => new SPListItemAdapter(this.ListItem, this.ObjectCache));
    }
  }
}
