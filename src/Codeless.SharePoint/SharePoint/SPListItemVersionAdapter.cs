using Microsoft.SharePoint;
using System;
using System.Reflection;

namespace Codeless.SharePoint {
  /// <summary>
  /// Provides field value accessors to list item versions.
  /// </summary>
  public class SPListItemVersionAdapter : SPListItemAdapterBase {
    private static readonly PropertyInfo FieldNamesProperty = typeof(SPListItem).GetProperty("FieldNames", BindingFlags.Instance | BindingFlags.NonPublic);
    private readonly SPListItemVersion instance;

    /// <summary>
    /// Creates an adapter.
    /// </summary>
    /// <param name="item">Version of a list item.</param>
    public SPListItemVersionAdapter(SPListItemVersion item)
      : this(item, null) { }

    /// <summary>
    /// Creates an adapter with the given object cache.
    /// </summary>
    /// <param name="item">Version of a list item.</param>
    /// <param name="objectCache">Object cache.</param>
    public SPListItemVersionAdapter(SPListItemVersion item, SPObjectCache objectCache)
      : base(objectCache) {
      CommonHelper.ConfirmNotNull(item, "item");
      this.instance = item;
    }

    /// <summary>
    /// Gets or sets values to the specified column.
    /// </summary>
    /// <param name="name">Field name.</param>
    /// <returns>Value of the specified column.</returns>
    protected override object this[string name] {
      get { return instance[name]; }
      set { throw new InvalidOperationException("Item version is read-only"); }
    }

    /// <summary>
    /// Gets the site collection associated with the list item represented by the adapter.
    /// </summary>
    public override SPSite Site {
      get { return instance.ListItem.Web.Site; }
    }

    /// <summary>
    /// Gets the parent site of the list item represented by the adapter.
    /// </summary>
    public override SPWeb Web {
      get { return instance.ListItem.Web; }
    }

    /// <summary>
    /// Gets the unique ID of the list item represented by the adapter.
    /// </summary>
    public override Guid UniqueId {
      get { return instance.ListItem.UniqueId; }
    }

    /// <summary>
    /// Gets the parent site ID of the list item represented by the adapter.
    /// </summary>
    public override Guid WebId {
      get { return instance.ListItem.Web.ID; }
    }

    /// <summary>
    /// Gets the parent list ID of the list item represented by the adapter.
    /// </summary>
    public override Guid ListId {
      get { return instance.ListItem.ParentList.ID; }
    }

    /// <summary>
    /// Gets the list item ID of the list item represented by the adapter.
    /// </summary>
    public override int ListItemId {
      get { return instance.ListItem.ID; }
    }

    /// <summary>
    /// Gets the list item represented by the adapter.
    /// </summary>
    public override SPListItem ListItem {
      get { return instance.ListItem; }
    }

    /// <summary>
    /// Determines whether the specified field is included in the data set.
    /// </summary>
    /// <param name="fieldName">Field name.</param>
    /// <returns>Returns *true* if the specified field is included in the data set.</returns>
    public override bool HasField(string fieldName) {
      try {
        object dummy = instance[fieldName];
        return true;
      } catch {
        return false;
      }
    }
  }
}