using Codeless.SharePoint.Internal;
using IQToolkit;
using Microsoft.Office.Server.Search.Query;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Taxonomy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Codeless.SharePoint.ObjectModel.Linq {
  internal class SPModelQueryProvider : QueryProvider {
    private readonly ISPModelManagerInternal manager;
    private readonly bool useOfficeSearch;
    private readonly string[] keywords;
    private readonly KeywordInclusion keywordInclusion;

    public SPModelQueryProvider(ISPModelManagerInternal manager) {
      CommonHelper.ConfirmNotNull(manager, "manager");
      this.manager = manager;
    }

    public SPModelQueryProvider(ISPModelManagerInternal manager, string[] keywords, KeywordInclusion keywordInclusion)
      : this(manager) {
      this.useOfficeSearch = true;
      this.keywords = keywords;
      this.keywordInclusion = keywordInclusion;
    }

    public override object Execute(Expression expression) {
      if (expression.NodeType == ExpressionType.Constant) {
        SPModelQuery query1 = new SPModelQuery(manager);
        PrepQuery(query1);
        return manager.GetItems(query1);
      }
      SPModelParameterizedQuery query = SPModelParameterizedQuery.Create(expression, manager);
      PrepQuery(query);
      return query.Execute();
    }

    public override string GetQueryText(Expression expression) {
      throw new NotSupportedException();
    }

    private void PrepQuery(SPModelQuery query) {
      if (useOfficeSearch) {
        query.ForceKeywordSearch = true;
        query.Keywords = keywords;
        query.KeywordInclusion = keywordInclusion;
      }
    }
  }
}