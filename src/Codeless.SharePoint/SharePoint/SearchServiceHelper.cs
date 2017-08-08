using Codeless.SharePoint.Internal;
using Codeless.SharePoint.ObjectModel;
using Microsoft.Office.Server.Search.Administration;
using Microsoft.Office.Server.Search.Query;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Taxonomy;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Hosting;

namespace Codeless.SharePoint {
  /// <summary>
  /// Provides helper methods to Office search service.
  /// </summary>
  public static class SearchServiceHelper {
    private class ManagedPropertyDefinition {
      public readonly string CrawledPropertyName;
      public readonly string MappedPropertyName;
      public readonly ManagedDataType DataType;

      public ManagedPropertyDefinition(SPField field) {
        this.CrawledPropertyName = String.Concat("ows_", field.InternalName);
        this.MappedPropertyName = field.InternalName.Replace("_", "");
        this.DataType = GetManagedDataType(field);
      }

      private static ManagedDataType GetManagedDataType(SPField field) {
        switch (field.Type) {
          case SPFieldType.Boolean:
            return ManagedDataType.YesNo;
          case SPFieldType.DateTime:
            return ManagedDataType.DateTime;
          case SPFieldType.Number:
          case SPFieldType.Currency:
            return ManagedDataType.Decimal;
          case SPFieldType.Integer:
            return ManagedDataType.Integer;
          default:
            return ManagedDataType.Text;
        }
      }
    }

    private static readonly ConcurrentFactory<Guid, IReadOnlyDictionary<string, string>> SearchApplicationIdFactory = new ConcurrentFactory<Guid, IReadOnlyDictionary<string, string>>();
    private static readonly ConcurrentFactory<Guid, IReadOnlyDictionary<string, string>> SPSiteIdFactory = new ConcurrentFactory<Guid, IReadOnlyDictionary<string, string>>();
    private static readonly IReadOnlyDictionary<string, string> EmptyDictionary = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

    private static readonly ReadOnlyDictionary<string, string> MappedSystemProperties = (new Dictionary<string, string>{
      { SPBuiltInFieldName.ID, BuiltInManagedPropertyName.ListItemID }
    }).AsReadOnly();

    private static readonly ReadOnlyDictionary<ManagedDataType, int> VariantTypeDictionary = (new Dictionary<ManagedDataType, int> {
      { ManagedDataType.Binary, 8 },
      { ManagedDataType.DateTime, 64 },
      { ManagedDataType.Decimal, 5 },
      { ManagedDataType.Integer, 20 },
      { ManagedDataType.Text, 31 },
      { ManagedDataType.YesNo, 11 }
    }).AsReadOnly();

    [ThreadStatic]
    private static string lastQueryText;

    /// <summary>
    /// Gets the last query text translated from a <see cref="CamlExpression"/> instance to a <see cref="KeywordQuery"/> instance.
    /// </summary>
    public static string LastQueryText {
      get { return lastQueryText; }
      private set { lastQueryText = value; }
    }

    /// <summary>
    /// Clears the managed property cache for a specified site collection.
    /// </summary>
    /// <param name="site">A site collection object.</param>
    public static void FlushCache(SPSite site) {
      CommonHelper.ConfirmNotNull(site, "site");
      SPSiteIdFactory.Destroy(site.ID);
      SearchServiceApplication searchApplication = GetSearchServiceApplication(site);
      if (searchApplication != null) {
        SearchApplicationIdFactory.Destroy(searchApplication.Id);
      }
    }

    /// <summary>
    /// Creates a <see cref="KeywordQuery"/> instance with query text and certain properties set in regards of the specified CAML expression.
    /// </summary>
    /// <param name="site">A site collection object.</param>
    /// <param name="expression">A CAML expression.</param>
    /// <returns>A prepared <see cref="KeywordQuery"/> instance.</returns>
    public static KeywordQuery CreateKeywordQueryFromCaml(SPSite site, CamlExpression expression) {
      return CreateKeywordQueryFromCaml(site, expression, new Hashtable());
    }

    /// <summary>
    /// Creates a <see cref="KeywordQuery"/> instance with query text and certain properties set in regards of the specified CAML expression, with values to be binded on parameters.
    /// </summary>
    /// <param name="site">A site collection object.</param>
    /// <param name="expression">A CAML expression.</param>
    /// <param name="bindings">A collection of parameter values.</param>
    /// <returns>A prepared <see cref="KeywordQuery"/> instance.</returns>
    public static KeywordQuery CreateKeywordQueryFromCaml(SPSite site, CamlExpression expression, Hashtable bindings) {
      CommonHelper.ConfirmNotNull(site, "site");
      CommonHelper.ConfirmNotNull(expression, "expression");
      CommonHelper.ConfirmNotNull(bindings, "bindings");

      KeywordQuery query = new KeywordQuery(site);
      KeywordQueryCamlVisitor visitor = new KeywordQueryCamlVisitor(query, bindings);
      visitor.Visit(expression);
      return query;
    }

    /// <summary>
    /// Creates a <see cref="KeywordQuery"/> instance with query text and certain properties set in regards of the specified CAML expression.
    /// </summary>
    /// <param name="site">A site collection object.</param>
    /// <param name="query">A CAML expression.</param>
    /// <param name="limit">Number of rows to be returned.</param>
    /// <param name="startRow">Number of rows to be skipped.</param>
    /// <param name="keywords">A list of keywords to be passed in query text.</param>
    /// <param name="inclusion">Whether to match all or any keywords supplied.</param>
    /// <param name="selectProperties">A list of managed properties to be returned.</param>
    /// <returns>A prepared <see cref="KeywordQuery"/> instance.</returns>
    public static KeywordQuery CreateQuery(SPSite site, CamlExpression query, int limit, int startRow, string[] keywords, KeywordInclusion inclusion, string[] selectProperties) {
      CommonHelper.ConfirmNotNull(site, "site");
      KeywordQuery keywordQuery = SearchServiceHelper.CreateKeywordQueryFromCaml(site, query);
      if (keywords != null) {
        keywordQuery.QueryText = String.Concat(String.Join(" ", keywords), " ", keywordQuery.QueryText);
      }
      keywordQuery.SelectProperties.AddRange(SPModel.RequiredSearchProperties);
      if (selectProperties != null) {
        keywordQuery.SelectProperties.AddRange(selectProperties);
      }
      keywordQuery.KeywordInclusion = inclusion;
      keywordQuery.ResultTypes = ResultType.RelevantResults | ResultType.RefinementResults;
      keywordQuery.ResultsProvider = SearchProvider.Default;
      keywordQuery.TrimDuplicates = true;
      keywordQuery.TrimDuplicatesOnProperty = BuiltInManagedPropertyName.UniqueID;
      keywordQuery.StartRow = startRow;
      keywordQuery.RowLimit = limit;
      keywordQuery.RowsPerPage = limit;
      return keywordQuery;
    }

    /// <summary>
    /// Executes a CAML query against Office search service.
    /// </summary>
    /// <param name="site">A site collection object.</param>
    /// <param name="query">A CAML expression.</param>
    /// <param name="limit">Number of rows to be returned.</param>
    /// <param name="startRow">Number of rows to be skipped.</param>
    /// <param name="keywords">A list of keywords to be passed in query text.</param>
    /// <param name="refiners">A list of <see cref="SearchRefiner"/> objects where refinement results are populated.</param>
    /// <param name="inclusion">Whether to match all or any keywords supplied.</param>
    /// <param name="selectProperties">A list of managed properties to be returned.</param>
    /// <returns>Results returned from Office search service.</returns>
    public static ResultTable ExecuteQuery(SPSite site, CamlExpression query, int limit, int startRow, string[] keywords, SearchRefiner[] refiners, KeywordInclusion inclusion, string[] selectProperties) {
      KeywordQuery keywordQuery = CreateQuery(site, query, limit, startRow, keywords, inclusion, selectProperties);
      return ExecuteQuery(keywordQuery, refiners);
    }

    /// <summary>
    /// Executes a keyword query against Office search service.
    /// </summary>
    /// <param name="keywordQuery">A keyword query instance.</param>
    /// <param name="refiners">A list of <see cref="SearchRefiner"/> objects where refinement results are populated.</param>
    /// <returns>Results returned from Office search service.</returns>
    public static ResultTable ExecuteQuery(KeywordQuery keywordQuery, SearchRefiner[] refiners) {
      CommonHelper.ConfirmNotNull(keywordQuery, "keywordQuery");
      LastQueryText = keywordQuery.QueryText;

      if (refiners != null) {
        keywordQuery.Refiners = String.Join(",", refiners.Select(v => v.PropertyName).ToArray());
        keywordQuery.RefinementFilters.AddRange(refiners.Where(v => v.RefinementToken != null).Select(v => v.RefinementToken).ToArray());
      }
      ResultTableCollection queryResults = keywordQuery.Execute();
      ResultTable relevantResults = queryResults[ResultType.RelevantResults];
      if (relevantResults == null) {
        throw new Exception("Search executor did not return result table of type RelevantResults");
      }
      if (refiners != null) {
        ResultTable refinementResults = queryResults[ResultType.RefinementResults];
        if (refinementResults == null) {
          throw new Exception("Search executor did not return result table of type RefinementResults");
        }
        foreach (SearchRefiner refiner in refiners) {
          foreach (DataRow row in refinementResults.Table.Rows.OfType<DataRow>().Where(v => refiner.PropertyName.Equals(v["RefinerName"]))) {
            refiner.AddRefinement((string)row["RefinementName"], (string)row["RefinementToken"], (int)row["RefinementCount"]);
          }
        }
      }
      return relevantResults;
    }

    /// <summary>
    /// Gets names of corresponding managed properties of the specified SharePoint fields.
    /// </summary>
    /// <param name="site">A site collection object.</param>
    /// <param name="internalName">A list of internal names of SharePoint fields.</param>
    /// <returns>A list of resolved managed property names.</returns>
    public static string[] GetManagedPropertyNames(SPSite site, params string[] internalName) {
      CommonHelper.ConfirmNotNull(site, "site");
      List<string> result = new List<string>();
      IReadOnlyDictionary<string, string> dictionary = GetManagedPropertyNames(site);
      foreach (string value in internalName) {
        string propertyName;
        if (MappedSystemProperties.TryGetValue(value, out propertyName) || dictionary.TryGetValue(value, out propertyName)) {
          result.Add(propertyName);
        }
      }
      return result.ToArray();
    }

    /// <summary>
    /// Gets a mapping of SharePoint field names to managed property names.
    /// </summary>
    /// <param name="site">A site collection object.</param>
    /// <returns>A mapping of SharePoint field names to managed property names.</returns>
    public static IReadOnlyDictionary<string, string> GetManagedPropertyNames(SPSite site) {
      CommonHelper.ConfirmNotNull(site, "site");
      return SPSiteIdFactory.GetInstance(site.ID, () => GetManagedPropertyNamesUncached(site).AsReadOnly());
    }

    /// <summary>
    /// Gets a mapping of SharePoint field names to managed property names.
    /// </summary>
    /// <param name="searchApplication">A search service application.</param>
    /// <returns>A mapping of SharePoint field names to managed property names.</returns>
    public static IReadOnlyDictionary<string, string> GetManagedPropertyNames(SearchServiceApplication searchApplication) {
      CommonHelper.ConfirmNotNull(searchApplication, "searchApplication");
      return SearchApplicationIdFactory.GetInstance(searchApplication.Id, () => GetManagedPropertyNamesUncached(searchApplication).AsReadOnly());
    }

    /// <summary>
    /// Gets a search service application connected to the specified site collection.
    /// </summary>
    /// <param name="site">A site collection object.</param>
    /// <returns>A search service application.</returns>
    public static SearchServiceApplication GetSearchServiceApplication(SPSite site) {
      CommonHelper.ConfirmNotNull(site, "site");
      SPServiceContext serviceContext = SPServiceContext.GetContext(site);
      SearchServiceApplicationProxy searchApplicationProxy = (SearchServiceApplicationProxy)serviceContext.GetDefaultProxy(typeof(SearchServiceApplicationProxy));
      Guid applicationId = searchApplicationProxy.GetSearchServiceApplicationInfo().SearchServiceApplicationId;
      return SearchService.Service.SearchApplications.GetValue<SearchServiceApplication>(applicationId);
    }

    /// <summary>
    /// Creates managed properties from crawled properties that corresponds to the specified SharePoint fields.
    /// </summary>
    /// <param name="site">A site collection object.</param>
    /// <param name="fields">A list of SharePoint fields.</param>
    public static void EnsureManagedProperties(SPSite site, IEnumerable<SPField> fields) {
      using (HostingEnvironment.Impersonate()) {
        EnsureManagedProperties(site, fields.Select(v => new ManagedPropertyDefinition(v)));
      }
    }

    private static void EnsureManagedProperties(SPSite site, IEnumerable<ManagedPropertyDefinition> definitions) {
      SearchServiceApplication searchApplication = GetSearchServiceApplication(site);
      if (searchApplication != null) {
        Schema schema = new Schema(searchApplication);
        IEnumerable<CrawledProperty> allCrawledProperties = schema.AllCategories["SharePoint"].GetAllCrawledProperties().OfType<CrawledProperty>();

        foreach (ManagedPropertyDefinition definition in definitions) {
          int variantType = VariantTypeDictionary[definition.DataType];
          CrawledProperty rawProperty = allCrawledProperties.FirstOrDefault(v => v.Name == definition.CrawledPropertyName && (v.VariantType == variantType || v.VariantType == (variantType | 4096)));
          if (rawProperty == null || rawProperty.GetMappedManagedProperties().GetEnumerator().MoveNext()) {
            continue;
          }

          ManagedProperty property;
          try {
            property = schema.AllManagedProperties[definition.MappedPropertyName];
          } catch (KeyNotFoundException) {
            property = schema.AllManagedProperties.Create(definition.MappedPropertyName, definition.DataType);
          }
          MappingCollection mappings = property.GetMappings();
          Mapping mapping = new Mapping(rawProperty.Propset, rawProperty.Name, rawProperty.VariantType, property.PID);
          mappings.Add(mapping);
          property.SetMappings(mappings);
          property.Update();
        }
        FlushCache(site);
      }
    }

    private static Dictionary<string, string> GetManagedPropertyNamesUncached(SPSite site) {
      Dictionary<string, string> dictionary = new Dictionary<string, string>();
      SearchServiceApplication searchApplication = GetSearchServiceApplication(site);
      if (searchApplication != null) {
        SPFieldCollection fields = site.RootWeb.Fields;
        foreach (KeyValuePair<string, string> entry in GetManagedPropertyNames(searchApplication)) {
          SPField field;
          try {
            field = fields.GetFieldByInternalName(entry.Key);
          } catch (ArgumentException) {
            continue;
          }
          dictionary.Add(entry.Key, entry.Value);
          TaxonomyField taxField = CommonHelper.TryCastOrDefault<TaxonomyField>(field);
          if (taxField != null && taxField.TextField != Guid.Empty) {
            try {
              SPField textField = fields[taxField.TextField];
              dictionary[textField.InternalName] = entry.Value;
            } catch {
            }
          }
        }
      }
      return dictionary;
    }

    private static Dictionary<string, string> GetManagedPropertyNamesUncached(SearchServiceApplication searchApplication) {
      Dictionary<string, string> dictionary = new Dictionary<string, string>();
      Schema schema = new Schema(searchApplication);
      foreach (ManagedProperty info in schema.AllManagedProperties) {
        foreach (CrawledProperty property in info.GetMappings()) {
          Match match = Regex.Match(property.Name, @"^ows_(?:taxId_|[qr]_(?:TEXT|MTXT|BOOL|INTG|GUID|URLH|DATE|HTML|IMGE|CHCS|USER)_)?");
          if (match.Success) {
            string fieldName = property.Name.Substring(match.Length);
            if (!dictionary.ContainsKey(fieldName)) {
              dictionary.Add(fieldName, info.Name);
            }
          }
        }
      }
      return dictionary;
    }
  }
}
