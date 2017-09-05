using Microsoft.SharePoint;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace Codeless.SharePoint {
  internal sealed class CamlInvalidJoinException : CamlException {
    public CamlInvalidJoinException(Caml.JoinType type, CamlExpressionType x, CamlExpressionType y)
      : base(String.Concat("Cannot perform ", type.ToString(), " operation on ", x.ToString(), " and ", y.ToString(), " expression")) { }

    public CamlInvalidJoinException(Caml.JoinType type, CamlExpressionType x)
      : base(String.Concat("Cannot perform ", type.ToString(), " operation on ", x.ToString(), " expression")) { }
  }

  /// <summary>
  /// Provides a base class of a range of objects representating different types of CAML expressions.
  /// </summary>
  public abstract class CamlExpression : Caml {
    private sealed class ReadOnlyHashtable : Hashtable {
      public override void Add(object key, object value) {
        throw new InvalidOperationException();
      }

      public override object this[object key] {
        get { return base[key]; }
        set { throw new InvalidOperationException(); }
      }
    }

    /// <summary>
    /// Appends a string value to a <see cref="StringBuilder"/> instance.
    /// </summary>
    /// <param name="value">Value to append.</param>
    /// <returns>A <see cref="StringBuilder"/> instance.</returns>
    protected delegate StringBuilder AppendToStringBuilder(string value);

    /// <summary>
    /// Represents an empty collection of parameter values. This collection is read-only.
    /// </summary>
    protected internal static readonly Hashtable EmptyBindings = new ReadOnlyHashtable();

    /// <summary>
    /// Performs a logical And operation between two CAML expressions.
    /// </summary>
    /// <param name="x">First expression.</param>
    /// <param name="y">Second expression.</param>
    /// <returns>A resulting expression.</returns>
    public static CamlExpression operator &(CamlExpression x, CamlExpression y) {
      return HandleAnd(x, y, true);
    }

    /// <summary>
    /// Performs a logical And operation between two CAML expressions.
    /// </summary>
    /// <param name="x">First expression.</param>
    /// <param name="y">Second expression.</param>
    /// <returns>A resulting expression.</returns>
    public static CamlExpression operator +(CamlExpression x, CamlExpression y) {
      return HandleAnd(x, y, true);
    }

    /// <summary>
    /// Performs a logical Or operation between two CAML expressions.
    /// </summary>
    /// <param name="x">First expression.</param>
    /// <param name="y">Second expression.</param>
    /// <returns>A resulting expression.</returns>
    public static CamlExpression operator |(CamlExpression x, CamlExpression y) {
      return HandleOr(x, y, true);
    }

    /// <summary>
    /// Performs a negation operation on a CAML expressions.
    /// </summary>
    /// <param name="x">A CAML expression.</param>
    /// <returns>A resulting expression.</returns>
    public static CamlExpression operator ~(CamlExpression x) {
      if (x == null) {
        return null;
      }
      return x.HandleNegate();
    }

    /// <summary>
    /// Converts this expression into an equivalent string representation.
    /// </summary>
    /// <param name="x">A CAML expression.</param>
    /// <returns>A string representation of this expression.</returns>
    public static explicit operator string(CamlExpression x) {
      if (x == null) {
        return String.Empty;
      }
      return x.ToString();
    }

    /// <summary>
    /// Gets the type of this expression.
    /// </summary>
    public virtual CamlExpressionType Type {
      get { return CamlExpressionType.Invalid; }
    }

    /// <summary>
    /// Gets an equivalent string representation of this expression.
    /// </summary>
    /// <returns>A string representation of this expression.</returns>
    public override string ToString() {
      return ToString(false);
    }

    /// <summary>
    /// Gets an equivalent string representation of this expression, optionally enables new lines and indentation when formatting output XML.
    /// </summary>
    /// <param name="indent">Whether to enable new lines and indentation when formatting output XML.</param>
    /// <returns>A string representation of this expression.</returns>
    public virtual string ToString(bool indent) {
      return ToString(EmptyBindings, indent);
    }

    /// <summary>
    /// Gets an equivalent string representation of this expression, with values to be binded on parameters.
    /// </summary>
    /// <param name="bindings">A collection of parameter values.</param>
    /// <returns>A string representation of this expression.</returns>
    public virtual string ToString(Hashtable bindings) {
      CommonHelper.ConfirmNotNull(bindings, "bindings");
      return ToString(bindings, true);
    }

    /// <summary>
    /// Gets an equivalent string representation of this expression, with values to be binded on parameters, and optionally enables new lines and indentation when formatting output XML.
    /// </summary>
    /// <param name="bindings">A collection of parameter values.</param>
    /// <param name="indent">Whether to enable new lines and indentation when formatting output XML.</param>
    /// <returns>A string representation of this expression.</returns>
    public virtual string ToString(Hashtable bindings, bool indent) {
      CommonHelper.ConfirmNotNull(bindings, "bindings");
      XmlWriterSettings settings = new XmlWriterSettings {
        Indent = indent,
        OmitXmlDeclaration = true
      };
      return ToString(settings, bindings);
    }

    /// <summary>
    /// Gets an equivalent string representation of this expression, with values to be binded on parameters and specified XML writer settings.
    /// </summary>
    /// <param name="settings">XML writer settings.</param>
    /// <param name="bindings">A collection of parameter values.</param>
    /// <returns>A string representation of this expression.</returns>
    protected virtual string ToString(XmlWriterSettings settings, Hashtable bindings) {
      StringBuilder sb = new StringBuilder();
      using (XmlWriter writer = XmlWriter.Create(sb, settings)) {
        WriteXml(writer, bindings);
        writer.Flush();
      }
      string queryText = sb.ToString();
      LastQueryText = queryText;
      return queryText;
    }

    /// <summary>
    /// Binds values to parameters.
    /// </summary>
    /// <param name="bindings">A collection of parameter values.</param>
    /// <returns>An expression with binding values.</returns>
    public virtual CamlExpression Bind(Hashtable bindings) {
      CommonHelper.ConfirmNotNull(bindings, "bindings");
      return new CamlBindedExpression(this, bindings);
    }

    /// <summary>
    /// Gets an expression equivalent to a &lt;ViewFields/&gt; that contains all fields referenced by this expression.
    /// </summary>
    /// <returns></returns>
    public virtual CamlExpression GetViewFieldsExpression() {
      if (this is ICamlFieldRefComponent) {
        HashSet<CamlFieldRefExpression> fieldRefs = new HashSet<CamlFieldRefExpression>();
        foreach (CamlFieldRefExpression fieldRef in ((ICamlFieldRefComponent)this).EnumerateFieldRefExpression()) {
          fieldRefs.Add(fieldRef);
        }
        return new CamlViewFieldsExpression(fieldRefs.Select(CamlFieldRefExpression.ConvertToViewFieldsFieldRefExpression));
      }
      return null;
    }

    /// <summary>
    /// When overriden, handles a logical And operation against another expression.
    /// </summary>
    /// <param name="x">Another expression.</param>
    /// <returns>A resulting expression.</returns>
    protected virtual CamlExpression HandleAnd(CamlExpression x, bool selfPreceding) {
      throw new CamlInvalidJoinException(JoinType.And, this.Type, x.Type);
    }

    /// <summary>
    /// When overriden, handles a logical Or operation against another expression.
    /// </summary>
    /// <param name="x">Another expression.</param>
    /// <returns>A resulting expression.</returns>
    protected virtual CamlExpression HandleOr(CamlExpression x, bool selfPreceding) {
      throw new CamlInvalidJoinException(JoinType.Or, this.Type, x.Type);
    }

    /// <summary>
    /// When overriden, handles a negation operation.
    /// </summary>
    /// <returns>A resulting expression.</returns>
    protected virtual CamlExpression HandleNegate() {
      throw new CamlInvalidJoinException(JoinType.Negate, this.Type);
    }

    internal void VisitInternal(CamlVisitor visitor) {
      Visit(visitor);
    }

    /// <summary>
    /// When overriden, handles a visitor visit.
    /// </summary>
    /// <param name="visitor"></param>
    protected abstract void Visit(CamlVisitor visitor);

    /// <summary>
    /// When overriden, outputs XML that is an equivalent representation of this expression.
    /// </summary>
    /// <param name="writer">An XML writer object.</param>
    /// <param name="bindings">A collection of parameter values.</param>
    protected abstract void WriteXml(XmlWriter writer, Hashtable bindings);

    /// <summary>
    /// Reserved for internal use.
    /// </summary>
    /// <param name="x"></param>
    /// <param name="settings"></param>
    /// <param name="bindings"></param>
    /// <returns></returns>
    protected static string ToStringStatic(CamlExpression x, XmlWriterSettings settings, Hashtable bindings) {
      return x.ToString(settings, bindings);
    }

    /// <summary>
    /// Reserved for internal use.
    /// </summary>
    /// <param name="x"></param>
    /// <param name="writer"></param>
    /// <param name="bindings"></param>
    protected static void WriteXmlStatic(CamlExpression x, XmlWriter writer, Hashtable bindings) {
      x.WriteXml(writer, bindings);
    }

    protected static CamlExpression HandleAnd(CamlExpression x, CamlExpression y, bool selfPreceding) {
      if (x == null) {
        return y;
      }
      if (y == null) {
        return x;
      }
      if (x.Type >= y.Type) {
        return x.HandleAnd(y, selfPreceding);
      }
      return y.HandleAnd(x, !selfPreceding);
    }

    protected static CamlExpression HandleOr(CamlExpression x, CamlExpression y, bool selfPreceding) {
      if (x == null) {
        return y;
      }
      if (y == null) {
        return x;
      }
      if (x.Type >= y.Type) {
        return x.HandleOr(y, selfPreceding);
      }
      return y.HandleOr(x, !selfPreceding);
    }

    /// <summary>
    /// Provides fast conversion from <see cref="CamlUnaryOperator"/> enum values to its string representation.
    /// </summary>
    /// <param name="value">Value to convert.</param>
    /// <returns>An string representation of the operator.</returns>
    protected static string GetOperatorString(CamlUnaryOperator value) {
      switch (value) {
        case CamlUnaryOperator.IsNotNull:
          return CompareOperatorString.IsNotNull;
        case CamlUnaryOperator.IsNull:
          return CompareOperatorString.IsNull;
        default:
          return value.ToString();
      }
    }

    /// <summary>
    /// Provides fast conversion from <see cref="CamlBinaryOperator"/> enum values to its string representation.
    /// </summary>
    /// <param name="value">Value to convert.</param>
    /// <returns>An string representation of the operator.</returns>
    protected static string GetOperatorString(CamlBinaryOperator value) {
      switch (value) {
        case CamlBinaryOperator.BeginsWith:
          return CompareOperatorString.BeginsWith;
        case CamlBinaryOperator.Contains:
          return CompareOperatorString.Contains;
        case CamlBinaryOperator.Eq:
          return CompareOperatorString.Eq;
        case CamlBinaryOperator.Geq:
          return CompareOperatorString.Geq;
        case CamlBinaryOperator.Gt:
          return CompareOperatorString.Gt;
        case CamlBinaryOperator.In:
          return CompareOperatorString.In;
        case CamlBinaryOperator.Includes:
          return CompareOperatorString.Includes;
        case CamlBinaryOperator.Leq:
          return CompareOperatorString.Leq;
        case CamlBinaryOperator.Lt:
          return CompareOperatorString.Lt;
        case CamlBinaryOperator.Membership:
          return CompareOperatorString.Membership;
        case CamlBinaryOperator.Neq:
          return CompareOperatorString.Neq;
        case CamlBinaryOperator.NotIncludes:
          return CompareOperatorString.NotIncludes;
        default:
          return value.ToString();
      }
    }

    /// <summary>
    /// Provides fast conversion from <see cref="CamlLogicalOperator"/> enum values to its string representation.
    /// </summary>
    /// <param name="value">Value to convert.</param>
    /// <returns>An string representation of the operator.</returns>
    protected static string GetOperatorString(CamlLogicalOperator value) {
      switch (value) {
        case CamlLogicalOperator.And:
          return CompareOperatorString.And;
        case CamlLogicalOperator.Not:
          return CompareOperatorString.Not;
        case CamlLogicalOperator.Or:
          return CompareOperatorString.Or;
        default:
          return value.ToString();
      }
    }

    /// <summary>
    /// Provides fast conversion from <see cref="CamlValueType"/> enum values to its string representation.
    /// </summary>
    /// <param name="value">Value to convert.</param>
    /// <returns>An string representation of the value type.</returns>
    protected static string GetValueTypeString(CamlValueType value) {
      switch (value) {
        case CamlValueType.Boolean:
          return ValueTypeString.Boolean;
        case CamlValueType.DateTime:
          return ValueTypeString.DateTime;
        case CamlValueType.Guid:
          return ValueTypeString.Guid;
        case CamlValueType.Integer:
          return ValueTypeString.Integer;
        case CamlValueType.Lookup:
          return ValueTypeString.Lookup;
        case CamlValueType.Number:
          return ValueTypeString.Number;
        case CamlValueType.Text:
          return ValueTypeString.Text;
        case CamlValueType.ContentTypeId:
          return ValueTypeString.ContentTypeId;
        case CamlValueType.URL:
          return ValueTypeString.URL;
        case CamlValueType.ModStat:
          return ValueTypeString.ModStat;
        default:
          return value.ToString();
      }
    }

    /// <summary>
    /// Creates a string builder delegate using the specified XML writer settings.
    /// </summary>
    /// <param name="sb">A string builder object.</param>
    /// <param name="settings">XML writer settings.</param>
    /// <returns>A delegate of a string builder.</returns>
    protected static AppendToStringBuilder CreateAppendToStringBuilderDelegate(StringBuilder sb, XmlWriterSettings settings) {
      if (settings.Indent) {
        return sb.AppendLine;
      }
      return sb.Append;
    }
  }

  #region Internal Implementation
  internal interface ICamlFieldRefComponent {
    IEnumerable<CamlFieldRefExpression> EnumerateFieldRefExpression();
  }

  internal interface ICamlQueryComponent<T> {
    T Expression { get; }
  }

  public abstract class CamlFieldRefExpression : CamlExpression, IEquatable<CamlFieldRefExpression>, ICamlFieldRefComponent {
    private readonly CamlParameterBindingFieldRef fieldName;

    internal CamlFieldRefExpression(CamlParameterBindingFieldRef fieldName)
      : base() {
      this.fieldName = fieldName;
    }

    public CamlParameterBindingFieldRef FieldName {
      get { return fieldName; }
    }

    public static bool operator ==(CamlFieldRefExpression x, CamlFieldRefExpression y) {
      return x.Equals(y);
    }

    public static bool operator !=(CamlFieldRefExpression x, CamlFieldRefExpression y) {
      return !x.Equals(y);
    }

    protected override void WriteXml(XmlWriter writer, Hashtable bindings) {
      writer.WriteStartElement(Element.FieldRef);
      writer.WriteAttributeString(Attribute.Name, FieldName.Bind(bindings));
      WriteAttributes(writer, bindings);
      writer.WriteEndElement();
    }

    protected abstract void WriteAttributes(XmlWriter writer, Hashtable bindings);

    public bool Equals(CamlFieldRefExpression other) {
      return FieldName.Equals(other.FieldName);
    }

    public override bool Equals(object obj) {
      if (obj is CamlFieldRefExpression) {
        return Equals((CamlFieldRefExpression)obj);
      }
      return base.Equals(obj);
    }

    public override int GetHashCode() {
      return FieldName.GetHashCode();
    }

    IEnumerable<CamlFieldRefExpression> ICamlFieldRefComponent.EnumerateFieldRefExpression() {
      yield return this;
    }

    public static CamlViewFieldsFieldRefExpression ConvertToViewFieldsFieldRefExpression(CamlFieldRefExpression x) {
      return new CamlViewFieldsFieldRefExpression(x.FieldName);
    }
  }

  public class CamlViewFieldsFieldRefExpression : CamlFieldRefExpression {
    internal CamlViewFieldsFieldRefExpression(CamlParameterBindingFieldRef fieldName)
      : base(fieldName) { }

    public override CamlExpressionType Type {
      get { return CamlExpressionType.ViewFieldsFieldRef; }
    }

    protected override CamlExpression HandleAnd(CamlExpression x, bool selfPreceding) {
      if (x.Type == this.Type) {
        return new CamlViewFieldsExpression(selfPreceding ? new[] { this, (CamlViewFieldsFieldRefExpression)x } : new[] { (CamlViewFieldsFieldRefExpression)x, this });
      }
      return base.HandleAnd(x, selfPreceding);
    }

    protected override void Visit(CamlVisitor visitor) {
      visitor.VisitViewFieldsFieldRefExpression(FieldName);
    }

    protected override void WriteAttributes(XmlWriter writer, Hashtable bindings) {
      writer.WriteAttributeString(Attribute.Nullable, BooleanString.True);
    }
  }

  public class CamlWhereFieldRefExpression : CamlFieldRefExpression {
    private readonly CamlValueType valueType;

    internal CamlWhereFieldRefExpression(CamlParameterBindingFieldRef fieldName)
      : this(fieldName, CamlValueType.Text) { }

    internal CamlWhereFieldRefExpression(CamlParameterBindingFieldRef fieldName, CamlValueType fieldType)
      : base(fieldName) {
      valueType = fieldType;
    }

    public override CamlExpressionType Type {
      get { return CamlExpressionType.Invalid; }
    }

    protected override void Visit(CamlVisitor visitor) { }

    protected override void WriteAttributes(XmlWriter writer, Hashtable bindings) {
      if (valueType == CamlValueType.Lookup) {
        writer.WriteAttributeString(Attribute.LookupId, BooleanString.True);
      }
    }
  }

  public class CamlOrderByFieldRefExpression : CamlFieldRefExpression, ICamlQueryComponent<CamlOrderByExpression> {
    private readonly CamlParameterBindingOrder orderBinding;

    internal CamlOrderByFieldRefExpression(CamlParameterBindingFieldRef fieldName, CamlParameterBindingOrder isAscending)
      : base(fieldName) {
      orderBinding = isAscending;
    }

    public override CamlExpressionType Type {
      get { return CamlExpressionType.OrderByFieldRef; }
    }

    protected override CamlExpression HandleAnd(CamlExpression x, bool selfPreceding) {
      if (x.Type == this.Type) {
        return new CamlOrderByExpression(selfPreceding ? new[] { this, (CamlOrderByFieldRefExpression)x } : new[] { (CamlOrderByFieldRefExpression)x, this });
      }
      return base.HandleAnd(x, selfPreceding);
    }

    protected override void Visit(CamlVisitor visitor) {
      visitor.VisitOrderByFieldRefExpression(FieldName, orderBinding);
    }

    protected override void WriteAttributes(XmlWriter writer, Hashtable bindings) {
      writer.WriteAttributeString(Attribute.Ascending, orderBinding.Bind(bindings));
    }

    CamlOrderByExpression ICamlQueryComponent<CamlOrderByExpression>.Expression {
      get { return new CamlOrderByExpression(this); }
    }
  }

  public class CamlGroupByFieldRefExpression : CamlFieldRefExpression, ICamlQueryComponent<CamlGroupByExpression> {
    internal CamlGroupByFieldRefExpression(CamlParameterBindingFieldRef fieldName)
      : base(fieldName) { }

    public override CamlExpressionType Type {
      get { return CamlExpressionType.GroupByFieldRef; }
    }

    protected override CamlExpression HandleAnd(CamlExpression x, bool selfPreceding) {
      if (x.Type == this.Type) {
        return new CamlGroupByExpression(selfPreceding ? new[] { this, (CamlGroupByFieldRefExpression)x } : new[] { (CamlGroupByFieldRefExpression)x, this });
      }
      return base.HandleAnd(x, selfPreceding);
    }

    protected override void Visit(CamlVisitor visitor) {
      visitor.VisitGroupByFieldRefExpression(FieldName);
    }

    protected override void WriteAttributes(XmlWriter writer, Hashtable bindings) { }

    CamlGroupByExpression ICamlQueryComponent<CamlGroupByExpression>.Expression {
      get { return new CamlGroupByExpression(this); }
    }
  }

  public abstract class CamlWhereComparisonExpression : CamlExpression, ICamlQueryComponent<CamlWhereExpression> {
    protected string OperatorString { get; private set; }

    internal CamlWhereComparisonExpression(string operatorString)
      : base() {
      this.OperatorString = operatorString;
    }

    protected override CamlExpression HandleAnd(CamlExpression x, bool selfPreceding) {
      switch (x.Type) {
        case CamlExpressionType.OrderByFieldRef:
        case CamlExpressionType.OrderBy:
        case CamlExpressionType.GroupByFieldRef:
        case CamlExpressionType.GroupBy:
          return new CamlWhereExpression(this) + x;
        case CamlExpressionType.WhereLogical:
        case CamlExpressionType.WhereUnaryComparison:
        case CamlExpressionType.WhereBinaryComparison:
          CamlWhereComparisonExpression other = (CamlWhereComparisonExpression)x;
          return selfPreceding ? new CamlWhereLogicalExpression(CamlLogicalOperator.And, this, other) : other.HandleAnd(this, true);
      }
      return base.HandleAnd(x, selfPreceding);
    }

    protected override CamlExpression HandleOr(CamlExpression x, bool selfPreceding) {
      switch (x.Type) {
        case CamlExpressionType.WhereUnaryComparison:
        case CamlExpressionType.WhereBinaryComparison:
        case CamlExpressionType.WhereLogical:
          CamlWhereComparisonExpression other = (CamlWhereComparisonExpression)x;
          return selfPreceding ? new CamlWhereLogicalExpression(CamlLogicalOperator.Or, this, other) : other.HandleOr(this, true);
      }
      return base.HandleOr(x, selfPreceding);
    }

    protected override CamlExpression HandleNegate() {
      return new CamlWhereLogicalExpression(CamlLogicalOperator.Not, this, null);
    }

    protected override string ToString(XmlWriterSettings settings, Hashtable bindings) {
      return ToStringStatic(new CamlWhereExpression(this), settings, bindings);
    }

    protected override void WriteXml(XmlWriter writer, Hashtable bindings) {
      writer.WriteStartElement(OperatorString);
      WriteOperationBody(writer, bindings);
      writer.WriteEndElement();
    }

    protected abstract void WriteOperationBody(XmlWriter writer, Hashtable bindings);

    CamlWhereExpression ICamlQueryComponent<CamlWhereExpression>.Expression {
      get { return new CamlWhereExpression(this); }
    }
  }

  public class CamlWhereUnaryComparisonExpression : CamlWhereComparisonExpression, ICamlFieldRefComponent {
    private readonly CamlFieldRefExpression fieldRef;
    private readonly CamlParameterBindingFieldRef fieldName;
    private readonly CamlUnaryOperator operatorValue;

    internal CamlWhereUnaryComparisonExpression(CamlUnaryOperator op, CamlParameterBindingFieldRef fieldName)
      : base(GetOperatorString(op)) {
      this.fieldRef = new CamlWhereFieldRefExpression(fieldName);
      this.fieldName = fieldName;
      this.operatorValue = op;
    }

    public override CamlExpressionType Type {
      get { return CamlExpressionType.WhereUnaryComparison; }
    }

    public CamlParameterBindingFieldRef FieldName {
      get { return fieldName; }
    }

    public CamlUnaryOperator Operator {
      get { return operatorValue; }
    }

    protected override void Visit(CamlVisitor visitor) {
      visitor.VisitWhereUnaryComparisonExpression(operatorValue, fieldName);
    }

    protected override CamlExpression HandleNegate() {
      switch (operatorValue) {
        case CamlUnaryOperator.IsNull:
          return new CamlWhereUnaryComparisonExpression(CamlUnaryOperator.IsNotNull, fieldName);
        case CamlUnaryOperator.IsNotNull:
          return new CamlWhereUnaryComparisonExpression(CamlUnaryOperator.IsNull, fieldName);
      }
      return base.HandleNegate();
    }

    protected override void WriteOperationBody(XmlWriter writer, Hashtable bindings) {
      WriteXmlStatic(fieldRef, writer, bindings);
    }

    IEnumerable<CamlFieldRefExpression> ICamlFieldRefComponent.EnumerateFieldRefExpression() {
      yield return fieldRef;
    }
  }

  public class CamlWhereBinaryComparisonExpression : CamlWhereComparisonExpression, ICamlFieldRefComponent {
    private readonly CamlFieldRefExpression fieldRef;
    private readonly CamlParameterBindingFieldRef fieldName;
    private readonly CamlBinaryOperator operatorValue;
    private readonly ICamlParameterBinding value;
    private readonly bool? includeTimeValue;

    internal CamlWhereBinaryComparisonExpression(CamlBinaryOperator op, CamlParameterBindingFieldRef fieldName, ICamlParameterBinding value)
      : base(GetOperatorString(op)) {
      this.fieldRef = new CamlWhereFieldRefExpression(fieldName, value.ValueType);
      this.fieldName = fieldName;
      this.operatorValue = op;
      this.value = value;

      if (value is CamlParameterBindingDateTime) {
        includeTimeValue = ((CamlParameterBindingDateTime)value).IncludeTimeValue;
      }
    }

    public override CamlExpressionType Type {
      get { return CamlExpressionType.WhereBinaryComparison; }
    }

    public CamlParameterBindingFieldRef FieldName {
      get { return fieldName; }
    }

    public ICamlParameterBinding Value {
      get { return value; }
    }

    public CamlBinaryOperator Operator {
      get { return operatorValue; }
    }

    protected override void Visit(CamlVisitor visitor) {
      visitor.VisitWhereBinaryComparisonExpression(operatorValue, fieldName, value, includeTimeValue);
    }

    protected override CamlExpression HandleNegate() {
      switch (operatorValue) {
        case CamlBinaryOperator.Eq:
          return new CamlWhereBinaryComparisonExpression(CamlBinaryOperator.Neq, fieldName, value);
        case CamlBinaryOperator.Geq:
          return new CamlWhereBinaryComparisonExpression(CamlBinaryOperator.Lt, fieldName, value);
        case CamlBinaryOperator.Gt:
          return new CamlWhereBinaryComparisonExpression(CamlBinaryOperator.Leq, fieldName, value);
        case CamlBinaryOperator.Leq:
          return new CamlWhereBinaryComparisonExpression(CamlBinaryOperator.Gt, fieldName, value);
        case CamlBinaryOperator.Lt:
          return new CamlWhereBinaryComparisonExpression(CamlBinaryOperator.Geq, fieldName, value);
        case CamlBinaryOperator.Neq:
          return new CamlWhereBinaryComparisonExpression(CamlBinaryOperator.Eq, fieldName, value);
        case CamlBinaryOperator.Includes:
          return new CamlWhereBinaryComparisonExpression(CamlBinaryOperator.NotIncludes, fieldName, value);
        case CamlBinaryOperator.NotIncludes:
          return new CamlWhereBinaryComparisonExpression(CamlBinaryOperator.Includes, fieldName, value);
      }
      return base.HandleNegate();
    }

    protected override void WriteXml(XmlWriter writer, Hashtable bindings) {
      switch (OperatorString) {
        case CompareOperatorString.Includes:
        case CompareOperatorString.BeginsWith:
        case CompareOperatorString.Contains:
          WriteEqualityToAnyExtension(writer, bindings, OperatorString, CompareOperatorString.Or);
          return;
        case CompareOperatorString.NotIncludes:
          WriteEqualityToAnyExtension(writer, bindings, OperatorString, CompareOperatorString.And);
          return;
        case CompareOperatorString.Membership:
          writer.WriteStartElement(OperatorString);
          writer.WriteAttributeString("Type", "SPGroup");
          writer.WriteAttributeString("ID", value.Bind(bindings));
          WriteXmlStatic(fieldRef, writer, bindings);
          writer.WriteEndElement();
          return;
      }
      base.WriteXml(writer, bindings);
    }

    protected override void WriteOperationBody(XmlWriter writer, Hashtable bindings) {
      WriteXmlStatic(fieldRef, writer, bindings);
      switch (OperatorString) {
        case CompareOperatorString.In:
          writer.WriteStartElement(Element.Values);
          foreach (string formattedValue in value.BindCollection(bindings)) {
            WriteValue(writer, formattedValue);
          }
          writer.WriteEndElement();
          break;
        default:
          WriteValue(writer, value.Bind(bindings));
          break;
      }
    }

    protected void WriteValue(XmlWriter writer, string formattedValue) {
      writer.WriteStartElement(Element.Value);
      writer.WriteAttributeString(Attribute.Type, GetValueTypeString(value.ValueType));
      if (value is CamlExpression) {
        WriteXmlStatic((CamlExpression)value, writer, EmptyBindings);
      } else {
        if (includeTimeValue.HasValue) {
          writer.WriteAttributeString(Attribute.IncludeTimeValue, includeTimeValue.Value ? BooleanString.True : BooleanString.False);
        }
        writer.WriteString(formattedValue);
      }
      writer.WriteEndElement();
    }

    protected void WriteEqualityToAnyExtension(XmlWriter writer, Hashtable bindings, string comparisonOperator, string logicalOperator) {
      string[] formattedValues = value.BindCollection(bindings).ToArray();
      if (formattedValues.Length > 1) {
        foreach (string formattedValue in formattedValues.Skip(1)) {
          writer.WriteStartElement(logicalOperator);
          writer.WriteStartElement(comparisonOperator);
          WriteXmlStatic(fieldRef, writer, bindings);
          WriteValue(writer, formattedValue);
          writer.WriteEndElement();
        }
        writer.WriteStartElement(comparisonOperator);
        WriteXmlStatic(fieldRef, writer, bindings);
        WriteValue(writer, formattedValues[0]);
        writer.WriteEndElement();
        for (int i = formattedValues.Length; i > 1; i--) {
          writer.WriteEndElement();
        }
      } else {
        writer.WriteStartElement(comparisonOperator);
        WriteXmlStatic(fieldRef, writer, bindings);
        WriteValue(writer, formattedValues[0]);
        writer.WriteEndElement();
      }
    }

    IEnumerable<CamlFieldRefExpression> ICamlFieldRefComponent.EnumerateFieldRefExpression() {
      yield return fieldRef;
    }
  }

  public class CamlWhereLogicalExpression : CamlWhereComparisonExpression, ICamlFieldRefComponent {
    private readonly CamlWhereComparisonExpression leftExpression;
    private readonly CamlWhereComparisonExpression rightExpression;
    private readonly CamlLogicalOperator operatorValue;

    internal CamlWhereLogicalExpression(CamlLogicalOperator op, CamlWhereComparisonExpression x, CamlWhereComparisonExpression y)
      : base(GetOperatorString(op)) {
      leftExpression = x;
      rightExpression = y;
      operatorValue = op;
    }

    public override CamlExpressionType Type {
      get { return CamlExpressionType.WhereLogical; }
    }

    public CamlWhereComparisonExpression Left {
      get { return leftExpression; }
    }

    public CamlWhereComparisonExpression Right {
      get { return rightExpression; }
    }

    public CamlLogicalOperator Operator {
      get { return operatorValue; }
    }

    protected override void Visit(CamlVisitor visitor) {
      visitor.VisitWhereLogicalExpression(operatorValue, leftExpression, rightExpression);
    }

    protected override CamlExpression HandleNegate() {
      switch (operatorValue) {
        case CamlLogicalOperator.And:
          return ~leftExpression | ~rightExpression;
        case CamlLogicalOperator.Or:
          return ~leftExpression & ~rightExpression;
        case CamlLogicalOperator.Not:
          return leftExpression;
      }
      return base.HandleNegate();
    }

    protected override void WriteOperationBody(XmlWriter writer, Hashtable bindings) {
      WriteXmlStatic(leftExpression, writer, bindings);
      if (rightExpression != null) {
        WriteXmlStatic(rightExpression, writer, bindings);
      }
    }

    IEnumerable<CamlFieldRefExpression> ICamlFieldRefComponent.EnumerateFieldRefExpression() {
      foreach (CamlFieldRefExpression fieldRef in ((ICamlFieldRefComponent)leftExpression).EnumerateFieldRefExpression()) {
        yield return fieldRef;
      }
      foreach (CamlFieldRefExpression fieldRef in ((ICamlFieldRefComponent)rightExpression).EnumerateFieldRefExpression()) {
        yield return fieldRef;
      }
    }
  }

  public class CamlWhereExpression : CamlExpression, ICamlFieldRefComponent, ICamlQueryComponent<CamlWhereExpression> {
    private CamlWhereComparisonExpression expression;

    internal CamlWhereExpression(CamlWhereComparisonExpression expression)
      : base() {
      this.expression = expression;
    }

    public override CamlExpressionType Type {
      get { return CamlExpressionType.Where; }
    }

    public CamlWhereComparisonExpression Body {
      get { return expression; }
    }

    protected override CamlExpression HandleAnd(CamlExpression x, bool selfPreceding) {
      switch (x.Type) {
        case CamlExpressionType.OrderByFieldRef:
          return new CamlQueryExpression(this, new CamlOrderByExpression((CamlOrderByFieldRefExpression)x), null);
        case CamlExpressionType.OrderBy:
          return new CamlQueryExpression(this, (CamlOrderByExpression)x, null);
        case CamlExpressionType.GroupByFieldRef:
          return new CamlQueryExpression(this, null, new CamlGroupByExpression((CamlGroupByFieldRefExpression)x));
        case CamlExpressionType.GroupBy:
          return new CamlQueryExpression(this, null, (CamlGroupByExpression)x);
        case CamlExpressionType.WhereLogical:
        case CamlExpressionType.WhereUnaryComparison:
        case CamlExpressionType.WhereBinaryComparison:
          return new CamlWhereExpression((CamlWhereComparisonExpression)HandleAnd(expression, x, selfPreceding));
        case CamlExpressionType.Where:
          return new CamlWhereExpression((CamlWhereComparisonExpression)HandleAnd(expression, ((CamlWhereExpression)x).expression, selfPreceding));
      }
      return base.HandleAnd(x, selfPreceding);
    }

    protected override CamlExpression HandleOr(CamlExpression x, bool selfPreceding) {
      switch (x.Type) {
        case CamlExpressionType.WhereLogical:
        case CamlExpressionType.WhereUnaryComparison:
        case CamlExpressionType.WhereBinaryComparison:
          return new CamlWhereExpression((CamlWhereComparisonExpression)HandleOr(expression, x, selfPreceding));
        case CamlExpressionType.Where:
          return new CamlWhereExpression((CamlWhereComparisonExpression)HandleOr(expression, ((CamlWhereExpression)x).expression, selfPreceding));
      }
      return base.HandleOr(x, selfPreceding);
    }

    protected override CamlExpression HandleNegate() {
      return new CamlWhereExpression(new CamlWhereLogicalExpression(CamlLogicalOperator.Not, expression, null));
    }

    protected override void Visit(CamlVisitor visitor) {
      visitor.VisitWhereExpression(expression);
    }

    protected override void WriteXml(XmlWriter writer, Hashtable bindings) {
      writer.WriteStartElement(Element.Where);
      if (expression != null) {
        WriteXmlStatic(expression, writer, bindings);
      }
      writer.WriteEndElement();
    }

    CamlWhereExpression ICamlQueryComponent<CamlWhereExpression>.Expression {
      get { return this; }
    }

    IEnumerable<CamlFieldRefExpression> ICamlFieldRefComponent.EnumerateFieldRefExpression() {
      foreach (CamlFieldRefExpression fieldRef in ((ICamlFieldRefComponent)expression).EnumerateFieldRefExpression()) {
        yield return fieldRef;
      }
    }
  }

  public abstract class CamlExpressionList<T> : CamlExpression where T : CamlExpression {
    protected readonly HashSet<T> expressions = new HashSet<T>();

    internal CamlExpressionList(T expression) {
      expressions.Add(expression);
    }

    internal CamlExpressionList(IEnumerable<T> list) {
      foreach (T expression in list) {
        expressions.Add(expression);
      }
    }

    public T[] Expressions {
      get { return expressions.ToArray(); }
    }

    protected T[] ConcatExpressions(T item, bool selfPreceding) {
      T[] arr = new T[expressions.Count + 1];
      if (selfPreceding) {
        expressions.CopyTo(arr, 0);
        arr[expressions.Count] = item;
      } else {
        arr[0] = item;
        expressions.CopyTo(arr, 1);
      }
      return arr;
    }

    protected T[] ConcatExpressions(CamlExpressionList<T> list, bool selfPreceding) {
      T[] arr = new T[expressions.Count + list.expressions.Count];
      if (selfPreceding) {
        expressions.CopyTo(arr, 0);
        list.expressions.CopyTo(arr, expressions.Count);
      } else {
        list.expressions.CopyTo(arr, 0);
        expressions.CopyTo(arr, list.expressions.Count);
      }
      return arr;
    }

    protected abstract string CollectionElementName { get; }

    protected override void Visit(CamlVisitor visitor) {
      foreach (T expression in expressions) {
        visitor.Visit(expression);
      }
    }

    protected override void WriteXml(XmlWriter writer, Hashtable bindings) {
      writer.WriteStartElement(CollectionElementName);
      foreach (T expression in expressions) {
        WriteXmlStatic(expression, writer, bindings);
      }
      writer.WriteEndElement();
    }
  }

  public abstract class CamlFieldRefExpressionList<T> : CamlExpressionList<T>, ICamlFieldRefComponent where T : CamlFieldRefExpression {
    internal CamlFieldRefExpressionList(T expression)
      : base(expression) { }

    internal CamlFieldRefExpressionList(IEnumerable<T> list)
      : base(list) { }

    IEnumerable<CamlFieldRefExpression> ICamlFieldRefComponent.EnumerateFieldRefExpression() {
      foreach (CamlFieldRefExpression expression in expressions) {
        foreach (CamlFieldRefExpression fieldRef in ((ICamlFieldRefComponent)expression).EnumerateFieldRefExpression()) {
          yield return fieldRef;
        }
      }
    }
  }

  public class CamlViewFieldsExpression : CamlFieldRefExpressionList<CamlViewFieldsFieldRefExpression> {
    internal CamlViewFieldsExpression(CamlViewFieldsFieldRefExpression expression)
      : base(expression) { }

    internal CamlViewFieldsExpression(IEnumerable<CamlViewFieldsFieldRefExpression> list)
      : base(list) { }

    public override CamlExpressionType Type {
      get { return CamlExpressionType.ViewFields; }
    }

    protected override string CollectionElementName {
      get { return Element.ViewFields; }
    }

    protected override CamlExpression HandleAnd(CamlExpression x, bool selfPreceding) {
      switch (x.Type) {
        case CamlExpressionType.ViewFieldsFieldRef:
          return new CamlViewFieldsExpression(ConcatExpressions((CamlViewFieldsFieldRefExpression)x, selfPreceding));
        case CamlExpressionType.ViewFields:
          return new CamlViewFieldsExpression(ConcatExpressions((CamlViewFieldsExpression)x, selfPreceding));
      }
      return base.HandleAnd(x, selfPreceding);
    }

    protected override string ToString(XmlWriterSettings settings, Hashtable bindings) {
      StringBuilder sb = new StringBuilder();
      AppendToStringBuilder append = CreateAppendToStringBuilderDelegate(sb, settings);
      foreach (CamlExpression expression in expressions) {
        append(ToStringStatic(expression, settings, bindings));
      }
      string queryText = sb.ToString();
      LastQueryText = queryText;
      return queryText;
    }
  }

  public class CamlOrderByExpression : CamlFieldRefExpressionList<CamlOrderByFieldRefExpression>, ICamlQueryComponent<CamlOrderByExpression> {
    internal CamlOrderByExpression(CamlOrderByFieldRefExpression expression)
      : base(expression) { }

    internal CamlOrderByExpression(IEnumerable<CamlOrderByFieldRefExpression> list)
      : base(list) { }

    public override CamlExpressionType Type {
      get { return CamlExpressionType.OrderBy; }
    }

    protected override string CollectionElementName {
      get { return Element.OrderBy; }
    }

    protected override CamlExpression HandleAnd(CamlExpression x, bool selfPreceding) {
      switch (x.Type) {
        case CamlExpressionType.OrderByFieldRef:
          return new CamlOrderByExpression(ConcatExpressions((CamlOrderByFieldRefExpression)x, selfPreceding));
        case CamlExpressionType.OrderBy:
          return new CamlOrderByExpression(ConcatExpressions((CamlOrderByExpression)x, selfPreceding));
      }
      return base.HandleAnd(x, selfPreceding);
    }

    CamlOrderByExpression ICamlQueryComponent<CamlOrderByExpression>.Expression {
      get { return this; }
    }
  }

  public class CamlGroupByExpression : CamlFieldRefExpressionList<CamlGroupByFieldRefExpression>, ICamlQueryComponent<CamlGroupByExpression> {
    internal CamlGroupByExpression(CamlGroupByFieldRefExpression expression)
      : base(expression) { }

    internal CamlGroupByExpression(IEnumerable<CamlGroupByFieldRefExpression> list)
      : base(list) { }

    public override CamlExpressionType Type {
      get { return CamlExpressionType.GroupBy; }
    }

    protected override string CollectionElementName {
      get { return Element.GroupBy; }
    }

    protected override CamlExpression HandleAnd(CamlExpression x, bool selfPreceding) {
      switch (x.Type) {
        case CamlExpressionType.GroupByFieldRef:
          return new CamlGroupByExpression(ConcatExpressions((CamlGroupByFieldRefExpression)x, selfPreceding));
        case CamlExpressionType.GroupBy:
          return new CamlGroupByExpression(ConcatExpressions((CamlGroupByExpression)x, selfPreceding));
      }
      return base.HandleAnd(x, selfPreceding);
    }

    CamlGroupByExpression ICamlQueryComponent<CamlGroupByExpression>.Expression {
      get { return this; }
    }
  }

  public class CamlQueryExpression : CamlExpression, ICamlFieldRefComponent {
    private CamlWhereExpression whereExpression;
    private CamlOrderByExpression orderByExpression;
    private CamlGroupByExpression groupByExpression;

    internal CamlQueryExpression(ICamlQueryComponent<CamlWhereExpression> x, ICamlQueryComponent<CamlOrderByExpression> y, ICamlQueryComponent<CamlGroupByExpression> z)
      : base() {
      if (x != null) whereExpression = x.Expression;
      if (y != null) orderByExpression = y.Expression;
      if (z != null) groupByExpression = z.Expression;
    }

    public override CamlExpressionType Type {
      get { return CamlExpressionType.Query; }
    }

    public CamlWhereExpression Where {
      get { return whereExpression; }
    }

    public CamlOrderByExpression OrderBy {
      get { return orderByExpression; }
    }

    public CamlGroupByExpression GroupBy {
      get { return groupByExpression; }
    }

    protected override CamlExpression HandleAnd(CamlExpression x, bool selfPreceding) {
      switch (x.Type) {
        case CamlExpressionType.OrderByFieldRef:
        case CamlExpressionType.OrderBy:
          return new CamlQueryExpression(whereExpression, (ICamlQueryComponent<CamlOrderByExpression>)HandleAnd(orderByExpression, x, selfPreceding), groupByExpression);
        case CamlExpressionType.GroupByFieldRef:
        case CamlExpressionType.GroupBy:
          return new CamlQueryExpression(whereExpression, orderByExpression, (ICamlQueryComponent<CamlGroupByExpression>)HandleAnd(groupByExpression, x, selfPreceding));
        case CamlExpressionType.WhereLogical:
        case CamlExpressionType.WhereUnaryComparison:
        case CamlExpressionType.WhereBinaryComparison:
        case CamlExpressionType.Where:
          return new CamlQueryExpression((ICamlQueryComponent<CamlWhereExpression>)HandleAnd(whereExpression, x, selfPreceding), orderByExpression, groupByExpression);
      }
      return base.HandleAnd(x, selfPreceding);
    }

    protected override CamlExpression HandleOr(CamlExpression x, bool selfPreceding) {
      switch (x.Type) {
        case CamlExpressionType.WhereLogical:
        case CamlExpressionType.WhereUnaryComparison:
        case CamlExpressionType.WhereBinaryComparison:
        case CamlExpressionType.Where:
          return new CamlQueryExpression((ICamlQueryComponent<CamlWhereExpression>)HandleOr(whereExpression, x, selfPreceding), orderByExpression, groupByExpression);
      }
      return base.HandleOr(x, selfPreceding);
    }

    protected override CamlExpression HandleNegate() {
      if (whereExpression == null) {
        return this;
      }
      return new CamlQueryExpression((CamlWhereExpression)~whereExpression, orderByExpression, groupByExpression);
    }

    protected override void Visit(CamlVisitor visitor) {
      foreach (CamlExpression expression in ForEachExpression()) {
        visitor.Visit(expression);
      }
    }

    protected override string ToString(XmlWriterSettings settings, Hashtable bindings) {
      StringBuilder sb = new StringBuilder();
      AppendToStringBuilder append = CreateAppendToStringBuilderDelegate(sb, settings);
      foreach (CamlExpression expr in ForEachExpression()) {
        append(ToStringStatic(expr, settings, bindings));
      }
      string queryText = sb.ToString();
      LastQueryText = queryText;
      return queryText;
    }

    protected override void WriteXml(XmlWriter writer, Hashtable bindings) {
      writer.WriteStartElement(Element.Query);
      foreach (CamlExpression expr in ForEachExpression()) {
        WriteXmlStatic(expr, writer, bindings);
      }
      writer.WriteEndElement();
    }

    private IEnumerable<CamlExpression> ForEachExpression() {
      if (whereExpression != null) {
        yield return whereExpression;
      }
      if (orderByExpression != null) {
        yield return orderByExpression;
      }
      if (groupByExpression != null) {
        yield return groupByExpression;
      }
    }

    IEnumerable<CamlFieldRefExpression> ICamlFieldRefComponent.EnumerateFieldRefExpression() {
      foreach (ICamlFieldRefComponent expression in ForEachExpression()) {
        foreach (CamlFieldRefExpression fieldRef in expression.EnumerateFieldRefExpression()) {
          yield return fieldRef;
        }
      }
    }
  }

  internal sealed class CamlSpecialValueExpression : CamlExpression, ICamlParameterBinding {
    private readonly string tagName;
    private readonly CamlValueType valueType;
    private readonly Dictionary<string, ICamlParameterBinding> attributes;

    internal CamlSpecialValueExpression(string tagName, CamlValueType ValueType, Dictionary<string, ICamlParameterBinding> attributes) {
      this.tagName = tagName;
      this.valueType = ValueType;
      this.attributes = attributes;
    }

    protected override void Visit(CamlVisitor visitor) { }

    protected override void WriteXml(XmlWriter writer, Hashtable bindings) {
      writer.WriteStartElement(tagName);
      if (attributes != null) {
        foreach (KeyValuePair<string, ICamlParameterBinding> entry in attributes) {
          writer.WriteAttributeString(entry.Key, entry.Value.Bind(bindings));
        }
      }
      writer.WriteEndElement();
    }

    CamlValueType ICamlParameterBinding.ValueType {
      get { return valueType; }
    }

    string ICamlParameterBinding.Bind(Hashtable bindings) {
      return null;
    }

    IEnumerable<string> ICamlParameterBinding.BindCollection(Hashtable bindings) {
      yield break;
    }
  }

  internal sealed class CamlListsScopeExpression : CamlExpression {
    private readonly List<Guid> listIdCollection = new List<Guid>();
    private readonly SPBaseType? baseType;
    private readonly int? serverTemplate;
    private readonly bool includeHidden;

    public CamlListsScopeExpression(SPBaseType baseType)
      : this(baseType, false) { }

    public CamlListsScopeExpression(SPBaseType baseType, bool includeHidden)
      : base() {
      this.baseType = baseType;
      this.includeHidden = includeHidden;
    }

    public CamlListsScopeExpression(int serverTemplate)
      : this(serverTemplate, false) { }

    public CamlListsScopeExpression(int serverTemplate, bool includeHidden)
      : base() {
      this.serverTemplate = serverTemplate;
      this.includeHidden = includeHidden;
    }

    public CamlListsScopeExpression(IEnumerable<Guid> listId) {
      this.listIdCollection.AddRange(listId);
      this.includeHidden = true;
    }

    protected override void Visit(CamlVisitor visitor) { }

    protected override void WriteXml(XmlWriter writer, Hashtable bindings) {
      writer.WriteStartElement(Element.Lists);
      if (baseType.HasValue) {
        writer.WriteAttributeString(Attribute.BaseType, ((int)baseType.Value).ToString());
      }
      if (serverTemplate.HasValue) {
        writer.WriteAttributeString(Attribute.ServerTemplate, serverTemplate.Value.ToString());
      }
      if (includeHidden) {
        writer.WriteAttributeString(Attribute.Hidden, Caml.BooleanString.True);
      }
      foreach (Guid listId in listIdCollection) {
        writer.WriteStartElement(Element.List);
        writer.WriteAttributeString(Attribute.ID, listId.ToString("D"));
        writer.WriteEndElement();
      }
      writer.WriteEndElement();
    }
  }

  public sealed class CamlBindedExpression : CamlExpression {
    private readonly CamlExpression expression;
    private readonly Hashtable bindings;

    internal CamlBindedExpression(CamlExpression expression, Hashtable bindings) {
      this.expression = expression;
      this.bindings = bindings;
    }

    public override CamlExpressionType Type {
      get { return CamlExpressionType.Binded; }
    }

    public CamlExpression Expression {
      get { return expression; }
    }

    public Hashtable Bindings {
      get { return bindings; }
    }

    protected override CamlExpression HandleAnd(CamlExpression x, bool selfPreceding) {
      if (x.Type == CamlExpressionType.Binded) {
        CamlBindedExpression other = (CamlBindedExpression)x;
        return new CamlBindedExpression(HandleAnd(expression, other.expression, selfPreceding), CopyBindings(other.bindings));
      }
      return new CamlBindedExpression(HandleAnd(expression, x, selfPreceding), bindings);
    }

    protected override CamlExpression HandleOr(CamlExpression x, bool selfPreceding) {
      if (x.Type == CamlExpressionType.Binded) {
        CamlBindedExpression other = (CamlBindedExpression)x;
        return new CamlBindedExpression(HandleOr(expression, other.expression, selfPreceding), CopyBindings(other.bindings));
      }
      return new CamlBindedExpression(HandleOr(expression, x, selfPreceding), bindings);
    }

    protected override CamlExpression HandleNegate() {
      return new CamlBindedExpression(~expression, bindings);
    }

    public override string ToString(bool indent) {
      return expression.ToString(bindings, indent);
    }

    public override string ToString(Hashtable bindings, bool indent) {
      return expression.ToString(CopyBindings(bindings), indent);
    }

    public override CamlExpression Bind(Hashtable bindings) {
      return new CamlBindedExpression(expression, CopyBindings(bindings));
    }

    public override CamlExpression GetViewFieldsExpression() {
      return expression.GetViewFieldsExpression();
    }

    protected override void Visit(CamlVisitor visitor) {
      visitor.Visit(expression);
    }

    protected override void WriteXml(XmlWriter writer, Hashtable bindings) {
      WriteXmlStatic(expression, writer, bindings);
    }

    private Hashtable CopyBindings(Hashtable other) {
      if (Object.ReferenceEquals(other, EmptyBindings)) {
        return bindings;
      }
      Hashtable result = new Hashtable();
      foreach (DictionaryEntry entry in bindings) {
        result[entry.Key] = entry.Value;
      }
      foreach (DictionaryEntry entry in other) {
        result[entry.Key] = entry.Value;
      }
      return result;
    }
  }

  internal sealed class CamlEmptyExpression : CamlExpression {
    private readonly EmptyExpressionType emptyType;

    public CamlEmptyExpression(EmptyExpressionType emptyType)
      : base() {
      this.emptyType = emptyType;
    }

    public override CamlExpressionType Type {
      get { return CamlExpressionType.Empty; }
    }

    protected override CamlExpression HandleAnd(CamlExpression x, bool selfPreceding) {
      switch (emptyType) {
        case EmptyExpressionType.False:
          return this;
        default:
          return x;
      }
    }

    protected override CamlExpression HandleOr(CamlExpression x, bool selfPreceding) {
      switch (emptyType) {
        case EmptyExpressionType.True:
          return this;
        default:
          return x;
      }
    }

    protected override CamlExpression HandleNegate() {
      switch (emptyType) {
        case EmptyExpressionType.True:
          return False;
        case EmptyExpressionType.False:
          return True;
        default:
          return this;
      }
    }

    public override string ToString(bool indent) {
      return String.Empty;
    }

    public override string ToString(Hashtable bindings, bool indent) {
      return String.Empty;
    }

    protected override string ToString(XmlWriterSettings settings, Hashtable bindings) {
      return String.Empty;
    }

    public override CamlExpression GetViewFieldsExpression() {
      return this;
    }

    protected override void Visit(CamlVisitor visitor) { }

    protected override void WriteXml(XmlWriter writer, Hashtable bindings) { }
  }
  #endregion
}
