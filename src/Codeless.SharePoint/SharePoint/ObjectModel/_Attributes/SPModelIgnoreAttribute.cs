using System;

namespace Codeless.SharePoint.ObjectModel {
  /// <summary>
  /// Marks the attributed interface to be ignored in provisioning.
  /// </summary>
  [AttributeUsage(AttributeTargets.Interface)]
  public sealed class SPModelIgnoreAttribute : Attribute { }
}
