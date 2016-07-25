using System.Security;

namespace Codeless.SharePoint {
  /// <summary>
  /// Defines a configuration entry.
  /// </summary>
  public interface ISiteConfigEntry {
    /// <summary>
    /// Returns the key of a configuration entry.
    /// </summary>
    string Key { get; }
    /// <summary>
    /// Returns the value of a configuration entry.
    /// </summary>
    string Value { get; set; }
    /// <summary>
    /// Returns the category of a configuration entry.
    /// </summary>
    string Category { get; set; }
    /// <summary>
    /// Returns the description of a configuration entry.
    /// </summary>
    string Description { get; set; }
    /// <summary>
    /// Returns whether the value stored references default value from code.
    /// If this flag is set to *true*, value will be updated when the default value from code is changed.
    /// </summary>
    bool UseDefaultValue { get; }
  }

  /// <summary>
  /// Defines a configuration entry where its value is stored in a secured manner.
  /// </summary>
  public interface ISecureSiteConfigEntry : ISiteConfigEntry {
    /// <summary>
    /// Returns the secured value of a configuration entry.
    /// </summary>
    SecureString SecureValue { get; set; }
  }
}
