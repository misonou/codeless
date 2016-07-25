using Microsoft.SharePoint;

namespace Codeless.SharePoint.Publishing {
  /// <summary>
  /// Provides SEO meta data from the specified list item.
  /// </summary>
  public class SeoMetaListItem : ISeoMetaProvider {
    private readonly SPListItem listItem;

    /// <summary>
    /// Creates an instance of the <see cref="SeoMetaListItem"/> with the specified list item.
    /// </summary>
    /// <param name="listItem"></param>
    public SeoMetaListItem(SPListItem listItem) {
      this.listItem = listItem;
    }

    /// <summary>
    /// Gets SEO-friendly title. For example to be used to specify in og:title tag.
    /// </summary>
    public string Title {
      get { return listItem.Title; }
    }

    /// <summary>
    /// Gets SEO-friendly description. For example to be used to specify in og:description or "description" meta tag.
    /// </summary>
    public string Description {
      get { return (string)listItem.Properties["SeoMetaDescription"]; }
    }

    /// <summary>
    /// Gets SEO-friendly keyword list. For example to be used to specify in "keywords" meta tag.
    /// </summary>
    public string Keywords {
      get { return (string)listItem.Properties["SeoKeywords"]; }
    }

    string ISeoMetaProvider.Image {
      get { return null; }
    }
  }
}
