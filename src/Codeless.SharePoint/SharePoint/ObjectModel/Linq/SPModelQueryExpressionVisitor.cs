using Codeless.SharePoint.Internal;
using IQToolkit;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Taxonomy;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Codeless.SharePoint.ObjectModel.Linq {
  internal enum SPModelQueryExecuteMode {
    Select,
    First,
    FirstOrDefault,
    ElementAt,
    ElementAtOrDefault,
    Single,
    SingleOrDefault,
    Count,
    Any,
    All
  }

  internal class SPModelQueryExpressionTranslateResult {
    public Type ModelType { get; set; }
    public CamlExpression Expression { get; set; }
    public LambdaExpression SelectExpression { get; set; }
    public int Offset { get; set; }
    public int Limit { get; set; }
    public SPModelQueryExecuteMode ExecuteMode { get; set; }
  }

  internal class SPModelQueryExpressionVisitor : IQToolkit.ExpressionVisitor {
    private readonly Stack<SPModelQueryExpressionScope> stack = new Stack<SPModelQueryExpressionScope>();
    private readonly SPModelQueryExpressionTranslateResult result = new SPModelQueryExpressionTranslateResult();
    private readonly string[] allowedFields;
    private readonly ISPModelManagerInternal manager;

    public SPModelQueryExpressionVisitor(ISPModelManagerInternal manager, string[] allowedFields) {
      CommonHelper.ConfirmNotNull(manager, "manager");
      this.manager = manager;
      this.allowedFields = allowedFields;
      this.result.Limit = (int)manager.Site.WebApplication.MaxItemsPerThrottledOperation;
    }

    public ISPModelManagerInternal Manager {
      get { return manager; }
    }

    public bool IsFieldAllowed(string fieldName) {
      return allowedFields == null || Array.IndexOf(allowedFields, fieldName) >= 0;
    }

    public SPModelQueryExpressionTranslateResult Translate(Expression expression) {
      CommonHelper.ConfirmNotNull(expression, "expression");
      SPModelQueryExpressionScope currentScope = new SPModelQueryExpressionScope(this);
      stack.Push(currentScope);

      Expression evaledExpression = PartialEvaluator.Eval(expression, CanBeEvaluatedLocally);
      Visit(evaledExpression);
      result.Expression = currentScope.Expression;
      return result;
    }

    protected override Expression VisitBinary(BinaryExpression expression) {
      SPModelQueryExpressionScope currentScope = stack.Peek();
      SPModelQueryExpressionScope childScope = new SPModelQueryExpressionScope(this);
      SPModelQueryExpressionScope childScopeTwo = null;
      stack.Push(childScope);

      Visit(expression.Left);
      if (expression.NodeType == ExpressionType.AndAlso || expression.NodeType == ExpressionType.OrElse) {
        stack.Pop();
        childScopeTwo = new SPModelQueryExpressionScope(this);
        stack.Push(childScopeTwo);
      }
      Visit(expression.Right);

      switch (expression.NodeType) {
        case ExpressionType.AndAlso:
          currentScope.Expression = childScope.Expression & childScopeTwo.Expression;
          break;
        case ExpressionType.OrElse:
          currentScope.Expression = childScope.Expression | childScopeTwo.Expression;
          break;
        case ExpressionType.Equal:
          if (childScope.Value == null) {
            currentScope.Expression = childScope.GetExpression(s => HandleNullExpression(s, false));
          } else {
            currentScope.Expression = childScope.GetExpression((SPModelQueryExpressionScope.ExpressionGenerator)Caml.Equals);
          }
          break;
        case ExpressionType.NotEqual:
          if (childScope.Value == null) {
            currentScope.Expression = childScope.GetExpression(s => HandleNullExpression(s, true));
          } else {
            currentScope.Expression = childScope.GetExpression(Caml.NotEquals);
          }
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
          throw new NotSupportedException(String.Format("The binary operator '{0}' is not supported", expression.NodeType));
      }
      stack.Pop();
      return expression;
    }

    protected override Expression VisitTypeIs(TypeBinaryExpression expression) {
      SPModelQueryExpressionScope currentScope = stack.Peek();
      try {
        SPModelDescriptor descriptor = SPModelDescriptor.Resolve(expression.TypeOperand);
        currentScope.Expression = descriptor.GetContentTypeExpression(manager.Descriptor);
      } catch (ArgumentException) {
        currentScope.Expression = Caml.False;
      }
      return expression;
    }

    protected override Expression VisitUnary(UnaryExpression expression) {
      SPModelQueryExpressionScope currentScope = stack.Peek();
      SPModelQueryExpressionScope childScope = new SPModelQueryExpressionScope(this);
      stack.Push(childScope);

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
          throw new NotSupportedException(String.Format("The unary operator '{0}' is not supported", expression.NodeType));
      }
      stack.Pop();
      return expression;
    }

    protected override Expression VisitConditional(ConditionalExpression expression) {
      if (expression.Test.NodeType == ExpressionType.Constant) {
        if (true.Equals(((ConstantExpression)expression.Test).Value)) {
          Visit(expression.IfTrue);
        } else {
          Visit(expression.IfFalse);
        }
      } else {
        SPModelQueryExpressionScope currentScope = stack.Peek();
        SPModelQueryExpressionScope childScope = new SPModelQueryExpressionScope(this);
        CamlExpression condition, trueExpression, falseExpression;
        stack.Push(childScope);

        Visit(expression.Test);
        condition = childScope.Expression;

        childScope.Reset();
        Visit(expression.IfTrue);
        trueExpression = childScope.Expression;

        childScope.Reset();
        Visit(expression.IfFalse);
        falseExpression = childScope.Expression;

        currentScope.Expression = ((condition & trueExpression) | ((~condition) & falseExpression));
        stack.Pop();
      }
      return expression;
    }

    protected override Expression VisitConstant(ConstantExpression expression) {
      IQueryable q = CommonHelper.TryCastOrDefault<IQueryable>(expression.Value);
      if (q != null) {
        if (q.Expression.NodeType == ExpressionType.Call) {
          Visit(q.Expression);
        }
      } else {
        stack.Peek().Value = expression.Value;
      }
      return expression;
    }

    protected override Expression VisitMemberAccess(MemberExpression expression) {
      SPModelQueryExpressionScope currentScope = stack.Peek();
      switch (expression.Expression.NodeType) {
        case ExpressionType.Parameter:
        case ExpressionType.MemberAccess:
        case ExpressionType.Convert:
        case ExpressionType.Call:
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
                throw new NotSupportedException(String.Format("The member '{0}' is not supported", expression.Member.Name));
            }
          } else if (expression.Member.DeclaringType == typeof(TaxonomyItem) || expression.Member.DeclaringType == typeof(SPPrincipal)) {
            switch (expression.Member.Name) {
              case "Id":
              case "ID":
                Visit(expression.Expression);
                break;
              default:
                throw new NotSupportedException(String.Format("The member '{0}' is not supported", expression.Member.Name));
            }
          } else {
            currentScope.MemberType = expression.Type;
            currentScope.Member = expression.Member;
            currentScope.FieldAssociations = SPModelFieldAssociationCollection.GetByMember(expression.Member);
          }
          break;
        default:
          throw new NotSupportedException(String.Format("The member '{0}' is not supported", expression.Member.Name));
      }
      return expression;
    }

    protected override Expression VisitMethodCall(MethodCallExpression expression) {
      SPModelQueryExpressionScope currentScope = stack.Peek();
      SPModelQueryExpressionScope childScope = new SPModelQueryExpressionScope(this);
      stack.Push(childScope);

      if (expression.Method.DeclaringType == typeof(Queryable)) {
        Visit(expression.Arguments[0]);
        currentScope.Expression = childScope.Expression;
        childScope.Reset();

        switch (expression.Method.Name) {
          case "Where":
            if (expression.Arguments.Count == 3) {
              throw new NotSupportedException(String.Format("The method '{0}' with element's index used in the logic is not supported", expression.Method.Name));
            }
            Visit(expression.Arguments[1]);
            currentScope.Expression += childScope.Expression;
            break;
          case "Union":
            if (expression.Arguments.Count == 3) {
              throw new NotSupportedException(String.Format("The method '{0}' with element's index used in the logic is not supported", expression.Method.Name));
            }
            Visit(expression.Arguments[1]);
            currentScope.Expression |= childScope.Expression;
            break;
          case "Count":
            result.ExecuteMode = Enum<SPModelQueryExecuteMode>.Parse(expression.Method.Name);
            if (expression.Arguments.Count > 1) {
              Visit(expression.Arguments[1]);
              currentScope.Expression += childScope.Expression;
            }
            break;
          case "All":
          case "Any":
          case "FirstOrDefault":
          case "First":
          case "SingleOrDefault":
          case "Single":
          case "ElementAtOrDefault":
          case "ElementAt":
            result.ExecuteMode = Enum<SPModelQueryExecuteMode>.Parse(expression.Method.Name);
            if (result.ExecuteMode == SPModelQueryExecuteMode.ElementAt || result.ExecuteMode == SPModelQueryExecuteMode.ElementAtOrDefault) {
              result.Limit += Math.Max(0, Convert.ToInt32(((ConstantExpression)expression.Arguments[1]).Value));
            } else if (result.ExecuteMode == SPModelQueryExecuteMode.Single || result.ExecuteMode == SPModelQueryExecuteMode.SingleOrDefault) {
              result.Limit = 2;
            } else {
              result.Limit = 1;
            }
            if (expression.Arguments.Count > 1) {
              Visit(expression.Arguments[1]);
              currentScope.Expression += childScope.Expression;
            }
            break;
          case "Take":
            result.Limit = Math.Max(0, Convert.ToInt32(((ConstantExpression)expression.Arguments[1]).Value));
            break;
          case "Skip":
            result.Offset = Math.Max(0, Convert.ToInt32(((ConstantExpression)expression.Arguments[1]).Value));
            break;
          case "OrderBy":
          case "ThenBy":
            if (expression.Arguments.Count == 3) {
              throw new NotSupportedException(String.Format("The method '{0}' with specified comparer is not supported", expression.Method.Name));
            }
            Visit(expression.Arguments[1]);
            currentScope.Expression += childScope.GetExpression(s => Caml.OrderByAscending(s.FieldRef), true);
            break;
          case "OrderByDescending":
          case "ThenByDescending":
            if (expression.Arguments.Count == 3) {
              throw new NotSupportedException(String.Format("The method '{0}' with specified comparer is not supported", expression.Method.Name));
            }
            Visit(expression.Arguments[1]);
            currentScope.Expression += childScope.GetExpression(s => Caml.OrderByDescending(s.FieldRef), true);
            break;
          case "Select":
            result.SelectExpression = (LambdaExpression)StripQuotes(expression.Arguments[1]);
            break;
          case "OfType":
            result.ModelType = expression.Method.GetGenericArguments()[0];
            break;
          default:
            throw new NotSupportedException(String.Format("The method '{0}' is not supported", expression.Method.Name));
        }
      } else if (expression.Method.DeclaringType == typeof(Enumerable)) {
        switch (expression.Method.Name) {
          case "Contains":
            if (expression.Arguments.Count == 3) {
              throw new NotSupportedException(String.Format("The method '{0}' with specified comparer is not supported", expression.Method.Name));
            }
            Visit(expression.Arguments[0]);
            Visit(expression.Arguments[1]);
            if (childScope.Value is IEnumerable) {
              if (((IEnumerable)childScope.Value).OfType<object>().Any()) {
                currentScope.Expression = childScope.GetExpression(Caml.EqualsAny);
              } else {
                currentScope.Expression = CamlExpression.False;
              }
            } else {
              currentScope.Expression = childScope.GetExpression(Caml.Includes);
            }
            break;
          default:
            throw new NotSupportedException(String.Format("The method '{0}' is not supported", expression.Method.Name));
        }
      } else if (expression.Method.DeclaringType == typeof(String)) {
        Visit(expression.Object);
        switch (expression.Method.Name) {
          case "StartsWith":
            Visit(expression.Arguments[0]);
            currentScope.Expression = childScope.GetExpression(s => Caml.BeginsWith(s.FieldRef, (childScope.Value ?? String.Empty).ToString()));
            break;
          default:
            throw new NotSupportedException(String.Format("The method '{0}' is not supported", expression.Method.Name));
        }
      } else if (expression.Method.DeclaringType.IsOf(typeof(ICollection<>))) {
        switch (expression.Method.Name) {
          case "Contains":
            Visit(expression.Object);
            Visit(expression.Arguments[0]);
            if (childScope.Value is IEnumerable) {
              if (((IEnumerable)childScope.Value).OfType<object>().Any()) {
                currentScope.Expression = childScope.GetExpression(Caml.EqualsAny);
              } else {
                currentScope.Expression = CamlExpression.False;
              }
            } else {
              currentScope.Expression = childScope.GetExpression(Caml.Includes);
            }
            break;
          default:
            throw new NotSupportedException(String.Format("The method '{0}' is not supported", expression.Method.Name));
        }
      } else if (expression.Method.Name == "Equals" && expression.Arguments.Count == 1) {
        Visit(expression.Object);
        Visit(expression.Arguments[0]);
        currentScope.Expression = childScope.GetExpression((SPModelQueryExpressionScope.ExpressionGenerator)Caml.Equals);
      } else {
        throw new NotSupportedException(String.Format("The method '{0}' is not supported", expression.Method.Name));
      }
      stack.Pop();
      return expression;
    }

    private CamlExpression HandleNullExpression(SPModelQueryFieldInfo field, bool negate) {
      CamlExpression expression = Caml.IsNull(field.FieldRef);
      if (field.FieldTypeAsString == "TaxonomyFieldType" || field.FieldTypeAsString == "TaxonomyFieldTypeMulti") {
        SPList taxonomyHiddenList = SPExtensionHelper.GetTaxonomyHiddenList(manager.Site);
        foreach (SPListItem item in taxonomyHiddenList.GetItems("IdForTerm")) {
          if (manager.TermStore.GetTerm(new Guid((string)item["IdForTerm"])) == null) {
            expression |= Caml.LookupIdEquals(field.FieldRef, item.ID);
          }
        }
      }
      if (negate) {
        return ~expression;
      }
      return expression;
    }

    private bool CanBeEvaluatedLocally(Expression expression) {
      if (expression.NodeType == ExpressionType.Parameter) {
        return false;
      }
      if (expression.NodeType == ExpressionType.Call) {
        MethodCallExpression methodCallExpression = (MethodCallExpression)expression;
        Expression thisObjectExpression = methodCallExpression.Arguments.FirstOrDefault();
        if (methodCallExpression.Method.DeclaringType == typeof(Queryable) && thisObjectExpression != null && thisObjectExpression.NodeType == ExpressionType.Constant) {
          object thisObject = ((ConstantExpression)thisObjectExpression).Value;
          if (thisObject != null && thisObject.GetType().IsOf(typeof(SPModelQuery<>))) {
            return false;
          }
        }
      }
      return true;
    }

    private static Expression StripQuotes(Expression expression) {
      while (expression.NodeType == ExpressionType.Quote) {
        expression = ((UnaryExpression)expression).Operand;
      }
      return expression;
    }
  }
}
