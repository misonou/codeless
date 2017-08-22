using Microsoft.SharePoint;
using System;

namespace Codeless.SharePoint.ObjectModel {
  /// <summary>
  /// Defines the meta-data of the list item associated with an <see cref="SPModel"/> instance.
  /// </summary>
  [SPModelIgnore]
  public interface ISPModelMetaData {
    /// <summary>
    /// Gets the list item ID of the list item associated with an <see cref="SPModel"/> instance.
    /// </summary>
    int ID { get; }
    /// <summary>
    /// Gets the unique ID of the list item associated with an <see cref="SPModel"/> instance.
    /// </summary>
    Guid UniqueId { get; }
    /// <summary>
    /// Gets the server-relative path of the list item associated with an <see cref="SPModel"/> instance, without starting slash.
    /// </summary>
    [Obsolete("Use ServerRelativeUrl instead which has a more comprehensable property name.")]
    string FileRef { get; }
    /// <summary>
    /// Gets the server-relative path of the list item associated with an <see cref="SPModel"/> instance, with starting slash.
    /// </summary>
    string ServerRelativeUrl { get; }
    /// <summary>
    /// Gets the parent site collection ID of the list item associated with an <see cref="SPModel"/> instance.
    /// </summary>
    Guid SiteId { get; }
    /// <summary>
    /// Gets the parent site ID of the list item associated with an <see cref="SPModel"/> instance.
    /// </summary>
    Guid WebId { get; }
    /// <summary>
    /// Gets the parent list ID of the list item associated with an <see cref="SPModel"/> instance.
    /// </summary>
    Guid ListId { get; }
    /// <summary>
    /// Gets the filename of the list item associated with an <see cref="SPModel"/> instance.
    /// </summary>
    [Obsolete("Use Filename instead which has a more comprehensable property name.")]
    string FileLeafRef { get; }
    /// <summary>
    /// Gets the filename of the list item associated with an <see cref="SPModel"/> instance.
    /// </summary>
    string Filename { get; }
    /// <summary>
    /// Gets the last modified time of the list item associated with an <see cref="SPModel"/> instance.
    /// </summary>
    DateTime LastModified { get; }
    /// <summary>
    /// Gets the permission granted to the user that fetched the list item associated with an <see cref="SPModel"/> instance.
    /// </summary>
    SPBasePermissions EffectivePermissions { get; }
    /// <summary>
    /// Gets the content type ID of the list item associated with an <see cref="SPModel"/> instance.
    /// </summary>
    SPContentTypeId ContentTypeId { get; }
    /// <summary>
    /// Gets the version number of the list item associated with an <see cref="SPModel"/> instance.
    /// </summary>
    SPItemVersion Version { get; }
    /// <summary>
    /// Gets the user ID who has checked out the list item associated with an <see cref="SPModel"/> instance.
    /// </summary>
    int CheckOutUserID { get; }
    /// <summary>
    /// Gets a highlighted summary returned from Office search service.
    /// </summary>
    string HitHighlightSummary { get; }
  }
}
