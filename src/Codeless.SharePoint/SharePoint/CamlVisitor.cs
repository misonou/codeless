namespace Codeless.SharePoint {
  /// <summary>
  /// Providers a base class that visits a CAML expression.
  /// </summary>
  public abstract class CamlVisitor {
    /// <summary>
    /// Visits a CAML expression.
    /// </summary>
    /// <param name="expression">A CAML expression.</param>
    public void Visit(CamlExpression expression) {
      expression.VisitInternal(this);
    }

    /// <summary>
    /// Called when visiting a &lt;FieldRef/&gt; expression inside a &lt;ViewFields/&gt; element.
    /// </summary>
    /// <param name="fieldName">Name of view field.</param>
    protected internal abstract void VisitViewFieldsFieldRefExpression(CamlParameterBindingFieldRef fieldName);

    /// <summary>
    /// Called when visiting a &lt;FieldRef/&gt; expression inside an &lt;OrderBy/&gt; element.
    /// </summary>
    /// <param name="fieldName">Name of order field.</param>
    /// <param name="orderBinding">Sort direction of order field.</param>
    protected internal abstract void VisitOrderByFieldRefExpression(CamlParameterBindingFieldRef fieldName, CamlParameterBindingOrder orderBinding);

    /// <summary>
    /// Called when visiting a &lt;FieldRef/&gt; expression inside a &lt;GroupBy/&gt; element.
    /// </summary>
    /// <param name="fieldName">Name of grouping field.</param>
    protected internal abstract void VisitGroupByFieldRefExpression(CamlParameterBindingFieldRef fieldName);

    /// <summary>
    /// Called when visiting a unary expression inside a &lt;Where/&gt; element.
    /// </summary>
    /// <param name="operatorValue">Type of unary operator.</param>
    /// <param name="fieldName">Name of field.</param>
    protected internal abstract void VisitWhereUnaryComparisonExpression(CamlUnaryOperator operatorValue, CamlParameterBindingFieldRef fieldName);

    /// <summary>
    /// Called when visiting a binary expression inside a &lt;Where/&gt; element.
    /// </summary>
    /// <param name="operatorValue">Type of binary operator.</param>
    /// <param name="fieldName">Name of field.</param>
    /// <param name="value">Value to operate against the field.</param>
    /// <param name="includeTimeValue">Indicates whether time component is included in comparison.</param>
    protected internal abstract void VisitWhereBinaryComparisonExpression(CamlBinaryOperator operatorValue, CamlParameterBindingFieldRef fieldName, ICamlParameterBinding value, bool? includeTimeValue);

    /// <summary>
    /// Called when visiting a logical comparison expression inside a &lt;Where/&gt; element.
    /// </summary>
    /// <param name="operatorValue">Type of logical operator.</param>
    /// <param name="leftExpression">Left expression.</param>
    /// <param name="rightExpression">Right expression.</param>
    protected internal abstract void VisitWhereLogicalExpression(CamlLogicalOperator operatorValue, CamlExpression leftExpression, CamlExpression rightExpression);

    /// <summary>
    /// Called when visiting a &lt;Where/&gt; expression.
    /// </summary>
    /// <param name="expression">Subexpression contained.</param>
    protected internal abstract void VisitWhereExpression(CamlExpression expression);
  }
}
