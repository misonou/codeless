using Codeless.SharePoint.ObjectModel;

namespace Codeless.SharePoint.Publishing {
  /// <summary>
  /// Represents a catalog page when implemented by a descandant class of the <see cref="SPModel"/> class.
  /// This interface is consumed by the <see cref="TaxonomyNavigationHelper"/> class that computes information about
  /// the current HTTP request exposed by the <see cref="IRequestContext"/> interface.
  /// </summary>
  [SPModelIgnore]
  public interface ICatalogPage {
    /// <summary>
    /// Gets an object implementing the <see cref="ICatalogPageFilter"/> interface that resolves catalog item by the given catalog URL segments.
    /// </summary>
    ICatalogPageFilter Filter { get; }
  }
}
