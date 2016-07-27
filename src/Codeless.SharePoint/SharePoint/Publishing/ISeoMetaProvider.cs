namespace Codeless.SharePoint.Publishing {
  /// <summary>
  /// Provides SEO meta data.
  /// </summary>
  public interface ISeoMetaProvider {
    /// <summary>
    /// Gets SEO-friendly title. For example to be used to specify in og:title tag.
    /// </summary>
    string Title { get; }

    /// <summary>
    /// Gets SEO-friendly description. For example to be used to specify in og:description or "description" meta tag.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets SEO-friendly keyword list. For example to be used to specify in "keywords" meta tag.
    /// </summary>
    string Keywords { get; }

    /// <summary>
    /// Gets SEO-friendly image URL. For example to be used to specify in og:image tag.
    /// </summary>
    string Image { get; }
  }
}
