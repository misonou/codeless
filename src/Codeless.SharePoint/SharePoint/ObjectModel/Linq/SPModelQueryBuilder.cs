using Codeless.SharePoint.Internal;
using Microsoft.SharePoint;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace Codeless.SharePoint.ObjectModel.Linq {
  internal class SPModelQueryBuilder {
    private HashSet<string> selectProperties;
    private bool? selectAllProperties;

    public SPModelQueryBuilder() {
      this.ContentTypeIds = new List<SPContentTypeId>();
      this.TaxonomyFields = new HashSet<string>();
      this.Parameters = new Hashtable();
      this.ParameterEvaluators = new Dictionary<string, SPModelParameterizedQuery.ParameterEvaluator>();
    }

    public Type ModelType { get; set; }
    public List<SPContentTypeId> ContentTypeIds { get; private set; }
    public SPModelQueryExecuteMode ExecuteMode { get; set; }
    public CamlExpression Expression { get; set; }
    public Expression SelectExpression { get; set; }
    public Hashtable Parameters { get; private set; }
    public HashSet<string> TaxonomyFields { get; private set; }
    public Dictionary<string, SPModelParameterizedQuery.ParameterEvaluator> ParameterEvaluators { get; private set; }

    public string[] SelectProperties {
      get { return selectAllProperties == true || selectProperties == null ? null : selectProperties.ToArray(); }
    }

    public bool SelectAllProperties {
      get { return selectAllProperties.GetValueOrDefault(true); }
      set { selectAllProperties = value; }
    }

    public void AddSelectProperty(string name) {
      CommonHelper.ConfirmNotNull(name, "name");
      if (!selectAllProperties.HasValue) {
        selectAllProperties = false;
      }
      if (selectProperties == null) {
        selectProperties = new HashSet<string>();
      }
      selectProperties.Add(name);
    }
  }
}
