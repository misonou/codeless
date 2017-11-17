using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codeless.SharePoint {
  /// <summary>
  /// Exposes properties and methods related to a parameter in a CAML expression.
  /// </summary>
  public interface ICamlParameterBinding {
    /// <summary>
    /// Gets a boolean value indicating whether this instance binds to any given arguments.
    /// </summary>
    bool IsParameter { get; }
    /// <summary>
    /// Gets the name of this parameter. <see cref="CamlParameterName.NoBinding"/> is returned if this instance does not bind to any given arguments.
    /// </summary>
    CamlParameterName ParameterName { get; }
    /// <summary>
    /// Gets the value type this parameter representing.
    /// </summary>
    CamlValueType ValueType { get; }
    /// <summary>
    /// Binds a single value and returns a string representation of the value from a collection of parameter values.
    /// </summary>
    /// <param name="bindings"></param>
    /// <returns></returns>
    string Bind(Hashtable bindings);
    /// <summary>
    /// Bings a list of values and returns a string representation of the values from a collection of parameter values.
    /// </summary>
    /// <param name="bindings"></param>
    /// <returns></returns>
    IEnumerable<string> BindCollection(Hashtable bindings);
  }
}
