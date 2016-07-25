using Microsoft.SharePoint;
using System.Web.Caching;

namespace Codeless.SharePoint {
  /// <summary>
  /// Provides a mechanism to persist site configurations.
  /// </summary>
  public interface ISiteConfigProvider {
    /// <summary>
    /// Initializes the provider with the specified site collection.
    /// When implemented, provider should mark itself associated to the given site collection.
    /// </summary>
    /// <param name="site">Site collection object.</param>
    void Initialize(SPSite site);
    /// <summary>
    /// Gets a <see cref="CacheDependency"/> object for site configuration cache to flush entries associated with the site collection.
    /// </summary>
    /// <returns>A <see cref="CacheDependency"/> object.</returns>
    CacheDependency GetCacheDependency();
    /// <summary>
    /// Gets a configuration entry associated with the site collection with the identification key.
    /// hen implemented, if such entry is not found, *NULL* should be returned.
    /// </summary>
    /// <returns>A configuration entry.</returns>
    ISiteConfigEntry GetEntry(string key);
    /// <summary>
    /// Creates an entry associated with the site collection.
    /// When implemented, provider should take the <see cref="ISiteConfigEntry.Key"/> as the identification key.
    /// </summary>
    /// <param name="entry">A configuration entry.</param>
    void CreateEntry(ISiteConfigEntry entry);
    /// <summary>
    /// Updates an entry associated with the site collection.
    /// When implemented, provider should take the <see cref="ISiteConfigEntry.Key"/> as the identification key.
    /// </summary>
    /// <param name="entry">A configuration entry.</param>
    void UpdateEntry(ISiteConfigEntry entry);
    /// <summary>
    /// Commits changes.
    /// When implemented, provider should persist changes made by <see cref="CreateEntry"/> and <see cref="UpdateEntry"/>.
    /// </summary>
    void CommitChanges();
  }
}
