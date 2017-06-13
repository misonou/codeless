using Microsoft.SharePoint;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Codeless.SharePoint.ObjectModel {
  [DebuggerDisplay("{ServerRelativeUrl}")]
  internal sealed class SPModelUsage : IEquatable<SPModelUsage> {
    private static readonly ConcurrentDictionary<SPContentTypeId, SPModelUsage> dictionary = new ConcurrentDictionary<SPContentTypeId, SPModelUsage>();

    private SPModelUsage() { }

    private SPModelUsage(SPList list, bool maintainReference) {
      CommonHelper.ConfirmNotNull(list, "list");
      this.WebId = list.ParentWeb.ID;
      this.ListId = list.ID;
      this.ServerRelativeUrl = list.RootFolder.ServerRelativeUrl;
      if (maintainReference) {
        this.List = list;
      }
    }

    public string ServerRelativeUrl { get; private set; }
    public Guid WebId { get; private set; }
    public Guid ListId { get; private set; }
    public SPList List { get; private set; }

    public SPModelUsage EnsureList(SPSite site) {
      CommonHelper.ConfirmNotNull(site, "site");
      if (this.List != null || this.ListId == Guid.Empty) {
        return this;
      }
      SPWeb web = site.TryGetWebForCurrentUser(this.WebId);
      if (web == null) {
        return this;
      }
      try {
        using (new SPSecurity.SuppressAccessDeniedRedirectInScope()) {
          return new SPModelUsage(web.Lists[this.ListId], true);
        }
      } catch (Exception) {
        return this;
      }
    }

    public SPModelUsage GetWithoutList() {
      if (this.List == null) {
        return this;
      }
      return new SPModelUsage {
        ServerRelativeUrl = this.ServerRelativeUrl,
        WebId = this.WebId,
        ListId = this.ListId
      };
    }

    public bool Equals(SPModelUsage other) {
      if (other != null) {
        return WebId == other.WebId && ListId == other.ListId;
      }
      return false;
    }

    public override bool Equals(object obj) {
      SPModelUsage other = CommonHelper.TryCastOrDefault<SPModelUsage>(obj);
      if (other != null) {
        return Equals(other);
      }
      return base.Equals(obj);
    }

    public override int GetHashCode() {
      return WebId.GetHashCode() ^ ListId.GetHashCode();
    }

    public static SPModelUsage Create(SPList list) {
      CommonHelper.ConfirmNotNull(list, "list");
      SPModelUsage returnValue = new SPModelUsage(list, true);
      SPModelUsage item = returnValue.GetWithoutList();
      foreach (SPContentType contentType in list.ContentTypes) {
        dictionary.EnsureKeyValue(contentType.Id, () => item);
      }
      return returnValue;
    }

    public static SPModelUsage Create(SPSite site, SPContentTypeUsage usage) {
      CommonHelper.ConfirmNotNull(site, "site");
      CommonHelper.ConfirmNotNull(usage, "usage");
      if (!usage.IsUrlToList) {
        throw new ArgumentException("Usage must be list content type", "usage");
      }
      SPModelUsage value;
      if (dictionary.TryGetValue(usage.Id, out value)) {
        return value;
      }
      using (SPSite newSite = new SPSite(site.MakeFullUrl(usage.Url), SPUserToken.SystemAccount)) {
        using (SPWeb web = newSite.OpenWeb()) {
          try {
            SPList list = web.GetListSafe(usage.Url);
            value = SPModelUsage.Create(list).GetWithoutList();
          } catch (FileNotFoundException) {
            value = new SPModelUsage {
              ServerRelativeUrl = usage.Url
            };
            dictionary.TryAdd(usage.Id, value);
          }
        }
      }
      return value;
    }
  }

  internal sealed class SPModelUsageCollection : ReadOnlyCollection<SPModelUsage> {
    private readonly SPSite parentSite;

    public SPModelUsageCollection(SPSite parentSite, IList<SPModelUsage> collection)
      : base(collection) {
      this.parentSite = parentSite;
    }

    public IList<SPList> GetListCollection() {
      return this.Select(v => v.EnsureList(parentSite).List).Where(v => v != null).ToArray();
    }
  }
}
