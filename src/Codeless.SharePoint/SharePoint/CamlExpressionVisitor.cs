using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Codeless.SharePoint {
  /// <summary>
  /// Providers a base class that visits a CAML expression.
  /// </summary>
  public abstract class CamlExpressionVisitor {
    /// <summary>
    /// Instantiate an instance of the <see cref="CamlExpressionVisitor"/> class.
    /// </summary>
    public CamlExpressionVisitor() {
      this.Bindings = CamlExpression.EmptyBindings;
    }

    /// <summary>
    /// Gets the values binded to the CAML expression that is visiting.
    /// </summary>
    protected Hashtable Bindings { get; private set; }

    /// <summary>
    /// Called when visiting a sub-expression.
    /// </summary>
    /// <param name="expression">An instance of the <see cref="CamlExpression"/> class representing the visiting expression.</param>
    /// <returns>When overriden, returns an expression to replace the expression given in arguments.</returns>
    protected virtual CamlExpression Visit(CamlExpression expression) {
      CommonHelper.ConfirmNotNull(expression, "expression");
      switch (expression.Type) {
        case CamlExpressionType.Binded:
          return VisitBindedExpression((CamlBindedExpression)expression);
        case CamlExpressionType.GroupBy:
          return VisitGroupByExpression((CamlGroupByExpression)expression);
        case CamlExpressionType.GroupByFieldRef:
          return VisitGroupByFieldRefExpression((CamlGroupByFieldRefExpression)expression);
        case CamlExpressionType.OrderBy:
          return VisitOrderByExpression((CamlOrderByExpression)expression);
        case CamlExpressionType.OrderByFieldRef:
          return VisitOrderByFieldRefExpression((CamlOrderByFieldRefExpression)expression);
        case CamlExpressionType.Query:
          return VisitQueryExpression((CamlQueryExpression)expression);
        case CamlExpressionType.ViewFields:
          return VisitViewFieldsExpression((CamlViewFieldsExpression)expression);
        case CamlExpressionType.ViewFieldsFieldRef:
          return VisitViewFieldsFieldRefExpression((CamlViewFieldsFieldRefExpression)expression);
        case CamlExpressionType.Where:
          return VisitWhereExpression((CamlWhereExpression)expression);
        case CamlExpressionType.WhereBinaryComparison:
          return VisitWhereBinaryComparisonExpression((CamlWhereBinaryComparisonExpression)expression);
        case CamlExpressionType.WhereLogical:
          return VisitWhereLogicalExpression((CamlWhereLogicalExpression)expression);
        case CamlExpressionType.WhereUnaryComparison:
          return VisitWhereUnaryComparisonExpression((CamlWhereUnaryComparisonExpression)expression);
      }
      return expression;
    }

    /// <summary>
    /// Called when visiting a CAML expression with binded values.
    /// </summary>
    /// <param name="expression">An instance of the <see cref="CamlBindedExpression"/> class representing the value-binded expression.</param>
    /// <returns>When overriden, returns an expression to replace the expression given in arguments.</returns>
    protected virtual CamlExpression VisitBindedExpression(CamlBindedExpression expression) {
      Hashtable previous = this.Bindings;
      try {
        this.Bindings = expression.Bindings;
        CamlExpression result = Visit(expression.Expression);
        if (result != expression.Expression) {
          return new CamlBindedExpression(result, this.Bindings);
        }
        return expression;
      } finally {
        this.Bindings = previous;
      }
    }

    /// <summary>
    /// Called when visiting a &lt;ViewFields/&gt; element.
    /// </summary>=
    /// <param name="expression">An instance of the <see cref="CamlViewFieldsExpression"/> class representing the &lt;ViewFields/&gt; expression.</param>
    /// <returns>When overriden, returns an expression to replace the expression given in arguments.</returns>
    protected virtual CamlExpression VisitViewFieldsExpression(CamlViewFieldsExpression expression) {
      CommonHelper.ConfirmNotNull(expression, "expression");
      return VisitExpressionList(expression);
    }

    /// <summary>
    /// Called when visiting a &lt;OrderBy/&gt; element.
    /// </summary>=
    /// <param name="expression">An instance of the <see cref="CamlOrderByExpression"/> class representing the &lt;OrderBy/&gt; expression.</param>
    /// <returns>When overriden, returns an expression to replace the expression given in arguments.</returns>
    protected virtual CamlExpression VisitOrderByExpression(CamlOrderByExpression expression) {
      CommonHelper.ConfirmNotNull(expression, "expression");
      return VisitExpressionList(expression);
    }

    /// <summary>
    /// Called when visiting a &lt;GroupBy/&gt; element.
    /// </summary>=
    /// <param name="expression">An instance of the <see cref="CamlGroupByExpression"/> class representing the &lt;GroupBy/&gt; expression.</param>
    /// <returns>When overriden, returns an expression to replace the expression given in arguments.</returns>
    protected virtual CamlExpression VisitGroupByExpression(CamlGroupByExpression expression) {
      CommonHelper.ConfirmNotNull(expression, "expression");
      return VisitExpressionList(expression);
    }

    /// <summary>
    /// Called when visiting a &lt;FieldRef/&gt; expression inside a &lt;ViewFields/&gt; element.
    /// </summary>
    /// <param name="expression">An instance of the <see cref="CamlViewFieldsFieldRefExpression"/> class representing the &lt;FieldRef/&gt; expression.</param>
    /// <returns>When overriden, returns an expression to replace the expression given in arguments.</returns>
    protected virtual CamlExpression VisitViewFieldsFieldRefExpression(CamlViewFieldsFieldRefExpression expression) {
      return expression;
    }

    /// <summary>
    /// Called when visiting a &lt;FieldRef/&gt; expression inside an &lt;OrderBy/&gt; element.
    /// </summary>
    /// <param name="expression">An instance of the <see cref="CamlOrderByFieldRefExpression"/> class representing the &lt;FieldRef/&gt; expression.</param>
    /// <returns>When overriden, returns an expression to replace the expression given in arguments.</returns>
    protected virtual CamlExpression VisitOrderByFieldRefExpression(CamlOrderByFieldRefExpression expression) {
      return expression;
    }

    /// <summary>
    /// Called when visiting a &lt;FieldRef/&gt; expression inside a &lt;GroupBy/&gt; element.
    /// </summary>
    /// <param name="expression">An instance of the <see cref="CamlGroupByFieldRefExpression"/> class representing the &lt;FieldRef/&gt; expression.</param>
    /// <returns>When overriden, returns an expression to replace the expression given in arguments.</returns>
    protected virtual CamlExpression VisitGroupByFieldRefExpression(CamlGroupByFieldRefExpression expression) {
      return expression;
    }

    /// <summary>
    /// Called when visiting a unary comparison expression inside a &lt;Where/&gt; element.
    /// </summary>
    /// <param name="expression">An instance of the <see cref="CamlWhereUnaryComparisonExpression"/> class representing the unary comparison expression.</param>
    /// <returns>When overriden, returns an expression to replace the expression given in arguments.</returns>
    protected virtual CamlExpression VisitWhereUnaryComparisonExpression(CamlWhereUnaryComparisonExpression expression) {
      return expression;
    }

    /// <summary>
    /// Called when visiting a binary comparison expression inside a &lt;Where/&gt; element.
    /// </summary>
    /// <param name="expression">An instance of the <see cref="CamlWhereBinaryComparisonExpression"/> class representing the binary comparison expression.</param>
    /// <returns>When overriden, returns an expression to replace the expression given in arguments.</returns>
    protected virtual CamlExpression VisitWhereBinaryComparisonExpression(CamlWhereBinaryComparisonExpression expression) {
      return expression;
    }

    /// <summary>
    /// Called when visiting a logical comparison expression inside a &lt;Where/&gt; element.
    /// </summary>
    /// <param name="expression">An instance of the <see cref="CamlWhereLogicalExpression"/> class representing the logical comparison expression.</param>
    /// <returns>When overriden, returns an expression to replace the expression given in arguments.</returns>
    protected virtual CamlExpression VisitWhereLogicalExpression(CamlWhereLogicalExpression expression) {
      CommonHelper.ConfirmNotNull(expression, "expression");
      CamlExpression l = VisitChecked(expression.Left, CamlExpressionType.Where);
      CamlExpression r = VisitChecked(expression.Right, CamlExpressionType.Where);
      if (l != expression.Left || r != expression.Right) {
        switch (expression.Operator) {
          case CamlLogicalOperator.And:
            return Caml.And(l, r);
          case CamlLogicalOperator.Or:
            return Caml.Or(l, r);
          case CamlLogicalOperator.Not:
            return Caml.Not(l);
        }
        throw new InvalidOperationException();
      }
      return expression;
    }

    /// <summary>
    /// Called when visiting a &lt;Where/&gt; expression.
    /// </summary>
    /// <param name="expression">An instance of the <see cref="CamlWhereExpression"/> class representing the &lt;Where/&gt; expression.</param>
    /// <returns>When overriden, returns an expression to replace the expression given in arguments.</returns>
    protected virtual CamlExpression VisitWhereExpression(CamlWhereExpression expression) {
      CommonHelper.ConfirmNotNull(expression, "expression");
      CamlExpression bodyExpression = VisitChecked(expression.Body, CamlExpressionType.Where);
      if (bodyExpression != expression.Body) {
        switch (bodyExpression.Type) {
          case CamlExpressionType.WhereUnaryComparison:
          case CamlExpressionType.WhereBinaryComparison:
          case CamlExpressionType.WhereLogical:
            return new CamlWhereExpression((CamlWhereComparisonExpression)bodyExpression);
          case CamlExpressionType.Where:
          case CamlExpressionType.Empty:
            return bodyExpression;
        }
        throw new InvalidOperationException();
      }
      return expression;
    }

    /// <summary>
    /// Called when visiting a &lt;Query/&gt; expression.
    /// </summary>
    /// <param name="expression">An instance of the <see cref="CamlQueryExpression"/> class representing the &lt;Query/&gt; expression.</param>
    /// <returns>When overriden, returns an expression to replace the expression given in arguments.</returns>
    protected virtual CamlExpression VisitQueryExpression(CamlQueryExpression expression) {
      CommonHelper.ConfirmNotNull(expression, "expression");
      CamlExpression x = VisitChecked(expression.Where, CamlExpressionType.Where);
      CamlExpression y = VisitChecked(expression.OrderBy, CamlExpressionType.OrderBy);
      CamlExpression z = VisitChecked(expression.GroupBy, CamlExpressionType.GroupBy);
      if (x != expression.Where || y != expression.OrderBy || z != expression.GroupBy) {
        return new CamlQueryExpression(x as ICamlQueryComponent<CamlWhereExpression>, y as ICamlQueryComponent<CamlOrderByExpression>, z as ICamlQueryComponent<CamlGroupByExpression>);
      }
      return expression;
    }

    private CamlExpression VisitExpressionList<T>(CamlExpressionList<T> expression) where T : CamlExpression {
      CamlExpression[] src = expression.Expressions;
      CamlExpression[] dst = new CamlExpression[src.Length];
      Type expectedType = null;
      switch (expression.Type) {
        case CamlExpressionType.GroupBy:
          expectedType = typeof(CamlGroupByExpression);
          break;
        case CamlExpressionType.OrderBy:
          expectedType = typeof(CamlOrderByExpression);
          break;
        case CamlExpressionType.ViewFields:
          expectedType = typeof(CamlViewFieldsExpression);
          break;
      }
      for (int i = 0; i < src.Length; i++) {
        dst[i] = VisitChecked(src[i], expression.Type);
      }
      if (!src.SequenceEqual(dst)) {
        IEnumerable<CamlFieldRefExpression> fields = dst.OfType<ICamlFieldRefComponent>().SelectMany(v => v.EnumerateFieldRefExpression());
        switch (expression.Type) {
          case CamlExpressionType.GroupBy:
            return new CamlGroupByExpression(fields.OfType<CamlGroupByFieldRefExpression>());
          case CamlExpressionType.OrderBy:
            return new CamlOrderByExpression(fields.OfType<CamlOrderByFieldRefExpression>());
          case CamlExpressionType.ViewFields:
            return new CamlViewFieldsExpression(fields.OfType<CamlViewFieldsFieldRefExpression>());
        }
      }
      return expression;
    }

    private CamlExpression VisitChecked(CamlExpression expression, CamlExpressionType expressionType) {
      if (expression != null) {
        CamlExpression result = Visit(expression);
        if (result != expression) {
          if (result == null) {
            throw new InvalidOperationException(String.Format("Expected an expression of compatiable type to {0} but NULL returned.", expressionType));
          }
          if (result.Type != CamlExpressionType.Empty && result.Type != expression.Type) {
            Type expectedType = null;
            switch (expressionType) {
              case CamlExpressionType.Where:
                expectedType = typeof(ICamlQueryComponent<CamlWhereExpression>);
                break;
              case CamlExpressionType.GroupBy:
                expectedType = typeof(ICamlQueryComponent<CamlWhereExpression>);
                break;
              case CamlExpressionType.OrderBy:
                expectedType = typeof(ICamlQueryComponent<CamlWhereExpression>);
                break;
              case CamlExpressionType.ViewFields:
                expectedType = typeof(CamlViewFieldsExpression);
                break;
            }
            if (!result.GetType().IsOf(expectedType)) {
              throw new InvalidOperationException(String.Format("Expected an expression of compatiable type to {0} but expression of type {1} returned.", expressionType, result.Type));
            }
          }
        }
        return result;
      }
      return null;
    }
  }
}
