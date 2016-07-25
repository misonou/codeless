using System;
using System.Reflection;

namespace Codeless.SharePoint.ObjectModel {
  [AttributeUsage(AttributeTargets.Property)]
  public class SPModelQueryPropertyAttribute : Attribute {
    public SPModelQueryPropertyAttribute(Type type, string propertyName) {
      CommonHelper.ConfirmNotNull(type, "type");
      CommonHelper.ConfirmNotNull(propertyName, "propertyName");
      this.QueryProperty = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
    }

    public PropertyInfo QueryProperty { get; private set; }
  }
}
