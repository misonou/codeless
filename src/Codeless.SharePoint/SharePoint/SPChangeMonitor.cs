using Codeless.SharePoint.Internal;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Administration;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace Codeless.SharePoint {
  public enum SPChangeObjectType {
    Alert,
    ContentType,
    Field,
    File,
    Folder,
    Group,
    Item,
    List,
    SecurityPolicy,
    Site,
    User,
    View,
    Web
  }

  [Flags]
  public enum SPChangeFlags {
    None,
    Add = 1,
    Update = 2,
    Delete = 4,
    Rename = 8,
    MoveAway = 16,
    MoveInto = 32,
    Restore = 64,
    RoleAdd = 128,
    RoleDelete = 256,
    RoleUpdate = 512,
    AssignmentAdd = 1024,
    AssignmentDelete = 2048,
    MemberAdd = 4096,
    MemberDelete = 8192,
    SystemUpdate = 16384,
    Navigation = 32768,
    ScopeAdd = 65536,
    ScopeDelete = 131072,
    ListContentTypeAdd = 262144,
    ListContentTypeDelete = 524288,
    All = 0xFFFFFF
  }

  public class SPChangeMonitorEventArgs : EventArgs {
    internal SPChangeMonitorEventArgs(Guid siteId, ReadOnlyCollection<SPAggregatedChange> collection) {
      this.SiteId = siteId;
      this.Changes = collection;
    }

    public Guid SiteId { get; private set; }
    public IReadOnlyCollection<SPAggregatedChange> Changes { get; private set; }
  }

  public class SPChangeMonitorFilter {
    public SPChangeMonitorFilter(SPChangeObjectType objectType, SPChangeFlags flags) {
      this.Bitmask = SPChangeMonitor.GetBitmaskValue(objectType) | (ulong)flags;
    }

    public ulong Bitmask { get; private set; }
  }

  public abstract class SPChangeMonitor : IDisposable {
    private event EventHandler<SPChangeMonitorEventArgs> objectChanged = delegate { };
    private bool disposed;

    public event EventHandler<SPChangeMonitorEventArgs> ObjectChanged {
      add {
        if (disposed) {
          throw new ObjectDisposedException("SPChangeMonitor");
        }
        objectChanged += value;
      }
      remove {
        objectChanged -= value;
      }
    }

    protected internal Guid SiteId { get; private set; }

    internal ICollection<SPChangeMonitorFilter> Filters { get; private set; }

    public static IEnumerable<T> GetMonitors<T>(Guid siteId) where T : SPChangeMonitor {
      return SPChangeQueryExecutor.GetMonitors<T>(siteId);
    }

    public static SPChangeMonitor CreateMonitor(Guid siteId, params SPChangeMonitorFilter[] filters) {
      return new SimpleMonitor(siteId, filters);
    }

    public void Dispose() {
      if (disposed) {
        throw new ObjectDisposedException("SPChangeMonitor");
      }
      try {
        MonitorDispose();
      } finally {
        SPChangeQueryExecutor.RemoveMonitor(this);
        disposed = true;
      }
    }

    protected void Initialize(Guid siteId, params SPChangeMonitorFilter[] filters) {
      this.SiteId = siteId;
      this.Filters = filters;
      SPChangeQueryExecutor.AddMonitor(this);
    }
    
    /// <summary>
    /// When overriden, releases the resources acquired when this monitor is initializing.
    /// </summary>
    protected virtual void MonitorDispose() { }

    #region ShouldNotify
    protected virtual bool ShouldNotify(SPChangeAlert change) {
      return true;
    }

    protected virtual bool ShouldNotify(SPChangeContentType change) {
      return true;
    }

    protected virtual bool ShouldNotify(SPChangeField change) {
      return true;
    }

    protected virtual bool ShouldNotify(SPChangeFile change) {
      return true;
    }

    protected virtual bool ShouldNotify(SPChangeFolder change) {
      return true;
    }

    protected virtual bool ShouldNotify(SPChangeGroup change) {
      return true;
    }

    protected virtual bool ShouldNotify(SPChangeItem change) {
      return true;
    }

    protected virtual bool ShouldNotify(SPChangeList change) {
      return true;
    }

    protected virtual bool ShouldNotify(SPChangeSecurityPolicy change) {
      return true;
    }

    protected virtual bool ShouldNotify(SPChangeUser change) {
      return true;
    }

    protected virtual bool ShouldNotify(SPChangeView change) {
      return true;
    }

    protected virtual bool ShouldNotify(SPChangeWeb change) {
      return true;
    }
    #endregion

    internal void ProcessChanges(ICollection<SPAggregatedChange> collection) {
      CommonHelper.ConfirmNotNull(collection, "collection");
      List<SPAggregatedChange> filteredCollection = new List<SPAggregatedChange>();
      foreach (SPAggregatedChange item in collection) {
        ulong bitmask = GetBitmaskValue(item.ObjectType) | (ulong)item.ChangeFlags;
        if (this.Filters.Any(v => (v.Bitmask & bitmask) == bitmask)) {
          List<SPChange> filteredChanges = new List<SPChange>(item.Where(ShouldNotify));
          if (filteredChanges.Count > 0) {
            filteredCollection.Add(new SPAggregatedChange(filteredChanges));
          }
        }
      }
      if (filteredCollection.Count > 0) {
        SPChangeMonitorEventArgs eventArg = new SPChangeMonitorEventArgs(this.SiteId, filteredCollection.AsReadOnly());
        try {
          objectChanged(this, eventArg);
        } catch (Exception ex) {
          SPDiagnosticsService.Local.WriteTrace(TraceCategory.General, ex);
        }
      }
    }

    private bool ShouldNotify(SPChange item) {
      switch (GetChangeObjectType(item)) {
        case SPChangeObjectType.Alert:
          return ShouldNotify((SPChangeAlert)item);
        case SPChangeObjectType.ContentType:
          return ShouldNotify((SPChangeContentType)item);
        case SPChangeObjectType.Field:
          return ShouldNotify((SPChangeField)item);
        case SPChangeObjectType.File:
          return ShouldNotify((SPChangeFile)item);
        case SPChangeObjectType.Folder:
          return ShouldNotify((SPChangeFolder)item);
        case SPChangeObjectType.Group:
          return ShouldNotify((SPChangeGroup)item);
        case SPChangeObjectType.Item:
          return ShouldNotify((SPChangeItem)item);
        case SPChangeObjectType.List:
          return ShouldNotify((SPChangeList)item);
        case SPChangeObjectType.SecurityPolicy:
          return ShouldNotify((SPChangeSecurityPolicy)item);
        case SPChangeObjectType.Site:
          return ShouldNotify((SPChangeSite)item);
        case SPChangeObjectType.User:
          return ShouldNotify((SPChangeUser)item);
        case SPChangeObjectType.View:
          return ShouldNotify((SPChangeView)item);
        case SPChangeObjectType.Web:
          return ShouldNotify((SPChangeWeb)item);
      }
      throw new ArgumentException();
    }

    internal static SPChangeObjectType GetChangeObjectType(SPChange item) {
      CommonHelper.ConfirmNotNull(item, "item");
      return Enum<SPChangeObjectType>.Parse(item.GetType().Name.Substring(8));
    }

    internal static ulong GetBitmaskValue(SPChangeType value) {
      return 1ul << ((int)value - 1);
    }

    internal static ulong GetBitmaskValue(SPChangeObjectType value) {
      return 1ul << (32 + (int)value);
    }

    internal static object GetUniqueKey(SPChange item) {
      CommonHelper.ConfirmNotNull(item, "item");
      switch (GetChangeObjectType(item)) {
        case SPChangeObjectType.Alert:
          return ((SPChangeAlert)item).Id;
        case SPChangeObjectType.ContentType:
          return ((SPChangeContentType)item).Id;
        case SPChangeObjectType.Field:
          return ((SPChangeField)item).Id;
        case SPChangeObjectType.File:
          return ((SPChangeFile)item).UniqueId;
        case SPChangeObjectType.Folder:
          return ((SPChangeFolder)item).UniqueId;
        case SPChangeObjectType.Group:
          return ((SPChangeGroup)item).Id;
        case SPChangeObjectType.Item:
          return ((SPChangeItem)item).UniqueId;
        case SPChangeObjectType.List:
          return ((SPChangeList)item).Id;
        case SPChangeObjectType.SecurityPolicy:
          return ((SPChangeSecurityPolicy)item).SiteId;
        case SPChangeObjectType.Site:
          return ((SPChangeSite)item).SiteId;
        case SPChangeObjectType.User:
          return ((SPChangeUser)item).Id;
        case SPChangeObjectType.View:
          return ((SPChangeView)item).Id;
        case SPChangeObjectType.Web:
          return ((SPChangeWeb)item).Id;
      }
      throw new ArgumentException();
    }

    private class SimpleMonitor : SPChangeMonitor {
      public SimpleMonitor(Guid siteId, SPChangeMonitorFilter[] filters) {
        Initialize(siteId, filters);
      }
    }
  }
}
