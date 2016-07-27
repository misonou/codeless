using System;

namespace Codeless.SharePoint {
  /// <summary>
  /// Specifies a custom site configuration provider for <see cref="SiteConfig{T}"/>.
  /// </summary>
  [AttributeUsage(AttributeTargets.Class)]
  public sealed class SiteConfigProviderAttribute : Attribute {
    /// <summary>
    /// Initializes a new instance of the <see cref="SiteConfigProviderAttribute"/> class with the specified <see cref="Type"/> object.
    /// </summary>
    /// <param name="providerType"></param>
    public SiteConfigProviderAttribute(Type providerType) {
      CommonHelper.ConfirmNotNull(providerType, "providerType");
      this.ProviderType = providerType;
    }

    /// <summary>
    /// Gets the provider type.
    /// </summary>
    public Type ProviderType { get; private set; }
  }
}
