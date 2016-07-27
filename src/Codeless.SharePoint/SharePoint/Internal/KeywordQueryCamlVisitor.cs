using Microsoft.Office.Server.Search.Query;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Codeless.SharePoint.Internal {
  internal class KeywordQueryCamlVisitor : CamlVisitor {
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

    protected internal override void VisitViewFieldsFieldRefExpression(CamlParameterBindingFieldRef fieldName) {
      query.SelectProperties.Add(GetPropertyName(fieldName));
    }

    protected internal override void VisitOrderByFieldRefExpression(CamlParameterBindingFieldRef fieldName, CamlParameterBindingOrder orderBinding) {
      string orderDirection = orderBinding.Bind(bindings);
      if (orderDirection == Caml.BooleanString.True) {
        query.SortList.Add(GetPropertyName(fieldName), SortDirection.Ascending);
      } else {
        query.SortList.Add(GetPropertyName(fieldName), SortDirection.Descending);
      }
    }

    protected internal override void VisitGroupByFieldRefExpression(CamlParameterBindingFieldRef fieldName) {
      throw new NotSupportedException("Unsupported GroupBy expression");
    }

    protected internal override void VisitWhereUnaryComparisonExpression(CamlUnaryOperator operatorValue, CamlParameterBindingFieldRef fieldName) {
      throw new NotSupportedException(String.Format("Unsupported {0} unary operator", operatorValue));
    }

    protected internal override void VisitWhereBinaryComparisonExpression(CamlBinaryOperator operatorValue, CamlParameterBindingFieldRef fieldName, ICamlParameterBinding value, bool? includeTimeValue) {
      using (new WhereExpressionScope(this)) {
        queryBuilder.Append(GetPropertyName(fieldName));
        queryBuilder.Append(GetKqlOperator(operatorValue));
        queryBuilder.Append("\"");
        queryBuilder.Append(value.Bind(bindings));
        if (operatorValue == CamlBinaryOperator.BeginsWith) {
          queryBuilder.Append("*");
        }
        queryBuilder.Append("\"");
      }
    }

    protected internal override void VisitWhereLogicalExpression(CamlLogicalOperator operatorValue, CamlExpression leftExpression, CamlExpression rightExpression) {
      CamlLogicalOperator previousOperator = currentOperater;
      using (new WhereExpressionScope(this, operatorValue)) {
        if (operatorValue == CamlLogicalOperator.Not) {
          queryBuilder.Append(" -");
          Visit(leftExpression);
        } else {
          if (previousOperator != operatorValue) {
            queryBuilder.Append("(");
          }
          Visit(leftExpression);
          if (operatorValue == CamlLogicalOperator.Or) {
            queryBuilder.Append(" OR ");
          } else {
            queryBuilder.Append(" ");
          }
          Visit(rightExpression);
          if (previousOperator != operatorValue) {
            queryBuilder.Append(")");
          }
        }
      }
    }

    protected internal override void VisitWhereExpression(CamlExpression expression) {
      using (new WhereExpressionScope(this)) {
        Visit(expression);
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
