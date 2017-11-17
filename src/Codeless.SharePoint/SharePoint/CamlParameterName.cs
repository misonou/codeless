using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codeless.SharePoint {
  /// <summary>
  /// Indicates the name of a parameter which its value can be binded after.
  /// </summary>
  public struct CamlParameterName {
    internal static readonly CamlParameterName NoBinding = default(CamlParameterName);

    internal readonly string Value;

    /// <summary>
    /// Creates an instance of the <see cref="CamlParameterName"/> class.
    /// </summary>
    /// <param name="value"></param>
    public CamlParameterName(string value) {
      CommonHelper.ConfirmNotNull(value, "value");
      this.Value = value;
    }

    /// <summary>
    /// Implicitly converts the name of a parameter specified by this instance to a string representation.
    /// </summary>
    /// <param name="p"></param>
    /// <returns></returns>
    public static implicit operator string(CamlParameterName p) {
      return p.Value;
    }
  }
}
