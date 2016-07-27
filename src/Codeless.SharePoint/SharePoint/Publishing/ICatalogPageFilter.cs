using System.Collections.Generic;

namespace Codeless.SharePoint.Publishing {
  /// <summary>
  /// Provides mechanism to resolve catalog item by the specified catalog URL segments.
  /// </summary>
  public interface ICatalogPageFilter {
    /// <summary>
    /// Validates if the specified catalog URL segments matches the intended format.
    /// </summary>
    /// <param name="segments"></param>
    /// <returns></returns>
    bool Validate(IList<string> segments);

    /// <summary>
    /// Resolves the catalog item represented by the specified catalog URL segments.
    /// If there is no catalog item matched, *null* should be returned.
    /// </summary>
    /// <param name="segments">Catalog URL segments.</param>
    /// <returns>The resolved catalog item.</returns>
    object Execute(IList<string> segments);
  }
}
