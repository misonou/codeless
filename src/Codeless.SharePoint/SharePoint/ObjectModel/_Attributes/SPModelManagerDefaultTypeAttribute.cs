using System;

namespace Codeless.SharePoint.ObjectModel {
  /// <summary>
  /// Defines a default type of model manager to be instantiated using <see cref="SPModel.GetDefaultManager"/> or <see cref="SPModelManager{T}.Current"/>.
  /// </summary>
  [AttributeUsage(AttributeTargets.Class)]
  public class SPModelManagerDefaultTypeAttribute : Attribute {
    /// <summary>
    /// Creates an instance of the <see cref="SPModelManagerDefaultTypeAttribute"/> class with the specified type.
    /// </summary>
    /// <param name="type"></param>
    public SPModelManagerDefaultTypeAttribute(Type type) {
      CommonHelper.ConfirmNotNull(type, "type");
      this.DefaultType = type;
    }

    /// <summary>
    /// Gets the default type of model manager attributed to a model class.
    /// </summary>
    public Type DefaultType { get; private set; }
  }
}
