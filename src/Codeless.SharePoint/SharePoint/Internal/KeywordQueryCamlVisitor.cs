using Microsoft.Office.Server.Search.Query;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Codeless.SharePoint.Internal {
  internal class KeywordQueryCamlVisitor : CamlExpressionVisitor {
    private readonly StringBuilder queryBuilder = new StringBuilder();
    private readonly IReadOnlyDictionary<string, string> managedPropertyDictionary;
    private readonly KeywordQuery query;
    private readonly Hashtable bindings;
    private CamlLogicalOperator currentOperater = CamlLogicalOperator.And;
    private bool whereExpressionScope;

    private class WhereExpressionScope : IDisposable {
      private readonly StringBuilder queryBuilder;
      private readonly KeywordQuery query;
      private readonly KeywordQueryCamlVisitor visitor;
      private readonly CamlLogicalOperator previousOperator;

      public WhereExpressionScope(KeywordQueryCamlVisitor visitor)
        : this(visitor, visitor.currentOperater) { }

      public WhereExpressionScope(KeywordQueryCamlVisitor visitor, CamlLogicalOperator currentOperator) {
        if (!visitor.whereExpressionScope) {
          visitor.whereExpressionScope = true;
          this.query = visitor.query;
          this.queryBuilder = visitor.queryBuilder;
        }
        this.previousOperator = visitor.currentOperater;
        this.visitor = visitor;
        visitor.currentOperater = currentOperator == CamlLogicalOperator.Not ? previousOperator : currentOperator;
      }

      public void Dispose() {
        if (queryBuilder != null) {
          query.QueryText = queryBuilder.ToString();
        }
        visitor.currentOperater = previousOperator;
      }
    }

    public KeywordQueryCamlVisitor(KeywordQuery query, Hashtable bindings) {
      CommonHelper.ConfirmNotNull(query, "query");
      CommonHelper.ConfirmNotNull(bindings, "bindings");
      this.query = query;
      this.bindings = bindings;
      this.managedPropertyDictionary = SearchServiceHelper.GetManagedPropertyNames(query.Site);
    }

    public new void Visit(CamlExpression expression) {
      base.Visit(expression);
    }
    
    protected override CamlExpression VisitViewFieldsFieldRefExpression(CamlViewFieldsFieldRefExpression expression) {
      query.SelectProperties.Add(GetPropertyName(expression.FieldName));
      return base.VisitViewFieldsFieldRefExpression(expression);
    }

    protected override CamlExpression VisitOrderByFieldRefExpression(CamlOrderByFieldRefExpression expression) {
      string orderDirection = expression.Order.Bind(bindings);
      if (orderDirection == Caml.BooleanString.True) {
        query.SortList.Add(GetPropertyName(expression.FieldName), SortDirection.Ascending);
      } else {
        query.SortList.Add(GetPropertyName(expression.FieldName), SortDirection.Descending);
      }
      return base.VisitOrderByFieldRefExpression(expression);
    }

    protected override CamlExpression VisitGroupByFieldRefExpression(CamlGroupByFieldRefExpression expression) {
      throw new NotSupportedException("Unsupported GroupBy expression");
    }

    protected override CamlExpression VisitWhereUnaryComparisonExpression(CamlWhereUnaryComparisonExpression expression) {
      throw new NotSupportedException(String.Format("Unsupported {0} unary operator", expression.Operator));
    }

    protected override CamlExpression VisitWhereBinaryComparisonExpression(CamlWhereBinaryComparisonExpression expression) {
      using (new WhereExpressionScope(this)) {
        string propertyName = GetPropertyName(expression.FieldName);
        if (expression.Operator == CamlBinaryOperator.In) {
          bool appendOr = false;
          queryBuilder.Append("(");
          foreach (string value in expression.Value.BindCollection(bindings)) {
            if (appendOr) {
              queryBuilder.Append(" OR ");
            }
            queryBuilder.Append(propertyName);
            queryBuilder.Append("=\"");
            queryBuilder.Append(value);
            queryBuilder.Append("\"");
            appendOr = true;
          }
          queryBuilder.Append(")");
        } else {
          queryBuilder.Append(GetPropertyName(expression.FieldName));
          queryBuilder.Append(GetKqlOperator(expression.Operator));
          queryBuilder.Append("\"");
          queryBuilder.Append(expression.Value.Bind(bindings));
          if (expression.Operator == CamlBinaryOperator.BeginsWith) {
            queryBuilder.Append("*");
          }
          queryBuilder.Append("\"");
        }
      }
      return base.VisitWhereBinaryComparisonExpression(expression);
    }

    protected override CamlExpression VisitWhereLogicalExpression(CamlWhereLogicalExpression expression) {
      CamlLogicalOperator previousOperator = currentOperater;
      using (new WhereExpressionScope(this, expression.Operator)) {
        if (expression.Operator == CamlLogicalOperator.Not) {
          queryBuilder.Append(" -");
          Visit(expression.Left);
        } else {
          if (previousOperator != expression.Operator) {
            queryBuilder.Append("(");
          }
          Visit(expression.Left);
          if (expression.Operator == CamlLogicalOperator.Or) {
            queryBuilder.Append(" OR ");
          } else {
            queryBuilder.Append(" ");
          }
          Visit(expression.Right);
          if (previousOperator != expression.Operator) {
            queryBuilder.Append(")");
          }
        }
      }
      return expression;
    }

    protected override CamlExpression VisitWhereExpression(CamlWhereExpression expression) {
      using (new WhereExpressionScope(this)) {
        return base.VisitWhereExpression(expression);
      }
    }

    private string GetPropertyName(CamlParameterBindingFieldRef fieldRef) {
      string fieldName = fieldRef.Bind(bindings);
      string propertyName;
      if (managedPropertyDictionary.TryGetValue(fieldName, out propertyName)) {
        return propertyName;
      }
      return fieldName;
    }

    private string GetKqlOperator(CamlBinaryOperator value) {
      switch (value) {
        case CamlBinaryOperator.Eq:
          return "=";
        case CamlBinaryOperator.Neq:
          return "<>";
        case CamlBinaryOperator.Geq:
          return ">=";
        case CamlBinaryOperator.Gt:
          return ">";
        case CamlBinaryOperator.Leq:
          return "<=";
        case CamlBinaryOperator.Lt:
          return "<";
        case CamlBinaryOperator.Contains:
        case CamlBinaryOperator.BeginsWith:
          return ":";
        default:
          throw new NotSupportedException(String.Format("Unsupported {0} binary operator", value));
      }
    }
  }
}
