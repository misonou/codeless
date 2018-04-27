using IQToolkit;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Codeless.SharePoint.Internal {
  [DebuggerDisplay("{Expression,nq}")]
  internal class ParameterizedExpression : IEquatable<ParameterizedExpression> {
    private readonly LambdaExpression expression;
    private readonly int hashCode;

    private ParameterizedExpression(LambdaExpression expression, int hashCode) {
      this.expression = expression;
      this.hashCode = hashCode;
    }

    public Expression Expression {
      get { return expression.Body; }
    }

    public ReadOnlyCollection<ParameterExpression> Parameters {
      get { return expression.Parameters; }
    }

    public static ParameterizedExpression Create(Expression expression, out object[] args) {
      CommonHelper.ConfirmNotNull(expression, "expression");
      return new ExpressionParameterizeVisitor().Visit(expression, out args);
    }

    public bool Equals(ParameterizedExpression other) {
      if (other == null || hashCode != other.hashCode) {
        return false;
      }
      return ExpressionComparer.AreEqual(expression, other.expression);
    }

    public override bool Equals(object obj) {
      ParameterizedExpression other = obj as ParameterizedExpression;
      return other != null && Equals(other);
    }

    public override int GetHashCode() {
      return hashCode;
    }

    private class ExpressionParameterizeVisitor : IQToolkit.ExpressionVisitor {
      private readonly List<object> arguments = new List<object>();
      private readonly List<ParameterExpression> parameters = new List<ParameterExpression>();
      private int hashCode = 5381;

      public ParameterizedExpression Visit(Expression expression, out object[] args) {
        Expression body = Visit(expression);
        args = arguments.ToArray();
        return new ParameterizedExpression(Expression.Lambda(body, parameters.ToArray()), hashCode);
      }

      protected override Expression Visit(Expression expression) {
        if (expression != null) {
          hashCode = ((hashCode << 5) + hashCode) ^ expression.NodeType.GetHashCode();
          hashCode = ((hashCode << 5) + hashCode) ^ expression.Type.GetHashCode();
        }
        return base.Visit(expression);
      }

      protected override Expression VisitConstant(ConstantExpression expression) {
        ParameterExpression param = Expression.Parameter(expression.Type, "p" + arguments.Count);
        parameters.Add(param);
        arguments.Add(expression.Value);
        return param;
      }
    }
  }
}
