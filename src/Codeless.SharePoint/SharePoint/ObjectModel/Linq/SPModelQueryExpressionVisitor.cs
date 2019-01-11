using Codeless.SharePoint.Internal;
using IQToolkit;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Taxonomy;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Codeless.SharePoint.ObjectModel.Linq {
  internal class SPModelQueryExpressionVisitor : IQToolkit.ExpressionVisitor {
    private static readonly ParameterExpression pArr = Expression.Parameter(typeof(object[]), "args");
    private static readonly ParameterExpression pRes = Expression.Parameter(typeof(IEnumerable), "result");

    private readonly SPModelQueryBuilder builder = new SPModelQueryBuilder();
    private readonly ISPModelManagerInternal manager;
    private readonly ReadOnlyCollection<ParameterExpression> parameters;
    private SPModelQueryExpressionScope currentScope;
    private ParameterExpression lambdaParam;
    private bool invariantExpression;
    private int exprTypes;

    private SPModelQueryExpressionVisitor(ISPModelManagerInternal manager, ReadOnlyCollection<ParameterExpression> parameters) {
      this.manager = manager;
      this.parameters = parameters;
      this.currentScope = new SPModelQueryExpressionScope(this);
    }

    public ISPModelManagerInternal Manager {
      get { return manager; }
    }

    public static SPModelQueryBuilder Translate(ParameterizedExpression expression, ISPModelManagerInternal manager) {
      CommonHelper.ConfirmNotNull(manager, "manager");
      CommonHelper.ConfirmNotNull(expression, "expression");
      SPModelQueryExpressionVisitor visitor = new SPModelQueryExpressionVisitor(manager, expression.Parameters);
      visitor.Visit(expression);
      return visitor.builder;
    }

    protected void Visit(ParameterizedExpression expression) {
      base.VisitLambda(Expression.Lambda(expression.Expression, expression.Parameters.ToArray()));
      builder.Expression = this.currentScope.Expression;
      if (builder.SelectExpression != null) {
        builder.SelectExpression = Expression.Lambda<SPModelParameterizedQuery.ResultEvaluator>(EnsureReturnObject(builder.SelectExpression), pRes, pArr);
      }
    }

    protected override Expression Visit(Expression expression) {
      // extract expression that is invariant to the query to compile parameter evaluators later on
      // directly mentioned invariant parameters are skipped because compilation is unncessary
      if (expression != null && !invariantExpression && expression.NodeType != ExpressionType.Parameter && lambdaParam != null && !ContainsOrEquals(expression, lambdaParam)) {
        invariantExpression = true;

        Expression result = base.Visit(expression);
        Expression body = EnsureReturnObject(result);
        currentScope.ParameterName = "c" + builder.ParameterEvaluators.Count;
        builder.ParameterEvaluators.Add(currentScope.ParameterName, Expression.Lambda<SPModelParameterizedQuery.ParameterEvaluator>(body, pArr).Compile());
        invariantExpression = false;
        return result;
      }
      return base.Visit(expression);
    }

    protected override Expression VisitConstant(ConstantExpression expression) {
      // there should be no constant
      throw new InvalidOperationException();
    }

    protected override Expression VisitParameter(ParameterExpression expression) {
      if (builder.SelectExpression != null && expression == lambdaParam) {
        // if the first parameter of selector expression is mentioned without distinguishable field access
        // all content type columns should be selected
        builder.SelectAllProperties = true;
      }
      if (parameters.Contains(expression)) {
        currentScope.ParameterName = expression.Name;
        return Expression.Convert(Expression.ArrayIndex(pArr, Expression.Constant(parameters.IndexOf(expression))), expression.Type);
      }
      return expression;
    }

    protected override Expression VisitLambda(LambdaExpression expression) {
      return base.VisitLambda(expression);
    }

    protected override Expression VisitBinary(BinaryExpression expression) {
      if (invariantExpression || builder.SelectExpression != null) {
        return base.VisitBinary(expression);
      }
      SPModelQueryExpressionScope currentScope = this.currentScope;
      SPModelQueryExpressionScope childScope = new SPModelQueryExpressionScope(this);
      SPModelQueryExpressionScope childScopeTwo = null;
      this.currentScope = childScope;

      if (expression.NodeType == ExpressionType.AndAlso || expression.NodeType == ExpressionType.OrElse) {
        childScopeTwo = new SPModelQueryExpressionScope(this);
        VisitConditionalBranch(expression.Left);
        this.currentScope = childScopeTwo;
        VisitConditionalBranch(expression.Right);
      } else {
        Visit(expression.Left);
        Visit(expression.Right);
      }
      switch (expression.NodeType) {
        case ExpressionType.AndAlso:
          currentScope.Expression = childScope.Expression & childScopeTwo.Expression;
          break;
        case ExpressionType.OrElse:
          currentScope.Expression = childScope.Expression | childScopeTwo.Expression;
          break;
        case ExpressionType.Equal:
          currentScope.Expression = childScope.GetExpression(s => HandleEqualityComparison(s, CamlBinaryOperator.Eq));
          break;
        case ExpressionType.NotEqual:
          currentScope.Expression = childScope.GetExpression(s => HandleEqualityComparison(s, CamlBinaryOperator.Neq));
          break;
        case ExpressionType.LessThan:
          currentScope.Expression = childScope.GetExpression(Caml.LessThan);
          break;
        case ExpressionType.LessThanOrEqual:
          currentScope.Expression = childScope.GetExpression(Caml.LessThanOrEqual);
          break;
        case ExpressionType.GreaterThan:
          currentScope.Expression = childScope.GetExpression(Caml.GreaterThan);
          break;
        case ExpressionType.GreaterThanOrEqual:
          currentScope.Expression = childScope.GetExpression(Caml.GreaterThanOrEqual);
          break;
        default:
          throw new NotSupportedException(String.Format("Binary operator '{0}' is not supported", expression.NodeType));
      }
      this.currentScope = currentScope;
      return expression;
    }

    protected override Expression VisitTypeIs(TypeBinaryExpression expression) {
      if (invariantExpression || builder.SelectExpression != null) {
        return base.VisitTypeIs(expression);
      }
      try {
        SPModelDescriptor descriptor = SPModelDescriptor.Resolve(expression.TypeOperand);
        currentScope.Expression = descriptor.GetContentTypeExpression(manager.Descriptor);
      } catch (ArgumentException) {
        currentScope.Expression = Caml.False;
      }
      return expression;
    }

    protected override Expression VisitUnary(UnaryExpression expression) {
      if (invariantExpression || builder.SelectExpression != null) {
        return base.VisitUnary(expression);
      }
      SPModelQueryExpressionScope currentScope = this.currentScope;
      SPModelQueryExpressionScope childScope = new SPModelQueryExpressionScope(this);
      this.currentScope = childScope;

      Visit(expression.Operand);
      switch (expression.NodeType) {
        case ExpressionType.Not:
          currentScope.Expression = ~childScope.Expression;
          break;
        case ExpressionType.Convert:
        case ExpressionType.ConvertChecked:
        case ExpressionType.Quote:
          childScope.CopyTo(currentScope);
          break;
        default:
          throw new NotSupportedException(String.Format("Unary operator '{0}' is not supported", expression.NodeType));
      }
      this.currentScope = currentScope;
      return expression;
    }

    protected override Expression VisitConditional(ConditionalExpression expression) {
      if (invariantExpression || builder.SelectExpression != null) {
        return base.VisitConditional(expression);
      }
      SPModelQueryExpressionScope currentScope = this.currentScope;
      SPModelQueryExpressionScope childScope = new SPModelQueryExpressionScope(this);
      CamlExpression condition, trueExpression, falseExpression;
      this.currentScope = childScope;

      VisitConditionalBranch(expression.Test);
      condition = childScope.Expression;

      childScope.Reset();
      Visit(expression.IfTrue);
      trueExpression = childScope.Expression;

      childScope.Reset();
      Visit(expression.IfFalse);
      falseExpression = childScope.Expression;

      currentScope.Expression = ((condition & trueExpression) | ((~condition) & falseExpression));
      this.currentScope = currentScope;
      return expression;
    }

    protected override Expression VisitMemberAccess(MemberExpression expression) {
      if (invariantExpression) {
        return base.VisitMemberAccess(expression);
      }
      if (expression.Expression != lambdaParam) {
        if (expression.Member.DeclaringType == typeof(ISPModelMetaData) &&
           ((expression.Expression.NodeType == ExpressionType.Call && ((MethodCallExpression)expression.Expression).Method == typeof(SPModelExtension).GetMethod("GetMetaData") && ((MethodCallExpression)expression.Expression).Arguments[0] == lambdaParam) ||
            (expression.Expression.NodeType == ExpressionType.Convert && ((UnaryExpression)expression.Expression).Operand == lambdaParam))) {
          // allow non-direct field access on the ISPModelMetaData interface
        } else {
          return base.VisitMemberAccess(expression);
        }
      }

      currentScope.MemberType = expression.Type;
      currentScope.Member = expression.Member;
      currentScope.Field = default(SPModelQueryFieldInfo);
      currentScope.FieldAssociations = null;

      if (expression.Member.DeclaringType == typeof(ISPModelMetaData)) {
        switch (expression.Member.Name) {
          case "ID":
            currentScope.Field = SPModelQueryFieldInfo.ID;
            break;
          case "UniqueId":
            currentScope.Field = SPModelQueryFieldInfo.UniqueId;
            break;
          case "FileRef":
            currentScope.Field = SPModelQueryFieldInfo.FileRef;
            break;
          case "FileLeafRef":
            currentScope.Field = SPModelQueryFieldInfo.FileLeafRef;
            break;
          case "LastModified":
            currentScope.Field = SPModelQueryFieldInfo.LastModified;
            break;
          case "CheckOutUserID":
            currentScope.Field = SPModelQueryFieldInfo.CheckOutUserID;
            break;
          default:
            throw new NotSupportedException(String.Format("Member '{0}' is not supported", GetMemberFullName(expression.Member)));
        }
      } else {
        currentScope.FieldAssociations = SPModelFieldAssociationCollection.GetByMember(expression.Member);
        foreach (SPFieldAttribute field in currentScope.FieldAssociations.Fields) {
          if (field.TypeAsString == "TaxonomyFieldType" || field.TypeAsString == "TaxonomyFieldTypeMulti") {
            builder.TaxonomyFields.Add(field.ListFieldInternalName);
          }
        }
      }
      if (builder.SelectExpression != null) {
        if (currentScope.Field.FieldRef != null) {
          builder.AddSelectProperty(currentScope.Field.FieldRef);
        } else if (currentScope.FieldAssociations.Queryable && expression.Member.MemberType == MemberTypes.Property && ((PropertyInfo)expression.Member).GetGetMethod().IsAbstract) {
          builder.AddSelectProperty(currentScope.FieldAssociations.Fields.First().ListFieldInternalName);
        } else {
          builder.SelectAllProperties = true;
        }
      }
      return expression;
    }

    protected override Expression VisitMethodCall(MethodCallExpression expression) {
      if (invariantExpression) {
        return base.VisitMethodCall(expression);
      }
      bool sameQueryable = expression.Method.DeclaringType == typeof(Queryable) && ContainsOrEquals(expression.Arguments[0], parameters[0]);
      if (builder.SelectExpression != null && !sameQueryable) {
        return base.VisitMethodCall(expression);
      }

      SPModelQueryExpressionScope currentScope = this.currentScope;
      try {
        SPModelQueryExpressionScope childScope = new SPModelQueryExpressionScope(this);
        this.currentScope = childScope;

        if (sameQueryable) {
          ValidateQueryableMethodCall(expression);

          ParameterExpression currentLambdaParam = lambdaParam;
          Visit(expression.Arguments[0]);
          currentScope.Expression = childScope.Expression;
          childScope.Reset();

          currentScope.Expression += GetExpressionFromQueryableMethod(expression);
          lambdaParam = currentLambdaParam;
          return expression;
        }
        if (expression.Method.DeclaringType == typeof(Enumerable) || expression.Method.DeclaringType.IsOf(typeof(ICollection<>))) {
          currentScope.Expression = GetExpressionFromEnumerableMethod(expression);
          return expression;
        }
        if (expression.Method.DeclaringType == typeof(string)) {
          currentScope.Expression = GetExpressionFromStringMethod(expression);
          return expression;
        }
        if (expression.Method.Name == "Equals" && expression.Arguments.Count == 1) {
          Visit(expression.Object);
          Visit(expression.Arguments[0]);
          currentScope.Expression = childScope.GetExpression((SPModelQueryExpressionScope.ExpressionGenerator)Caml.Equals);
          return expression;
        }
        throw ThrowMethodNotSupported(expression.Method);
      } finally {
        this.currentScope = currentScope;
      }
    }

    private CamlExpression GetExpressionFromQueryableMethod(MethodCallExpression expression) {
      int argCount = expression.Arguments.Count;
      if (argCount > 1) {
        LambdaExpression lamdba = StripQuotes(expression.Arguments[1]) as LambdaExpression;
        if (lamdba != null) {
          lambdaParam = lamdba.Parameters[0];
        }
      }

      switch (expression.Method.Name) {
        case "Count":
        case "Any":
        case "FirstOrDefault":
        case "First":
        case "SingleOrDefault":
        case "Single":
          if (argCount == 2) {
            Visit(expression.Arguments[1]);
          }
          builder.ExecuteMode = Enum<SPModelQueryExecuteMode>.Parse(expression.Method.Name);
          AppendSelectExpression(expression, builder.ExecuteMode.ToString(), true);
          return currentScope.Expression;

        case "All":
          Visit(expression.Arguments[1]);
          builder.ExecuteMode = SPModelQueryExecuteMode.Any;
          AppendSelectExpression(expression, builder.ExecuteMode.ToString(), true);
          return ~currentScope.Expression;

        case "ElementAtOrDefault":
        case "ElementAt":
          Visit(expression.Arguments[1]);
          builder.Parameters[SPModelParameterizedQuery.PIndexOffset] = currentScope.ParameterName;
          builder.ExecuteMode = expression.Method.Name == "ElementAt" ? SPModelQueryExecuteMode.First : SPModelQueryExecuteMode.FirstOrDefault;
          AppendSelectExpression(expression, builder.ExecuteMode.ToString(), true, Expression.Constant(0));
          return Caml.Empty;

        case "Aggregate":
        case "Average":
        case "Max":
        case "Min":
        case "Sum":
        case "Select":
        case "SelectMany":
          AppendSelectExpression(expression);
          return Caml.Empty;

        case "Take":
          Visit(expression.Arguments[1]);
          builder.Parameters[SPModelParameterizedQuery.PIndexLimit] = currentScope.ParameterName;
          return Caml.Empty;

        case "Skip":
          Visit(expression.Arguments[1]);
          builder.Parameters[SPModelParameterizedQuery.PIndexOffset] = currentScope.ParameterName;
          return Caml.Empty;

        case "OrderBy":
        case "ThenBy":
        case "OrderByDescending":
        case "ThenByDescending":
          CamlOrder dir = expression.Method.Name.Contains("Descending") ? CamlOrder.Descending : CamlOrder.Ascending;
          Visit(expression.Arguments[1]);
          return this.currentScope.GetExpression(s => Caml.OrderBy(s.FieldRef, dir), true);

        case "Where":
          Visit(expression.Arguments[1]);
          return currentScope.Expression;

        case "OfType":
          if (builder.SelectExpression != null) {
            AppendSelectExpression(expression);
          } else {
            if (builder.ModelType == null) {
              builder.ContentTypeIds.AddRange(manager.Descriptor.ContentTypeIds);
            }
            builder.ModelType = expression.Method.GetGenericArguments()[0];
            SPModelDescriptor descriptor;
            try {
              descriptor = SPModelDescriptor.Resolve(builder.ModelType);
            } catch (ArgumentException) {
              throw new NotSupportedException("'OfType' constraint must be used with valid model type or interface type");
            }
            SPContentTypeId[] result = SPModelDescriptor.IntersectContentTypeIds(builder.ContentTypeIds, descriptor.ContentTypeIds.ToArray());
            builder.ContentTypeIds.Clear();
            builder.ContentTypeIds.AddRange(result);
          }
          return Caml.Empty;
      }
      throw ThrowMethodNotSupported(expression.Method);
    }

    private void ValidateQueryableMethodCall(MethodCallExpression expression) {
      const int Type_Predicate = 1;
      const int Type_Skip = 2;
      const int Type_Take = 4;
      const int Type_Projection = 8;
      const int Type_Aggregate = 16;

      switch (expression.Method.Name) {
        case "OfType":
          return;

        case "Count":
        case "Any":
        case "FirstOrDefault":
        case "First":
        case "SingleOrDefault":
        case "Single":
        case "All":
        case "ElementAtOrDefault":
        case "ElementAt":
        case "Aggregate":
        case "Average":
        case "Max":
        case "Min":
        case "Sum":
          exprTypes |= Type_Aggregate;
          return;

        case "OrderBy":
        case "ThenBy":
        case "OrderByDescending":
        case "ThenByDescending":
        case "Where":
          if (expression.Arguments.Count == 3) {
            throw ThrowMethodNotSupported(expression.Method);
          }
          exprTypes |= Type_Predicate;
          return;

        case "Skip":
          if ((exprTypes & Type_Predicate) != 0) {
            throw new NotSupportedException("'Where' or 'OrderBy' constraint after 'Skip' constraint is not supported");
          }
          if ((exprTypes & Type_Skip) != 0) {
            throw new NotSupportedException("Multiple 'Skip' constraints is not supported");
          }
          exprTypes |= Type_Skip;
          return;

        case "Take":
          if ((exprTypes & Type_Predicate) != 0) {
            throw new NotSupportedException("'Where' or 'OrderBy' constraint after 'Take' constraint is not supported");
          }
          if ((exprTypes & Type_Skip) != 0) {
            throw new NotSupportedException("'Skip' constraint after 'Take' constraint is not supported");
          }
          if ((exprTypes & Type_Take) != 0) {
            throw new NotSupportedException("Multiple 'Take' constraints is not supported");
          }
          exprTypes |= Type_Take;
          return;

        case "Select":
        case "SelectMany":
          if ((exprTypes & Type_Predicate) != 0) {
            throw new NotSupportedException("'Where' or 'OrderBy' constraint after result projection is not supported");
          }
          if ((exprTypes & Type_Skip) != 0) {
            throw new NotSupportedException("'Skip' constraint after result projection is not supported");
          }
          if ((exprTypes & Type_Take) != 0) {
            throw new NotSupportedException("'Take' constraint after result projection is not supported");
          }
          if ((exprTypes & Type_Projection) != 0) {
            throw new NotSupportedException("Multiple result projections is not supported");
          }
          exprTypes |= Type_Projection;
          return;
      }
      throw ThrowMethodNotSupported(expression.Method);
    }

    private CamlExpression GetExpressionFromStringMethod(MethodCallExpression expression) {
      Visit(expression.Object);
      switch (expression.Method.Name) {
        case "StartsWith":
          Visit(expression.Arguments[0]);
          return currentScope.GetExpression(Caml.BeginsWith);
      }
      throw ThrowMethodNotSupported(expression.Method);
    }

    private CamlExpression GetExpressionFromEnumerableMethod(MethodCallExpression expression) {
      switch (expression.Method.Name) {
        case "Contains":
          Expression argument;
          if (expression.Method.IsStatic) {
            if (expression.Arguments.Count == 3) {
              throw ThrowMethodNotSupported(expression.Method);
            }
            Visit(expression.Arguments[0]);
            argument = expression.Arguments[1];
          } else {
            Visit(expression.Object);
            argument = expression.Arguments[0];
          }
          Visit(argument);
          if (ContainsOrEquals(argument, lambdaParam)) {
            return currentScope.GetExpression(Caml.EqualsAny);
          } else {
            return currentScope.GetExpression(Caml.Includes);
          }
      }
      throw ThrowMethodNotSupported(expression.Method);
    }

    private Expression VisitConditionalBranch(Expression expression) {
      Expression result = Visit(expression);
      if (currentScope.Expression == null && currentScope.ParameterName != null && expression.Type == typeof(bool)) {
        currentScope.Expression = new CamlLateBoundEmptyExpression(Caml.Parameter.BooleanString(currentScope.ParameterName));
      }
      return result;
    }

    private void AppendSelectExpression(MethodCallExpression expression) {
      AppendSelectExpression(expression, expression.Method.Name, false);
    }

    private void AppendSelectExpression(MethodCallExpression expression, string methodName, bool replaceArgs, params Expression[] newArgs) {
      Expression[] args = new Expression[replaceArgs ? newArgs.Length + 1 : expression.Arguments.Count];

      if (builder.SelectExpression == null) {
        // return type of ISPModelManagerBase.GetItems is always IEnumerable<T>
        // where T is the manager's descriptor model type
        builder.SelectExpression = Expression.Convert(pRes, typeof(IEnumerable<>).MakeGenericType(manager.Descriptor.ModelType));
        if (builder.ModelType != null) {
          builder.SelectExpression = Expression.Call(typeof(Enumerable), "OfType", new[] { builder.ModelType }, builder.SelectExpression);
        }
      }
      args[0] = builder.SelectExpression;

      if (replaceArgs) {
        Array.Copy(newArgs, 0, args, 1, newArgs.Length);
      } else {
        for (int i = 1, count = expression.Arguments.Count; i < count; i++) {
          if (expression.Arguments[i].Type.IsOf(typeof(Expression<>))) {
            args[i] = Visit(StripQuotes(expression.Arguments[i]));
          } else {
            args[i] = Visit(expression.Arguments[i]);
          }
        }
      }
      MethodInfo method = typeof(Enumerable).GetMethod(methodName, false, args.Select(v => v.Type).ToArray());
      if (method == null) {
        throw ThrowMethodNotSupported(expression.Method);
      }
      builder.SelectExpression = Expression.Call(method, args);
    }

    private CamlExpression HandleEqualityComparison(SPModelQueryFieldInfo s, CamlBinaryOperator op) {
      ICamlParameterBinding value = currentScope.GetValueBinding(s);
      CamlExpression expression = new CamlWhereBinaryComparisonExpression(op, s.FieldRef, value);
      if (currentScope.MemberType.IsValueType || currentScope.MemberType == typeof(string)) {
        string defaultValue = value.Bind(new Hashtable { { currentScope.ParameterName, currentScope.MemberType.IsValueType ? currentScope.MemberType.GetDefaultValue() : "" } });
        CamlExpression lateBoundCond = new CamlLateBoundDefaultValueAsNullExpression(s.FieldRef, value, defaultValue);
        return op == CamlBinaryOperator.Eq ? expression | lateBoundCond : expression & ~lateBoundCond;
      }
      return expression;
    }

    private static Expression EnsureReturnObject(Expression expression) {
      if (expression.Type == typeof(object)) {
        return expression;
      }
      return Expression.Convert(expression, typeof(object));
    }

    private static Exception ThrowMethodNotSupported(MethodInfo method) {
      throw new NotSupportedException(String.Format("Method '{0}' is not supported", GetMethodFullName(method)));
    }

    private static string GetMethodFullName(MethodInfo method) {
      StringBuilder sb = new StringBuilder();
      sb.Append(method.DeclaringType.FullName);
      sb.Append(".");
      sb.Append(method.Name);
      sb.Append("(");
      Type[] paramTypes = method.GetParameterTypes();
      for (int i = 0; i < paramTypes.Length; i++) {
        if (i > 0) {
          sb.Append(", ");
        }
        sb.Append(paramTypes[i].ToString());
      }
      sb.Append(")");
      return sb.ToString();
    }

    private static string GetMemberFullName(MemberInfo member) {
      return String.Concat(member.DeclaringType.FullName, ".", member.Name);
    }

    private static bool ContainsOrEquals(Expression expression, Expression searchFor) {
      return expression == searchFor || TypedSubtreeFinder.Find(expression, searchFor.Type) == searchFor;
    }

    private static Expression StripQuotes(Expression expression) {
      while (expression.NodeType == ExpressionType.Quote) {
        expression = ((UnaryExpression)expression).Operand;
      }
      return expression;
    }
  }
}
