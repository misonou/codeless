using Microsoft.SharePoint;
using System;

namespace Codeless.SharePoint.Internal {
  internal class SPWebAllowUnsafeUpdatesScope : IDisposable {
    private readonly SPWeb web;
    private readonly bool originalValue;
    private bool disposed = false;

    public SPWebAllowUnsafeUpdatesScope(SPWeb web) {
      CommonHelper.ConfirmNotNull(web, "web");
      this.web = web;
      this.originalValue = web.AllowUnsafeUpdates;
      web.AllowUnsafeUpdates = true;
    }

    public void Dispose() {
      if (!disposed) {
        web.AllowUnsafeUpdates = originalValue;
        disposed = true;
      }
    }
  }
}
