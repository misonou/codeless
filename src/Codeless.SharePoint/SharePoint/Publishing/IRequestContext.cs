using Microsoft.SharePoint.Publishing.Navigation;
using Microsoft.SharePoint.Utilities;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace Codeless.SharePoint.Publishing {
  /// <summary>
  /// Determines what should be displayed in the requesting catalog page based on the request URL.
  /// </summary>
  public enum CatalogPageMode {
    /// <summary>
    /// Indicates that current page is not a catalog page.
    /// </summary>
    Invalid,
    /// <summary>
    /// Indicates that current page should display a content listing.
    /// </summary>
    Listing,
    /// <summary>
    /// Indicates that current page should display the resolved item returned by <see cref="IRequestContext.ContentItem"/>
    /// </summary>
    Item,
    /// <summary>
    /// Indicates that current page should display an error message showing no items were found the request.
    /// </summary>
    ItemNotFound
  }

  /// <summary>
  /// Gets information on the current HTTP request using managed navigation.
  /// </summary>
  public interface IRequestContext {
    /// <summary>
    /// Gets a server-relative friendly URL for the current HTTP request.
    /// In contrast to <see cref="SPUtility.OriginalServerRelativeRequestPath"/> where it returns the resolved URL to the physical page referred by the friendly URL.
    /// </summary>
    string ServerRelativeUrl { get; }

    /// <summary>
    /// Gets a server-relative friendly URL for the current HTTP request with query string appended.
    /// In contrast to <see cref="SPUtility.OriginalServerRelativeRequestPath"/> where it returns the resolved URL to the physical page referred by the friendly URL.
    /// </summary>
    string ServerRelativeUrlWithQuery { get; }

    /// <summary>
    /// Gets a variation-relative friendly URL for the current HTTP request.
    /// See <see cref="ServerRelativeUrl"/> for details.
    /// </summary>
    string VariationRelativeUrl { get; }

    /// <summary>
    /// Gets a variation-relative friendly URL for the current HTTP request with query string appended.
    /// See <see cref="ServerRelativeUrl"/> for details.
    /// </summary>
    string VariationRelativeUrlWithQuery { get; }

    /// <summary>
    /// See <see cref="TaxonomyNavigationContext.CatalogUrlSegments"/>.
    /// </summary>
    IList<string> CatalogUrlSegments { get; }

    /// <summary>
    /// Gets a name-value collection for the query string.
    /// </summary>
    NameValueCollection Query { get; }

    /// <summary>
    /// Gets the navigation term or term set associated with the requested page.
    /// </summary>
    NavigationTermSetItem NavigationTermSetItem { get; }

    /// <summary>
    /// Gets the navigation term associated with the requested page.
    /// </summary>
    NavigationTerm NavigationTerm { get; }

    /// <summary>
    /// Gets the object representing the page or resolved catalog item if appropriate.
    /// </summary>
    object ContentItem { get; }

    /// <summary>
    /// Gets the object representing the catalog page if appropriate.
    /// </summary>
    object CatalogPageItem { get; }

    /// <summary>
    /// Gets a collection of objects to be listed on the catalog page.
    /// </summary>
    IReadOnlyCollection<object> CatalogItems { get; }

    /// <summary>
    /// Gets the resolved display mode of the catalog page.
    /// </summary>
    CatalogPageMode CatalogPageMode { get; }

    /// <summary>
    /// Gets the SEO meta data associated with the resolved content.
    /// </summary>
    SeoMetaWithFallback SeoMeta { get; }

    /// <summary>
    /// Gets the variation information associated with the current HTTP request.
    /// </summary>
    VariationContext Variation { get; }
  }
}
