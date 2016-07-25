using Microsoft.SharePoint;
using Microsoft.SharePoint.Publishing.Navigation;
using Microsoft.SharePoint.Taxonomy;
using Microsoft.SharePoint.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Web;

namespace Codeless.SharePoint.Publishing {
  /// <summary>
  /// Provides helper methods for managed navigation.
  /// </summary>
  public static class TaxonomyNavigationHelper {
    private static readonly MethodInfo GetFriendlyUrlsForTargetUrl = typeof(TaxonomyNavigation).GetMethod("GetFriendlyUrlsForTargetUrl", true);

    /// <summary>
    /// Gets information of the current HTTP request using managed navigation.
    /// </summary>
    public static IRequestContext RequestContext {
      get { return CommonHelper.HttpContextSingleton<TaxonomyNavigationRequestContext>(); }
    }

    /// <summary>
    /// Sets the catalog page filter explicitly to create context for catalog page.
    /// </summary>
    /// <param name="filter"></param>
    public static void SetCatalogPageFilter(ICatalogPageFilter filter) {
      CommonHelper.ConfirmNotNull(filter, "filter");
      TaxonomyNavigationRequestContext instance = CommonHelper.HttpContextSingleton<TaxonomyNavigationRequestContext>();
      instance.SetCatalogPageFilter(filter);
    }

    /// <summary>
    /// Gets a server-relative friendly URL for the current HTTP request.
    /// In contrast to <see cref="SPUtility.OriginalServerRelativeRequestPath"/> where it returns the resolved URL to the physical page referred by the friendly URL.
    /// </summary>
    [Obsolete("Use TaxonomyNavigationHelper.RequestContext instead.")]
    public static string ServerRelativeFriendlyRequestUrl {
      get { return RequestContext.ServerRelativeUrl; }
    }

    /// <summary>
    /// Gets a variation-relative friendly URL for the current HTTP request.
    /// See <see cref="ServerRelativeFriendlyRequestUrl"/> for details.
    /// </summary>
    [Obsolete("Use TaxonomyNavigationHelper.RequestContext instead.")]
    public static string VariationRelativeFriendlyRequestUrl {
      get { return RequestContext.VariationRelativeUrl; }
    }

    /// <summary>
    /// Gets a server-relative friendly URL for the current HTTP request with query string appended.
    /// In contrast to <see cref="SPUtility.OriginalServerRelativeRequestPath"/> where it returns the resolved URL to the physical page referred by the friendly URL.
    /// </summary>
    [Obsolete("Use TaxonomyNavigationHelper.RequestContext instead.")]
    public static string ServerRelativeFriendlyRequestUrlWithQuery {
      get { return RequestContext.ServerRelativeUrlWithQuery; }
    }

    /// <summary>
    /// Gets a variation-relative friendly URL for the current HTTP request with query string appended.
    /// See <see cref="ServerRelativeFriendlyRequestUrl"/> for details.
    /// </summary>
    [Obsolete("Use TaxonomyNavigationHelper.RequestContext instead.")]
    public static string VariationRelativeFriendlyRequestUrlWithQuery {
      get { return RequestContext.VariationRelativeUrlWithQuery; }
    }

    /// <summary>
    /// Gets the navigation term or term set associated with the current HTTP request.
    /// </summary>
    [Obsolete("Use TaxonomyNavigationHelper.RequestContext instead.")]
    public static NavigationTermSetItem CurrentNavigationTermSetItem {
      get { return RequestContext.NavigationTermSetItem; }
    }

    /// <summary>
    /// Gets the navigation term associated with the current HTTP request.
    /// </summary>
    [Obsolete("Use TaxonomyNavigationHelper.RequestContext instead.")]
    public static NavigationTerm CurrentNavigationTerm {
      get { return RequestContext.NavigationTerm; }
    }
    
    /// <summary>
    /// Gets a <see cref="NavigationTerm"/> object by the specified unique ID.
    /// </summary>
    /// <param name="termId">Term unique identifier.</param>
    /// <returns>A <see cref="NavigationTerm"/> object.</returns>
    public static NavigationTerm GetNavigationTerm(Guid termId) {
      if (SPContext.Current != null && termId != Guid.Empty) {
        SPWeb currentWeb = SPContext.Current.Web;
        NavigationTermSet navigationTermSet = TaxonomyNavigation.GetTermSetForWeb(currentWeb, StandardNavigationProviderNames.GlobalNavigationTaxonomyProvider, true);
        if (navigationTermSet != null) {
          TaxonomySession session = new TaxonomySession(currentWeb, false);
          Term term = navigationTermSet.GetTaxonomyTermSet(session).GetTerm(termId);
          if (term != null) {
            using (SPWeb navigationRootWeb = currentWeb.Site.OpenWeb(navigationTermSet.GetResolvedDisplayUrl(null))) {
              return NavigationTerm.GetAsResolvedByWeb(term, navigationRootWeb, StandardNavigationProviderNames.GlobalNavigationTaxonomyProvider);
            }
          }
        }
      }
      return null;
    }

    /// <summary>
    /// Gets a friendly URL associated to the physical page referred by the specified server-relative URL.
    /// </summary>
    /// <param name="web">Site object where the physical page relies in.</param>
    /// <param name="serverRelativeUrl">Server-relative URL of the physical page.</param>
    /// <returns>A friendly URL if any; or the supplied URL if there is no friendly URL is associated with the page or the supplied URL does not exist.</returns>
    public static string ResolveFriendlyUrl(SPWeb web, string serverRelativeUrl) {
      CommonHelper.ConfirmNotNull(web, "web");
      CommonHelper.ConfirmNotNull(serverRelativeUrl, "serverRelativeUrl");
      if (GetFriendlyUrlsForTargetUrl == null) {
        throw new MissingMethodException("GetFriendlyUrlsForTargetUrl");
      }
      IList<NavigationTerm> matchedTerms = GetFriendlyUrlsForTargetUrl.Invoke<IList<NavigationTerm>>(null, web, serverRelativeUrl, true);
      if (matchedTerms.Count > 0) {
        return matchedTerms[0].GetResolvedDisplayUrl(null);
      }
      return serverRelativeUrl;
    }

    /// <summary>
    /// Determines whether the current HTTP request can be resolved to a <see cref="NavigationTermSet"/> object.
    /// When client is visiting the welcome page of the site where it is set to have a unique managed navigation, 
    /// <see cref="TaxonomyNavigationContext.HasNavigationContext"/> returns *false* even though this page can be referred by the friendly URL resolved by the <see cref="NavigationTermSet"/> object.
    /// </summary>
    /// <param name="navigationTermSet">The resolved <see cref="NavigationTermSet"/> object; otherwise *null*.</param>
    /// <returns>*true* if the current HTTP request can be resolved to a <see cref="NavigationTermSet"/> object.</returns>
    public static bool IsRequestingNavigationTermSet(out NavigationTermSet navigationTermSet) {
      if (SPContext.Current != null) {
        SPWeb currentWeb = SPContext.Current.Web;
        navigationTermSet = TaxonomyNavigation.GetTermSetForWeb(currentWeb, StandardNavigationProviderNames.GlobalNavigationTaxonomyProvider, true);
        using (new SPSecurity.GrantAdditionalPermissionsInScope(SPBasePermissions.FullMask)) {
          if (navigationTermSet != null && navigationTermSet.GetResolvedDisplayUrl(null) == currentWeb.ServerRelativeUrl && SPUrlUtility.CombineUrl(currentWeb.ServerRelativeUrl, currentWeb.RootFolder.WelcomePage) == SPUtility.OriginalServerRelativeRequestPath) {
            return true;
          }
        }
      }
      navigationTermSet = null;
      return false;
    }

    /// <summary>
    /// Resolved a <see cref="NavigationTerm"/> object with the specified URL against the current site.
    /// If the specified URL resolves to a navigation term with catalog enabled, and there are remaining segments, 
    /// <paramref name="matchedUrl"/> will be set to a friendly URL resolved from the navigation term without the excess segments.
    /// </summary>
    /// <param name="inputUrl">Input URL.</param>
    /// <param name="navigationTerm">Resolved <see cref="NavigationTerm"/> object if any; otherwise *null*.</param>
    /// <param name="matchedUrl">Resolved URL for the <see cref="NavigationTerm"/> object if any; otherwise *null*.</param>
    /// <returns>*true* if the specified URL resolves to a <see cref="NavigationTerm"/> object.</returns>
    public static bool TryGetNavigationTerm(string inputUrl, out NavigationTerm navigationTerm, out string matchedUrl) {
      CommonHelper.ConfirmNotNull(inputUrl, "inputUrl");
      if (SPContext.Current != null && !String.IsNullOrEmpty(inputUrl) && inputUrl[0] == '/') {
        int pathEndPos = inputUrl.IndexOfAny(new[] { '?', '#' });
        if (pathEndPos > 0) {
          inputUrl = inputUrl.Substring(0, pathEndPos);
        }
        SPSite currentSite = SPContext.Current.Site;
        while (inputUrl.Length > 0) {
          string[] segments;
          if (TaxonomyNavigation.TryParseFriendlyUrl(currentSite, inputUrl, out navigationTerm, out segments)) {
            matchedUrl = inputUrl;
            return true;
          }
          inputUrl = inputUrl.Substring(0, inputUrl.LastIndexOf('/'));
        }
      }
      navigationTerm = null;
      matchedUrl = null;
      return false;
    }

    /// <summary>
    /// Ensures that the client is requesting the current page by the associated friendly URLs (if any).
    /// If the client is not requesting the associated friendly URLs, the client will be transfered to the default friendly URL by a redirect response.
    /// </summary>
    /// <param name="context">The instance of the <see cref="HttpContext"/> class representing the client.</param>
    public static void TransferToFriendlyUrl(HttpContext context) {
      CommonHelper.ConfirmNotNull(context, "context");
      if (SPContext.Current != null && !TaxonomyNavigationContext.Current.HasNavigationContext) {
        SPListItem currentItem = SPContext.Current.ListItem;
        if (currentItem != null) {
          string rawRequestPath = context.Request.RawUrl;
          int pathEndPos = rawRequestPath.IndexOfAny(new[] { '?', '#' });
          if (pathEndPos >= 0) {
            rawRequestPath = rawRequestPath.Substring(0, pathEndPos);
          }
          NavigationTermSetItem matchedTerm = TaxonomyNavigation.GetFriendlyUrlsForListItem(currentItem, true).FirstOrDefault();
          if (matchedTerm == null) {
            NavigationTermSet matchedTermSet;
            if (IsRequestingNavigationTermSet(out matchedTermSet)) {
              matchedTerm = matchedTermSet;
            }
          }
          if (matchedTerm != null) {
            string friendlyUrl = matchedTerm.GetResolvedDisplayUrl(null);
            if (!friendlyUrl.Equals(rawRequestPath, StringComparison.OrdinalIgnoreCase)) {
              if (pathEndPos >= 0) {
                NameValueCollection query = HttpUtility.ParseQueryString(context.Request.RawUrl.Substring(pathEndPos + 1));
                query.Remove(null);
                query.Remove("TermStoreId");
                query.Remove("TermSetId");
                query.Remove("TermId");
                friendlyUrl = String.Concat(friendlyUrl, "?", query.ToString());
              }
              SPUtility.Redirect(friendlyUrl, SPRedirectFlags.Default, context);
            }
          }
        }
      }
    }
  }
}
