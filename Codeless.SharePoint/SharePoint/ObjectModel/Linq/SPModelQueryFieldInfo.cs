using Codeless.SharePoint.Internal;
using Microsoft.SharePoint;

namespace Codeless.SharePoint.ObjectModel.Linq {
  internal struct SPModelQueryFieldInfo {
    public string FieldRef { get; private set; }
    public SPFieldType FieldType { get; private set; }
    public string FieldTypeAsString { get; private set; }
    public bool IncludeTimeValue { get; private set; }

    public static readonly SPModelQueryFieldInfo ID = new SPModelQueryFieldInfo {
      FieldRef = SPBuiltInFieldName.ID,
      FieldType = SPFieldType.Counter,
      FieldTypeAsString = "Counter"
    };

    public static readonly SPModelQueryFieldInfo UniqueId = new SPModelQueryFieldInfo {
      FieldRef = SPBuiltInFieldName.UniqueId,
      FieldType = SPFieldType.Guid,
      FieldTypeAsString = "Guid"
    };

    public static readonly SPModelQueryFieldInfo FileRef = new SPModelQueryFieldInfo {
      FieldRef = SPBuiltInFieldName.FileRef,
      FieldType = SPFieldType.URL,
      FieldTypeAsString = "URL"
    };

    public static readonly SPModelQueryFieldInfo FileLeafRef = new SPModelQueryFieldInfo {
      FieldRef = SPBuiltInFieldName.FileLeafRef,
      FieldType = SPFieldType.Text,
      FieldTypeAsString = "Text"
    };

    public static readonly SPModelQueryFieldInfo LastModified = new SPModelQueryFieldInfo {
      FieldRef = SPBuiltInFieldName.Modified,
      FieldType = SPFieldType.DateTime,
      FieldTypeAsString = "DateTime"
    };

    public static readonly SPModelQueryFieldInfo CheckOutUserID = new SPModelQueryFieldInfo {
      FieldRef = SPBuiltInFieldName.CheckoutUser,
      FieldType = SPFieldType.User,
      FieldTypeAsString = "User"
    };

    public SPModelQueryFieldInfo(SPSite site, SPModelFieldAssociation association)
      : this() {
      CommonHelper.ConfirmNotNull(site, "site");
      CommonHelper.AccessNotNull(association.Attribute, "Attribute");

      FieldRef = association.Attribute.ListFieldInternalName;
      if (association.Attribute is SPBuiltInFieldAttribute) {
        SPFieldType fieldType;
        if (KnownFields.FieldTypeDictionary.TryGetValue(association.Attribute.InternalName, out fieldType)) {
          FieldType = fieldType;
          FieldTypeAsString = fieldType.ToString();
          if (fieldType == SPFieldType.DateTime) {
            IncludeTimeValue = !KnownFields.DateOnlyFields.Contains(association.Attribute.InternalName);
          }
        } else {
          SPField field = site.RootWeb.Fields.GetFieldByInternalName(association.Attribute.InternalName);
          FieldType = field.Type;
          FieldTypeAsString = field.TypeAsString;
          if (field.Type == SPFieldType.DateTime) {
            IncludeTimeValue = ((SPFieldDateTime)field).DisplayFormat == SPDateTimeFieldFormatType.DateTime;
          }
        }
      } else {
        FieldType = association.Attribute.Type;
        FieldTypeAsString = association.Attribute.TypeAsString;
        if (association.Attribute.Type == SPFieldType.DateTime) {
          IncludeTimeValue = ((SPDateTimeFieldAttribute)association.Attribute).DisplayFormat == SPDateTimeFieldFormatType.DateTime;
        }
      }
    }
  }
}
