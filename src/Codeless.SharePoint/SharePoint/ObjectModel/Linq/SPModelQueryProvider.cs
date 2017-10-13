using IQToolkit;
using Microsoft.Office.Server.Search.Query;
using Microsoft.SharePoint;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Codeless.SharePoint.ObjectModel.Linq {
  internal class SPModelQueryProvider<T> : QueryProvider {
    private readonly SPModelManagerBase<T> manager;
    private readonly bool useOfficeSearch;
    private readonly string[] keywords;
    private readonly KeywordInclusion keywordInclusion;

    public SPModelQueryProvider(SPModelManagerBase<T> manager) {
      CommonHelper.ConfirmNotNull(manager, "manager");
      this.manager = manager;
    }

    public SPModelQueryProvider(SPModelManagerBase<T> manager, string[] keywords, KeywordInclusion keywordInclusion)
      : this(manager) {
      this.useOfficeSearch = true;
      this.keywords = keywords;
      this.keywordInclusion = keywordInclusion;
    }

    public SPModelQueryExpressionTranslateResult Translate(Expression expression) {
      string[] allowedFields = null;
      if (!useOfficeSearch && manager.ImplicitQueryMode == SPModelImplicitQueryMode.ListQuery) {
        SPList targetList = manager.ContextLists.First().EnsureList(manager.ObjectCache).List;
        if (targetList != null) {
          allowedFields = targetList.Fields.OfType<SPField>().Select(v => v.InternalName).ToArray();
        }
      }
      return new SPModelQueryExpressionVisitor(manager, allowedFields).Translate(expression);
    }

    public override object Execute(Expression expression) {
      SPModelQueryExpressionTranslateResult result = Translate(expression);
      if (result.ModelType != null && result.ModelType != typeof(T)) {
        return typeof(SPModelQueryProvider<T>).GetMethod("ExecuteInternal", true).MakeGenericMethod(result.ModelType).Invoke<object>(this, result);
      }
      return ExecuteInternal<T>(result);
    }

    public override string GetQueryText(Expression expression) {
      return Translate(expression).Expression.ToString();
    }

    private object ExecuteInternal<U>(SPModelQueryExpressionTranslateResult result) {
      if (result.ExecuteMode == SPModelQueryExecuteMode.Count) {
        if (useOfficeSearch) {
          return Math.Max(0, manager.GetCount<U>(result.Expression, keywords, keywordInclusion) - result.Offset);
        }
        return Math.Max(0, manager.GetCount<U>(result.Expression) - result.Offset);
      }
      if (result.ExecuteMode == SPModelQueryExecuteMode.All) {
        result.Expression = ~result.Expression;
      }

      IEnumerable<U> items;
      if (useOfficeSearch) {
        int dummy;
        items = manager.GetItems<U>(result.Expression, (uint)result.Limit, (uint)result.Offset, keywords, null, keywordInclusion, out dummy);
        result.Offset = 0;
      } else {
        items = manager.GetItems<U>(result.Expression, (uint)(result.Limit + result.Offset));
      }
      if (result.Offset > 0) {
        items = items.Skip(result.Offset);
      }

      if (result.SelectExpression != null && result.ExecuteMode != SPModelQueryExecuteMode.Any && result.ExecuteMode != SPModelQueryExecuteMode.All) {
        Delegate selector = result.SelectExpression.Compile();
        return typeof(SPModelQueryProvider<T>).GetMethod("ProjectResultWithSelector", true).MakeGenericMethod(typeof(U), selector.Method.ReturnType).Invoke<object>(null, items, selector, result.ExecuteMode);
      }
      return ProjectResult(items, result.ExecuteMode);
    }

    private static object ProjectResultWithSelector<TSource, TResult>(IEnumerable<TSource> items, Func<TSource, TResult> selector, SPModelQueryExecuteMode executeMode) {
      return ProjectResult(items.Select(selector), executeMode);
    }

    private static object ProjectResult<TResult>(IEnumerable<TResult> items, SPModelQueryExecuteMode executeMode) {
      switch (executeMode) {
        case SPModelQueryExecuteMode.FirstOrDefault:
        case SPModelQueryExecuteMode.ElementAtOrDefault:
          return items.FirstOrDefault();
        case SPModelQueryExecuteMode.First:
        case SPModelQueryExecuteMode.ElementAt:
          return items.First();
        case SPModelQueryExecuteMode.SingleOrDefault:
          return items.SingleOrDefault();
        case SPModelQueryExecuteMode.Single:
          return items.Single();
        case SPModelQueryExecuteMode.Any:
        case SPModelQueryExecuteMode.All:
          return items.Any();
        default:
          return items;
      }
    }
  }
}