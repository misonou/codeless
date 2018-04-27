using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Codeless.SharePoint.ObjectModel.Linq {
  internal class SPModelParameterizedQueryExpressionFilter : SPModelQueryExpressionFilter {
    protected override CamlExpression VisitWhereUnaryComparisonExpression(CamlWhereUnaryComparisonExpression expression) {
      CamlLateBoundExpression lateBoundCond = expression as CamlLateBoundExpression;
      if (lateBoundCond != null) {
        return lateBoundCond.Bind(this.Bindings);
      }
      return base.VisitWhereUnaryComparisonExpression(expression);
    }

    protected override CamlExpression VisitWhereBinaryComparisonExpression(CamlWhereBinaryComparisonExpression expression) {
      if (expression.Operator == CamlBinaryOperator.In) {
        try {
          expression.Value.BindCollection(this.Bindings).Any();
        } catch (CamlParameterBindingEmptyCollectionException) {
          return Caml.False;
        }
      }
      return expression;
    }
  }
}
