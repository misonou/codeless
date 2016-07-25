using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;

namespace Codeless.SharePoint.Publishing {
  /// <summary>
  /// Provides SEO meta data from multiple sources with fallback mechanism.
  /// </summary>
  public sealed class SeoMetaWithFallback : Collection<ISeoMetaProvider>, ISeoMetaProvider {
    private static readonly Dictionary<string, PropertyInfo> properties = typeof(ISeoMetaProvider).GetProperties().ToDictionary(v => v.Name);
    private readonly Dictionary<string, string> values = new Dictionary<string, string>();

    /// <summary>
    /// Gets SEO-friendly title. For example to be used to specify in og:title tag.
    /// </summary>
    public string Title {
      get { return GetFirstOrDefault(properties["Title"]); }
    }

    /// <summary>
    /// Gets SEO-friendly description. For example to be used to specify in og:description or "description" meta tag.
    /// </summary>
    public string Description {
      get { return GetFirstOrDefault(properties["Description"]); }
    }

    /// <summary>
    /// Gets SEO-friendly keyword list. For example to be used to specify in "keywords" meta tag.
    /// </summary>
    public string Keywords {
      get { return GetFirstOrDefault(properties["Keywords"]); }
    }

    /// <summary>
    /// Gets SEO-friendly image URL. For example to be used to specify in og:image tag.
    /// </summary>
    public string Image {
      get { return GetFirstOrDefault(properties["Image"]); }
    }

    /// <summary>
    /// Overriden.
    /// </summary>
    /// <param name="index"></param>
    /// <param name="item"></param>
    protected override void InsertItem(int index, ISeoMetaProvider item) {
      base.InsertItem(index, item);
      values.Clear();
    }

    /// <summary>
    /// Overriden.
    /// </summary>
    /// <param name="index"></param>
    /// <param name="item"></param>
    protected override void SetItem(int index, ISeoMetaProvider item) {
      base.SetItem(index, item);
      values.Clear();
    }

    /// <summary>
    /// Overriden.
    /// </summary>
    /// <param name="index"></param>
    protected override void RemoveItem(int index) {
      base.RemoveItem(index);
      values.Clear();
    }

    /// <summary>
    /// Overriden.
    /// </summary>
    protected override void ClearItems() {
      base.ClearItems();
      values.Clear();
    }

    private string GetFirstOrDefault(PropertyInfo m) {
      string value;
      if (values.TryGetValue(m.Name, out value)) {
        return value;
      }
      foreach (ISeoMetaProvider instance in this) {
        value = m.GetValue<string>(instance);
        if (!String.IsNullOrEmpty(value)) {
          values.Add(m.Name, value);
          return value;
        }
      }
      values.Add(m.Name, value);
      return String.Empty;
    }
  }
}
