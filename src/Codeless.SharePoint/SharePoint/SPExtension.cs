using Codeless.SharePoint.Internal;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Administration;
using Microsoft.SharePoint.Publishing;
using Microsoft.SharePoint.Taxonomy;
using Microsoft.SharePoint.Utilities;
using Microsoft.SharePoint.Workflow;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Xml;
using Group = Microsoft.SharePoint.Taxonomy.Group;

namespace Codeless.SharePoint {
  /// <summary>
  /// Provides extension methods to SharePoint classes.
  /// </summary>
  public static class SPExtension {
    private static readonly Regex GuidRegex = new Regex(@"^(?<b>\{)?[A-F0-9]{8}(?:-?[A-F0-9]{4}){3}-?[A-F0-9]{12}(?(b)\}|)$", RegexOptions.IgnoreCase);
    private static readonly Regex UserDataFieldColNameRegex = new Regex(@"^(bit|datetime|float|int|ntext|nvarchar|sql_variant|uniqueidentifier)\d+$", RegexOptions.IgnoreCase);
    private static readonly DateTime SPDateTimeMin = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SPDateTimeMax = new DateTime(8900, 12, 31, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Creates a generic data access adapter from a list item.
    /// </summary>
    /// <param name="listItem">List item where data is read from or write to.</param>
    /// <returns>A list item data access adapter.</returns>
    public static ISPListItemAdapter CreateAdapter(this SPListItem listItem) {
      return new SPListItemAdapter(listItem);
    }

    /// <summary>
    /// Gets value from a URL field.
    /// If <paramref name="uriKind"/> is set to <see cref="UriKind.Absolute"/>, the URL is always absolute;
    /// otherwise the URL is normalized to a server-relative path if it points to the same SharePoint web application.
    /// </summary>
    /// <param name="adapter">Data access adapter of list item.</param>
    /// <param name="fieldName">Name of field to read.</param>
    /// <param name="uriKind">Specifies to always return absolute URL or not.</param>
    /// <returns>Value in the specified field.</returns>
    public static SPFieldUrlValue GetUrlFieldValue(this ISPListItemAdapter adapter, string fieldName, UriKind uriKind) {
      SPFieldUrlValue value = adapter.GetUrlFieldValue(fieldName);
      Uri dummy;
      if (uriKind == UriKind.Absolute && !String.IsNullOrEmpty(value.Url) && !Uri.TryCreate(value.Url, UriKind.Absolute, out dummy)) {
        return new SPFieldUrlValue {
          Url = adapter.Site.MakeFullUrl(value.Url),
          Description = value.Description
        };
      }
      return value;
    }

    /// <summary>
    /// Sets value to a URL field with the specified URL.
    /// </summary>
    /// <param name="adapter">Data access adapter of list item.</param>
    /// <param name="fieldName">Name of field to write.</param>
    /// <param name="url">URL value.</param>
    public static void SetUrlFieldValue(this ISPListItemAdapter adapter, string fieldName, string url) {
      adapter.SetUrlFieldValue(fieldName, new SPFieldUrlValue { Url = url });
    }

    /// <summary>
    /// Sets value to a URL field with the specified URL and description.
    /// </summary>
    /// <param name="adapter">Data access adapter of list item.</param>
    /// <param name="fieldName">Name of field to write.</param>
    /// <param name="url">URL value.</param>
    /// <param name="description">Description of the URL.</param>
    public static void SetUrlFieldValue(this ISPListItemAdapter adapter, string fieldName, string url, string description) {
      adapter.SetUrlFieldValue(fieldName, new SPFieldUrlValue { Url = url, Description = description });
    }

    /// <summary>
    /// Gets value from a date time field. If the field does not contain value, the largest date supported in SharePoint is returned, that is 8900/12/31 23:59:59 UTC.
    /// </summary>
    /// <param name="adapter">Data access adapter of list item.</param>
    /// <param name="fieldName">Name of field to read.</param>
    /// <returns>Value of the data time field.</returns>
    public static DateTime GetDateTimeOrMax(this ISPListItemAdapter adapter, string fieldName) {
      return adapter.GetDateTime(fieldName).GetValueOrDefault(SPDateTimeMax);
    }

    /// <summary>
    /// Gets value from a date time field. If the field does not contain value, the smallest date supported in SharePoint is returned, that is 1900/1/1 0:00:00 UTC.
    /// </summary>
    /// <param name="adapter">Data access adapter of list item.</param>
    /// <param name="fieldName">Name of field to read.</param>
    /// <returns>Value of the data time field.</returns>
    public static DateTime GetDateTimeOrMin(this ISPListItemAdapter adapter, string fieldName) {
      return adapter.GetDateTime(fieldName).GetValueOrDefault(SPDateTimeMin);
    }

    /// <summary>
    /// Gets value from a date time field ignoring the time component.
    /// </summary>
    /// <param name="adapter">Data access adapter of list item.</param>
    /// <param name="fieldName">Name of field to read.</param>
    /// <returns>Value of the data time field without the time component.</returns>
    public static DateTime? GetDateOnly(this ISPListItemAdapter adapter, string fieldName) {
      DateTime? value = adapter.GetDateTime(fieldName);
      if (value.HasValue) {
        return value.Value.ToUniversalTime().Date;
      }
      return null;
    }

    /// <summary>
    /// Gets value from a date time field ignoring the time component. If the field does not contain value, the largest date supported in SharePoint is returned, that is 1900/1/1 0:00:00 UTC.
    /// </summary>
    /// <param name="adapter">Data access adapter of list item.</param>
    /// <param name="fieldName">Name of field to read.</param>
    /// <returns>Value of the data time field without the time component.</returns>
    public static DateTime GetDateOnlyOrMax(this ISPListItemAdapter adapter, string fieldName) {
      return adapter.GetDateOnly(fieldName).GetValueOrDefault(SPDateTimeMax);
    }

    /// <summary>
    /// Gets value from a date time field ignoring the time component. If the field does not contain value, the smallest date supported in SharePoint is returned, that is 1900/1/1 0:00:00 UTC.
    /// </summary>
    /// <param name="adapter">Data access adapter of list item.</param>
    /// <param name="fieldName">Name of field to read.</param>
    /// <returns>Value of the data time field without the time component.</returns>
    public static DateTime GetDateOnlyOrMin(this ISPListItemAdapter adapter, string fieldName) {
      return adapter.GetDateOnly(fieldName).GetValueOrDefault(SPDateTimeMin);
    }

    /// <summary>
    /// Sets value to a date time field without the time component.
    /// </summary>
    /// <param name="adapter">Data access adapter of list item.</param>
    /// <param name="fieldName">Name of field to write.</param>
    /// <param name="value">Date value.</param>
    public static void SetDateOnly(this ISPListItemAdapter adapter, string fieldName, DateTime? value) {
      if (value.HasValue) {
        adapter.SetDateTime(fieldName, DateTime.SpecifyKind(value.Value.Date, DateTimeKind.Utc));
      } else {
        adapter.SetDateTime(fieldName, null);
      }
    }

    /// <summary>
    /// Performs operation on the site collection with system account.
    /// </summary>
    /// <param name="site">A site collection object.</param>
    /// <param name="codeToRun">Action to run.</param>
    public static void WithElevatedPrivileges(this SPSite site, Action<SPSite> codeToRun) {
      CommonHelper.ConfirmNotNull(codeToRun, "codeToRun");
      using (SPSite elevatedSite = new SPSite(site.ID, SPUserToken.SystemAccount)) {
        using (elevatedSite.GetAllowUnsafeUpdatesScope()) {
          using (elevatedSite.RootWeb.GetAllowUnsafeUpdatesScope()) {
            codeToRun(elevatedSite);
          }
        }
      }
    }

    /// <summary>
    /// Performs operation on the site with system account.
    /// </summary>
    /// <param name="web">A site object.</param>
    /// <param name="codeToRun">Action to run.</param>
    public static void WithElevatedPrivileges(this SPWeb web, Action<SPWeb> codeToRun) {
      CommonHelper.ConfirmNotNull(codeToRun, "codeToRun");
      using (SPSite elevatedSite = new SPSite(web.Site.ID, SPUserToken.SystemAccount)) {
        using (SPWeb elevatedWeb = elevatedSite.OpenWeb(web.ID)) {
          using (elevatedWeb.GetAllowUnsafeUpdatesScope()) {
            codeToRun(elevatedWeb);
          }
        }
      }
    }

    /// <summary>
    /// Performs operation on the list with system account.
    /// </summary>
    /// <param name="list">A list object.</param>
    /// <param name="codeToRun">Action to run.</param>
    public static void WithElevatedPrivileges(this SPList list, Action<SPList> codeToRun) {
      CommonHelper.ConfirmNotNull(codeToRun, "codeToRun");
      using (SPSite elevatedSite = new SPSite(list.ParentWeb.Site.ID, SPUserToken.SystemAccount)) {
        using (SPWeb elevatedWeb = elevatedSite.OpenWeb(list.ParentWeb.ID)) {
          using (elevatedWeb.GetAllowUnsafeUpdatesScope()) {
            SPList elevatedList = elevatedWeb.Lists[list.ID];
            codeToRun(elevatedList);
          }
        }
      }
    }

    /// <summary>
    /// Performs operation on the list item with system account.
    /// </summary>
    /// <param name="listItem">A list item object.</param>
    /// <param name="codeToRun">Action to run.</param>
    public static void WithElevatedPrivileges(this SPListItem listItem, Action<SPListItem> codeToRun) {
      CommonHelper.ConfirmNotNull(codeToRun, "codeToRun");
      using (SPSite elevatedSite = new SPSite(listItem.Web.Site.ID, SPUserToken.SystemAccount)) {
        using (SPWeb elevatedWeb = elevatedSite.OpenWeb(listItem.Web.ID)) {
          using (elevatedWeb.GetAllowUnsafeUpdatesScope()) {
            SPList elevatedList = elevatedWeb.Lists[listItem.ParentList.ID];
            SPListItem elevatedItem = elevatedList.GetItemById(listItem.ID);
            codeToRun(elevatedItem);
          }
        }
      }
    }

    /// <summary>
    /// Performs operation on the folder with system account.
    /// </summary>
    /// <param name="folder">A folder objet.</param>
    /// <param name="codeToRun">Action to run.</param>
    public static void WithElevatedPrivileges(this SPFolder folder, Action<SPFolder> codeToRun) {
      CommonHelper.ConfirmNotNull(codeToRun, "codeToRun");
      using (SPSite elevatedSite = new SPSite(folder.ParentWeb.Site.ID, SPUserToken.SystemAccount)) {
        using (SPWeb elevatedWeb = elevatedSite.OpenWeb(folder.ParentWeb.ID)) {
          using (elevatedWeb.GetAllowUnsafeUpdatesScope()) {
            SPFolder elevatedFolder = elevatedWeb.GetFolder(folder.UniqueId);
            codeToRun(elevatedFolder);
          }
        }
      }
    }

    /// <summary>
    /// Performs operation on the file with system account.
    /// </summary>
    /// <param name="file">A file objet.</param>
    /// <param name="codeToRun">Action to run.</param>
    public static void WithElevatedPrivileges(this SPFile file, Action<SPFile> codeToRun) {
      CommonHelper.ConfirmNotNull(codeToRun, "codeToRun");
      using (SPSite elevatedSite = new SPSite(file.Web.Site.ID, SPUserToken.SystemAccount)) {
        using (SPWeb elevatedWeb = elevatedSite.OpenWeb(file.Web.ID)) {
          using (elevatedWeb.GetAllowUnsafeUpdatesScope()) {
            SPFile elevatedFile = elevatedWeb.GetFile(file.UniqueId);
            codeToRun(elevatedFile);
          }
        }
      }
    }

    /// <summary>
    /// Performs operation on the secureable object with system account.
    /// </summary>
    /// <param name="securableObject">SPSecurableObject.</param>
    /// <param name="codeToRun">Action to run.</param>
    public static void WithElevatedPrivileges(this SPSecurableObject securableObject, Action<SPSecurableObject> codeToRun) {
      CommonHelper.ConfirmNotNull(codeToRun, "codeToRun");
      if (securableObject is SPWeb) {
        ((SPWeb)securableObject).WithElevatedPrivileges((SPWeb v) => codeToRun(v));
      } else if (securableObject is SPList) {
        ((SPList)securableObject).WithElevatedPrivileges((SPList v) => codeToRun(v));
      } else if (securableObject is SPListItem) {
        ((SPListItem)securableObject).WithElevatedPrivileges((SPListItem v) => codeToRun(v));
      } else {
        throw new ArgumentException("securableObject", "securableObject");
      }
    }

    /// <summary>
    /// Performs operation on the site collection with the specified account.
    /// </summary>
    /// <param name="site">A site collection object.</param>
    /// <param name="user">A user object that represents the account who operations are performed as.</param>
    /// <param name="codeToRun">Action to run.</param>
    public static void WithUser(this SPSite site, SPUser user, Action<SPSite> codeToRun) {
      CommonHelper.ConfirmNotNull(user, "user");
      CommonHelper.ConfirmNotNull(codeToRun, "codeToRun");
      using (new SPSecurity.SuppressAccessDeniedRedirectInScope()) {
        using (SPSite elevatedSite = new SPSite(site.ID, user.UserToken)) {
          using (elevatedSite.GetAllowUnsafeUpdatesScope()) {
            using (elevatedSite.RootWeb.GetAllowUnsafeUpdatesScope()) {
              codeToRun(elevatedSite);
            }
          }
        }
      }
    }

    /// <summary>
    /// Performs operation on the site with the specified account.
    /// </summary>
    /// <param name="web">A site object.</param>
    /// <param name="user">A user object that represents the account who operations are performed as.</param>
    /// <param name="codeToRun">Action to run.</param>
    public static void WithUser(this SPWeb web, SPUser user, Action<SPWeb> codeToRun) {
      CommonHelper.ConfirmNotNull(user, "user");
      CommonHelper.ConfirmNotNull(codeToRun, "codeToRun");
      using (new SPSecurity.SuppressAccessDeniedRedirectInScope()) {
        using (SPSite elevatedSite = new SPSite(web.Site.ID, user.UserToken)) {
          using (SPWeb elevatedWeb = elevatedSite.OpenWeb(web.ID)) {
            using (elevatedWeb.GetAllowUnsafeUpdatesScope()) {
              codeToRun(elevatedWeb);
            }
          }
        }
      }
    }

    /// <summary>
    /// Performs operation on the list with the specified account.
    /// </summary>
    /// <param name="list">A list object.</param>
    /// <param name="user">A user object that represents the account who operations are performed as.</param>
    /// <param name="codeToRun">Action to run.</param>
    public static void WithUser(this SPList list, SPUser user, Action<SPList> codeToRun) {
      CommonHelper.ConfirmNotNull(user, "user");
      CommonHelper.ConfirmNotNull(codeToRun, "codeToRun");
      using (new SPSecurity.SuppressAccessDeniedRedirectInScope()) {
        using (SPSite elevatedSite = new SPSite(list.ParentWeb.Site.ID, user.UserToken)) {
          using (SPWeb elevatedWeb = elevatedSite.OpenWeb(list.ParentWeb.ID)) {
            using (elevatedWeb.GetAllowUnsafeUpdatesScope()) {
              SPList elevatedList = elevatedWeb.Lists[list.ID];
              codeToRun(elevatedList);
            }
          }
        }
      }
    }

    /// <summary>
    /// Performs operation on the list item with the specified account.
    /// </summary>
    /// <param name="listItem">A list item object.</param>
    /// <param name="user">A user object that represents the account who operations are performed as.</param>
    /// <param name="codeToRun">Action to run.</param>
    public static void WithUser(this SPListItem listItem, SPUser user, Action<SPListItem> codeToRun) {
      CommonHelper.ConfirmNotNull(user, "user");
      CommonHelper.ConfirmNotNull(codeToRun, "codeToRun");
      using (new SPSecurity.SuppressAccessDeniedRedirectInScope()) {
        using (SPSite elevatedSite = new SPSite(listItem.Web.Site.ID, user.UserToken)) {
          using (SPWeb elevatedWeb = elevatedSite.OpenWeb(listItem.Web.ID)) {
            using (elevatedWeb.GetAllowUnsafeUpdatesScope()) {
              SPList elevatedList = elevatedWeb.Lists[listItem.ParentList.ID];
              SPListItem elevatedItem = elevatedList.GetItemById(listItem.ID);
              codeToRun(elevatedItem);
            }
          }
        }
      }
    }

    /// <summary>
    /// Performs operation on the folder with the specified account.
    /// </summary>
    /// <param name="folder">A folder objet.</param>
    /// <param name="user">A user object that represents the account who operations are performed as.</param>
    /// <param name="codeToRun">Action to run.</param>
    public static void WithUser(this SPFolder folder, SPUser user, Action<SPFolder> codeToRun) {
      CommonHelper.ConfirmNotNull(user, "user");
      CommonHelper.ConfirmNotNull(codeToRun, "codeToRun");
      using (new SPSecurity.SuppressAccessDeniedRedirectInScope()) {
        using (SPSite elevatedSite = new SPSite(folder.ParentWeb.Site.ID, user.UserToken)) {
          using (SPWeb elevatedWeb = elevatedSite.OpenWeb(folder.ParentWeb.ID)) {
            using (elevatedWeb.GetAllowUnsafeUpdatesScope()) {
              SPFolder elevatedFolder = elevatedWeb.GetFolder(folder.UniqueId);
              codeToRun(elevatedFolder);
            }
          }
        }
      }
    }

    /// <summary>
    /// Performs operation on the file with the specified account.
    /// </summary>
    /// <param name="file">A file objet.</param>
    /// <param name="user">A user object that represents the account who operations are performed as.</param>
    /// <param name="codeToRun">Action to run.</param>
    public static void WithUser(this SPFile file, SPUser user, Action<SPFile> codeToRun) {
      CommonHelper.ConfirmNotNull(user, "user");
      CommonHelper.ConfirmNotNull(codeToRun, "codeToRun");
      using (new SPSecurity.SuppressAccessDeniedRedirectInScope()) {
        using (SPSite elevatedSite = new SPSite(file.Web.Site.ID, user.UserToken)) {
          using (SPWeb elevatedWeb = elevatedSite.OpenWeb(file.Web.ID)) {
            using (elevatedWeb.GetAllowUnsafeUpdatesScope()) {
              SPFile elevatedFile = elevatedWeb.GetFile(file.UniqueId);
              codeToRun(elevatedFile);
            }
          }
        }
      }
    }

    /// <summary>
    /// Performs operation on the secureable object with the specified account.
    /// </summary>
    /// <param name="securableObject">SPSecurableObject.</param>
    /// <param name="user">A user object that represents the account who operations are performed as.</param>
    /// <param name="codeToRun">Action to run.</param>
    public static void WithUser(this SPSecurableObject securableObject, SPUser user, Action<SPSecurableObject> codeToRun) {
      CommonHelper.ConfirmNotNull(codeToRun, "codeToRun");
      if (securableObject is SPWeb) {
        ((SPWeb)securableObject).WithUser(user, (SPWeb v) => codeToRun(v));
      } else if (securableObject is SPList) {
        ((SPList)securableObject).WithUser(user, (SPList v) => codeToRun(v));
      } else if (securableObject is SPListItem) {
        ((SPListItem)securableObject).WithUser(user, (SPListItem v) => codeToRun(v));
      } else {
        throw new ArgumentException("securableObject", "securableObject");
      }
    }

    /// <summary>
    /// Finds a descendant site of the specified ID.
    /// </summary>
    /// <param name="site">A site collection object.</param>
    /// <param name="webId">GUID of a descendant site.</param>
    /// <returns>A site object or *null* if the sub-site does not exist or access is denied.</returns>
    public static SPWeb TryGetWebForCurrentUser(this SPSite site, Guid webId) {
      SPWeb value = site.RootWeb;
      if (value.ID == webId) {
        return value;
      }
      Stack<Guid> parentWebIds = new Stack<Guid>();
      using (SPSite elevatedSite = new SPSite(site.ID, SPUserToken.SystemAccount)) {
        using (SPWeb targetWeb = elevatedSite.OpenWeb(webId)) {
          for (SPWeb web = targetWeb; web.ID != value.ID; web = web.ParentWeb) {
            parentWebIds.Push(web.ID);
          }
        }
      }
      while (parentWebIds.Count > 0) {
        value = value.GetSubWebByIDSafe(parentWebIds.Pop());
        if (value == null) {
          return null;
        }
      }
      return value;
    }

    /// <summary>
    /// Gets a sub-site with the specified ID. This method derives from <see cref="SPWeb.GetSubwebsForCurrentUser()"/> except that all exceptions are caught.
    /// </summary>
    /// <param name="web">A site object.</param>
    /// <param name="name">GUID of sub-site.</param>
    /// <returns>A site object or *null* if the sub-site does not exist or access is denied.</returns>
    public static SPWeb GetSubWebByIDSafe(this SPWeb web, Guid name) {
      using (new SPSecurity.SuppressAccessDeniedRedirectInScope()) {
        try {
          SPWeb childWeb = web.GetSubwebsForCurrentUser()[name];
          if (childWeb.Exists) {
            return childWeb;
          }
        } catch (ArgumentException) {
        } catch (UnauthorizedAccessException) { }
      }
      return null;
    }

    /// <summary>
    /// Gets a sub-site with the specified name. This method derives from <see cref="SPWeb.GetSubwebsForCurrentUser()"/> except that all exceptions are caught.
    /// </summary>
    /// <param name="web">A site object.</param>
    /// <param name="name">Name of sub-site.</param>
    /// <returns>A site object or *null* if the sub-site does not exist or access is denied.</returns>
    public static SPWeb GetSubWebByNameSafe(this SPWeb web, string name) {
      using (new SPSecurity.SuppressAccessDeniedRedirectInScope()) {
        try {
          SPWeb childWeb = web.GetSubwebsForCurrentUser()[name];
          if (childWeb.Exists) {
            return childWeb;
          }
        } catch (SPException) {
          // supplied name is a reserved name
        } catch (ArgumentException) {
          // sub-site does not exist
        } catch (UnauthorizedAccessException) {
          // sub-site exist but current user does not have permission to access
        }
      }
      return null;
    }

    /// <summary>
    /// Gets all descendant sites which current user have permission to access.
    /// </summary>
    /// <param name="web">A site object.</param>
    /// <returns>An enumerable of all descendant sites which current user have permission to access.</returns>
    public static IEnumerable<SPWeb> GetAllWebsForCurrentUser(this SPWeb web) {
      yield return web;
      foreach (SPWeb child in web.GetSubwebsForCurrentUser()) {
        yield return child;
      }
    }

    /// <summary>
    /// Sets <see cref="SPWeb.AllowUnsafeUpdates"/> to *true* on the root site and restore previous value on dispose.
    /// </summary>
    /// <param name="site">A site collection object.</param>
    /// <returns>An <see cref="IDisposable"/> object.</returns>
    public static IDisposable GetAllowUnsafeUpdatesScope(this SPSite site) {
      return new SPSiteAllowUnsafeUpdatesScope(site);
    }

    /// <summary>
    /// Sets <see cref="SPWeb.AllowUnsafeUpdates"/> to *true* on the specified site and restore previous value on dispose.
    /// </summary>
    /// <param name="web">A site object.</param>
    /// <returns>An <see cref="IDisposable"/> object.</returns>
    public static IDisposable GetAllowUnsafeUpdatesScope(this SPWeb web) {
      return new SPWebAllowUnsafeUpdatesScope(web);
    }

    /// <summary>
    /// Gets all lists under the given site which contain the specified content type.
    /// </summary>
    /// <param name="web">A site object.</param>
    /// <param name="contentTypeId">Content type ID.</param>
    /// <returns>A enumerable of list objects.</returns>
    public static IEnumerable<SPList> GetListsOfContentType(this SPWeb web, SPContentTypeId contentTypeId) {
      foreach (SPList list in web.Lists) {
        if (list.ContainsContentType(contentTypeId)) {
          yield return list;
        }
      }
    }

    /// <summary>
    /// Gets a single list under the given site which contains the specified content type.
    /// </summary>
    /// <param name="web">A site object.</param>
    /// <param name="contentTypeId">Content type ID.</param>
    /// <returns>A list object or *null* if there is none.</returns>
    public static SPList GetListOfContentType(this SPWeb web, SPContentTypeId contentTypeId) {
      return web.GetListOfContentType(contentTypeId, false);
    }

    /// <summary>
    /// Gets a single list under the given site which contains the specified content type, and optionally throw an exception if there is none.
    /// </summary>
    /// <param name="web">A site object.</param>
    /// <param name="contentTypeId">Content type ID.</param>
    /// <param name="throwIfNotExists">Whether to throw an exception if there is none.</param>
    /// <returns>A list object.</returns>
    public static SPList GetListOfContentType(this SPWeb web, SPContentTypeId contentTypeId, bool throwIfNotExists) {
      SPList list = web.GetListsOfContentType(contentTypeId).FirstOrDefault();
      if (list == null && throwIfNotExists) {
        throw new ArgumentException("contentTypeId", String.Format("There is no list with specifed content type in {0}", web.Url));
      }
      return list;
    }

    /// <summary>
    /// Determines whether the specified list contains the content type.
    /// </summary>
    /// <param name="list">A list object.</param>
    /// <param name="contentTypeId">Content type ID.</param>
    /// <returns>*true* if the specified list contains the content type.</returns>
    public static bool ContainsContentType(this SPList list, SPContentTypeId contentTypeId) {
      foreach (SPContentType contentType in list.ContentTypes) {
        if (contentType.Id.IsChildOf(contentTypeId)) {
          return true;
        }
      }
      return false;
    }

    /// <summary>
    /// Returns the list that is associated with the specified site-relative URL.
    /// </summary>
    /// <param name="web">A site object.</param>
    /// <param name="strUrl">A string that contains the site-relative URL for a list.</param>
    /// <remarks>This method caters unexpected <see cref="System.Runtime.InteropServices.COMException"/> with native error code 0x80004005.</remarks>
    /// <returns>An <see cref="Microsoft.SharePoint.SPList"/> object that represents the list.</returns>
    public static SPList GetListSafe(this SPWeb web, string strUrl) {
      try {
        return web.GetList(strUrl);
      } catch (Exception ex) {
        if (ex.InnerException is COMException && ex.InnerException.Message.IndexOf("0x80004005") >= 0) {
          SPFolder folder = web.GetFolder(strUrl);
          if (folder.Exists && folder.ParentListId != Guid.Empty) {
            return web.Lists[folder.ParentListId];
          }
          throw new FileNotFoundException();
        }
        throw;
      }
    }

    /// <summary>
    /// Creates a list under the specified site if no list exists at the given URL.
    /// </summary>
    /// <param name="web">A site object.</param>
    /// <param name="title">Title of the list to create.</param>
    /// <param name="webRelativeUrl">URL of the list to create at.</param>
    /// <param name="templateType">Template type.</param>
    /// <returns>Existing or newly created list object.</returns>
    public static SPList EnsureListByUrl(this SPWeb web, string title, string webRelativeUrl, SPListTemplateType templateType) {
      try {
        return web.GetListSafe(SPUrlUtility.CombineUrl(web.ServerRelativeUrl, webRelativeUrl));
      } catch (FileNotFoundException) {
        Guid listId = web.Lists.Add(title, String.Empty, webRelativeUrl, String.Empty, (int)templateType, "100");
        return web.Lists[listId];
      }
    }

    /// <summary>
    /// Creates a list under the specified site if no list exists at the given URL.
    /// </summary>
    /// <param name="web">A site object.</param>
    /// <param name="title">Title of the list to create.</param>
    /// <param name="webRelativeUrl">URL of the list to create at.</param>
    /// <param name="templateType">List template.</param>
    /// <returns>Existing or newly created list object.</returns>
    public static SPList EnsureListByUrl(this SPWeb web, string title, string webRelativeUrl, SPListTemplate templateType) {
      try {
        return web.GetListSafe(SPUrlUtility.CombineUrl(web.ServerRelativeUrl, webRelativeUrl));
      } catch (FileNotFoundException) {
        Guid listId = web.Lists.Add(title, String.Empty, webRelativeUrl, templateType.FeatureId.ToString(), (int)templateType.Type, templateType.DocumentTemplate);
        return web.Lists[listId];
      }
    }

    /// <summary>
    /// Creates a sub-folder of the specified name if it does not exists.
    /// </summary>
    /// <param name="folder">Parent folder.</param>
    /// <param name="name">Name of sub-folder to create.</param>
    /// <returns>Existing or newly created folder object.</returns>
    public static SPFolder EnsureSubFolder(this SPFolder folder, string name) {
      try {
        return folder.SubFolders[name];
      } catch (ArgumentException) {
        return folder.SubFolders.Add(name);
      }
    }

    /// <summary>
    /// Gets an <see cref="SPFile"/> or <see cref="SPFolder"/> object at the specfied URL.
    /// </summary>
    /// <param name="site">A site collection.</param>
    /// <param name="strUrl">Server-relatve or site-collection-relative URL.</param>
    /// <returns>An <see cref="SPFile"/> or <see cref="SPFolder"/> object, or *null* if the specified URL does not exist.</returns>
    public static object GetFileOrFolder(this SPSite site, string strUrl) {
      CommonHelper.ConfirmNotNull(strUrl, "strUrl");
      SPWeb currentWeb = site.RootWeb;
      if (strUrl.StartsWith(currentWeb.ServerRelativeUrl, StringComparison.OrdinalIgnoreCase)) {
        strUrl = strUrl.Substring(currentWeb.ServerRelativeUrl.Length).TrimStart('/');
      } else if (strUrl.StartsWith(currentWeb.Url, StringComparison.OrdinalIgnoreCase)) {
        strUrl = strUrl.Substring(currentWeb.Url.Length).TrimStart('/');
      }
      foreach (string segment in strUrl.Split('/')) {
        SPWeb childWeb = currentWeb.GetSubWebByNameSafe(segment);
        if (childWeb != null) {
          currentWeb = childWeb;
        } else {
          break;
        }
      }
      try {
        return currentWeb.GetFileOrFolderObject(SPUrlUtility.CombineUrl(site.ServerRelativeUrl, strUrl));
      } catch (FileNotFoundException) {
        return null;
      }
    }

    /// <summary>
    /// Iterates all files under the specified folder.
    /// </summary>
    /// <param name="folder">An <see cref="SPFolder"/> object.</param>
    /// <returns>An enumerable of all files under the specified folder.</returns>
    public static IEnumerable<SPFile> GetAllFiles(this SPFolder folder) {
      foreach (SPFile file in folder.Files) {
        yield return file;
      }
      foreach (SPFolder subFolder in folder.SubFolders) {
        foreach (SPFile file in subFolder.GetAllFiles()) {
          yield return file;
        }
      }
    }

    /// <summary>
    /// Ensures the specified list item are approved. If the list item is under list folders, all parent folders are also approved.
    /// </summary>
    /// <param name="item">A list item.</param>
    public static void EnsureApproved(this SPListItem item) {
      if (item.ModerationInformation != null && item.ModerationInformation.Status != SPModerationStatusType.Approved) {
        item.ModerationInformation.Status = SPModerationStatusType.Approved;
        item.Update();
      }
      SPFolder folder;
      if (item.Folder != null) {
        folder = item.Folder.ParentFolder;
      } else {
        folder = item.File.ParentFolder;
      }
      if (folder != null && folder.ParentListId != Guid.Empty && folder.Item != null) {
        folder.Item.EnsureApproved();
      }
    }

    /// <summary>
    /// Ensures the specified folder and all parent folders are approved.
    /// </summary>
    /// <param name="folder">An <see cref="SPFolder"/> object.</param>
    public static void EnsureApproved(this SPFolder folder) {
      if (folder.Item != null) {
        folder.Item.EnsureApproved();
      }
    }

    /// <summary>
    /// Ensures the specified file is published.
    /// </summary>
    /// <param name="file">An <see cref="SPFile"/> object.</param>
    /// <param name="comment">Comment message.</param>
    public static void EnsurePublished(this SPFile file, string comment) {
      new SPFileCheckOutScope(file, false, true, comment).Dispose();
    }

    /// <summary>
    /// Ensures the specified file is checked out to the current user before performing edit operation, and optionally publish the file on dispose.
    /// </summary>
    /// <param name="file">An <see cref="SPFile"/> object.</param>
    /// <param name="publishOnDispose">Whether to publish the file on dispose.</param>
    /// <returns>An <see cref="IDisposable"/> object.</returns>
    public static IDisposable GetCheckOutScope(this SPFile file, bool publishOnDispose) {
      return new SPFileCheckOutScope(file, true, publishOnDispose, null);
    }

    /// <summary>
    /// Enables scheduled publishing on the given list.
    /// </summary>
    /// <param name="targetList">List object.</param>
    /// <exception cref="System.MissingMethodException">Throws when the private static method ScheduledItem.RegisterSchedulingEventOnList does not exist.</exception>
    public static void EnableScheduledPublishing(this SPList targetList) {
      MethodInfo method = typeof(ScheduledItem).GetMethod("RegisterSchedulingEventOnList", true);
      if (method == null) {
        throw new MissingMethodException("RegisterSchedulingEventOnList");
      }
      method.Invoke<object>(null, targetList);
    }

    /// <summary>
    /// Determines whether a event receiver of the specified class is registered.
    /// If it does not, registers a event receiver with the given event receiver class.
    /// </summary>
    /// <param name="collection"></param>
    /// <param name="receiverType"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    /// <exception cref="System.ArgumentNullException">Throws when input parameter <paramref name="type"/> is null.</exception>
    public static SPEventReceiverDefinition EnsureEventReceiver(this SPEventReceiverDefinitionCollection collection, SPEventReceiverType receiverType, Type type) {
      return EnsureEventReceiver(collection, receiverType, type, SPEventReceiverSynchronization.Default);
    }

    /// <summary>
    /// Determines whether a event receiver of the specified class is registered.
    /// If it does not, registers a event receiver with the given event receiver class.
    /// </summary>
    /// <param name="collection"></param>
    /// <param name="receiverType"></param>
    /// <param name="type"></param>
    /// <param name="synchronization"></param>
    /// <returns></returns>
    /// <exception cref="System.ArgumentNullException">Throws when input parameter <paramref name="type"/> is null.</exception>
    public static SPEventReceiverDefinition EnsureEventReceiver(this SPEventReceiverDefinitionCollection collection, SPEventReceiverType receiverType, Type type, SPEventReceiverSynchronization synchronization) {
      CommonHelper.ConfirmNotNull(type, "type");
      IDisposable unsafeUpdatesScope = null;
      if (collection.HostType == SPEventHostType.Site && collection.Site.AllowUnsafeUpdates) {
        unsafeUpdatesScope = new SPWebAllowUnsafeUpdatesScope(collection.Web);
      }
      try {
        SPEventReceiverDefinition receiver = collection.OfType<SPEventReceiverDefinition>().FirstOrDefault(v => v.Type == receiverType && v.Assembly == type.Assembly.FullName && v.Class == type.FullName);
        if (receiver != null && synchronization != SPEventReceiverSynchronization.Default && receiver.Synchronization != synchronization) {
          receiver.Delete();
          receiver = null;
        }
        if (receiver == null) {
          receiver = collection.Add();
          receiver.Type = receiverType;
          receiver.Assembly = type.Assembly.FullName;
          receiver.Class = type.FullName;
          receiver.Synchronization = synchronization;
          receiver.Update();
        }
        return receiver;
      } finally {
        if (unsafeUpdatesScope != null) {
          unsafeUpdatesScope.Dispose();
        }
      }
    }

    /// <summary>
    /// Gets the workflow association with the given workflow GUID from a workflow association collection.
    /// </summary>
    /// <param name="collection">Workflow association collection object.</param>
    /// <param name="workflowBaseId">The GUID of a workflow.</param>
    /// <returns>The workflow association object that associates the specified workflow.</returns>
    public static SPWorkflowAssociation GetAssociationByBaseIDSafe(this SPWorkflowAssociationCollection collection, Guid workflowBaseId) {
      IEnumerable<SPWorkflowAssociation> wfAssoc = collection.OfType<SPWorkflowAssociation>().Where(v => v.BaseId == workflowBaseId).OrderByDescending(v => v.Enabled);
      return wfAssoc.FirstOrDefault();
    }

    /// <summary>
    /// Determines whether a workflow with the given GUID is associated with the given list.
    /// If it does not, associates that workflow with the given list.
    /// </summary>
    /// <param name="list">List object.</param>
    /// <param name="workflowBaseId">The GUID of a workflow.</param>
    /// <returns>The workflow association object that associates the specified workflow and the given list.</returns>
    public static SPWorkflowAssociation EnsureWorkflowAssociation(this SPList list, Guid workflowBaseId) {
      bool associationUpdated;
      SPWorkflowAssociation wfAssoc = SPExtensionHelper.EnsureWorkflowAssociation(list.WorkflowAssociations, workflowBaseId, new SPExtensionHelper.SPWorkflowAssociationCreator(SPWorkflowAssociation.CreateListAssociation), out associationUpdated);
      return wfAssoc;
    }

    /// <summary>
    /// Determines whether a workflow with the given GUID is associated with the given content type.
    /// If it does not, associates that workflow with the given content type.
    /// </summary>
    /// <param name="contentType">Content type object.</param>
    /// <param name="workflowBaseId">The GUID of a workflow.</param>
    /// <returns>The workflow association object that associates the specified workflow and the given content type.</returns>
    public static SPWorkflowAssociation EnsureWorkflowAssociation(this SPContentType contentType, Guid workflowBaseId) {
      bool associationUpdated;
      SPExtensionHelper.SPWorkflowAssociationCreator createDelegate;
      if (contentType.ParentList != null) {
        createDelegate = new SPExtensionHelper.SPWorkflowAssociationCreator(SPWorkflowAssociation.CreateListContentTypeAssociation);
      } else {
        createDelegate = new SPExtensionHelper.SPWorkflowAssociationCreator((w, n, t, h) => SPWorkflowAssociation.CreateWebContentTypeAssociation(w, n, t.Title, h.Title));
      }
      SPWorkflowAssociation wfAssoc = SPExtensionHelper.EnsureWorkflowAssociation(contentType.WorkflowAssociations, workflowBaseId, createDelegate, out associationUpdated);
      if (associationUpdated) {
        contentType.UpdateWorkflowAssociationsOnChildren(true, true, true, false);
      }
      return wfAssoc;
    }

    /// <summary>
    /// Starts the specified workflow on the list item that is associated to the parent list.
    /// </summary>
    /// <param name="listItem">A list item to start workflow with.</param>
    /// <param name="workflowBaseId">A GUID specifying the workflow.</param>
    /// <param name="data">A string containing custom data.</param>
    public static void StartWorkflow(this SPListItem listItem, Guid workflowBaseId, string data) {
      SPWorkflowAssociation assoc = listItem.ParentList.WorkflowAssociations.GetAssociationByBaseIDSafe(workflowBaseId);
      if (assoc == null) {
        throw new ArgumentOutOfRangeException("workflowBaseId", "Specified workflow is not associated with this list.");
      }
      SPWorkflowManager manager = listItem.Web.Site.WorkflowManager;
      manager.StartWorkflow(listItem, assoc, data, SPWorkflowRunOptions.Synchronous);
      foreach (SPWorkflow wf in manager.GetItemActiveWorkflows(listItem)) {
        if (wf.ParentAssociation.BaseId == workflowBaseId) {
          return;
        }
      }
      SPWorkflow workflow = manager.GetItemWorkflows(listItem, new SPWorkflowFilter(SPWorkflowState.Cancelled, SPWorkflowState.None))[0];
      SPQuery query = new SPQuery();
      query.Query = Caml.And(
        Caml.Equals(SPBuiltInFieldName.WorkflowInstance, workflow.InstanceId.ToString("B")),
        Caml.Equals(SPBuiltInFieldName.Event, (int)SPWorkflowHistoryEventType.WorkflowError)).ToString();
      SPListItemCollection collection = workflow.HistoryList.GetItems(query);
      if (collection.Count > 0) {
        throw new Exception("Workflow failed to start with the following error: " + collection[0][SPBuiltInFieldName.Description]);
      }
      throw new Exception("Workflow failed to start.");
    }

    /// <summary>
    /// Gets the user or group with the specified member ID.
    /// </summary>
    /// <param name="web">Site object.</param>
    /// <param name="id">Member ID.</param>
    /// <returns>A <see cref="SPPrincipal"/> object that represents either the specified user or group; -or- *null* if there is no member with the specified member ID.</returns>
    public static SPPrincipal GetSiteMemberByID(this SPWeb web, int id) {
      try {
        return web.SiteUsers.GetByID(id);
      } catch { }
      try {
        return web.SiteGroups.GetByID(id);
      } catch { }
      return null;
    }

    /// <summary>
    /// Determines whether a SharePoint group with the given name exists.
    /// If it does not, creates a new SharePoint group with the given name.
    /// </summary>
    /// <param name="web">Site object.</param>
    /// <param name="name">Group name.</param>
    /// <returns>SharePoint group object with the given name.</returns>
    /// <exception cref="System.ArgumentNullException">Throws when input parameter <paramref name="name"/> is null.</exception>
    public static SPGroup EnsureGroup(this SPWeb web, string name) {
      CommonHelper.ConfirmNotNull(name, "name");
      try {
        return web.SiteGroups[name];
      } catch (SPException) {
        web.SiteGroups.Add(name, web.CurrentUser, null, String.Empty);
        return web.SiteGroups[name];
      }
    }

    /// <summary>
    /// Determines whether a role with the given name exists and its permissions is set to be the given set.
    /// If it does not, creates a new role with the given name and/or sets its permissions to the specified set.
    /// </summary>
    /// <param name="site">Site collection object.</param>
    /// <param name="name">Role name.</param>
    /// <param name="permissions">Permission granted for this role.</param>
    /// <returns>Role definition name with the given name.</returns>
    /// <exception cref="System.ArgumentNullException">Throws when input parameter <paramref name="name"/> is null.</exception>
    public static SPRoleDefinition EnsureCustomRoleDefinition(this SPSite site, string name, SPBasePermissions permissions) {
      CommonHelper.ConfirmNotNull(name, "name");
      try {
        SPRoleDefinition definition = site.RootWeb.RoleDefinitions[name];
        if (definition.BasePermissions != permissions) {
          definition.BasePermissions = permissions;
          definition.Update();
        }
        return definition;
      } catch (SPException) {
        SPRoleDefinition customDefinition = new SPRoleDefinition();
        customDefinition.Name = name;
        customDefinition.BasePermissions = permissions;
        site.RootWeb.RoleDefinitions.Add(customDefinition);
        site.RootWeb.Update();
        return site.RootWeb.RoleDefinitions[name];
      }
    }

    /// <summary>
    /// Determines whether a role with the given name exists and its permissions is set to be the given set.
    /// If it does not, creates a new role with the given name and/or sets its permissions to the specified set.
    /// The associated permission set is the union of that of built-in permission role and the additional permissions specified by <paramref name="permissions"/>.
    /// </summary>
    /// <param name="site">Site collection object.</param>
    /// <param name="name">Role name.</param>
    /// <param name="roleType">Built-in role which its permission set is inherited.</param>
    /// <param name="permissions">Additional permissions.</param>
    /// <returns>Role definition name with the given name.</returns>
    /// <exception cref="System.ArgumentNullException">Throws when input parameter <paramref name="name"/> is null.</exception>
    public static SPRoleDefinition EnsureCustomRoleDefinition(this SPSite site, string name, SPRoleType roleType, SPBasePermissions permissions) {
      SPRoleDefinition definition = site.RootWeb.RoleDefinitions.GetByType(roleType);
      return site.EnsureCustomRoleDefinition(name, definition.BasePermissions | permissions);
    }

    /// <summary>
    /// Grants specified role to a user for a given object.
    /// </summary>
    /// <param name="obj">Securable object which permission can be uniquely assigned.</param>
    /// <param name="principal">User object.</param>
    /// <param name="role">Role to be granted.</param>
    /// <exception cref="System.ArgumentNullException">Throws when input parameter <paramref name="principal"/> or <paramref name="role"/> is null.</exception>
    public static void GrantPermission(this SPSecurableObject obj, SPPrincipal principal, SPRoleDefinition role) {
      obj.GrantPermission(principal, role, false);
    }

    /// <summary>
    /// Grants specified role to a user for a given object, optionally removes existing roles.
    /// </summary>
    /// <param name="obj">Securable object which permission can be uniquely assigned.</param>
    /// <param name="principal">User object.</param>
    /// <param name="role">Role to be granted.</param>
    /// <param name="removeExistingPermissions">Whether to remove existing roles from the user.</param>
    /// <exception cref="System.ArgumentNullException">Throws when input parameter <paramref name="principal"/> or <paramref name="role"/> is null.</exception>
    public static void GrantPermission(this SPSecurableObject obj, SPPrincipal principal, SPRoleDefinition role, bool removeExistingPermissions) {
      CommonHelper.ConfirmNotNull(principal, "principal");
      CommonHelper.ConfirmNotNull(role, "role");

      if (!obj.HasUniqueRoleAssignments) {
        obj.BreakRoleInheritance(true);
      }
      SPRoleAssignment assignment = null;
      try {
        assignment = obj.RoleAssignments.GetAssignmentByPrincipal(principal);
        if (removeExistingPermissions) {
          assignment.RoleDefinitionBindings.RemoveAll();
        }
      } catch (ArgumentException) {
        assignment = new SPRoleAssignment(principal);
        assignment.RoleDefinitionBindings.Add(role);
        obj.RoleAssignments.Add(assignment);
        return;
      }
      if (!assignment.RoleDefinitionBindings.Contains(role)) {
        assignment.RoleDefinitionBindings.Add(role);
        assignment.Update();
      }
    }

    /// <summary>
    /// Removes specified role from a user for a given object.
    /// </summary>
    /// <param name="obj">Securable object which permission can be uniquely assigned.</param>
    /// <param name="principal">User object.</param>
    /// <param name="role">Role to be removed.</param>
    /// <exception cref="System.ArgumentNullException">Throws when input parameter <paramref name="principal"/> or <paramref name="role"/> is null.</exception>
    public static void RemovePermission(this SPSecurableObject obj, SPPrincipal principal, SPRoleDefinition role) {
      CommonHelper.ConfirmNotNull(principal, "principal");
      CommonHelper.ConfirmNotNull(role, "role");

      if (!obj.HasUniqueRoleAssignments) {
        obj.BreakRoleInheritance(true);
      }
      SPRoleAssignment assignment = null;
      try {
        assignment = obj.RoleAssignments.GetAssignmentByPrincipal(principal);
      } catch (ArgumentException) {
        return;
      }
      if (assignment.RoleDefinitionBindings.Contains(role)) {
        assignment.RoleDefinitionBindings.Remove(role);
        assignment.Update();
        if (assignment.RoleDefinitionBindings.Count == 0) {
          obj.RoleAssignments.Remove(principal);
        }
      }
    }

    /// <summary>
    /// Copies permissions from one securable object to another.
    /// </summary>
    /// <param name="obj">Source object of which permissions to be copied.</param>
    /// <param name="other">Target object to which permissions are copied.</param>
    public static void CopyPermissions(this SPSecurableObject obj, SPSecurableObject other) {
      CommonHelper.ConfirmNotNull(other, "other");
      if (!other.HasUniqueRoleAssignments) {
        other.BreakRoleInheritance(true);
      }
      foreach (SPRoleAssignment assignment in other.RoleAssignments) {
        foreach (SPRoleDefinition role in assignment.RoleDefinitionBindings.OfType<SPRoleDefinition>().ToArray()) {
          if (role.Name != "Limited Access") {
            assignment.RoleDefinitionBindings.Remove(role);
          }
        }
      }
      foreach (SPRoleAssignment assignment in obj.RoleAssignments) {
        foreach (SPRoleDefinition role in assignment.RoleDefinitionBindings) {
          if (role.Name != "Limited Access") {
            other.GrantPermission(assignment.Member, role);
          }
        }
      }
    }

    /// <summary>
    /// Removes role assignments that only contains the *Limited Access* role.
    /// </summary>
    /// <param name="collection">Role assignment collection object.</param>
    public static void RemoveLimitedAccessBindings(this SPRoleAssignmentCollection collection) {
      List<SPPrincipal> membersToRemove = new List<SPPrincipal>();
      foreach (SPRoleAssignment assignment in collection) {
        if (assignment.RoleDefinitionBindings.Count == 0 || (assignment.RoleDefinitionBindings.Count == 1 && assignment.RoleDefinitionBindings[0].Name == "Limited Access")) {
          membersToRemove.Add(assignment.Member);
        }
      }
      foreach (SPPrincipal member in membersToRemove) {
        collection.Remove(member);
      }
    }

    /// <summary>
    /// Computes the permissions granted by a given role assignment.
    /// </summary>
    /// <param name="assignment">Role assignment object.</param>
    /// <returns>Permission granted by the given role assignment.</returns>
    public static SPBasePermissions GetEffectivePermissions(this SPRoleAssignment assignment) {
      SPBasePermissions permissions = SPBasePermissions.EmptyMask;
      foreach (SPRoleDefinition definition in assignment.RoleDefinitionBindings) {
        permissions |= definition.BasePermissions;
      }
      return permissions;
    }

    /// <summary>
    /// Gets a collection of users or groups that are granted the specified permissions on the specified securable object.
    /// </summary>
    /// <param name="obj">Securable object.</param>
    /// <param name="permissions">A bitmask value representing required permissions.</param>
    /// <returns>A enumerable collection of users or groups that are granted the specified permissions.</returns>
    public static IEnumerable<SPPrincipal> GetMembersWithPermissions(this SPSecurableObject obj, SPBasePermissions permissions) {
      foreach (SPRoleAssignment assignment in obj.RoleAssignments) {
        if (assignment.GetEffectivePermissions().HasFlag(permissions)) {
          yield return assignment.Member;
        }
      }
    }

    /// <summary>
    /// Determines whether current logon user is the specified user or belongs to the specified SharePoint/AD group.
    /// </summary>
    /// <param name="member">SharePoint user or group.</param>
    /// <returns>*true* if the current logon user is the specified user or belongs to the specified SharePoint/AD group.</returns>
    public static bool IsMembershipOfCurrentUser(this SPPrincipal member) {
      if (member.ParentWeb.CurrentUser != null) {
        if (member is SPUser) {
          if (!((SPUser)member).IsDomainGroup) {
            return member.ParentWeb.CurrentUser.ID == member.ID;
          }
          return member.ParentWeb.IsCurrentUserMemberOfGroup(member.ID);
        }
        return ((SPGroup)member).ContainsCurrentUser;
      }
      return false;
    }

    /// <summary>
    /// Determines whether a field is built-in.
    /// </summary>
    /// <param name="field">Field object to be checked.</param>
    /// <returns>*true* if the given field is built-in.</returns>
    public static bool IsBuiltIn(this SPField field) {
      return !GuidRegex.IsMatch(field.SourceId);
    }

    /// <summary>
    /// Determines whether a field is system built-in, by checking the ColName in SchemaXml 
    /// </summary>
    /// <param name="field">Field object to be checked.</param>
    /// <returns>*true* if the given field is system field (not user data).</returns>
    public static bool IsSystemField(this SPField field) {
      XmlDocument xd = new XmlDocument();
      xd.LoadXml(field.SchemaXml);
      XmlAttribute xa = xd.DocumentElement.Attributes["ColName"];
      return (xa == null || !UserDataFieldColNameRegex.IsMatch(xa.Value));
    }

    /// <summary>
    /// Sets the anchor <see cref="Term"/> or <see cref="TermSet"/> object for a <see cref="TaxonomyField"/> object.
    /// </summary>
    /// <param name="field">Taxonomy field object.</param>
    /// <param name="term">Term or term set object.</param>
    /// <exception cref="System.ArgumentNullException">Throws when input parameter <paramref name="term"/> is null.</exception>
    public static void SetTermAnchor(this TaxonomyField field, TermSetItem term) {
      CommonHelper.ConfirmNotNull(term, "term");
      field.SspId = term.TermStore.Id;
      field.TermSetId = (term is Term) ? ((Term)term).TermSet.Id : term.Id;
      field.AnchorId = (term is Term) ? term.Id : Guid.Empty;
      field.TargetTemplate = String.Empty;
      field.Update();
    }

    /// <summary>
    /// Gets the anchor <see cref="Term"/> or <see cref="TermSet"/> object for a <see cref="TaxonomyField"/> object.
    /// </summary>
    /// <param name="field">Taxonomy field object.</param>
    /// <param name="session">Taxonomy session object.</param>
    /// <returns>The anchor term or term set.</returns>
    /// <exception cref="System.ArgumentNullException">Throws when input parameter <paramref name="session"/> is null.</exception>
    public static TermSetItem GetTermAnchor(this TaxonomyField field, TaxonomySession session) {
      CommonHelper.ConfirmNotNull(session, "session");
      try {
        TermStore termStore = session.TermStores[field.SspId];
        TermSet termSet = termStore.GetTermSet(field.TermSetId);
        if (termSet != null) {
          if (field.AnchorId != Guid.Empty) {
            return termSet.GetTerm(field.AnchorId);
          }
          return termSet;
        }
      } catch (ArgumentOutOfRangeException) {
      }
      return null;
    }

    /// <summary>
    /// Updated values of the given taxonomy field.
    /// </summary>
    /// <param name="taxonomyField">Taxonomy field object.</param>
    public static void UpdateTaxonomyFieldValue(this TaxonomyField taxonomyField) {
      SPWeb parentWeb = typeof(SPField).GetProperty("Web", true).GetValue<SPWeb>(taxonomyField);
      TaxonomySession session = new TaxonomySession(parentWeb.Site, true);
      TermSetItem termSetItem = taxonomyField.GetTermAnchor(session);

      if (termSetItem != null) {
        Dictionary<Guid, TaxonomyFieldValue> mappedValues = new Dictionary<Guid, TaxonomyFieldValue>();
        TermSet termSet = CommonHelper.TryCastOrDefault<TermSet>(termSetItem) ?? ((Term)termSetItem).TermSet;

        foreach (SPFieldTemplateUsage usage in taxonomyField.ListsFieldUsedIn()) {
          using (SPWeb usageWeb = parentWeb.Site.OpenWeb(usage.WebID)) {
            SPList usageList = usageWeb.Lists[usage.ListID];
            TaxonomyField listField = (TaxonomyField)usageList.Fields[taxonomyField.Id];
            foreach (SPListItem item in usageList.Items) {
              bool fieldUpdated = false;
              if (taxonomyField.AllowMultipleValues) {
                TaxonomyFieldValueCollection fieldValues = (TaxonomyFieldValueCollection)item[taxonomyField.Id];
                foreach (TaxonomyFieldValue fieldValue in fieldValues) {
                  fieldUpdated |= SPExtensionHelper.UpdateTaxonomyFieldValue(parentWeb.Site, termSet, fieldValue, mappedValues);
                }
                listField.SetFieldValue(item, fieldValues);
              } else if (item[taxonomyField.Id] != null) {
                TaxonomyFieldValue fieldValue = (TaxonomyFieldValue)item[taxonomyField.Id];
                fieldUpdated |= SPExtensionHelper.UpdateTaxonomyFieldValue(parentWeb.Site, termSet, fieldValue, mappedValues);
                listField.SetFieldValue(item, fieldValue);
              }
              if (fieldUpdated) {
                try {
                  item.SystemUpdate(false);
                } catch {
                }
              }
            }
          }
        }
      }
    }

    /// <summary>
    /// Determines whether a <see cref="TermSet"/> object with the given GUID exists in the term store.
    /// If it does not, it creates a term set with the given term set name under the specified group.
    /// </summary>
    /// <param name="termStore">Term store object.</param>
    /// <param name="uniqueId">Term set GUID.</param>
    /// <param name="groupName">Default group name.</param>
    /// <param name="termSetName">Default term set name.</param>
    /// <returns>The <see cref="TermSet"/> object with the given GUID.</returns>
    /// <exception cref="System.ArgumentNullException">Throws when input parameter <paramref name="groupName"/> or <paramref name="termSetName"/> is null.</exception>
    public static TermSet EnsureTermSet(this TermStore termStore, Guid uniqueId, string groupName, string termSetName) {
      CommonHelper.ConfirmNotNull(groupName, "groupName");
      CommonHelper.ConfirmNotNull(termSetName, "termSetName");

      Group matchedGroup;
      TermSet matchedTermSet = termStore.GetTermSet(uniqueId);

      if (matchedTermSet != null) {
        return matchedTermSet;
      }
      try {
        matchedGroup = termStore.Groups[TaxonomyItem.NormalizeName(groupName)];
      } catch (ArgumentOutOfRangeException) {
        matchedGroup = termStore.CreateGroup(groupName);
        matchedTermSet = matchedGroup.CreateTermSet(termSetName, uniqueId, termStore.DefaultLanguage);
        termStore.CommitAll();
        return matchedTermSet;
      }
      try {
        TermSet existingTermSet = matchedGroup.TermSets[TaxonomyItem.NormalizeName(termSetName)];
        matchedTermSet = matchedGroup.CreateTermSet(String.Concat(termSetName, uniqueId.ToString("B")), uniqueId, termStore.DefaultLanguage);
      } catch (ArgumentOutOfRangeException) {
        matchedTermSet = matchedGroup.CreateTermSet(termSetName, uniqueId, termStore.DefaultLanguage);
      }
      termStore.CommitAll();
      return matchedTermSet;
    }

    /// <summary>
    /// Determines whether a <see cref="Term"/> object with the given label exists under a <see cref="TermSet"/> object.
    /// If it does not, it creates a term with the given label.
    /// </summary>
    /// <param name="termSet">Taxonomy term set object.</param>
    /// <param name="label">Label to match.</param>
    /// <returns>The <see cref="Term"/> object matching the condition.</returns>
    /// <exception cref="System.ArgumentNullException">Throws when input parameter <paramref name="label"/> is null.</exception>
    public static Term EnsureTerm(this TermSet termSet, string label) {
      CommonHelper.ConfirmNotNull(label, "label");
      TermCollection matchedTerms = termSet.GetTerms(label, termSet.TermStore.DefaultLanguage, false);
      if (matchedTerms.Count > 0) {
        return matchedTerms[0];
      }
      Term term = termSet.CreateTerm(label, termSet.TermStore.DefaultLanguage);
      termSet.TermStore.CommitAll();
      return term;
    }

    /// <summary>
    /// Determines whether a <see cref="Term"/> object having the specified custom property and value exists under a <see cref="TermSet"/> object.
    /// If it does not, it creates a term with the given default label.
    /// </summary>
    /// <param name="termSet">Taxonomy term set object.</param>
    /// <param name="key">Custom property name.</param>
    /// <param name="value">Custom property value to match.</param>
    /// <param name="defaultLabel">Default label used in creating new term.</param>
    /// <returns>The <see cref="Term"/> object matching the condition.</returns>
    /// <exception cref="System.ArgumentNullException">Throws when input parameter <paramref name="key"/>, <paramref name="value"/> or <paramref name="defaultLabel"/> is null.</exception>
    public static Term EnsureTermWithCustomProperty(this TermSet termSet, string key, string value, string defaultLabel) {
      CommonHelper.ConfirmNotNull(key, "key");
      CommonHelper.ConfirmNotNull(value, "value");
      CommonHelper.ConfirmNotNull(defaultLabel, "defaultLabel");

      TermCollection matchedTerms = termSet.GetTermsWithCustomProperty(key, value, false);
      if (matchedTerms.Count > 0) {
        return matchedTerms[0];
      }
      Term term = termSet.CreateTerm(defaultLabel, termSet.TermStore.DefaultLanguage);
      term.SetCustomProperty(key, value);
      termSet.TermStore.CommitAll();
      return term;
    }

    /// <summary>
    /// Gets all <see cref="Term"/> objects that are reused from the given term. The given term is also included in the returned list.
    /// </summary>
    /// <param name="term">Taxonomy term object.</param>
    /// <returns>A list of <see cref="Term"/> objects reused across the term store.</returns>
    public static IEnumerable<Term> GetReusedTermsAndSelf(this Term term) {
      yield return term;
      foreach (Term reusedTerm in term.ReusedTerms) {
        yield return reusedTerm;
      }
    }

    /// <summary>
    /// Gets a list of terms which are ancestors of the given <see cref="Term"/> object. The given term is also included in the returned list.
    /// </summary>
    /// <param name="term">Taxonomy term object.</param>
    /// <returns>A list of terms.</returns>
    public static IList<Term> GetAncestorsAndSelf(this Term term) {
      Stack<Term> stack = new Stack<Term>();
      for (; term != null; term = term.Parent) {
        stack.Push(term);
      }
      return stack.ToArray();
    }

    /// <summary>
    /// Gets the lookup ID for a <see cref="Term"/> object under a specified site collection, 
    /// optionally along with the lookup IDs for descendant terms.
    /// </summary>
    /// <param name="term">Taxonomy term object.</param>
    /// <param name="site">Site collection object.</param>
    /// <param name="includeDescendants">Whether to include lookup IDs for descendant terms</param>
    /// <returns>A list of lookup IDs.</returns>
    /// <exception cref="System.ArgumentNullException">Throws when input parameter <paramref name="site"/> is null.</exception>
    public static IList<int> GetWssIds(this Term term, SPSite site, bool includeDescendants) {
      CommonHelper.ConfirmNotNull(site, "site");
      int[] result = TaxonomyField.GetWssIdsOfTerm(site, term.TermStore.Id, term.TermSet.Id, term.Id, includeDescendants, 1024);
      if (result.Length == 0) {
        SPQuery query = new SPQuery { RowLimit = 1, Query = Caml.Equals("IdForTerm", term.Id).ToString() };
        SPList taxonomyHiddenList = SPExtensionHelper.GetTaxonomyHiddenList(site);
        if (taxonomyHiddenList.GetItems(query).Count > 0) {
          TaxonomySession session = new TaxonomySession(site);
          session.TermStores[term.TermStore.Id].FlushCache();
          return TaxonomyField.GetWssIdsOfTerm(site, term.TermStore.Id, term.TermSet.Id, term.Id, includeDescendants, 1024);
        }
      }
      return result;
    }

    /// <summary>
    /// Gets the lookup ID for a <see cref="Term"/> object under a specified site collection.
    /// If there is no lookup ID corresponds to the term, a new one is created.
    /// </summary>
    /// <param name="term">Taxonomy term object.</param>
    /// <param name="site">Site collection object.</param>
    /// <param name="isKeywordField">Whether the term is used under the Enterprise Keyword column.</param>
    /// <returns>Lookup ID of the given term.</returns>
    /// <exception cref="System.ArgumentNullException">Throws when input parameter <paramref name="site"/> is null.</exception>
    /// <exception cref="System.MissingMethodException">Throws when the private static method TaxonomyField.AddTaxonomyGuidToWss does not exist.</exception>
    public static int EnsureWssId(this Term term, SPSite site, bool isKeywordField) {
      CommonHelper.ConfirmNotNull(site, "site");
      IList<int> wssId = term.GetWssIds(site, false);
      if (wssId.Count > 0) {
        return wssId[0];
      }
      MethodInfo addTaxonomyGuidToWss = typeof(TaxonomyField).GetMethod("AddTaxonomyGuidToWss", true);
      if (addTaxonomyGuidToWss == null) {
        throw new MissingMethodException("TaxonomyField", "AddTaxonomyGuidToWss");
      }
      return addTaxonomyGuidToWss.Invoke<int>(null, site, term, isKeywordField);
    }

    /// <summary>
    /// Gets the term associated with the given lookup ID for the specified site collection.
    /// </summary>
    /// <param name="termStore">Term store object.</param>
    /// <param name="site">Site collection object.</param>
    /// <param name="wssId">Looup ID.</param>
    /// <returns></returns>
    public static Term GetTermByWssId(this TermStore termStore, SPSite site, int wssId) {
      CommonHelper.ConfirmNotNull(site, "site");
      SPList taxonomyHiddenList = SPExtensionHelper.GetTaxonomyHiddenList(site);
      SPListItem wssItem;
      try {
        wssItem = taxonomyHiddenList.GetItemById(wssId);
      } catch (ArgumentException) {
        return null;
      }
      Guid termSetId = new Guid(wssItem["IdForTermSet"].ToString());
      TermSet termSet = termStore.GetTermSet(termSetId);
      if (termSet != null) {
        Guid termId = new Guid(wssItem["IdForTerm"].ToString());
        return termSet.GetTerm(termId);
      }
      return null;
    }

    /// <summary>
    /// Write verbose information to ULS log.
    /// </summary>
    /// <param name="diagnosticsService">An <see cref="SPDiagnosticsService"/> instance.</param>
    /// <param name="traceCategory">Trace category.</param>
    /// <param name="message">Message to be logged.</param>
    public static void WriteTrace(this SPDiagnosticsService diagnosticsService, SPDiagnosticsCategory traceCategory, string message) {
      diagnosticsService.WriteTrace(0, traceCategory, traceCategory.DefaultTraceSeverity, message);
    }

    /// <summary>
    /// Write exception information to ULS log. Stack trace of inner exceptions are also logged.
    /// </summary>
    /// <param name="diagnosticsService">An <see cref="SPDiagnosticsService"/> instance.</param>
    /// <param name="traceCategory">Trace category.</param>
    /// <param name="ex">Exception to be logged.</param>
    public static void WriteTrace(this SPDiagnosticsService diagnosticsService, string traceCategory, Exception ex) {
      WriteTrace(diagnosticsService, new SPDiagnosticsCategory(traceCategory, TraceSeverity.Unexpected, EventSeverity.Error), ex);
    }

    /// <summary>
    /// Write exception information to ULS log. Stack trace of inner exceptions are also logged.
    /// </summary>
    /// <param name="diagnosticsService">An <see cref="SPDiagnosticsService"/> instance.</param>
    /// <param name="traceCategory">Trace category.</param>
    /// <param name="ex">Exception to be logged.</param>
    public static void WriteTrace(this SPDiagnosticsService diagnosticsService, SPDiagnosticsCategory traceCategory, Exception ex) {
      Stack<Exception> exceptions = new Stack<Exception>();
      for (Exception innerEx = ex; innerEx != null; innerEx = innerEx.InnerException) {
        exceptions.Push(innerEx);
      }
      while (exceptions.Count > 0) {
        Exception innerEx = exceptions.Pop();
        diagnosticsService.WriteTrace(0, traceCategory, TraceSeverity.Unexpected, String.Format("{0}: {1} {2}", innerEx.GetType().Name, innerEx.Message, innerEx.StackTrace));
      }
    }
  }
}
