using Codeless.SharePoint.Internal;
using Microsoft.SharePoint;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Timers;
using System.Web.Caching;
using System.Collections.ObjectModel;

namespace Codeless.SharePoint {
  /// <summary>
  /// Establishes a dependency relationship between an item stored in an ASP.NET application's <see cref="Cache"/> object, and a SharePoint list.
  /// When a list item is created, updated or deleted under the supplied list, or the list itself is deleted, the cached item will be automatically removed.
  /// </summary>
  public class SPListCacheDependency : CacheDependency {
    private readonly SPListChangeMonitor monitor;
    private readonly string uniqueId;

    /// <summary>
    /// Initialize a new instance of the <see cref="SPListCacheDependency"/> class that monitors a SharePoint list.
    /// </summary>
    /// <param name="list">SharePoint list instance.</param>
    /// <exception cref="System.ArgumentNullException">Throws when input parameter <paramref name="list"/> is null.</exception>
    public SPListCacheDependency(SPList list) {
      CommonHelper.ConfirmNotNull(list, "list");
      this.uniqueId = String.Concat(list.ID, list.ParentWeb.ID, list.ParentWeb.Site.ID);
      this.monitor = new SPListChangeMonitor(list);
      monitor.ObjectChanged += OnObjectChanged;
    }

    /// <summary>
    /// Initialize a new instance of the <see cref="SPListCacheDependency"/> class that monitors a SharePoint list, with the specified monitoring interval.
    /// </summary>
    /// <param name="list">SharePoint list instance.</param>
    /// <param name="interval">Monitoring interval.</param>
    /// <exception cref="System.ArgumentNullException">Throws when input parameter <paramref name="list"/> is null.</exception>
    [Obsolete("The interval parameter is no longer supported.")]
    public SPListCacheDependency(SPList list, double interval)
      : this(list) { }

    /// <summary>
    /// Retrieves a unique identifier for a <see cref="CacheDependency"/> object.
    /// </summary>
    /// <returns>The unique identifier for the <see cref="CacheDependency"/> object.</returns>
    public override string GetUniqueID() {
      return uniqueId;
    }

    /// <summary>
    /// Overriden.
    /// </summary>
    protected override void DependencyDispose() {
      base.DependencyDispose();
      monitor.ObjectChanged -= OnObjectChanged;
    }

    private void OnObjectChanged(object sender, SPChangeMonitorEventArgs e) {
      NotifyDependencyChanged(this, EventArgs.Empty);
    }

    private class SPListChangeMonitor : SPChangeMonitor {
      private readonly Guid listId;

      public SPListChangeMonitor(SPList list) {
        this.listId = list.ID;
        Initialize(list.ParentWeb.Site.ID,
          new SPChangeMonitorFilter(SPChangeObjectType.List, SPChangeFlags.Update | SPChangeFlags.Delete),
          new SPChangeMonitorFilter(SPChangeObjectType.Item, SPChangeFlags.Add | SPChangeFlags.Update | SPChangeFlags.SystemUpdate | SPChangeFlags.Delete | SPChangeFlags.Restore | SPChangeFlags.MoveAway | SPChangeFlags.MoveInto | SPChangeFlags.Rename));
      }

      public static SPListChangeMonitor GetMonitor(SPList list) {
        return GetMonitors<SPListChangeMonitor>(list.ParentWeb.Site.ID).FirstOrDefault(v => v.listId == list.ID) ?? new SPListChangeMonitor(list);
      }
      
      protected override bool ShouldNotify(SPChangeList change) {
        return change.Id == listId;
      }

      protected override bool ShouldNotify(SPChangeItem change) {
        return change.ListId == listId;
      }
    }
  }
}
