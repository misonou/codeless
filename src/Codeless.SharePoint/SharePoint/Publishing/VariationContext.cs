using Microsoft.SharePoint;
using Microsoft.SharePoint.Publishing;
using System;
using System.Globalization;

namespace Codeless.SharePoint.Publishing {
  /// <summary>
  /// Provides information on the variation of current HTTP request.
  /// </summary>
  public sealed class VariationContext {
    /// <summary>
    /// Creates an instance of the <see cref="VariationContext"/> class with the specified SharePoint request context.
    /// </summary>
    /// <param name="context">SharePoint request context.</param>
    public VariationContext(SPContext context) :
      this(CommonHelper.ConfirmNotNull(context, "context").Web) { }

    /// <summary>
    /// Creates an instance of the <see cref="VariationContext"/> class with the specified site.
    /// </summary>
    /// <param name="web">Site object.</param>
    public VariationContext(SPWeb web) {
      CommonHelper.ConfirmNotNull(web, "web");
      try {
        using (new SPSecurity.SuppressAccessDeniedRedirectInScope()) {
          InitializeObject(web);
        }
      } catch (UnauthorizedAccessException) {
        web.WithElevatedPrivileges(InitializeObject);
      }
    }

    /// <summary>
    /// Gets a cached instance of the <see cref="VariationContext"/> class for the current HTTP request.
    /// </summary>
    public static VariationContext Current {
      get {
        if (SPContext.Current != null) {
          return CommonHelper.HttpContextSingleton(() => new VariationContext(SPContext.Current.Web));
        }
        return null;
      }
    }

    /// <summary>
    /// Indicates if the requested web belongs to a variation.
    /// </summary>
    public bool IsVariationWeb { get; private set; }

    /// <summary>
    /// Indicates if the variation of the requested web is the source variation.
    /// </summary>
    public bool IsSource { get; private set; }

    /// <summary>
    /// Gets the server-relative URL of the top web of the current variation.
    /// If the requested web does not belong to a variation, the server-relative URL of the root site is returned.
    /// </summary>
    public string TopWebServerRelativeUrl { get; private set; }

    /// <summary>
    /// Gets the current variation label, that is the URL segment that identify the variation.
    /// If the requested web does not belong to a variation, an empty string is returned.
    /// </summary>
    public string VariationLabel { get; private set; }

    /// <summary>
    /// Gets the name of publishing page library under the current variation.
    /// </summary>
    public string PagesListName { get; private set; }

    /// <summary>
    /// Gets the culture associated with the current variation.
    /// </summary>
    public CultureInfo Culture { get; private set; }

    private void InitializeObject(SPWeb web) {
      if (PublishingWeb.IsPublishingWeb(web)) {
        PublishingWeb currentWeb = PublishingWeb.GetPublishingWeb(web);
        VariationLabel currentLabel = GetVariationLabel(currentWeb);
        if (currentLabel != null) {
          this.IsVariationWeb = true;
          this.IsSource = currentLabel.IsSource;
          this.TopWebServerRelativeUrl = new Uri(currentLabel.TopWebUrl).AbsolutePath;
          this.VariationLabel = currentLabel.Title;
          this.PagesListName = currentWeb.PagesListName;
          int lcid;
          if (Int32.TryParse(currentLabel.Locale, out lcid)) {
            this.Culture = CultureInfo.GetCultureInfo(lcid);
          } else {
            this.Culture = CultureInfo.GetCultureInfo(currentLabel.Language);
          }
        }
      }
      if (!this.IsVariationWeb) {
        this.TopWebServerRelativeUrl = web.Site.ServerRelativeUrl;
        this.VariationLabel = String.Empty;
        this.PagesListName = "Pages";
        this.Culture = web.UICulture;
      }
    }

    private static VariationLabel GetVariationLabel(PublishingWeb web) {
      foreach (VariationLabel label in (new PublishingSite(web.Web.Site)).GetVariationLabels(false)) {
        string prefix = new Uri(label.TopWebUrl).AbsolutePath;
        if (web.Web.ServerRelativeUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            (web.Web.ServerRelativeUrl.Length == prefix.Length || web.Web.ServerRelativeUrl[prefix.Length] == '/')) {
          return label;
        }
      }
      return null;
    }
  }
}
