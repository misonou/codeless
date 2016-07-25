using Microsoft.SharePoint;
using Microsoft.SharePoint.Utilities;
using System;
using System.Reflection;

namespace Codeless.SharePoint {
  /// <summary>
  /// Provides field value accessors to list item.
  /// </summary>
  public class SPListItemAdapter : SPListItemAdapterBase {
    private static readonly PropertyInfo FieldNamesProperty = typeof(SPListItem).GetProperty("FieldNames", BindingFlags.Instance | BindingFlags.NonPublic);
    private readonly SPListItem instance;

    /// <summary>
    /// Creates an adapter.
    /// </summary>
    /// <param name="item">List item.</param>
    public SPListItemAdapter(SPListItem item)
      : this(item, null) { }

    /// <summary>
    /// Creates an adapter with the given object cache.
    /// </summary>
    /// <param name="item">List item.</param>
    /// <param name="objectCache">Object cache.</param>
    public SPListItemAdapter(SPListItem item, SPObjectCache objectCache)
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
      get {
        return instance[name];
      }
      set {
        instance[name] = value;
        object currentContentTypeId = instance[SPBuiltInFieldId.ContentTypeId];
        if (currentContentTypeId != null) {
          instance[SPBuiltInFieldId.ContentTypeId] = currentContentTypeId;
        }
      }
    }

    /// <summary>
    /// Gets the title of the list item represented by the adapter.
    /// </summary>
    public override string Title {
      get { return instance.Title; }
    }

    /// <summary>
    /// Gets the server-relative URL of the list item represented by the adapter.
    /// </summary>
    public override string ServerRelativeUrl {
      get { return SPUrlUtility.CombineUrl(instance.Web.ServerRelativeUrl, instance.Url); }
    }

    /// <summary>
    /// Gets the site collection associated with the list item represented by the adapter.
    /// </summary>
    public override SPSite Site {
      get { return instance.Web.Site; }
    }

    /// <summary>
    /// Gets the parent site of the list item represented by the adapter.
    /// </summary>
    public override SPWeb Web {
      get { return instance.Web; }
    }

    /// <summary>
    /// Gets the unique ID of the list item represented by the adapter.
    /// </summary>
    public override Guid UniqueId {
      get { return instance.UniqueId; }
    }

    /// <summary>
    /// Gets the parent site ID of the list item represented by the adapter.
    /// </summary>
    public override Guid WebId {
      get { return instance.Web.ID; }
    }

    /// <summary>
    /// Gets the parent list ID of the list item represented by the adapter.
    /// </summary>
    public override Guid ListId {
      get { return instance.ParentList.ID; }
    }

    /// <summary>
    /// Gets the list item ID of the list item represented by the adapter.
    /// </summary>
    public override int ListItemId {
      get { return instance.ID; }
    }

    /// <summary>
    /// Gets the list item represented by the adapter.
    /// </summary>
    public override SPListItem ListItem {
      get { return instance; }
    }

    /// <summary>
    /// Gets the content type ID of the list item represented by the adapter.
    /// </summary>
    public override SPContentTypeId ContentTypeId {
      get { return instance.ContentTypeId; }
    }

    /// <summary>
    /// Gets the permissions of the list item represented by the adapter which is granted to the current user.
    /// </summary>
    public override SPBasePermissions EffectivePermissions {
      get { return instance.EffectiveBasePermissions; }
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