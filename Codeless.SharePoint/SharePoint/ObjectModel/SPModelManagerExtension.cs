using Codeless.SharePoint.ObjectModel.Linq;
using Microsoft.Office.Server.Search.Query;
using System;
using System.Linq;

namespace Codeless.SharePoint.ObjectModel {
  /// <summary>
  /// Provides extension methods to model manager.
  /// </summary>
  public static class SPModelManagerExtension {
    /// <summary>
    /// Gets a list item of model type <typeparamref name="T"/> with the specified list item ID.
    /// </summary>
    /// <typeparam name="T">Model type.</typeparam>
    /// <param name="manager">A model manager instance.</param>
    /// <param name="id">List item ID.</param>
    /// <returns>A list item; or *null* if there is no list item with the specified ID.</returns>
    public static T GetItemByID<T>(this SPModelManager<T> manager, int id) {
      return manager.GetItems(Caml.Equals(SPBuiltInFieldName.ID, id), 1u).FirstOrDefault();
    }

    /// <summary>
    /// Gets a list item of model type <typeparamref name="T"/> with the specified unique ID.
    /// </summary>
    /// <typeparam name="T">Model type.</typeparam>
    /// <param name="manager">A model manager instance.</param>
    /// <param name="uniqueId">Unique ID.</param>
    /// <returns>A list item; or *null* if there is no list item with the specified unique ID.</returns>
    public static T GetItemByUniqueId<T>(this SPModelManager<T> manager, Guid uniqueId) {
      return manager.GetItems(Caml.Equals(SPBuiltInFieldName.UniqueId, uniqueId), 1u).FirstOrDefault();
    }

    /// <summary>
    /// Creates a LINQ queryable interface.
    /// </summary>
    /// <typeparam name="T">Model type.</typeparam>
    /// <param name="manager">A model manager instance.</param>
    /// <returns>A LINQ queryable interface.</returns>
    public static IQueryable<T> Query<T>(this SPModelManagerBase<T> manager) {
      return new SPModelQuery<T>(new SPModelQueryProvider<T>(manager));
    }

    /// <summary>
    /// Creates a LINQ queryable interface which explicitly use Office search service to query items.
    /// </summary>
    /// <typeparam name="T">Model type.</typeparam>
    /// <param name="manager">A model manager instance.</param>
    /// <param name="keywords">A list of keywords.</param>
    /// <param name="keywordInclusion">Whether to match all or any keywords.</param>
    /// <returns>A LINQ queryable interface.</returns>
    public static IQueryable<T> Query<T>(this SPModelManagerBase<T> manager, string[] keywords, KeywordInclusion keywordInclusion) {
      return new SPModelQuery<T>(new SPModelQueryProvider<T>(manager, keywords, keywordInclusion));
    }

    /// <summary>
    /// Creates a LINQ queryable interface.
    /// </summary>
    /// <typeparam name="T">Model type.</typeparam>
    /// <param name="manager">A model manager instance.</param>
    /// <returns>A LINQ queryable interface.</returns>
    public static IQueryable<T> Query<T>(this ISPModelManager manager) {
      Type modelType = ((ISPModelManagerInternal)manager).Descriptor.ModelType;
      Type queryProviderType = typeof(SPModelQueryProvider<>).MakeGenericType(modelType);
      Type queryType = typeof(SPModelQuery<>).MakeGenericType(modelType);
      object queryProvider = Activator.CreateInstance(queryProviderType, manager);
      return ((IQueryable)Activator.CreateInstance(queryType, queryProvider)).OfType<T>();
    }

    /// <summary>
    /// Creates a LINQ queryable interface which explicitly use Office search service to query items.
    /// </summary>
    /// <typeparam name="T">Model type.</typeparam>
    /// <param name="manager">A model manager instance.</param>
    /// <param name="keywords">A list of keywords.</param>
    /// <param name="keywordInclusion">Whether to match all or any keywords.</param>
    /// <returns>A LINQ queryable interface.</returns>
    public static IQueryable<T> Query<T>(this ISPModelManager manager, string[] keywords, KeywordInclusion keywordInclusion) {
      Type modelType = ((ISPModelManagerInternal)manager).Descriptor.ModelType;
      Type queryProviderType = typeof(SPModelQueryProvider<>).MakeGenericType(modelType);
      Type queryType = typeof(SPModelQuery<>).MakeGenericType(modelType);
      object queryProvider = Activator.CreateInstance(queryProviderType, manager, keywords, keywordInclusion);
      return ((IQueryable)Activator.CreateInstance(queryType, queryProvider)).OfType<T>();
    }
  }
}
