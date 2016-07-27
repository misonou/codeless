using Microsoft.SharePoint;
using Microsoft.SharePoint.Taxonomy;
using Microsoft.SharePoint.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Codeless.SharePoint {
  /// <summary>
  /// Provides field value accessors to an <see cref="SPItemEventProperties"/> instance.
  /// </summary>
  public class SPItemEventDataCollectionAdapter : SPListItemAdapterBase {
    private static readonly Dictionary<string, string> SpecialFields = new Dictionary<string, string> {
      { SPBuiltInFieldName.Title, "vti_title" },
      { SPBuiltInFieldName.ProgId, "vti_progid" },
      { SPBuiltInFieldName.Modified_x0020_By, "vti_modifiedby" },
      { SPBuiltInFieldName._Level, "vti_level" },
      { SPBuiltInFieldName.FolderChildCount, "vti_foldersubfolderitemcount" },
      { SPBuiltInFieldName.ItemChildCount, "vti_folderitemcount" },
    };

    private static readonly FieldInfo PropertiesParamField = typeof(SPItemEventDataCollection).GetField("m_propertiesParam", true);
    private readonly SPItemEventProperties instance;

    /// <summary>
    /// Creates an adapter.
    /// </summary>
    /// <param name="properties">An <see cref="SPItemEventProperties"/> object.</param>
    public SPItemEventDataCollectionAdapter(SPItemEventProperties properties)
      : this(properties, null) { }

    /// <summary>
    /// Creates an adapter with the given object cache.
    /// </summary>
    /// <param name="properties">An <see cref="SPItemEventProperties"/> object.</param>
    /// <param name="objectCache">Object cache.</param>
    public SPItemEventDataCollectionAdapter(SPItemEventProperties properties, SPObjectCache objectCache)
      : base(objectCache) {
      CommonHelper.ConfirmNotNull(properties, "properties");
      instance = properties;
    }

    /// <summary>
    /// Gets or sets values to the specified column.
    /// </summary>
    /// <param name="name">Field name.</param>
    /// <returns>Value of the specified column.</returns>
    protected override object this[string name] {
      get {
        string propertiesKey;
        if (!SpecialFields.TryGetValue(name, out propertiesKey)) {
          propertiesKey = name;
        }
        object afterPropertiesValue = instance.AfterProperties[propertiesKey];
        if (afterPropertiesValue != null) {
          return afterPropertiesValue;
        }
        if (instance.EventType == SPEventReceiverType.ItemAdding) {
          SPField field = instance.List.Fields.GetFieldByInternalName(name);
          return field.DefaultValue;
        }
        Array propertiesParam = (Array)PropertiesParamField.GetValue(instance.AfterProperties);
        for (int i = propertiesParam.GetUpperBound(1); --i >= 0; ) {
          object obj = propertiesParam.GetValue(0, i);
          if (name.Equals(obj)) {
            return propertiesParam.GetValue(1, i);
          }
        }
        if (instance.ListItem != null) {
          return instance.ListItem[name];
        }
        return null;
      }
      set {
        string propertiesKey;
        if (!SpecialFields.TryGetValue(name, out propertiesKey)) {
          propertiesKey = name;
        }
        instance.AfterProperties[propertiesKey] = value;
      }
    }

    /// <summary>
    /// Overriden.
    /// </summary>
    public override string Filename {
      get {
        if (instance.AfterUrl != null) {
          return Path.GetFileName(instance.AfterUrl);
        }
        return base.Filename;
      }
    }

    /// <summary>
    /// Gets the server-relative URL of the list item represented by the adapter.
    /// </summary>
    public override string ServerRelativeUrl {
      get {
        if (instance.AfterUrl != null) {
          return SPUrlUtility.CombineUrl(instance.RelativeWebUrl, instance.AfterUrl);
        }
        return base.ServerRelativeUrl;
      }
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
    /// Gets the parent site ID of the list item represented by the adapter.
    /// </summary>
    public override Guid WebId {
      get { return instance.Web.ID; }
    }

    /// <summary>
    /// Gets the parent list ID of the list item represented by the adapter.
    /// </summary>
    public override Guid ListId {
      get { return instance.ListId; }
    }

    /// <summary>
    /// Gets the list item ID of the list item represented by the adapter.
    /// </summary>
    public override int ListItemId {
      get { return instance.ListItemId; }
    }

    /// <summary>
    /// Gets the list item represented by the adapter.
    /// </summary>
    public override SPListItem ListItem {
      get { return instance.ListItem; }
    }

    /// <summary>
    /// Gets the permissions of the list item represented by the adapter which is granted to the current user.
    /// </summary>
    public override SPBasePermissions EffectivePermissions {
      get {
        if (instance.ListItem != null) {
          return instance.ListItem.EffectiveBasePermissions;
        }
        return instance.List.EffectiveBasePermissions;
      }
    }

    /// <summary>
    /// Determines whether the specified field is included in the data set.
    /// </summary>
    /// <param name="fieldName">Field name.</param>
    /// <returns>Returns *true* if the specified field is included in the data set.</returns>
    public override bool HasField(string fieldName) {
      try {
        SPField dummy = instance.List.Fields.GetFieldByInternalName(fieldName);
        return true;
      } catch (ArgumentException) {
        return false;
      }
    }

    /// <summary>
    /// Gets value from a boolean field.
    /// If the field does not contain value or the string representation of the value does not form a boolean value, default value is returned.
    /// </summary>
    /// <param name="fieldName">Field name.</param>
    /// <returns>Value in the specified field.</returns>
    public override bool GetBoolean(string fieldName) {
      object value = this[fieldName];
      return true.Equals(value) || "1".Equals(value);
    }

    public override void SetBoolean(string fieldName, bool value) {
      this[fieldName] = value ? "1" : "0";
    }

    public override void SetDateTime(string fieldName, DateTime? value) {
      if (value.HasValue) {
        this[fieldName] = SPUtility.CreateISO8601DateTimeFromSystemDateTime(value.Value.ToUniversalTime());
      } else {
        this[fieldName] = String.Empty;
      }
    }

    public override void SetGuid(string fieldName, Guid value) {
      this[fieldName] = value.ToString("B");
    }

    public override void SetInteger(string fieldName, int value) {
      this[fieldName] = value.ToString();
    }

    public override void SetNumber(string fieldName, double value) {
      this[fieldName] = value.ToString();
    }

    public override void SetTaxonomy(string fieldName, Term value) {
      this[fieldName] = value.EnsureWssId(this.Site, fieldName.Equals("TaxKeyword"));
    }

    public override void SetUrlFieldValue(string fieldName, SPFieldUrlValue value) {
      this[fieldName] = value.ToString();
    }
  }
}
