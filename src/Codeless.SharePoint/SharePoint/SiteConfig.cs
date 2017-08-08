using Codeless.SharePoint.Internal;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Administration;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Caching;

namespace Codeless.SharePoint {
  /// <summary>
  /// Provides the base class for site configuration object.
  /// </summary>
  /// <remarks>
  /// When a new instance of a derived site configuration class is instantiated, values are pulled from a predefined list (Lists/SiteConfig).
  /// All public writable properties are propagated with the values.
  /// If the corresponding entry of a property are not found on the predefined list, 
  /// a new entry with value specified by <see cref="DefaultValueAttribute"/> is created on the list, and the property is propagated with that default value.
  /// <see cref="TypeConverterAttribute"/> is supported for custom data types.
  /// </remarks>
  /// <typeparam name="T">The derived type itself.</typeparam>
  public abstract class SiteConfig<T> where T : SiteConfig<T>, new() {
    private static readonly ConcurrentFactory<Guid, T> InstanceFactory = new ConcurrentFactory<Guid, T>();
    private Guid siteId;

    private class Entry : ISiteConfigEntry {
      public string Key { get; set; }
      public string Value { get; set; }
      public string Category { get; set; }
      public string Description { get; set; }
      public bool UseDefaultValue { get { return true; } }
    }

    private class SecureEntry : Entry, ISecureSiteConfigEntry {
      public SecureString SecureValue { get; set; }
    }

    /// <summary>
    /// Gets an instance of the current type <typeparamref name="T"/> which loads configuration from the current site collection.
    /// </summary>
    public static T Current {
      get {
        if (SPContext.Current != null) {
          return CommonHelper.HttpContextSingleton(() => Load(SPContext.Current.Site.ID));
        }
        return null;
      }
    }

    /// <summary>
    /// Gets an instance of the current type <typeparamref name="T"/> which loads configuration from the given site collection.
    /// </summary>
    /// <param name="site">Site collection object.</param>
    /// <returns>An instance of the current type <typeparamref name="T"/> which loads configuration from the given site collection.</returns>
    /// <exception cref="System.ArgumentNullException">Throws when input parameter <paramref name="site"/> is null.</exception>
    public static T Load(SPSite site) {
      CommonHelper.ConfirmNotNull(site, "site");
      return Load(site.ID);
    }

    /// <summary>
    /// Gets an instance of the current type <typeparamref name="T"/> which loads configuration from the given site collection,
    /// and optionally refresh the cache.
    /// </summary>
    /// <param name="site">Site collection object.</param>
    /// <param name="forceRefresh">Whether to refresh the cache.</param>
    /// <returns>An instance of the current type <typeparamref name="T"/> which loads configuration from the given site collection.</returns>
    /// <exception cref="System.ArgumentNullException">Throws when input parameter <paramref name="site"/> is null.</exception>
    public static T Load(SPSite site, bool forceRefresh) {
      CommonHelper.ConfirmNotNull(site, "site");
      return Load(site.ID, forceRefresh);
    }

    /// <summary>
    /// Gets an instance of the current type <typeparamref name="T"/> which loads configuration from the specified site collection.
    /// </summary>
    /// <param name="siteId">The GUID of a site collection.</param>
    /// <returns>An instance of the current type <typeparamref name="T"/> which loads configuration from the specified site collection.</returns>
    public static T Load(Guid siteId) {
      return Load(siteId, false);
    }

    /// <summary>
    /// Gets an instance of the current type <typeparamref name="T"/> which loads configuration from the specified site collection,
    /// and optionally refresh the cache.
    /// </summary>
    /// <param name="siteId">The GUID of a site collection.</param>
    /// <param name="forceRefresh">Whether to refresh the cache.</param>
    /// <returns>An instance of the current type <typeparamref name="T"/> which loads configuration from the given site collection.</returns>
    public static T Load(Guid siteId, bool forceRefresh) {
      if (forceRefresh) {
        InstanceFactory.Destroy(siteId);
      }
      T config = null;
      try {
        return InstanceFactory.GetInstance(siteId, () => {
          return LoadInternal(siteId, out config);
        });
      } catch (Exception ex) {
        Logger.Error(ex);
        return config ?? new T();
      }
    }

    /// <summary>
    /// Invalidate the cached instance of the current type <typeparamref name="T"/> for the specified site collection.
    /// </summary>
    /// <param name="siteId">The GUID of a site collection.</param>
    public static void Invalidate(Guid siteId) {
      InstanceFactory.Destroy(siteId);
    }

    /// <summary>
    /// Invalidate the cached instance of the current type <typeparamref name="T"/> for the specified site collection.
    /// </summary>
    /// <param name="site">Site collection object.</param>
    public static void Invalidate(SPSite site) {
      CommonHelper.ConfirmNotNull(site, "site");
      Invalidate(site.ID);
    }

    /// <summary>
    /// Invalidate this instance of the current type <typeparamref name="T"/>. 
    /// In the next time <see cref="Load(Guid)"/> or one of this overloads is called for the same site collection,
    /// a new instance is returned which its property values are loaded again from the site collection.
    /// </summary>
    public void Invalidate() {
      Invalidate(siteId);
    }

    private static T LoadInternal(Guid siteId, out T config) {
      config = new T();
      config.siteId = siteId;
      using (SPSite elevatedSite = new SPSite(siteId, SPUserToken.SystemAccount)) {
        SiteConfigProviderAttribute attribute = typeof(T).GetCustomAttribute<SiteConfigProviderAttribute>(false);
        ISiteConfigProvider provider;
        if (attribute == null) {
          provider = new SiteConfigProvider();
        } else {
          provider = (ISiteConfigProvider)attribute.ProviderType.CreateInstance();
        }
        provider.Initialize(elevatedSite);

        foreach (PropertyDescriptor pd in TypeDescriptor.GetProperties(config)) {
          ISiteConfigEntry entry = provider.GetEntry(pd.Name);
          bool needUpdate = false;
          bool needCreate = false;

          if (entry == null || entry.UseDefaultValue) {
            string defaultValueString = String.Empty;
            try {
              pd.ResetValue(config);
              defaultValueString = Convert.ToString(pd.GetValue(config));
            } catch (ArgumentException) {
              DefaultValueAttribute defaultValueAttribute = (DefaultValueAttribute)pd.Attributes[typeof(DefaultValueAttribute)];
              if (defaultValueAttribute != null && defaultValueAttribute.Value != null) {
                object defaultValue = ParseValue(pd, defaultValueAttribute.Value);
                pd.SetValue(config, defaultValue);
                defaultValueString = Convert.ToString(defaultValueAttribute.Value);
              }
            }
            if (entry == null) {
              needCreate = true;
              entry = (pd.PropertyType == typeof(SecureString)) ? new SecureEntry() { Key = pd.Name } : new Entry() { Key = pd.Name };
            }
            if (!CompareString(entry.Value, defaultValueString)) {
              entry.Value = defaultValueString;
              needUpdate |= CompareString(entry.Value, defaultValueString);
            }
          } else {
            object typedValue = ParseValueFromEntry(pd, entry);
            pd.SetValue(config, typedValue);
          }
          if (!CompareString(entry.Category, pd.Category)) {
            entry.Category = pd.Category;
            needUpdate |= CompareString(entry.Category, pd.Category);
          }
          if (!CompareString(entry.Description, pd.Description)) {
            entry.Description = pd.Description;
            needUpdate |= CompareString(entry.Description, pd.Description);
          }
          if (needCreate) {
            provider.CreateEntry(entry);
          } else if (needUpdate) {
            provider.UpdateEntry(entry);
          }
          if (pd.GetValue(config) == null && pd.PropertyType.GetConstructor(new Type[0]) != null) {
            pd.SetValue(config, Activator.CreateInstance(pd.PropertyType));
          }
        }
        provider.CommitChanges();

        if (HttpContext.Current != null) {
          CacheDependency cacheDependency = provider.GetCacheDependency();
          if (cacheDependency != null) {
            HttpContext.Current.Cache.Add(cacheDependency.GetUniqueID(), new object(), cacheDependency, Cache.NoAbsoluteExpiration, Cache.NoSlidingExpiration, CacheItemPriority.Normal, (k, v, r) => Invalidate(siteId));
          }
        }
      }
      return config;
    }

    private static bool CompareString(string x, string y) {
      bool xEmpty = String.IsNullOrEmpty(x);
      bool yEmpty = String.IsNullOrEmpty(y);
      if (xEmpty && yEmpty) {
        return true;
      }
      if (xEmpty || yEmpty) {
        return false;
      }
      return x.Replace("\r", "") == y.Replace("\r", "");
    }

    private static object ParseValueFromEntry(PropertyDescriptor pd, ISiteConfigEntry entry) {
      if (pd.PropertyType == typeof(SecureString) && entry is ISecureSiteConfigEntry) {
        return ((ISecureSiteConfigEntry)entry).SecureValue;
      }
      return ParseValue(pd, entry.Value);
    }

    private static object ParseValue(PropertyDescriptor pd, object value) {
      try {
        if (pd.Converter.CanConvertFrom(value.GetType())) {
          return pd.Converter.ConvertFrom(value);
        }
        if (pd.PropertyType == typeof(IniConfiguration)) {
          return IniConfiguration.Parse(value.ToString());
        }
        if (pd.PropertyType == typeof(StringCollection)) {
          StringCollection collection = new StringCollection();
          if (value is string) {
            if (!CommonHelper.IsNullOrWhiteSpace(value.ToString())) {
              collection.AddRange(Regex.Split(value.ToString(), @"\s*\r?\n\s*"));
            }
          } else if (value is IEnumerable<string>) {
            collection.AddRange(((IEnumerable<string>)value).ToArray());
          }
          return collection;
        }
      } catch (Exception ex) {
        SPDiagnosticsService.Local.WriteTrace(TraceCategory.SiteConfig, ex);
      }
      return null;
    }
  }
}
