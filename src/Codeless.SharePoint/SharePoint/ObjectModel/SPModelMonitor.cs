using Microsoft.SharePoint;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Codeless.SharePoint.ObjectModel {
  internal class SPModelMonitor<T> : SPChangeMonitor {
    private readonly SPModelDescriptor descriptor;
    private readonly HashSet<Guid> monitoredLists = new HashSet<Guid>();
    private readonly Guid siteId;

    private SPModelMonitor(SPSite site) {
      this.siteId = site.ID;
      this.descriptor = SPModelDescriptor.Resolve(typeof(T));
      foreach (SPModelUsage usage in descriptor.GetUsages(site.RootWeb)) {
        monitoredLists.Add(usage.ListId);
      }
      Initialize(site.ID,
        new SPChangeMonitorFilter(SPChangeObjectType.List, SPChangeFlags.ListContentTypeAdd | SPChangeFlags.ListContentTypeDelete),
        new SPChangeMonitorFilter(SPChangeObjectType.Item, SPChangeFlags.Add | SPChangeFlags.Update | SPChangeFlags.Delete | SPChangeFlags.SystemUpdate));
    }

    public static SPModelMonitor<T> GetMonitor(SPSite site) {
      CommonHelper.ConfirmNotNull(site, "site");
      return GetMonitors<SPModelMonitor<T>>(site.ID).FirstOrDefault() ?? new SPModelMonitor<T>(site);
    }
    
    protected override bool ShouldNotify(SPChangeList change) {
      if (change.ChangeType == SPChangeType.ListContentTypeAdd) {
        if (descriptor.Contains(change.ContentTypeId)) {
          monitoredLists.Add(change.Id);
        }
      }
      return false;
    }

    protected override bool ShouldNotify(SPChangeItem change) {
      return monitoredLists.Contains(change.ListId);
    }
  }
}
