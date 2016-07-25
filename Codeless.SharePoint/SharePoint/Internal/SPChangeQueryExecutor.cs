using Microsoft.SharePoint;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace Codeless.SharePoint.Internal {
  internal class SPChangeQueryExecutor {
    private static readonly ConcurrentFactory<Guid, SPChangeQueryExecutor> factory = new ConcurrentFactory<Guid, SPChangeQueryExecutor>();
    private static readonly Timer timer = new Timer(2000);
    private readonly List<SPChangeMonitor> monitors = new List<SPChangeMonitor>();
    private readonly Guid siteId;
    private DateTime lastCheckTime = DateTime.Now;

    static SPChangeQueryExecutor() {
      timer.Elapsed += OnTimerElapsed;
      timer.AutoReset = false;
      timer.Start();
    }

    private SPChangeQueryExecutor(Guid siteId) {
      this.siteId = siteId;
    }

    public static void AddMonitor(SPChangeMonitor monitor) {
      CommonHelper.ConfirmNotNull(monitor, "monitor");
      SPChangeQueryExecutor instance = factory.GetInstance(monitor.SiteId, () => new SPChangeQueryExecutor(monitor.SiteId));
      instance.monitors.Add(monitor);
    }

    public static void RemoveMonitor(SPChangeMonitor monitor) {
      CommonHelper.ConfirmNotNull(monitor, "monitor");
      SPChangeQueryExecutor instance = factory.GetInstance(monitor.SiteId, () => new SPChangeQueryExecutor(monitor.SiteId));
      instance.monitors.Remove(monitor);
    }

    public static IEnumerable<T> GetMonitors<T>(Guid siteId) where T : SPChangeMonitor {
      SPChangeQueryExecutor instance = factory.GetInstance(siteId, () => new SPChangeQueryExecutor(siteId));
      return instance.monitors.OfType<T>();
    }

    private void Execute() {
      DateTime now = DateTime.Now;
      if (monitors.Count > 0) {
        using (SPSite site = new SPSite(siteId, SPUserToken.SystemAccount)) {
          List<SPChange> collection = new List<SPChange>();
          SPChangeQuery query = CreateQuery(lastCheckTime);
          SPChangeCollection result = site.GetChanges(query);
          while (result.Count > 0) {
            collection.AddRange(result.OfType<SPChange>());
            query.ChangeTokenStart = result.LastChangeToken;
            result = site.GetChanges(query);
          }
          if (collection.Count > 0) {
            Dictionary<Tuple<Type, object>, SPAggregatedChange> dictionary = new Dictionary<Tuple<Type, object>, SPAggregatedChange>();
            foreach (SPChange item in collection) {
              SPAggregatedChange record = dictionary.EnsureKeyValue(Tuple.Create(item.GetType(), SPChangeMonitor.GetUniqueKey(item)));
              record.Add(item);
            }
            foreach (SPChangeMonitor monitor in monitors) {
              monitor.ProcessChanges(dictionary.Values);
            }
          }
        }
      }
      lastCheckTime = now;
    }

    private SPChangeQuery CreateQuery(DateTime changeSince) {
      ulong bitmask = monitors.SelectMany(v => v.Filters).Aggregate(0ul, (v, a) => v | a.Bitmask);
      return new SPChangeQuery(false, false) {
        ChangeTokenStart = new SPChangeToken(SPChangeCollection.CollectionScope.Site, siteId, changeSince.ToUniversalTime()),
        Add = IsSet(bitmask, SPChangeFlags.Add),
        Alert = IsSet(bitmask, SPChangeObjectType.Alert),
        ContentType = IsSet(bitmask, SPChangeObjectType.ContentType),
        Delete = IsSet(bitmask, SPChangeFlags.Delete),
        Field = IsSet(bitmask, SPChangeObjectType.Field),
        File = IsSet(bitmask, SPChangeObjectType.File),
        Folder = IsSet(bitmask, SPChangeObjectType.Folder),
        Group = IsSet(bitmask, SPChangeObjectType.Group),
        GroupMembershipAdd = IsSet(bitmask, SPChangeFlags.MemberAdd),
        GroupMembershipDelete = IsSet(bitmask, SPChangeFlags.MemberDelete),
        Item = IsSet(bitmask, SPChangeObjectType.Item),
        List = IsSet(bitmask, SPChangeObjectType.List),
        Move = IsSet(bitmask, SPChangeFlags.MoveAway) || IsSet(bitmask, SPChangeFlags.MoveInto),
        Navigation = IsSet(bitmask, SPChangeFlags.Navigation),
        Rename = IsSet(bitmask, SPChangeFlags.Rename),
        Restore = IsSet(bitmask, SPChangeFlags.Restore),
        RoleAssignmentAdd = IsSet(bitmask, SPChangeFlags.AssignmentAdd),
        RoleAssignmentDelete = IsSet(bitmask, SPChangeFlags.AssignmentDelete),
        RoleDefinitionAdd = IsSet(bitmask, SPChangeFlags.RoleAdd),
        RoleDefinitionDelete = IsSet(bitmask, SPChangeFlags.RoleDelete),
        RoleDefinitionUpdate = IsSet(bitmask, SPChangeFlags.RoleUpdate),
        SecurityPolicy = IsSet(bitmask, SPChangeObjectType.SecurityPolicy),
        Site = IsSet(bitmask, SPChangeObjectType.Site),
        SystemUpdate = IsSet(bitmask, SPChangeFlags.SystemUpdate),
        Update = IsSet(bitmask, SPChangeFlags.Update),
        User = IsSet(bitmask, SPChangeObjectType.User),
        View = IsSet(bitmask, SPChangeObjectType.View),
        Web = IsSet(bitmask, SPChangeObjectType.Web),
      };
    }

    private static bool IsSet(ulong bitmask, SPChangeObjectType value) {
      return (bitmask & SPChangeMonitor.GetBitmaskValue(value)) != 0;
    }

    private static bool IsSet(ulong bitmask, SPChangeFlags value) {
      return (bitmask & (ulong)value) != 0;
    }

    private static void OnTimerElapsed(object sender, ElapsedEventArgs e) {
      foreach (SPChangeQueryExecutor monitor in ((IDictionary)factory).Values) {
        monitor.Execute();
      }
      timer.Stop();
      timer.Start();
    }
  }
}
