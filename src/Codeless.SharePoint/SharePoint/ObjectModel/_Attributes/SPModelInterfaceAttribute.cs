using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Codeless.SharePoint.ObjectModel {
  /// <summary>
  /// Specifies behaviors on interfaces that are used with <see cref="SPModel"/> classes.
  /// </summary>
  [AttributeUsage(AttributeTargets.Interface)]
  public sealed class SPModelInterfaceAttribute : Attribute {
    /// <summary>
    /// Gets or sets the type of event handler to be instantiated to receiver events of items implementing the interface.
    /// </summary>
    public Type EventHandlerType { get; set; }
  }
}
