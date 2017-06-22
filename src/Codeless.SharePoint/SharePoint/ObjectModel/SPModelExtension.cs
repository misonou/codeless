using Microsoft.SharePoint;
using System.Collections;
using System.Collections.Generic;

namespace Codeless.SharePoint.ObjectModel {
  /// <summary>
  /// Provides extension methods to the <see cref="SPModel"/> class.
  /// </summary>
  public static class SPModelExtension {
    /// <summary>
    /// Gets the meta-data of the list item associated with an <see cref="SPModel"/> instance.
    /// </summary>
    public static ISPModelMetaData GetMetaData(this SPModel model) {
      return model;
    }

    /// <summary>
    /// Gets a specified major version of the list item.
    /// </summary>
    /// <typeparam name="T">Type of model.</typeparam>
    /// <param name="model">A model object representing the list item.</param>
    /// <param name="majorVersion">Major version number.</param>
    /// <returns>A read-only model object of type <typeparamref name="T"/> if the specified version or *null* if such version does not exist.</returns>
    public static T GetVersion<T>(this T model, int majorVersion) where T : SPModel {
      return model.GetVersion(new SPItemVersion(majorVersion, 0));
    }

    /// <summary>
    /// Gets a specified version of the list item.
    /// </summary>
    /// <typeparam name="T">Type of model.</typeparam>
    /// <param name="model">A model object representing the list item.</param>
    /// <param name="version">Version number.</param>
    /// <returns>A read-only model object of type <typeparamref name="T"/> if the specified version or *null* if such version does not exist.</returns>
    public static T GetVersion<T>(this T model, SPItemVersion version) where T : SPModel {
      if (model.Adapter.Version == version) {
        return model;
      }
      SPListItemVersion previousVersion = model.Adapter.ListItem.Versions.GetVersionFromLabel(version.ToString());
      if (previousVersion != null) {
        return (T)model.ParentCollection.Manager.TryCreateModel(new SPListItemVersionAdapter(previousVersion), true);
      }
      return null;
    }

    /// <summary>
    /// Gets all versions of the list item.
    /// </summary>
    /// <typeparam name="T">Type of model.</typeparam>
    /// <param name="model">A model object representing the list item.</param>
    /// <returns>A enumerable collection containing read-only model objects of type <typeparamref name="T"/> representing different versions of the list item.</returns>
    public static IEnumerable<T> GetVersions<T>(this T model) where T : SPModel {
      foreach (SPListItemVersion version in model.Adapter.ListItem.Versions) {
        yield return (T)model.ParentCollection.Manager.TryCreateModel(new SPListItemVersionAdapter(version), true);
      }
    }
  }
}
