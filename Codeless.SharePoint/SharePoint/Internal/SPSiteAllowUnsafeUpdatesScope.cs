using Microsoft.SharePoint;
using System;

namespace Codeless.SharePoint.Internal {
  internal class SPSiteAllowUnsafeUpdatesScope : IDisposable {
    private readonly SPSite site;
    private readonly bool originalValue;
    private bool disposed = false;

    public SPSiteAllowUnsafeUpdatesScope(SPSite site) {
      CommonHelper.ConfirmNotNull(site, "site");
      this.site = site;
      this.originalValue = site.AllowUnsafeUpdates;
      site.AllowUnsafeUpdates = true;
    }

    public void Dispose() {
      if (!disposed) {
        site.AllowUnsafeUpdates = originalValue;
        disposed = true;
      }
    }
  }
}
