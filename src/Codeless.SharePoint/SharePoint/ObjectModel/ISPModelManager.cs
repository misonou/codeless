using Microsoft.Office.Server.Search.Query;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Taxonomy;
using System;

namespace Codeless.SharePoint.ObjectModel {
  /// <summary>
  /// Encapsulates <see cref="SPModelManager{T}"/> when the model type is variable or unknown.
  /// </summary>
  public interface ISPModelManager {
    /// <summary>
    /// Gets the site collection associated with the site that initialized this instance of the <see cref="SPModelManagerBase{T}"/> class.
    /// </summary>
    SPSite Site { get; }

    /// <summary>
    /// Gets the term store connected with the site that initialized this instance of the <see cref="SPModelManagerBase{T}"/> class.
    /// </summary>
    TermStore TermStore { get; }

    /// <summary>
    /// Gets items of the associated content type(s).
    /// </summary>
    /// <returns>A collection containing the returned items.</returns>
    SPModelCollection GetItems();

    /// <summary>
    /// Gets items of the associated content type(s) that satisfy the condition.
    /// </summary>
    /// <param name="query">CAML query expression.</param>
    /// <returns>A collection containing the returned items.</returns>
    SPModelCollection GetItems(CamlExpression query);

    /// <summary>
    /// Gets items of the associated content type(s) that satisfy the condition.
    /// </summary>
    /// <param name="query">CAML query expression.</param>
    /// <param name="limit">Maximum number of items to be returned.</param>
    /// <returns>A collection containing the returned items.</returns>
    SPModelCollection GetItems(CamlExpression query, uint limit);

    /// <summary>
    /// Performs a keyword search against the items of the associated content type(s).
    /// </summary>
    /// <param name="query">CAML query expression.</param>
    /// <param name="limit">Maximum number of items to be returned.</param>
    /// <param name="keywords">A list of keywords to be searched against.</param>
    /// <param name="keywordInclusion">See <see cref="KeywordInclusion"/>.</param>
    /// <returns>A collection containing the returned items.</returns>
    SPModelCollection GetItems(CamlExpression query, uint limit, string[] keywords, KeywordInclusion keywordInclusion);

    /// <summary>
    /// Performs a keyword search against the items of the associated content type(s).
    /// </summary>
    /// <param name="query">CAML query expression.</param>
    /// <param name="limit">Maximum number of items to be returned.</param>
    /// <param name="startRow">Number of items to skip from start.</param>
    /// <param name="keywords">A list of keywords to be searched against.</param>
    /// <param name="refiners">A list of <see cref="SearchRefiner"/> instances. Refinement results are populated to the supplied instances.</param>
    /// <param name="keywordInclusion">See <see cref="KeywordInclusion"/>.</param>
    /// <param name="totalCount">Total number of items.</param>
    /// <returns>A collection containing the returned items.</returns>
    SPModelCollection GetItems(CamlExpression query, uint limit, uint startRow, string[] keywords, SearchRefiner[] refiners, KeywordInclusion keywordInclusion, out int totalCount);

    /// <summary>
    /// Gets the number of items of the associated content type(s).
    /// </summary>
    /// <returns>Number of items.</returns>
    int GetCount();

    /// <summary>
    /// Gets the number of items of the associated content type(s) that satisfy the condition.
    /// </summary>
    /// <param name="query">CAML query expression.</param>with the associated content type(s)
    /// <returns>Number of items.</returns>
    int GetCount(CamlExpression query);

    /// <summary>
    /// Performs a keyword search against the items of the associated content type(s) and returns the number of items.
    /// </summary>
    /// <param name="query">CAML query expression.</param>
    /// <param name="keywords">A list of keywords to be searched against.</param>
    /// <param name="keywordInclusion">See <see cref="KeywordInclusion"/>.</param>
    /// <returns>Number of items.</returns>
    int GetCount(CamlExpression query, string[] keywords, KeywordInclusion keywordInclusion);

    /// <summary>
    /// Creates an item of the associated content type. If the content type derives from File or Folder, a random name is used.
    /// See also <see cref="Create(Type,string)"/>
    /// </summary>
    /// <param name="modelType">Type of item to be created.</param>
    /// <returns>An item of the specified content type.</returns>
    object Create(Type modelType);

    /// <summary>
    /// Creates an item of the associated content type with the given file or folder name. 
    /// See also <see cref="Create(Type,string)"/>
    /// </summary>
    /// <param name="modelType">Type of item to be created.</param>
    /// <param name="filename">File or folder name.</param>
    /// <returns>An item of the specified content type.</returns>
    object Create(Type modelType, string filename);

    /// <summary>
    /// Deletes the specified item from a list. 
    /// </summary>
    /// <param name="model">An item to be deleted.</param>
    void Delete(object model);

    /// <summary>
    /// Commits changes made to model class instances fetched fromt this manager.
    /// </summary>
    void CommitChanges();

    /// <summary>
    /// Commits changes made to the specified model class instances.
    /// </summary>
    /// <param name="item">An item with changes to be persisted.</param>
    void CommitChanges(object item);
  }
}
