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

    private SPModelUsage(SPList list, SPContentTypeId id, bool maintainReference) {
      CommonHelper.ConfirmNotNull(list, "list");
      this.ContentTypeId = id;
      this.WebId = list.ParentWeb.ID;
      this.ListId = list.ID;
      this.ServerRelativeUrl = list.RootFolder.ServerRelativeUrl;
      if (maintainReference) {
        this.List = list;
      }
    }

    public string ServerRelativeUrl { get; private set; }
    public SPContentTypeId ContentTypeId { get; private set; }
    public Guid WebId { get; private set; }
    public Guid ListId { get; private set; }
    public SPList List { get; private set; }

    public SPModelUsage EnsureList(SPObjectCache objectCache) {
      CommonHelper.ConfirmNotNull(objectCache, "objectCache");
      if (this.List != null || this.ListId == Guid.Empty) {
        return this;
      }
      try {
        using (new SPSecurity.SuppressAccessDeniedRedirectInScope()) {
          SPList list = objectCache.GetList(this.WebId, this.ListId);
          if (list != null) {
            return new SPModelUsage(list, this.ContentTypeId, true);
          }
        }
      } catch { }
      return this;
    }

    public SPModelUsage GetWithoutList() {
      if (this.List == null) {
        return this;
      }
      return new SPModelUsage {
        ContentTypeId = this.ContentTypeId,
        ServerRelativeUrl = this.ServerRelativeUrl,
        WebId = this.WebId,
        ListId = this.ListId
      };
    }

    public bool Equals(SPModelUsage other) {
      if (other != null) {
        return this.ContentTypeId == other.ContentTypeId;
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
      return this.ContentTypeId.GetHashCode();
    }

    public static SPModelUsage Create(SPList list, SPContentTypeId id) {
      CommonHelper.ConfirmNotNull(list, "list");
      SPModelUsage returnValue = new SPModelUsage(list, id, true);
      dictionary.EnsureKeyValue(id, () => returnValue.GetWithoutList());
      foreach (SPContentType contentType in list.ContentTypes) {
        dictionary.EnsureKeyValue(contentType.Id, () => new SPModelUsage(list, contentType.Id, false));
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
            value = SPModelUsage.Create(list, usage.Id).GetWithoutList();
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

    public SPModelUsageCollection(SPSite parentSite, IEnumerable<SPModelUsage> collection)
      : base(new List<SPModelUsage>(collection)) {
      this.parentSite = parentSite;
    }

    public IList<SPList> GetListCollection() {
      SPObjectCache objectCache = new SPObjectCache(parentSite);
      return this.Distinct(ListIdComparer.Default).Select(v => v.EnsureList(objectCache).List).Where(v => v != null).ToArray();
    }

    private class ListIdComparer : IEqualityComparer<SPModelUsage> {
      public static readonly ListIdComparer Default = new ListIdComparer();

      public bool Equals(SPModelUsage x, SPModelUsage y) {
        return x.ListId == y.ListId;
      }

      public int GetHashCode(SPModelUsage obj) {
        return obj.ListId.GetHashCode();
      }
    }
  }
}
