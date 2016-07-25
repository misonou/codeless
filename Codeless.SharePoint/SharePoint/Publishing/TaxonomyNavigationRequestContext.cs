using Codeless.SharePoint.ObjectModel;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Publishing.Navigation;
using Microsoft.SharePoint.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Web;

namespace Codeless.SharePoint.Publishing {
  internal class TaxonomyNavigationRequestContext : IRequestContext {
    private readonly SeoMetaWithFallback seoMeta = new SeoMetaWithFallback();
    private readonly List<object> catalogItems = new List<object>();
    private readonly TaxonomyNavigationContext context;
    private readonly VariationContext variation;
    private readonly NameValueCollection query;
    private readonly NavigationTermSetItem navigationTermSetItem;
    private readonly NavigationTerm navigationTerm;
    private readonly string serverRelativeRequestUrl;
    private readonly string variationRelativeRequestUrl;
    private readonly string queryString;
    private object currentItem;
    private object catalogPageItem;
    private CatalogPageMode catalogPageMode;
    private ICatalogPageFilter catalogPageFilter;

    public TaxonomyNavigationRequestContext() {
      this.context = TaxonomyNavigationContext.Current;
      this.variation = VariationContext.Current;
      if (context.HasNavigationContext) {
        this.navigationTerm = context.NavigationTerm;
      } else {
        using (new SPSecurity.GrantAdditionalPermissionsInScope(SPBasePermissions.FullMask)) {
          SPWeb currentWeb = SPContext.Current.Web;
          string url = TaxonomyNavigationHelper.ResolveFriendlyUrl(currentWeb, SPUrlUtility.CombineUrl(currentWeb.ServerRelativeUrl, currentWeb.RootFolder.WelcomePage));
          TaxonomyNavigationHelper.TryGetNavigationTerm(url, out this.navigationTerm, out url);
        }
      }
      if (this.navigationTerm == null) {
        NavigationTermSet termSet;
        TaxonomyNavigationHelper.IsRequestingNavigationTermSet(out termSet);
        this.navigationTermSetItem = termSet;
      } else {
        this.navigationTermSetItem = navigationTerm;
      }

      if (context.HasFriendlyUrl || context.HasCatalogUrl) {
        this.serverRelativeRequestUrl = context.ResolvedDisplayUrl;
      } else if (navigationTermSetItem != null) {
        this.serverRelativeRequestUrl = navigationTermSetItem.GetResolvedDisplayUrl(null);
      } else {
        this.serverRelativeRequestUrl = SPUtility.OriginalServerRelativeRequestPath;
      }
      this.variationRelativeRequestUrl = PublishingWebHelper.TrimVariationFromPath(serverRelativeRequestUrl);

      this.query = HttpUtility.ParseQueryString(HttpContext.Current.Request.Url.Query);
      query.Remove(null);
      query.Remove("TermStoreId");
      query.Remove("TermSetId");
      query.Remove("TermId");
      this.queryString = query.AllKeys.Length > 0 ? "?" + query : String.Empty;

      SPListItem listItem = SPContext.Current.ListItem;
      if (listItem != null) {
        this.currentItem = SPModel.TryCreate(listItem);
      }
      if (currentItem is ICatalogPage) {
        ICatalogPageFilter filter = CommonHelper.AccessNotNull(((ICatalogPage)currentItem).Filter, "Filter");
        SetCatalogPageFilter(filter);
      }
      if (currentItem is ISeoMetaProvider) {
        seoMeta.Add((ISeoMetaProvider)currentItem);
      }
      seoMeta.Add(new SeoMetaListItem(listItem));
      for (NavigationTerm t = navigationTerm; t != null; t = t.Parent) {
        seoMeta.Add(new SeoMetaNavigationTerm(listItem.Web, t));
      }
    }

    public void SetCatalogPageFilter(ICatalogPageFilter filter) {
      if (this.catalogPageFilter != null) {
        throw new InvalidOperationException("Catalog page filter is already defined.");
      }
      this.catalogPageFilter = filter;
      this.catalogPageItem = currentItem;
      if (context.HasCatalogUrl && filter.Validate(context.CatalogUrlSegments)) {
        object resolvedItem = filter.Execute(context.CatalogUrlSegments);
        if (resolvedItem == null) {
          this.catalogPageMode = CatalogPageMode.ItemNotFound;
        } else if (resolvedItem.GetType().GetEnumeratedType() != null) {
          catalogItems.AddRange(((IEnumerable)resolvedItem).OfType<object>());
          this.catalogPageMode = CatalogPageMode.Listing;
        } else {
          this.currentItem = resolvedItem;
          this.catalogPageMode = CatalogPageMode.Item;
          if (this.currentItem is ISeoMetaProvider) {
            seoMeta.Insert(0, (ISeoMetaProvider)currentItem);
          }
        }
      } else {
        this.catalogPageMode = CatalogPageMode.Listing;
      }
    }

    #region IRequestContext
    string IRequestContext.ServerRelativeUrl {
      get { return serverRelativeRequestUrl; }
    }

    string IRequestContext.ServerRelativeUrlWithQuery {
      get { return serverRelativeRequestUrl + queryString; }
    }

    string IRequestContext.VariationRelativeUrl {
      get { return variationRelativeRequestUrl; }
    }

    string IRequestContext.VariationRelativeUrlWithQuery {
      get { return variationRelativeRequestUrl + queryString; }
    }

    IList<string> IRequestContext.CatalogUrlSegments {
      get { return context.CatalogUrlSegments; }
    }

    NavigationTermSetItem IRequestContext.NavigationTermSetItem {
      get { return navigationTermSetItem; }
    }

    NavigationTerm IRequestContext.NavigationTerm {
      get { return navigationTerm; }
    }

    object IRequestContext.ContentItem {
      get { return currentItem; }
    }

    object IRequestContext.CatalogPageItem {
      get { return catalogPageItem; }
    }

    IReadOnlyCollection<object> IRequestContext.CatalogItems {
      get { return catalogItems.AsReadOnly(); }
    }

    CatalogPageMode IRequestContext.CatalogPageMode {
      get { return catalogPageMode; }
    }

    NameValueCollection IRequestContext.Query {
      get {
        NameValueCollection collection = HttpUtility.ParseQueryString("?");
        collection.Add(query);
        return collection;
      }
    }

    SeoMetaWithFallback IRequestContext.SeoMeta {
      get { return seoMeta; }
    }

    VariationContext IRequestContext.Variation {
      get { return variation; }
    }
    #endregion
  }
}
