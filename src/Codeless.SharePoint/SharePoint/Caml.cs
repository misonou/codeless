using Microsoft.SharePoint;
using Microsoft.SharePoint.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace Codeless.SharePoint {
  /// <summary>
  /// Represents error encountered when parsing or generating CAML expressions.
  /// </summary>
  public abstract class CamlException : Exception {
    internal CamlException(string message)
      : base(message) { }
  }

  /// <summary>
  /// Represents a unary CAML operator.
  /// </summary>
  public enum CamlUnaryOperator {
    /// <summary>
    /// Represents a &lt;IsNotNull/&gt; element.
    /// </summary>
    IsNotNull,
    /// <summary>
    /// Represents a &lt;IsNull/&gt; element.
    /// </summary>
    IsNull
  }

  /// <summary>
  /// Represents a binary CAML operator.
  /// </summary>
  public enum CamlBinaryOperator {
    /// <summary>
    /// Represents a &lt;BeginsWith/&gt; element.
    /// </summary>
    BeginsWith,
    /// <summary>
    /// Represents a &lt;Contains/&gt; element.
    /// </summary>
    Contains,
    /// <summary>
    /// Represents a &lt;Eq/&gt; element.
    /// </summary>
    Eq,
    [Obsolete]
    EqAny,
    /// <summary>
    /// Represents a &lt;Geq/&gt; element.
    /// </summary>
    Geq,
    /// <summary>
    /// Represents a &lt;Gt/&gt; element.
    /// </summary>
    Gt,
    /// <summary>
    /// Represents a &lt;In/&gt; element.
    /// </summary>
    In,
    /// <summary>
    /// Represents a &lt;Includes/&gt; element.
    /// </summary>
    Includes,
    /// <summary>
    /// Represents a &lt;Leq/&gt; element.
    /// </summary>
    Leq,
    /// <summary>
    /// Represents a &lt;Lt/&gt; element.
    /// </summary>
    Lt,
    /// <summary>
    /// Represents a &lt;Membership/&gt; element.
    /// </summary>
    Membership,
    /// <summary>
    /// Represents a &lt;Neq/&gt; element.
    /// </summary>
    Neq,
    [Obsolete]
    NeqAny,
    /// <summary>
    /// Represents a &lt;NotIncludes/&gt; element.
    /// </summary>
    NotIncludes,
  }

  /// <summary>
  /// Represents a logical CAML operator.
  /// </summary>
  public enum CamlLogicalOperator {
    /// <summary>
    /// Represents a &lt;And/&gt; element.
    /// </summary>
    And,
    /// <summary>
    /// Represents a &lt;Not/&gt; element.
    /// </summary>
    Not,
    /// <summary>
    /// Represents a &lt;Or/&gt; element.
    /// </summary>
    Or
  }

  /// <summary>
  /// Represents a sort direction.
  /// </summary>
  public enum CamlOrder {
    /// <summary>
    /// Represents an ascending sort direction.
    /// </summary>
    Ascending,
    /// <summary>
    /// Represents a descending sort direction.
    /// </summary>
    Descending
  }

  /// <summary>
  /// Represents a value type supported in CAML expressions.
  /// </summary>
  public enum CamlValueType {
    /// <summary>
    /// Represents a &lt;Value Type="Text"/&gt; element.
    /// </summary>
    Text,
    /// <summary>
    /// Represents a &lt;Value Type="Lookup"/&gt; element.
    /// </summary>
    Lookup,
    /// <summary>
    /// Represents a &lt;Value Type="DateTime"/&gt; element.
    /// </summary>
    DateTime,
    /// <summary>
    /// Represents a &lt;Value Type="Integer"/&gt; element.
    /// </summary>
    Integer,
    /// <summary>
    /// Represents a &lt;Value Type="Number"/&gt; element.
    /// </summary>
    Number,
    /// <summary>
    /// Represents a &lt;Value Type="Boolean"/&gt; element.
    /// </summary>
    Boolean,
    /// <summary>
    /// Represents a &lt;Value Type="Guid"/&gt; element.
    /// </summary>
    Guid,
    /// <summary>
    /// Represents a &lt;Value Type="ContentTypeId"/&gt; element.
    /// </summary>
    ContentTypeId,
    /// <summary>
    /// Represents a &lt;Value Type="URL"/&gt; element.
    /// </summary>
    URL,
    /// <summary>
    /// Represents a &lt;Value Type="ModStat"/&gt; element.
    /// </summary>
    ModStat
  }

  /// <summary>
  /// Represents type of a <see cref="CamlExpression"/> sub-expression instance.
  /// </summary>
  public enum CamlExpressionType {
    /// <summary>
    /// Reserved.
    /// </summary>
    Invalid = 0,
    /// <summary>
    /// Represents a &lt;FieldRef/&gt; expression inside a &lt;ViewFields/&gt; clause.
    /// </summary>
    ViewFieldsFieldRef = (1 << 1) | Caml.ExpressionBaseType.ViewFields,
    /// <summary>
    /// Represents a &lt;FieldRef/&gt; expression inside an &lt;OrderBy/&gt; clause.
    /// </summary>
    OrderByFieldRef = (2 << 1) | Caml.ExpressionBaseType.Query,
    /// <summary>
    /// Represents a &lt;FieldRef/&gt; expression inside a &lt;GroupBy/&gt; clause.
    /// </summary>
    GroupByFieldRef = (3 << 1) | Caml.ExpressionBaseType.Query,
    /// <summary>
    /// Represents a &lt;ViewFields/&gt; element.
    /// </summary>
    ViewFields = (4 << 1) | Caml.ExpressionBaseType.ViewFields,
    /// <summary>
    /// Represents a &lt;GroupBy/&gt; element.
    /// </summary>
    GroupBy = (5 << 1) | Caml.ExpressionBaseType.Query,
    /// <summary>
    /// Represents an &lt;OrderBy/&gt; element.
    /// </summary>
    OrderBy = (6 << 1) | Caml.ExpressionBaseType.Query,
    /// <summary>
    /// Represents a unary comparison expression inside a &lt;Where/&gt; element.
    /// </summary>
    WhereUnaryComparison = (7 << 1) | Caml.ExpressionBaseType.Query,
    /// <summary>
    /// Represents a binary comparison expression inside a &lt;Where/&gt; element.
    /// </summary>
    WhereBinaryComparison = (8 << 1) | Caml.ExpressionBaseType.Query,
    /// <summary>
    /// Represents a logical comparison expression inside a &lt;Where/&gt; element.
    /// </summary>
    WhereLogical = (9 << 1) | Caml.ExpressionBaseType.Query,
    /// <summary>
    /// Represents a &lt;Where/&gt; element.
    /// </summary>
    Where = (10 << 1) | Caml.ExpressionBaseType.Query,
    /// <summary>
    /// Represents a &lt;Query/&gt; element.
    /// </summary>
    Query = (11 << 1) | Caml.ExpressionBaseType.Query,
    /// <summary>
    /// Represents a CAML expression that contains binded values.
    /// </summary>
    Binded = (255 << 1) | Caml.ExpressionBaseType.Query,
    /// <summary>
    /// Represents an empty CAML expression.
    /// </summary>
    Empty = (65535 << 1) | Caml.ExpressionBaseType.Query
  }

  /// <summary>
  /// Provides static methods to build CAML query expressions.
  /// </summary>
  public abstract partial class Caml {
    internal static class BooleanString {
      public const string True = "TRUE";
      public const string False = "FALSE";
    }

    internal static class CompareOperatorString {
      public const string And = "And";
      public const string BeginsWith = "BeginsWith";
      public const string Contains = "Contains";
      public const string Eq = "Eq";
      public const string Geq = "Geq";
      public const string Gt = "Gt";
      public const string In = "In";
      public const string Includes = "Includes";
      public const string IsNotNull = "IsNotNull";
      public const string IsNull = "IsNull";
      public const string Leq = "Leq";
      public const string Lt = "Lt";
      public const string Membership = "Membership";
      public const string Neq = "Neq";
      public const string Not = "Not";
      public const string NotIncludes = "NotIncludes";
      public const string Or = "Or";
    }

    internal static class ValueTypeString {
      public const string Boolean = "Boolean";
      public const string DateTime = "DateTime";
      public const string Guid = "Guid";
      public const string Integer = "Integer";
      public const string Lookup = "Lookup";
      public const string Number = "Number";
      public const string Text = "Text";
      public const string ContentTypeId = "ContentTypeId";
      public const string URL = "URL";
      public const string ModStat = "ModStat";
    }

    internal static class Element {
      public const string FieldRef = "FieldRef";
      public const string Value = "Value";
      public const string Values = "Values";
      public const string Where = "Where";
      public const string ViewFields = "ViewFields";
      public const string OrderBy = "OrderBy";
      public const string GroupBy = "GroupBy";
      public const string Lists = "Lists";
      public const string List = "List";
      public const string UserID = "UserID";
      public const string Today = "Today";
      public const string Now = "Now";
      public const string Query = "Query";
    }

    internal static class Attribute {
      public const string Name = "Name";
      public const string Nullable = "Nullable";
      public const string LookupId = "LookupId";
      public const string Ascending = "Ascending";
      public const string Type = "Type";
      public const string IncludeTimeValue = "IncludeTimeValue";
      public const string BaseType = "BaseType";
      public const string ServerTemplate = "ServerTemplate";
      public const string Hidden = "Hidden";
      public const string ID = "ID";
      public const string Collapse = "Collapse";
    }

    internal enum EmptyExpressionType {
      Empty,
      True,
      False
    }

    internal enum ExpressionBaseType {
      Query = 1,
      ViewFields = 2,
      MaxValue = 0xF
    }

    internal enum JoinType {
      And,
      Or,
      Negate
    }

    internal enum ParseState {
      OrderBy,
      GroupBy,
      Where,
      ViewFields
    }

    /// <summary>
    /// Represents a parameter of a certain data type which its value can be binded when available.
    /// </summary>
    public static class Parameter {
      /// <summary>
      /// Creates a parameter binding to a field name within a CAML expression.
      /// </summary>
      /// <param name="parameterName">Unique name to identify this parameter when binding values.</param>
      /// <returns>A parameter binding which can be supplied to expression building methods.</returns>
      public static CamlParameterBindingFieldRef FieldRef(string parameterName) {
        return new CamlParameterName(parameterName);
      }
      /// <summary>
      /// Creates a parameter binding to a sorting direction within a CAML expression.
      /// </summary>
      /// <param name="parameterName">Unique name to identify this parameter when binding values.</param>
      /// <returns>A parameter binding which can be supplied to expression building methods.</returns>
      public static CamlParameterBindingOrder Order(string parameterName) {
        return new CamlParameterName(parameterName);
      }
      /// <summary>
      /// Creates a parameter binding to a boolean string "TRUE" or "FALSE".
      /// </summary>
      /// <param name="parameterName">Unique name to identify this parameter when binding values.</param>
      /// <returns>A parameter binding which can be supplied to expression building methods.</returns>
      public static CamlParameterBindingBooleanString BooleanString(string parameterName) {
        return new CamlParameterBindingBooleanString(new CamlParameterName(parameterName));
      }
      /// <summary>
      /// Creates a parameter binding to a boolean value.
      /// </summary>
      /// <param name="parameterName">Unique name to identify this parameter when binding values.</param>
      /// <returns>A parameter binding which can be supplied to expression building methods.</returns>
      public static ICamlParameterBinding Boolean(string parameterName) {
        return new CamlParameterBindingBoolean(new CamlParameterName(parameterName));
      }
      /// <summary>
      /// Creates a parameter binding to an integer value or a list of integer values.
      /// </summary>
      /// <param name="parameterName">Unique name to identify this parameter when binding values.</param>
      /// <returns>A parameter binding which can be supplied to expression building methods.</returns>
      public static ICamlParameterBinding Int32(string parameterName) {
        return new CamlParameterBindingInteger(new CamlParameterName(parameterName));
      }
      /// <summary>
      /// Creates a parameter binding to a numerical value or a list of numerical values.
      /// </summary>
      /// <param name="parameterName">Unique name to identify this parameter when binding values.</param>
      /// <returns>A parameter binding which can be supplied to expression building methods.</returns>
      public static ICamlParameterBinding Double(string parameterName) {
        return new CamlParameterBindingNumber(new CamlParameterName(parameterName));
      }
      /// <summary>
      /// Creates a parameter binding to a text value or a list of text values.
      /// </summary>
      /// <param name="parameterName">Unique name to identify this parameter when binding values.</param>
      /// <returns>A parameter binding which can be supplied to expression building methods.</returns>
      public static ICamlParameterBinding String(string parameterName) {
        return new CamlParameterBindingString(new CamlParameterName(parameterName));
      }
      /// <summary>
      /// Creates a parameter binding to a date time value or a list of date time values.
      /// </summary>
      /// <param name="parameterName">Unique name to identify this parameter when binding values.</param>
      /// <returns>A parameter binding which can be supplied to expression building methods.</returns>
      public static ICamlParameterBinding DateTime(string parameterName) {
        return new CamlParameterBindingDateTime(new CamlParameterName(parameterName), true);
      }
      /// <summary>
      /// Creates a parameter binding to a date time value or a list of date time values. Time components can be optionally included.
      /// </summary>
      /// <param name="parameterName">Unique name to identify this parameter when binding values.</param>
      /// <param name="includeTimeValue">Specifies whether the time component should be included for comparison.</param>
      /// <returns>A parameter binding which can be supplied to expression building methods.</returns>
      public static ICamlParameterBinding DateTime(string parameterName, bool includeTimeValue) {
        return new CamlParameterBindingDateTime(new CamlParameterName(parameterName), includeTimeValue);
      }
      /// <summary>
      /// Creates a parameter binding to a GUID value or a list of GUID values.
      /// </summary>
      /// <param name="parameterName">Unique name to identify this parameter when binding values.</param>
      /// <returns>A parameter binding which can be supplied to expression building methods.</returns>
      public static ICamlParameterBinding Guid(string parameterName) {
        return new CamlParameterBindingGuid(new CamlParameterName(parameterName));
      }
      /// <summary>
      /// Creates a parameter binding to a lookup value or a list of lookup values.
      /// </summary>
      /// <param name="parameterName">Unique name to identify this parameter when binding values.</param>
      /// <returns>A parameter binding which can be supplied to expression building methods.</returns>
      public static ICamlParameterBinding Lookup(string parameterName) {
        return new CamlParameterBindingLookup(new CamlParameterName(parameterName));
      }
      /// <summary>
      /// Creates a parameter binding to a content type ID value or a list of content type ID values.
      /// </summary>
      /// <param name="parameterName">Unique name to identify this parameter when binding values.</param>
      /// <returns>A parameter binding which can be supplied to expression building methods.</returns>
      public static ICamlParameterBinding ContentTypeId(string parameterName) {
        return new CamlParameterBindingContentTypeId(new CamlParameterName(parameterName));
      }
      /// <summary>
      /// Creates a parameter binding to a URL value or a list of URL values.
      /// </summary>
      /// <param name="parameterName">Unique name to identify this parameter when binding values.</param>
      /// <returns>A parameter binding which can be supplied to expression building methods.</returns>
      public static ICamlParameterBinding Url(string parameterName) {
        return new CamlParameterBindingUrl(new CamlParameterName(parameterName));
      }
      /// <summary>
      /// Creates a parameter binding to a moderation status value or a list of moderation status values.
      /// </summary>
      /// <param name="parameterName">Unique name to identify this parameter when binding values.</param>
      /// <returns>A parameter binding which can be supplied to expression building methods.</returns>
      public static ICamlParameterBinding ModerationStatus(string parameterName) {
        return new CamlParameterBindingModStat(new CamlParameterName(parameterName));
      }
    }

    /// <summary>
    /// Represents special CAML elements within a CAML expression.
    /// </summary>
    public static class SpecialValue {
      /// <summary>
      /// Gets a parameter binding which can be supplied to expression building methods, represents the <UserID/> element.
      /// </summary>
      public static readonly ICamlParameterBinding UserID = new CamlSpecialValueExpression(Element.UserID, CamlValueType.Integer, null);
      /// <summary>
      /// Gets a parameter binding which can be supplied to expression building methods, represents the <Today/> element.
      /// </summary>
      public static readonly ICamlParameterBinding Today = new CamlSpecialValueExpression(Element.Today, CamlValueType.DateTime, null);
      /// <summary>
      /// Gets a parameter binding which can be supplied to expression building methods, represents the <Now/> element.
      /// </summary>
      public static readonly ICamlParameterBinding Now = new CamlSpecialValueExpression(Element.Now, CamlValueType.DateTime, null);
      /// <summary>
      /// Creates a <Today/> element with the specified offset.
      /// </summary>
      /// <param name="numOfDays">Number of day offset.</param>
      /// <returns>A parameter binding which can be supplied to expression building methods.</returns>
      public static ICamlParameterBinding TodayWithOffset(int numOfDays) {
        return TodayWithOffset(new CamlParameterBindingInteger(numOfDays));
      }
      /// <summary>
      /// Creates a <Today/> element with a offset parameter.
      /// </summary>
      /// <param name="numOfDays">Number of day offset.</param>
      /// <returns>A parameter binding which can be supplied to expression building methods.</returns>
      public static ICamlParameterBinding TodayWithOffset(ICamlParameterBinding numOfDays) {
        Dictionary<string, ICamlParameterBinding> attributes = new Dictionary<string, ICamlParameterBinding>();
        attributes["Offset"] = numOfDays;
        return new CamlSpecialValueExpression("Today", CamlValueType.DateTime, attributes);
      }
    }

    /// <summary>
    /// Provides constant CAML expression to be used in queries.
    /// </summary>
    public static class WebsScope {
      /// <summary>
      /// Represents <Webs Scope="Recursive"/>.
      /// </summary>
      public const string Recursive = "<Webs Scope=\"Recursive\"/>";
      /// <summary>
      /// Represents <Webs Scope="SiteCollection"/>.
      /// </summary>
      public const string SiteCollection = "<Webs Scope=\"SiteCollection\"/>";
    }

    [ThreadStatic]
    private static string lastQueryText;

    /// <summary>
    /// Gets the last CAML expression generated from calling <see cref="CamlExpression.ToString()"/> or its overloads.
    /// </summary>
    public static string LastQueryText {
      get { return lastQueryText; }
      protected set { lastQueryText = value; }
    }

    /// <summary>
    /// Gets an empty expression. Performing logical And or Or gives back the operand expression; while performing negation gives the same empty expression.
    /// </summary>
    public static readonly CamlExpression Empty = new CamlEmptyExpression(EmptyExpressionType.Empty);
    /// <summary>
    /// Gets an empty expression that evaluates to *true* when performing logical comparison.
    /// Performing logical And gives the operand expression; performing logical Or gives the same *true* expression; while performing negation gives the *false* empty expression.
    /// </summary>
    public static readonly CamlExpression True = new CamlEmptyExpression(EmptyExpressionType.True);
    /// <summary>
    /// Gets an empty expression that evaluates to *false* when performing logical comparison.
    /// Performing logical And gives the same *false* expression; performing logical Or gives the operand expression; while performing negation gives the *true* empty expression.
    /// </summary>
    public static readonly CamlExpression False = new CamlEmptyExpression(EmptyExpressionType.False);

    public static CamlExpression ViewFields(params string[] fieldName) {
      return new CamlViewFieldsExpression(fieldName.Select(v => new CamlViewFieldsFieldRefExpression(v)));
    }
    public static CamlExpression ViewFields(params CamlParameterBindingFieldRef[] fieldName) {
      return new CamlViewFieldsExpression(fieldName.Select(v => new CamlViewFieldsFieldRefExpression(v)));
    }
    public static CamlExpression ViewFields(IEnumerable<string> fieldName) {
      return new CamlViewFieldsExpression(fieldName.Select(v => new CamlViewFieldsFieldRefExpression(v)));
    }
    public static CamlExpression ViewFields(IEnumerable<CamlParameterBindingFieldRef> fieldName) {
      return new CamlViewFieldsExpression(fieldName.Select(v => new CamlViewFieldsFieldRefExpression(v)));
    }
    public static CamlExpression OrderBy(CamlParameterBindingFieldRef fieldName, CamlParameterBindingOrder order) {
      return new CamlOrderByExpression(new CamlOrderByFieldRefExpression(fieldName, order));
    }
    public static CamlExpression OrderBy(CamlParameterBindingFieldRef fieldName, CamlOrder order) {
      return new CamlOrderByExpression(new CamlOrderByFieldRefExpression(fieldName, order));
    }
    public static CamlExpression OrderByAscending(CamlParameterBindingFieldRef fieldName) {
      return OrderBy(fieldName, CamlOrder.Ascending);
    }
    public static CamlExpression OrderByDescending(CamlParameterBindingFieldRef fieldName) {
      return OrderBy(fieldName, CamlOrder.Descending);
    }
    public static CamlExpression GroupBy(params string[] fieldName) {
      return new CamlGroupByExpression(fieldName.Select(v => new CamlGroupByFieldRefExpression(v)));
    }
    public static CamlExpression GroupBy(params CamlParameterBindingFieldRef[] fieldName) {
      return new CamlGroupByExpression(fieldName.Select(v => new CamlGroupByFieldRefExpression(v)));
    }
    public static CamlExpression GroupBy(string[] fieldName, bool collapse) {
      return new CamlGroupByExpression(fieldName.Select(v => new CamlGroupByFieldRefExpression(v)), new CamlParameterBindingBooleanString(collapse));
    }
    public static CamlExpression GroupBy(CamlParameterBindingFieldRef[] fieldName, CamlParameterBindingBooleanString collapse) {
      return new CamlGroupByExpression(fieldName.Select(v => new CamlGroupByFieldRefExpression(v)), collapse);
    }
    public static CamlExpression GroupBy(IEnumerable<string> fieldName) {
      return new CamlGroupByExpression(fieldName.Select(v => new CamlGroupByFieldRefExpression(v)));
    }
    public static CamlExpression GroupBy(IEnumerable<CamlParameterBindingFieldRef> fieldName) {
      return new CamlGroupByExpression(fieldName.Select(v => new CamlGroupByFieldRefExpression(v)));
    }
    public static CamlExpression GroupBy(IEnumerable<string> fieldName, bool collapse) {
      return new CamlGroupByExpression(fieldName.Select(v => new CamlGroupByFieldRefExpression(v)), new CamlParameterBindingBooleanString(collapse));
    }
    public static CamlExpression GroupBy(IEnumerable<CamlParameterBindingFieldRef> fieldName, CamlParameterBindingBooleanString collapse) {
      return new CamlGroupByExpression(fieldName.Select(v => new CamlGroupByFieldRefExpression(v)), collapse);
    }

    public static CamlExpression ListsScope(SPBaseType baseType) {
      return new CamlListsScopeExpression(baseType);
    }
    public static CamlExpression ListsScope(SPBaseType baseType, bool includeHidden) {
      return new CamlListsScopeExpression(baseType, includeHidden);
    }
    public static CamlExpression ListsScope(int serverTemplate) {
      return new CamlListsScopeExpression(serverTemplate);
    }
    public static CamlExpression ListsScope(int serverTemplate, bool includeHidden) {
      return new CamlListsScopeExpression(serverTemplate, includeHidden);
    }
    public static CamlExpression ListsScope(params Guid[] listId) {
      return new CamlListsScopeExpression(listId);
    }

    public static CamlExpression Equals(CamlParameterBindingFieldRef fieldName, int value) {
      return Equals(fieldName, new CamlParameterBindingInteger(value));
    }
    public static CamlExpression Equals(CamlParameterBindingFieldRef fieldName, bool value) {
      return Equals(fieldName, new CamlParameterBindingBoolean(value));
    }
    public static CamlExpression Equals(CamlParameterBindingFieldRef fieldName, double value) {
      return Equals(fieldName, new CamlParameterBindingNumber(value));
    }
    public static CamlExpression Equals(CamlParameterBindingFieldRef fieldName, string value) {
      return Equals(fieldName, new CamlParameterBindingString(value));
    }
    public static CamlExpression Equals(CamlParameterBindingFieldRef fieldName, DateTime value) {
      return Equals(fieldName, new CamlParameterBindingDateTime(value, true));
    }
    public static CamlExpression Equals(CamlParameterBindingFieldRef fieldName, Guid value) {
      return Equals(fieldName, new CamlParameterBindingGuid(value));
    }
    public static CamlExpression Equals(CamlParameterBindingFieldRef fieldName, SPListItem value) {
      return Equals(fieldName, new CamlParameterBindingLookup(value));
    }
    public static CamlExpression Equals(CamlParameterBindingFieldRef fieldName, SPPrincipal value) {
      return Equals(fieldName, new CamlParameterBindingLookup(value));
    }
    public static CamlExpression Equals(CamlParameterBindingFieldRef fieldName, SPContentTypeId value) {
      return Equals(fieldName, new CamlParameterBindingContentTypeId(value));
    }
    public static CamlExpression Equals(CamlParameterBindingFieldRef fieldName, SPModerationStatusType value) {
      return Equals(fieldName, new CamlParameterBindingModStat(value));
    }
    public static CamlExpression Equals(CamlParameterBindingFieldRef fieldName, ICamlParameterBinding value) {
      return new CamlWhereBinaryComparisonExpression(CamlBinaryOperator.Eq, fieldName, value);
    }

    public static CamlExpression NotEquals(CamlParameterBindingFieldRef fieldName, int value) {
      return NotEquals(fieldName, new CamlParameterBindingInteger(value));
    }
    public static CamlExpression NotEquals(CamlParameterBindingFieldRef fieldName, double value) {
      return NotEquals(fieldName, new CamlParameterBindingNumber(value));
    }
    public static CamlExpression NotEquals(CamlParameterBindingFieldRef fieldName, string value) {
      return NotEquals(fieldName, new CamlParameterBindingString(value));
    }
    public static CamlExpression NotEquals(CamlParameterBindingFieldRef fieldName, DateTime value) {
      return NotEquals(fieldName, new CamlParameterBindingDateTime(value, true));
    }
    public static CamlExpression NotEquals(CamlParameterBindingFieldRef fieldName, Guid value) {
      return NotEquals(fieldName, new CamlParameterBindingGuid(value));
    }
    public static CamlExpression NotEquals(CamlParameterBindingFieldRef fieldName, SPListItem value) {
      return NotEquals(fieldName, new CamlParameterBindingLookup(value));
    }
    public static CamlExpression NotEquals(CamlParameterBindingFieldRef fieldName, SPPrincipal value) {
      return NotEquals(fieldName, new CamlParameterBindingLookup(value));
    }
    public static CamlExpression NotEquals(CamlParameterBindingFieldRef fieldName, SPContentTypeId value) {
      return NotEquals(fieldName, new CamlParameterBindingContentTypeId(value));
    }
    public static CamlExpression NotEquals(CamlParameterBindingFieldRef fieldName, SPModerationStatusType value) {
      return NotEquals(fieldName, new CamlParameterBindingModStat(value));
    }
    public static CamlExpression NotEquals(CamlParameterBindingFieldRef fieldName, ICamlParameterBinding value) {
      return new CamlWhereBinaryComparisonExpression(CamlBinaryOperator.Neq, fieldName, value);
    }

    public static CamlExpression GreaterThanOrEqual(CamlParameterBindingFieldRef fieldName, int value) {
      return GreaterThanOrEqual(fieldName, new CamlParameterBindingInteger(value));
    }
    public static CamlExpression GreaterThanOrEqual(CamlParameterBindingFieldRef fieldName, double value) {
      return GreaterThanOrEqual(fieldName, new CamlParameterBindingNumber(value));
    }
    public static CamlExpression GreaterThanOrEqual(CamlParameterBindingFieldRef fieldName, string value) {
      return GreaterThanOrEqual(fieldName, new CamlParameterBindingString(value));
    }
    public static CamlExpression GreaterThanOrEqual(CamlParameterBindingFieldRef fieldName, DateTime value) {
      return GreaterThanOrEqual(fieldName, new CamlParameterBindingDateTime(value, true));
    }
    public static CamlExpression GreaterThanOrEqual(CamlParameterBindingFieldRef fieldName, Guid value) {
      return GreaterThanOrEqual(fieldName, new CamlParameterBindingGuid(value));
    }
    public static CamlExpression GreaterThanOrEqual(CamlParameterBindingFieldRef fieldName, ICamlParameterBinding value) {
      return new CamlWhereBinaryComparisonExpression(CamlBinaryOperator.Geq, fieldName, value);
    }

    public static CamlExpression GreaterThan(CamlParameterBindingFieldRef fieldName, int value) {
      return GreaterThan(fieldName, new CamlParameterBindingInteger(value));
    }
    public static CamlExpression GreaterThan(CamlParameterBindingFieldRef fieldName, double value) {
      return GreaterThan(fieldName, new CamlParameterBindingNumber(value));
    }
    public static CamlExpression GreaterThan(CamlParameterBindingFieldRef fieldName, string value) {
      return GreaterThan(fieldName, new CamlParameterBindingString(value));
    }
    public static CamlExpression GreaterThan(CamlParameterBindingFieldRef fieldName, DateTime value) {
      return GreaterThan(fieldName, new CamlParameterBindingDateTime(value, true));
    }
    public static CamlExpression GreaterThan(CamlParameterBindingFieldRef fieldName, Guid value) {
      return GreaterThan(fieldName, new CamlParameterBindingGuid(value));
    }
    public static CamlExpression GreaterThan(CamlParameterBindingFieldRef fieldName, ICamlParameterBinding value) {
      return new CamlWhereBinaryComparisonExpression(CamlBinaryOperator.Gt, fieldName, value);
    }

    public static CamlExpression LessThanOrEqual(CamlParameterBindingFieldRef fieldName, int value) {
      return LessThanOrEqual(fieldName, new CamlParameterBindingInteger(value));
    }
    public static CamlExpression LessThanOrEqual(CamlParameterBindingFieldRef fieldName, double value) {
      return LessThanOrEqual(fieldName, new CamlParameterBindingNumber(value));
    }
    public static CamlExpression LessThanOrEqual(CamlParameterBindingFieldRef fieldName, string value) {
      return LessThanOrEqual(fieldName, new CamlParameterBindingString(value));
    }
    public static CamlExpression LessThanOrEqual(CamlParameterBindingFieldRef fieldName, DateTime value) {
      return LessThanOrEqual(fieldName, new CamlParameterBindingDateTime(value, true));
    }
    public static CamlExpression LessThanOrEqual(CamlParameterBindingFieldRef fieldName, Guid value) {
      return LessThanOrEqual(fieldName, new CamlParameterBindingGuid(value));
    }
    public static CamlExpression LessThanOrEqual(CamlParameterBindingFieldRef fieldName, ICamlParameterBinding value) {
      return new CamlWhereBinaryComparisonExpression(CamlBinaryOperator.Leq, fieldName, value);
    }

    public static CamlExpression LessThan(CamlParameterBindingFieldRef fieldName, int value) {
      return LessThan(fieldName, new CamlParameterBindingInteger(value));
    }
    public static CamlExpression LessThan(CamlParameterBindingFieldRef fieldName, double value) {
      return LessThan(fieldName, new CamlParameterBindingNumber(value));
    }
    public static CamlExpression LessThan(CamlParameterBindingFieldRef fieldName, string value) {
      return LessThan(fieldName, new CamlParameterBindingString(value));
    }
    public static CamlExpression LessThan(CamlParameterBindingFieldRef fieldName, DateTime value) {
      return LessThan(fieldName, new CamlParameterBindingDateTime(value, true));
    }
    public static CamlExpression LessThan(CamlParameterBindingFieldRef fieldName, Guid value) {
      return LessThan(fieldName, new CamlParameterBindingGuid(value));
    }
    public static CamlExpression LessThan(CamlParameterBindingFieldRef fieldName, ICamlParameterBinding value) {
      return new CamlWhereBinaryComparisonExpression(CamlBinaryOperator.Lt, fieldName, value);
    }

    public static CamlExpression EqualsAny(CamlParameterBindingFieldRef fieldName, params int[] values) {
      return EqualsAny(fieldName, (IEnumerable<int>)values);
    }
    public static CamlExpression EqualsAny(CamlParameterBindingFieldRef fieldName, params double[] values) {
      return EqualsAny(fieldName, (IEnumerable<double>)values);
    }
    public static CamlExpression EqualsAny(CamlParameterBindingFieldRef fieldName, params string[] values) {
      return EqualsAny(fieldName, (IEnumerable<string>)values);
    }
    public static CamlExpression EqualsAny(CamlParameterBindingFieldRef fieldName, params DateTime[] values) {
      return EqualsAny(fieldName, (IEnumerable<DateTime>)values);
    }
    public static CamlExpression EqualsAny(CamlParameterBindingFieldRef fieldName, params Guid[] values) {
      return EqualsAny(fieldName, (IEnumerable<Guid>)values);
    }
    public static CamlExpression EqualsAny(CamlParameterBindingFieldRef fieldName, params SPListItem[] values) {
      return EqualsAny(fieldName, (IEnumerable<SPListItem>)values);
    }
    public static CamlExpression EqualsAny(CamlParameterBindingFieldRef fieldName, params SPPrincipal[] values) {
      return EqualsAny(fieldName, (IEnumerable<SPPrincipal>)values);
    }
    public static CamlExpression EqualsAny(CamlParameterBindingFieldRef fieldName, IEnumerable<int> value) {
      return EqualsAny(fieldName, new CamlParameterBindingInteger(value));
    }
    public static CamlExpression EqualsAny(CamlParameterBindingFieldRef fieldName, IEnumerable<double> value) {
      return EqualsAny(fieldName, new CamlParameterBindingNumber(value));
    }
    public static CamlExpression EqualsAny(CamlParameterBindingFieldRef fieldName, IEnumerable<string> value) {
      return EqualsAny(fieldName, new CamlParameterBindingString(value));
    }
    public static CamlExpression EqualsAny(CamlParameterBindingFieldRef fieldName, IEnumerable<DateTime> value) {
      return EqualsAny(fieldName, new CamlParameterBindingDateTime(value, true));
    }
    public static CamlExpression EqualsAny(CamlParameterBindingFieldRef fieldName, IEnumerable<Guid> value) {
      return EqualsAny(fieldName, new CamlParameterBindingGuid(value));
    }
    public static CamlExpression EqualsAny(CamlParameterBindingFieldRef fieldName, IEnumerable<SPListItem> value) {
      return EqualsAny(fieldName, new CamlParameterBindingLookup(value));
    }
    public static CamlExpression EqualsAny(CamlParameterBindingFieldRef fieldName, IEnumerable<SPPrincipal> value) {
      return EqualsAny(fieldName, new CamlParameterBindingLookup(value));
    }
    public static CamlExpression EqualsAny(CamlParameterBindingFieldRef fieldName, ICamlParameterBinding value) {
      return new CamlWhereBinaryComparisonExpression(CamlBinaryOperator.In, fieldName, value);
    }

    public static CamlExpression NotEqualsAny(CamlParameterBindingFieldRef fieldName, params int[] values) {
      return NotEqualsAny(fieldName, (IEnumerable<int>)values);
    }
    public static CamlExpression NotEqualsAny(CamlParameterBindingFieldRef fieldName, params double[] values) {
      return NotEqualsAny(fieldName, (IEnumerable<double>)values);
    }
    public static CamlExpression NotEqualsAny(CamlParameterBindingFieldRef fieldName, params string[] values) {
      return NotEqualsAny(fieldName, (IEnumerable<string>)values);
    }
    public static CamlExpression NotEqualsAny(CamlParameterBindingFieldRef fieldName, params DateTime[] values) {
      return NotEqualsAny(fieldName, (IEnumerable<DateTime>)values);
    }
    public static CamlExpression NotEqualsAny(CamlParameterBindingFieldRef fieldName, params Guid[] values) {
      return NotEqualsAny(fieldName, (IEnumerable<Guid>)values);
    }
    public static CamlExpression NotEqualsAny(CamlParameterBindingFieldRef fieldName, params SPListItem[] values) {
      return NotEqualsAny(fieldName, (IEnumerable<SPListItem>)values);
    }
    public static CamlExpression NotEqualsAny(CamlParameterBindingFieldRef fieldName, params SPPrincipal[] values) {
      return NotEqualsAny(fieldName, (IEnumerable<SPPrincipal>)values);
    }
    public static CamlExpression NotEqualsAny(CamlParameterBindingFieldRef fieldName, IEnumerable<int> value) {
      return NotEqualsAny(fieldName, new CamlParameterBindingInteger(value));
    }
    public static CamlExpression NotEqualsAny(CamlParameterBindingFieldRef fieldName, IEnumerable<double> value) {
      return NotEqualsAny(fieldName, new CamlParameterBindingNumber(value));
    }
    public static CamlExpression NotEqualsAny(CamlParameterBindingFieldRef fieldName, IEnumerable<string> value) {
      return NotEqualsAny(fieldName, new CamlParameterBindingString(value));
    }
    public static CamlExpression NotEqualsAny(CamlParameterBindingFieldRef fieldName, IEnumerable<DateTime> value) {
      return NotEqualsAny(fieldName, new CamlParameterBindingDateTime(value, true));
    }
    public static CamlExpression NotEqualsAny(CamlParameterBindingFieldRef fieldName, IEnumerable<Guid> value) {
      return NotEqualsAny(fieldName, new CamlParameterBindingGuid(value));
    }
    public static CamlExpression NotEqualsAny(CamlParameterBindingFieldRef fieldName, IEnumerable<SPListItem> value) {
      return NotEqualsAny(fieldName, new CamlParameterBindingLookup(value));
    }
    public static CamlExpression NotEqualsAny(CamlParameterBindingFieldRef fieldName, IEnumerable<SPPrincipal> value) {
      return NotEqualsAny(fieldName, new CamlParameterBindingLookup(value));
    }
    public static CamlExpression NotEqualsAny(CamlParameterBindingFieldRef fieldName, ICamlParameterBinding value) {
      return ~EqualsAny(fieldName, value);
    }

    public static CamlExpression Includes(CamlParameterBindingFieldRef fieldName, int value) {
      return Includes(fieldName, new CamlParameterBindingLookup(value));
    }
    public static CamlExpression Includes(CamlParameterBindingFieldRef fieldName, string value) {
      return Includes(fieldName, new CamlParameterBindingString(value));
    }
    public static CamlExpression Includes(CamlParameterBindingFieldRef fieldName, SPListItem value) {
      return Includes(fieldName, new CamlParameterBindingLookup(value));
    }
    public static CamlExpression Includes(CamlParameterBindingFieldRef fieldName, SPPrincipal value) {
      return Includes(fieldName, new CamlParameterBindingLookup(value));
    }
    public static CamlExpression Includes(CamlParameterBindingFieldRef fieldName, ICamlParameterBinding value) {
      return new CamlWhereBinaryComparisonExpression(CamlBinaryOperator.Includes, fieldName, value);
    }

    public static CamlExpression NotIncludes(CamlParameterBindingFieldRef fieldName, int value) {
      return NotIncludes(fieldName, new CamlParameterBindingLookup(value));
    }
    public static CamlExpression NotIncludes(CamlParameterBindingFieldRef fieldName, string value) {
      return NotIncludes(fieldName, new CamlParameterBindingString(value));
    }
    public static CamlExpression NotIncludes(CamlParameterBindingFieldRef fieldName, SPListItem value) {
      return NotIncludes(fieldName, new CamlParameterBindingLookup(value));
    }
    public static CamlExpression NotIncludes(CamlParameterBindingFieldRef fieldName, SPPrincipal value) {
      return NotIncludes(fieldName, new CamlParameterBindingLookup(value));
    }
    public static CamlExpression NotIncludes(CamlParameterBindingFieldRef fieldName, ICamlParameterBinding value) {
      return new CamlWhereBinaryComparisonExpression(CamlBinaryOperator.NotIncludes, fieldName, value);
    }

    public static CamlExpression LookupIdEquals(CamlParameterBindingFieldRef fieldName, int value) {
      return Equals(fieldName, new CamlParameterBindingLookup(value));
    }
    public static CamlExpression LookupIdNotEquals(CamlParameterBindingFieldRef fieldName, int value) {
      return NotEquals(fieldName, new CamlParameterBindingLookup(value));
    }
    public static CamlExpression LookupIdEqualsAny(CamlParameterBindingFieldRef fieldName, IEnumerable<int> value) {
      return EqualsAny(fieldName, new CamlParameterBindingLookup(value));
    }
    public static CamlExpression LookupIdNotEqualsAny(CamlParameterBindingFieldRef fieldName, IEnumerable<int> value) {
      return NotEqualsAny(fieldName, new CamlParameterBindingLookup(value));
    }
    public static CamlExpression LookupIdIncludes(CamlParameterBindingFieldRef fieldName, int value) {
      return Includes(fieldName, new CamlParameterBindingLookup(value));
    }
    public static CamlExpression LookupIdNotIncludes(CamlParameterBindingFieldRef fieldName, int value) {
      return NotIncludes(fieldName, new CamlParameterBindingLookup(value));
    }

    public static CamlExpression Membership(CamlParameterBindingFieldRef fieldName, SPGroup value) {
      return Membership(fieldName, new CamlParameterBindingLookup(value));
    }
    public static CamlExpression Membership(CamlParameterBindingFieldRef fieldName, ICamlParameterBinding value) {
      return Equals(fieldName, value) | new CamlWhereBinaryComparisonExpression(CamlBinaryOperator.Membership, fieldName, value);
    }
    public static CamlExpression IsNull(CamlParameterBindingFieldRef fieldName) {
      return new CamlWhereUnaryComparisonExpression(CamlUnaryOperator.IsNull, fieldName);
    }
    public static CamlExpression IsNotNull(CamlParameterBindingFieldRef fieldName) {
      return new CamlWhereUnaryComparisonExpression(CamlUnaryOperator.IsNotNull, fieldName);
    }
    public static CamlExpression BeginsWith(CamlParameterBindingFieldRef fieldName, string value) {
      return BeginsWith(fieldName, new CamlParameterBindingString(value));
    }
    public static CamlExpression BeginsWith(CamlParameterBindingFieldRef fieldName, ICamlParameterBinding value) {
      return new CamlWhereBinaryComparisonExpression(CamlBinaryOperator.BeginsWith, fieldName, value);
    }
    public static CamlExpression Contains(CamlParameterBindingFieldRef fieldName, string value) {
      return Contains(fieldName, new CamlParameterBindingString(value));
    }
    public static CamlExpression Contains(CamlParameterBindingFieldRef fieldName, ICamlParameterBinding value) {
      return new CamlWhereBinaryComparisonExpression(CamlBinaryOperator.Contains, fieldName, value);
    }
    public static CamlExpression OfContentType(SPContentTypeId value) {
      return BeginsWith("ContentTypeId", new CamlParameterBindingContentTypeId(value));
    }

    /// <summary>
    /// Performs logical And on a set of CAML expressions. Calling this method is equilavent to E1+E2+E3+....
    /// </summary>
    /// <param name="expressions">A set of CAML expressions</param>
    /// <returns>A resulting CAML expression.</returns>
    public static CamlExpression And(IEnumerable<CamlExpression> expressions) {
      return expressions.Aggregate((x, y) => x + y);
    }
    /// <summary>
    /// Performs logical And on a set of CAML expressions. Calling this method is equilavent to E1+E2+E3+....
    /// </summary>
    /// <param name="expressions">A set of CAML expressions</param>
    /// <returns>A resulting CAML expression.</returns>
    public static CamlExpression And(params CamlExpression[] expressions) {
      return And((IEnumerable<CamlExpression>)expressions);
    }
    /// <summary>
    /// Performs logical Or on a set of CAML expressions. Calling this method is equilavent to E1|E2|E3|....
    /// </summary>
    /// <param name="expressions">A set of CAML expressions</param>
    /// <returns>A resulting CAML expression.</returns>
    public static CamlExpression Or(IEnumerable<CamlExpression> expressions) {
      return expressions.Aggregate((x, y) => x | y);
    }
    /// <summary>
    /// Performs logical Or on a set of CAML expressions. Calling this method is equilavent to E1|E2|E3|....
    /// </summary>
    /// <param name="expressions">A set of CAML expressions</param>
    /// <returns>A resulting CAML expression.</returns>
    public static CamlExpression Or(params CamlExpression[] expressions) {
      return Or((IEnumerable<CamlExpression>)expressions);
    }
    /// <summary>
    /// Performs negation on a CAML expression. Calling this method is equilavent to ~E.
    /// </summary>
    /// <param name="expr">A CAML expression.</param>
    /// <returns>A resulting CAML expression.</returns>
    public static CamlExpression Not(CamlExpression expr) {
      return ~expr;
    }

    /// <summary>
    /// Parses a given string representation of a CAML expression into a <see cref="CamlExpression"/> instance.
    /// </summary>
    /// <param name="value">A string representation of a CAML expression.</param>
    /// <returns>A parsed <see cref="CamlExpression"/> instance that represent the same expression.</returns>
    public static CamlExpression Parse(string value) {
      if (String.IsNullOrEmpty(value)) {
        return Caml.Empty;
      }
      ParseState currentState = ParseState.Where;
      ICamlParameterBinding currentValue = null;
      List<object> valueCollection = new List<object>();
      string currentValueType = null;
      string currentFieldName = null;
      bool isValueCollection = false;
      bool isLookupId = false;
      Stack<CamlExpression> parsedExpressions = new Stack<CamlExpression>(new CamlExpression[] { null });

      using (XmlReader reader = new XmlTextReader(new StringReader(value))) {
        while (reader.Read()) {
          rewind:
          switch (reader.NodeType) {
            case XmlNodeType.Element:
              switch (reader.LocalName) {
                case Element.OrderBy:
                  currentState = ParseState.OrderBy;
                  break;
                case Element.GroupBy:
                  currentState = ParseState.GroupBy;
                  string collapse = reader.GetAttribute(Attribute.Collapse);
                  if (collapse != null) {
                    parsedExpressions.Push(parsedExpressions.Pop() + new CamlGroupByExpression(new CamlGroupByFieldRefExpression[0], new CamlParameterBindingBooleanString(BooleanString.True.Equals(collapse, StringComparison.OrdinalIgnoreCase))));
                  }
                  break;
                case Element.ViewFields:
                  currentState = ParseState.ViewFields;
                  break;
                case Element.Where:
                  currentState = ParseState.Where;
                  break;
                case Element.FieldRef:
                  switch (currentState) {
                    case ParseState.Where:
                      break;
                    case ParseState.OrderBy:
                      parsedExpressions.Push(parsedExpressions.Pop() + new CamlOrderByFieldRefExpression(reader.GetAttribute(Attribute.Name), Caml.BooleanString.False.Equals(reader.GetAttribute(Attribute.Ascending), StringComparison.OrdinalIgnoreCase) ? CamlOrder.Descending : CamlOrder.Ascending));
                      break;
                    case ParseState.GroupBy:
                      parsedExpressions.Push(parsedExpressions.Pop() + new CamlGroupByFieldRefExpression(reader.GetAttribute(Attribute.Name)));
                      break;
                    case ParseState.ViewFields:
                      parsedExpressions.Push(parsedExpressions.Pop() + new CamlViewFieldsFieldRefExpression(reader.GetAttribute(Attribute.Name)));
                      break;
                  }
                  isLookupId = Caml.BooleanString.True.Equals(reader.GetAttribute(Attribute.LookupId), StringComparison.OrdinalIgnoreCase);
                  currentFieldName = reader.GetAttribute(Attribute.Name);
                  break;
                case Element.Values:
                  isValueCollection = true;
                  if (isValueCollection) {
                    valueCollection.Clear();
                  }
                  break;
                case Element.Value:
                  currentValueType = reader.GetAttribute(Attribute.Type);
                  switch (currentValueType) {
                    case ValueTypeString.Boolean:
                      if (isValueCollection) {
                        valueCollection.Add(reader.ReadElementContentAsBoolean());
                      } else {
                        currentValue = new CamlParameterBindingBoolean(reader.ReadElementContentAsBoolean());
                      }
                      break;
                    case ValueTypeString.Lookup:
                    case ValueTypeString.Integer:
                      if (!reader.HasValue && !reader.IsEmptyElement) {
                        reader.ReadStartElement();
                        if (Element.UserID.Equals(reader.Name, StringComparison.OrdinalIgnoreCase)) {
                          currentValue = SpecialValue.UserID;
                        } else {
                          throw new ArgumentException("Invalid UserID element");
                        }
                      } else {
                        if (isValueCollection) {
                          valueCollection.Add(reader.ReadElementContentAsInt());
                        } else if (isLookupId) {
                          currentValue = new CamlParameterBindingLookup(reader.ReadElementContentAsInt());
                        } else {
                          currentValue = new CamlParameterBindingInteger(reader.ReadElementContentAsInt());
                        }
                      }
                      break;
                    case ValueTypeString.Number:
                      if (isValueCollection) {
                        valueCollection.Add(reader.ReadElementContentAsDouble());
                      } else {
                        currentValue = new CamlParameterBindingNumber(reader.ReadElementContentAsDouble());
                      }
                      break;
                    case ValueTypeString.URL:
                      if (isValueCollection) {
                        valueCollection.Add(reader.ReadElementContentAsString());
                      } else {
                        currentValue = new CamlParameterBindingUrl(reader.ReadElementContentAsString());
                      }
                      break;
                    case ValueTypeString.Guid:
                      if (isValueCollection) {
                        valueCollection.Add(reader.ReadElementContentAsString());
                      } else {
                        currentValue = new CamlParameterBindingGuid(new Guid(reader.ReadElementContentAsString()));
                      }
                      break;
                    case ValueTypeString.DateTime:
                      if (!reader.HasValue && !reader.IsEmptyElement) {
                        reader.ReadStartElement();
                        if (Element.Today.Equals(reader.Name, StringComparison.OrdinalIgnoreCase)) {
                          currentValue = SpecialValue.Today;
                        } else if (Element.Now.Equals(reader.Name, StringComparison.OrdinalIgnoreCase)) {
                          currentValue = SpecialValue.Now;
                        } else {
                          throw new ArgumentException("Invalid DateTime element");
                        }
                      } else {
                        if (isValueCollection) {
                          valueCollection.Add(SPUtility.CreateDateTimeFromISO8601DateTimeString(reader.ReadElementContentAsString()));
                        } else {
                          currentValue = new CamlParameterBindingDateTime(SPUtility.CreateDateTimeFromISO8601DateTimeString(reader.ReadElementContentAsString()), !Caml.BooleanString.False.Equals(reader.GetAttribute("IncludeTimeValue"), StringComparison.OrdinalIgnoreCase));
                        }
                      }
                      break;
                    case ValueTypeString.ContentTypeId:
                      currentValue = new CamlParameterBindingContentTypeId(new SPContentTypeId(reader.ReadElementContentAsString()));
                      break;
                    case ValueTypeString.ModStat:
                      if (isValueCollection) {
                        valueCollection.Add(Enum<SPModerationStatusType>.Parse(reader.ReadElementContentAsString()));
                      } else {
                        currentValue = new CamlParameterBindingModStat(Enum<SPModerationStatusType>.Parse(reader.ReadElementContentAsString()));
                      }
                      break;
                    default:
                      if (isValueCollection) {
                        valueCollection.Add(reader.ReadElementContentAsString());
                      } else {
                        currentValue = new CamlParameterBindingString(reader.ReadElementContentAsString());
                      }
                      break;
                  }
                  goto rewind;
              }
              break;
            case XmlNodeType.EndElement:
              switch (reader.LocalName) {
                case Element.Values:
                  isValueCollection = false;
                  switch (currentValueType) {
                    case ValueTypeString.Lookup:
                    case ValueTypeString.Integer:
                      if (isLookupId) {
                        currentValue = new CamlParameterBindingLookup(valueCollection.OfType<int>());
                      } else {
                        currentValue = new CamlParameterBindingInteger(valueCollection.OfType<int>());
                      }
                      break;
                    case ValueTypeString.Number:
                      currentValue = new CamlParameterBindingNumber(valueCollection.OfType<double>());
                      break;
                    case ValueTypeString.URL:
                      currentValue = new CamlParameterBindingUrl(valueCollection.OfType<string>());
                      break;
                    case ValueTypeString.Guid:
                      currentValue = new CamlParameterBindingGuid(valueCollection.OfType<Guid>());
                      break;
                    case ValueTypeString.ModStat:
                      currentValue = new CamlParameterBindingModStat(valueCollection.OfType<SPModerationStatusType>());
                      break;
                    default:
                      currentValue = new CamlParameterBindingString(valueCollection.OfType<string>());
                      break;
                  }
                  break;
                case CompareOperatorString.IsNull:
                case CompareOperatorString.IsNotNull:
                  parsedExpressions.Push(new CamlWhereUnaryComparisonExpression(Enum<CamlUnaryOperator>.Parse(reader.LocalName), currentFieldName));
                  break;
                case CompareOperatorString.BeginsWith:
                case CompareOperatorString.Contains:
                case CompareOperatorString.Eq:
                case CompareOperatorString.Neq:
                case CompareOperatorString.Lt:
                case CompareOperatorString.Leq:
                case CompareOperatorString.Gt:
                case CompareOperatorString.Geq:
                case CompareOperatorString.Includes:
                case CompareOperatorString.NotIncludes:
                case CompareOperatorString.In:
                case CompareOperatorString.Membership:
                  parsedExpressions.Push(new CamlWhereBinaryComparisonExpression(Enum<CamlBinaryOperator>.Parse(reader.LocalName), currentFieldName, currentValue));
                  break;
                case CompareOperatorString.And:
                case CompareOperatorString.Or:
                  parsedExpressions.Push(new CamlWhereLogicalExpression(Enum<CamlLogicalOperator>.Parse(reader.LocalName), (CamlWhereComparisonExpression)parsedExpressions.Pop(), (CamlWhereComparisonExpression)parsedExpressions.Pop()));
                  break;
                case CompareOperatorString.Not:
                  parsedExpressions.Push(new CamlWhereLogicalExpression(Enum<CamlLogicalOperator>.Parse(reader.LocalName), (CamlWhereComparisonExpression)parsedExpressions.Pop(), null));
                  break;
              }
              break;
          }
        }
      }
      return parsedExpressions.Peek();
    }
  }
}
