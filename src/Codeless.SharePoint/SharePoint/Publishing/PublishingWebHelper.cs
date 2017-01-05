using Microsoft.SharePoint;
using Microsoft.SharePoint.Publishing;
using Microsoft.SharePoint.Publishing.Fields;
using Microsoft.SharePoint.Publishing.WebControls;
using Microsoft.SharePoint.Utilities;
using Microsoft.SharePoint.WebPartPages;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Web.UI.WebControls.WebParts;
using System.Xml;

namespace Codeless.SharePoint.Publishing {
  /// <summary>
  /// Provides helper methods for publishing web feature.
  /// </summary>
  public static class PublishingWebHelper {
    private static readonly MethodInfo GetVariationLabelMethod = typeof(VariationLabel).GetMethod("GetVariationLabel", true, typeof(SPListItem));

    /// <summary>
    /// Gets all variation labels in a site collection. This method is safe when called in console.
    /// </summary>
    /// <param name="publishingSite">Site collection.</param>
    /// <param name="includePending">Whether to return pending variation labels.</param>
    /// <returns>A read-only collection of all variation labels.</returns>
    public static ReadOnlyCollection<VariationLabel> GetVariationLabels(this PublishingSite publishingSite, bool includePending) {
      if (GetVariationLabelMethod == null) {
        throw new MissingMethodException("GetVariationLabel");
      }
      List<VariationLabel> collection = new List<VariationLabel>();
      publishingSite.Site.WithElevatedPrivileges(elevatedSite => {
        string variationsListId = (string)elevatedSite.RootWeb.AllProperties["_VarLabelsListId"];
        if (!String.IsNullOrEmpty(variationsListId)) {
          SPList variationsList = elevatedSite.RootWeb.Lists[new Guid(variationsListId)];
          CamlExpression queryExpr = Caml.IsNotNull(SPBuiltInFieldName.Title);
          if (!includePending) {
            queryExpr &= Caml.IsNotNull("Top_x0020_Web_x0020_URL");
          }
          SPQuery query = new SPQuery { Query = queryExpr.ToString() };
          foreach (SPListItem listItem in variationsList.GetItems(query)) {
            VariationLabel label = GetVariationLabelMethod.Invoke<VariationLabel>(null, listItem);
            collection.Add(label);
          }
        }
      });
      return collection.AsReadOnly();
    }

    /// <summary>
    /// Gets the path relative to the top URL of the current variation.
    /// </summary>
    /// <param name="requestPath"></param>
    /// <returns></returns>
    public static string TrimVariationFromPath(string requestPath) {
      string variationRootUrl = VariationContext.Current.TopWebServerRelativeUrl;
      if (requestPath.StartsWith(variationRootUrl)) {
        string relativePath = requestPath.Substring(variationRootUrl.Length);
        if (relativePath.Length == 0) {
          return "/";
        }
        if (relativePath[0] == '/' || relativePath[0] == '?' || relativePath[0] == '#') {
          return relativePath;
        }
      }
      return requestPath;
    }

    /// <summary>
    /// Creates a publishing page under the specified site with the content type ID and specified title.
    /// Filename of the created page is automatically chosen to avoid collision to existing pages.
    /// </summary>
    /// <param name="currentWeb">Publishing web.</param>
    /// <param name="contentTypeId">Content type ID of the new page.</param>
    /// <param name="title">Title of the new page.</param>
    /// <exception cref="InvalidOperationException">Throws if there is no page layouts associated with the specified content type ID.</exception>
    /// <returns>A publishing page object.</returns>
    public static PublishingPage CreatePublishingPage(this PublishingWeb currentWeb, SPContentTypeId contentTypeId, string title) {
      CommonHelper.ConfirmNotNull(currentWeb, "currentWeb");
      CommonHelper.ConfirmNotNull(title, "title");

      PageLayout pageLayout = null;

      currentWeb.Web.Site.WithElevatedPrivileges(elevatedSite => {
        PublishingSite publishingSite = new PublishingSite(elevatedSite);

        IEnumerable<PageLayout> pageLayouts = publishingSite.GetPageLayouts(true).Where(p => p.AssociatedContentType != null);
        pageLayout = pageLayouts.FirstOrDefault(p => p.AssociatedContentType.Id == contentTypeId);
        if (pageLayout == null) {
          pageLayout = pageLayouts.FirstOrDefault(p => p.AssociatedContentType.Id.IsChildOf(contentTypeId));
        }
        //pageLayout = publishingWeb.GetAvailablePageLayouts().FirstOrDefault(p => p.AssociatedContentType.Id == contentTypeId);
        //pageLayout = publishingWeb.GetAvailablePageLayouts(contentTypeId).FirstOrDefault();
      });
      if (pageLayout == null) {
        throw new InvalidOperationException(String.Format("Could not find available page layout for content type {0} at {1}", contentTypeId, currentWeb.Url));
      }

      MethodInfo getUniquePageName =
        typeof(PublishingPage).GetMethod("GetUniquePageName", true, typeof(string), typeof(bool), typeof(PublishingWeb), typeof(bool)) ??
        typeof(PublishingPage).GetMethod("GetUniquePageName", true, typeof(string), typeof(bool), typeof(PublishingWeb));
      if (getUniquePageName == null) {
        throw new MissingMethodException("PublishingPage", "GetUniquePageName");
      }
      object[] param = getUniquePageName.GetParameters().Length == 4 ?
        new object[] { title, true, currentWeb, true } :
        new object[] { title, true, currentWeb };
      string uniquePageName = getUniquePageName.Invoke<string>(null, param);
      PublishingPage publishingPage = currentWeb.AddPublishingPage(uniquePageName, pageLayout);
      publishingPage.Title = title;
      publishingPage.Update();
      return publishingPage;
    }

    /// <summary>
    /// Removes extra white spaces in rich-text web parts.
    /// </summary>
    /// <param name="adapter">Data access adapter of the list item.</param>
    public static void FixRichTextWebPartWhiteSpaces(ISPListItemAdapter adapter) {
      CommonHelper.ConfirmNotNull(adapter, "adapter");
      using (SPLimitedWebPartManager wpm = adapter.ListItem.File.GetLimitedWebPartManager(PersonalizationScope.Shared)) {
        List<ContentEditorWebPart> contentWebParts = wpm.WebParts.OfType<ContentEditorWebPart>().ToList();
        foreach (ContentEditorWebPart contentWebpart in contentWebParts) {
          if (contentWebpart.Content != null) {
            try {
              string resultXml = FixWhiteSpaces(contentWebpart.Content.OuterXml);
              XmlDocument resultDoc = new XmlDocument();
              resultDoc.LoadXml(resultXml);
              contentWebpart.Content = resultDoc.DocumentElement;
              wpm.SaveChanges(contentWebpart);
            } catch {
            }
          }
        }
      }
    }

    /// <summary>
    /// Removes extra white spaces in rich-text fields.
    /// </summary>
    /// <param name="adapter">Data access adapter of the list item.</param>
    public static void FixRichTextFieldWhiteSpaces(ISPListItemAdapter adapter) {
      CommonHelper.ConfirmNotNull(adapter, "adapter");
      foreach (BaseRichFieldType field in adapter.ListItem.Fields.OfType<BaseRichFieldType>()) {
        object value = adapter.GetString(field.InternalName);
        if (value != null) {
          adapter.SetString(field.InternalName, FixWhiteSpaces(value.ToString()));
        }
      }
    }

    /// <summary>
    /// Approves and publishes all assets referred in any link fields (such as URL or Summary Links), rich-text content and rich-text web parts.
    /// </summary>
    /// <param name="adapter">Data access adapter of the list item.</param>
    /// <param name="additionalFields">Internal name of fields where its HTML content need to be parsed.</param>
    public static void PublishRelatedAssets(ISPListItemAdapter adapter, params string[] additionalFields) {
      CommonHelper.ConfirmNotNull(adapter, "adapter");
      CommonHelper.ConfirmNotNull(additionalFields, "additionalFields");

      SPListItem item = adapter.ListItem;
      if (item != null) {
        using (SPItemEventHelper.GetEventFiringDisabledScope()) {
          try {
            foreach (SPField field in item.Fields) {
              try {
                object value = item[field.Id];
                if (value != null) {
                  if (additionalFields.Contains(field.StaticName)) {
                    PublishAssetFromHtml(value.ToString(), item);
                  } else if (field is BaseRichFieldType) {
                    PublishAssetFromHtml(value.ToString(), item);
                  } else if (field is SPFieldUrl) {
                    PublishAssetInternal(new SPFieldUrlValue(value.ToString()).Url, item);
                  } else if (field is SummaryLinkField) {
                    foreach (SummaryLink link in new SummaryLinkFieldValue(value.ToString()).SummaryLinks) {
                      PublishAssetInternal(link.LinkUrl, item);
                    }
                  }
                }
              } catch {
              }
            }
            if (item.File != null && item.File.Name.EndsWith(".aspx")) {
              using (SPLimitedWebPartManager wpm = item.File.GetLimitedWebPartManager(PersonalizationScope.Shared)) {
                foreach (object wp in wpm.WebParts) {
                  if (wp is ContentEditorWebPart) {
                    XmlElement elm = ((ContentEditorWebPart)wp).Content;
                    if (elm != null) {
                      PublishAssetFromHtml(elm.InnerText, item);
                    }
                  } else if (wp is ImageWebPart) {
                    PublishAssetInternal(((ImageWebPart)wp).ImageLink, item);
                  } else if (wp is MediaWebPart) {
                    PublishAssetInternal(((MediaWebPart)wp).MediaSource, item);
                  } else if (wp is SummaryLinkWebPart) {
                    foreach (SummaryLink link in ((SummaryLinkWebPart)wp).SummaryLinkValue.SummaryLinks) {
                      PublishAssetInternal(link.LinkUrl, item);
                    }
                  }
                }
              }
            }
          } catch (Exception ex) {
            throw new Exception("Aborting approval because of some related contents cannot be published or approved", ex);
          }
        }
      }
    }

    /// <summary>
    /// Approves and publishes asset referred by the specified URL.
    /// </summary>
    /// <param name="assetUrl">URL to the asset.</param>
    /// <param name="adapter">Data access adapter of the list item.</param>
    public static void PublishRelatedAsset(string assetUrl, ISPListItemAdapter adapter) {
      CommonHelper.ConfirmNotNull(assetUrl, "assetUrl");
      CommonHelper.ConfirmNotNull(adapter, "adapter");
      PublishAssetInternal(assetUrl, adapter.ListItem);
    }

    /// <summary>
    /// Extension to <see cref="SPUtility.GetServerRelativeUrlFromPrefixedUrl"/>. 
    /// See <see cref="ResolvePrefixedUrl(string,UriKind)"/> for details.
    /// </summary>
    /// <param name="value">URL with prefixes to be resolved.</param>
    /// <returns>Resolved URL.</returns>
    public static string ResolvePrefixedUrl(string value) {
      return ResolvePrefixedUrl(value, UriKind.Relative);
    }

    /// <summary>
    /// Extension to <see cref="SPUtility.GetServerRelativeUrlFromPrefixedUrl"/>.
    /// This method supports two more tokens. "~variation/" will be resolved to the top web of the current variation;
    /// where "~pages/" will be resolved to the name of publishing page library under the locale of the current variation.
    /// It also allows to specify the returned URL to be server-relative or absolute.
    /// </summary>
    /// <param name="value">Input URL.</param>
    /// <param name="uriKind">Format of the resolved URL.</param>
    /// <returns>Resolved URL.</returns>
    public static string ResolvePrefixedUrl(string value, UriKind uriKind) {
      if (SPContext.Current != null) {
        string resolvedPath;
        if (value.StartsWith("~variation/", StringComparison.OrdinalIgnoreCase)) {
          resolvedPath = SPUrlUtility.CombineUrl(VariationContext.Current.TopWebServerRelativeUrl, value.Substring(11));
          int pagesPos = resolvedPath.IndexOf("~pages/", 0, StringComparison.OrdinalIgnoreCase);
          if (pagesPos >= 0) {
            resolvedPath = String.Concat(resolvedPath.Substring(0, pagesPos), VariationContext.Current.PagesListName, resolvedPath.Substring(pagesPos + 6));
          }
        } else if (value.StartsWith("~pages/", StringComparison.OrdinalIgnoreCase)) {
          string libraryPath = SPUrlUtility.CombineUrl(VariationContext.Current.TopWebServerRelativeUrl, VariationContext.Current.PagesListName);
          resolvedPath = String.Concat(libraryPath, value.Substring(6));
        } else {
          resolvedPath = SPUtility.GetServerRelativeUrlFromPrefixedUrl(value);
        }
        if (uriKind == UriKind.Absolute) {
          return SPUtility.GetFullUrl(SPContext.Current.Site, resolvedPath);
        }
        return resolvedPath;
      }
      return value;
    }

    #region Private Helpers
    private static void PublishAssetFromHtml(string html, SPListItem sourceItem) {
      if (!CommonHelper.IsNullOrWhiteSpace(html)) {
        Match match = Regex.Match(html, @"\b(?:src|href|url)=(?>(?<dq>"")?|(?<sq>')?)(?<url>(?(dq)[^""]+|(?(sq)[^']+|[^\s]+)))", RegexOptions.IgnoreCase);
        while (match.Success) {
          PublishAssetInternal(SPHttpUtility.HtmlDecode(match.Groups["url"].Value), sourceItem);
          match = match.NextMatch();
        }
      }
    }

    private static void PublishAssetInternal(string assetUrl, SPListItem sourceItem) {
      if (!CommonHelper.IsNullOrWhiteSpace(assetUrl)) {
        object fileSystemObj;
        try {
          int pathEndPos = assetUrl.IndexOfAny(new[] { '?', '#' });
          if (pathEndPos >= 0) {
            assetUrl = assetUrl.Substring(0, pathEndPos);
          }
          fileSystemObj = sourceItem.Web.Site.GetFileOrFolder(assetUrl);
        } catch {
          return;
        }
        try {
          if (fileSystemObj is SPFolder) {
            SPFolder folder = (SPFolder)fileSystemObj;
            if (folder.ParentListId != Guid.Empty) {
              folder.EnsureApproved();
            }
          } else if (fileSystemObj is SPFile) {
            SPFile file = (SPFile)fileSystemObj;
            if (file.ParentFolder.ParentListId != Guid.Empty) {
              if (file.Item != null && file.Item.ContentTypeId.IsChildOf(SPBuiltInContentTypeId.Document) && !file.Item.ContentTypeId.IsChildOf(ContentTypeId.MasterPage.Parent) && !file.Item.ContentTypeId.IsChildOf(ContentTypeId.Page.Parent)) {
                file.EnsurePublished(String.Concat("Publish linked asset from ", SPUrlUtility.CombineUrl(sourceItem.Web.ServerRelativeUrl, sourceItem.Url)));
              }
            }
          }
        } catch (Exception ex) {
          throw new Exception(String.Concat("Cannot approve or publish content at ", assetUrl), ex);
        }
      }
    }

    private static string FixWhiteSpaces(string value) {
      return Regex.Replace(value, @"\u200b|(?!<div)<(?<tag>\w+)[^>]*>(?:\s|\u200b)*</\k<tag>>", String.Empty, RegexOptions.IgnoreCase);
    }
    #endregion
  }
}
