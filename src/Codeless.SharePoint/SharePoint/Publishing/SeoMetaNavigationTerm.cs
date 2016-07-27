using Microsoft.SharePoint;
using Microsoft.SharePoint.Publishing.Navigation;
using Microsoft.SharePoint.Taxonomy;
using System;

namespace Codeless.SharePoint.Publishing {
  /// <summary>
  /// Provides SEO meta data from the specified managed navigation node.
  /// </summary>
  public class SeoMetaNavigationTerm : ISeoMetaProvider {
    private readonly Term term;

    /// <summary>
    /// Creates an instance of the <see cref="SeoMetaListItem"/> with the specified managed navigation node.
    /// </summary>
    /// <param name="contextWeb"></param>
    /// <param name="term"></param>
    public SeoMetaNavigationTerm(SPWeb contextWeb, NavigationTerm term) {
      TaxonomySession session = new TaxonomySession(contextWeb, false);
      this.term = term.GetTaxonomyTerm(session);
    }

    /// <summary>
    /// Gets SEO-friendly title. For example to be used to specify in og:title tag.
    /// </summary>
    public string Title {
      get { return GetLocalCustomProperty("_Sys_Seo_PropBrowserTitle"); }
    }

    /// <summary>
    /// Gets SEO-friendly description. For example to be used to specify in og:description or "description" meta tag.
    /// </summary>
    public string Description {
      get { return GetLocalCustomProperty("_Sys_Seo_PropDescription"); }
    }

    /// <summary>
    /// Gets SEO-friendly keyword list. For example to be used to specify in "keywords" meta tag.
    /// </summary>
    public string Keywords {
      get { return GetLocalCustomProperty("_Sys_Seo_PropKeywords"); }
    }

    string ISeoMetaProvider.Image {
      get { return null; }
    }

    private string GetLocalCustomProperty(string key) {
      string value;
      if (term.LocalCustomProperties.TryGetValue(key, out value)) {
        return value;
      }
      return String.Empty;
    }
  }
}
