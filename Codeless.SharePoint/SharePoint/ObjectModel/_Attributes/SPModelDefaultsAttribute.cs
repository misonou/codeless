using System;

namespace Codeless.SharePoint.ObjectModel {
  /// <summary>
  /// Defines a default column group and content type group for model class located in an assembly.
  /// </summary>
  [AttributeUsage(AttributeTargets.Assembly)]
  public sealed class SPModelDefaultsAttribute : Attribute {
    /// <summary>
    /// Gets or sets a default column group name.
    /// </summary>
    public string DefaultFieldGroup { get; set; }

    /// <summary>
    /// Gets or sets a default content type group name.
    /// </summary>
    public string DefaultContentTypeGroup { get; set; }
  }
}
