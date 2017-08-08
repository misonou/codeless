using Codeless.SharePoint.Internal;
using Codeless.SharePoint.ObjectModel;
using Microsoft.SharePoint;
using System.Collections.Generic;
using System.Web.Caching;

namespace Codeless.SharePoint {
  /// <summary>
  /// Provides a default mechanism to store and retrieve configuration entries to a site collection.
  /// </summary>
  public class SiteConfigProvider : ISiteConfigProvider {
    private readonly Dictionary<string, ISiteConfigEntry> items = new Dictionary<string, ISiteConfigEntry>();
    private SPModelManager<SiteConfigEntry> manager;

    /// <summary>
    /// Configure customizations on the SharePoint list definition that stores configuration entries.
    /// </summary>
    /// <param name="attribute">List definition.</param>
    /// <returns>Modified list definition.</returns>
    protected virtual SPListAttribute InitializeListSettings(SPListAttribute attribute) {
      return attribute;
    }

    private SPModelManager<SiteConfigEntry> CreateManager(SPSite site) {
      SPListAttribute listAttribute = new SPListAttribute();
      listAttribute.Url = "Lists/SiteConfig";
      listAttribute.Title = "Site Config";
      listAttribute.EnableVersioning = SPOption.True;
      listAttribute.OnQuickLaunch = true;
      listAttribute.DefaultViewQuery = "<OrderBy><FieldRef Name=\"SiteConfigCategory\" Ascending=\"TRUE\"/><FieldRef Name=\"Title\" Ascending=\"TRUE\"/></OrderBy>";

      listAttribute = InitializeListSettings(listAttribute);
      SPModelDescriptor descriptor = SPModelDescriptor.Resolve(typeof(SiteConfigEntry));
      descriptor.Provision(site.RootWeb, SPModelProvisionOptions.Asynchronous, new SPModelListProvisionOptions(listAttribute)).GetListCollection();
      return new SPModelManager<SiteConfigEntry>(site.RootWeb);
    }

    void ISiteConfigProvider.Initialize(SPSite site) {
      this.manager = CreateManager(site);
      foreach (ISiteConfigEntry item in manager.GetItems()) {
        if (!items.ContainsKey(item.Key)) {
          items.Add(item.Key, item);
        }
      }
    }

    CacheDependency ISiteConfigProvider.GetCacheDependency() {
      foreach (SPModelUsage usage in manager.ContextLists) {
        SPList list = usage.EnsureList(manager.Site).List;
        if (list != null) {
          return new SPListCacheDependency(list);
        }
      }
      return null;
    }

    ISiteConfigEntry ISiteConfigProvider.GetEntry(string key) {
      ISiteConfigEntry item = null;
      items.TryGetValue(key, out item);
      return item;
    }

    void ISiteConfigProvider.CreateEntry(ISiteConfigEntry entry) {
      SiteConfigEntry item = manager.Create<SiteConfigEntry>();
      item.Key = entry.Key;
      item.Value = entry.Value;
      item.UseDefaultValue = entry.UseDefaultValue;
      item.Category = entry.Category;
      item.Description = entry.Description;
      if (entry is ISecureSiteConfigEntry) {
        item.SecureValue = ((ISecureSiteConfigEntry)entry).SecureValue;
      }
    }

    void ISiteConfigProvider.UpdateEntry(ISiteConfigEntry entry) { }

    void ISiteConfigProvider.CommitChanges() {
      try {
        SiteConfigEntry.IsInternalUpdate = true;
        manager.CommitChanges();
      } finally {
        SiteConfigEntry.IsInternalUpdate = false;
      }
    }
  }
}
