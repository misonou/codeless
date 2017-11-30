using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Codeless.SharePoint.ObjectModel {
  internal abstract class SPModelQueryExpressionFilter : CamlExpressionVisitor, ISPModelQueryFilter {
    public virtual bool ShouldTransformExpression(SPModelQuery query) {
      return true;
    }

    protected virtual void Initialize(SPModelQuery query) { }

    protected sealed override CamlExpression VisitBindedExpression(CamlBindedExpression expression) {
      CamlBindedExpression result = (CamlBindedExpression)base.VisitBindedExpression(expression);
      if (result.Expression == Caml.False) {
        return Caml.False;
      }
      return result;
    }

    CamlExpression ISPModelQueryFilter.TransformExpression(SPModelQuery query, CamlExpression expression) {
      Initialize(query);
      return base.Visit(expression);
    }

    object ICloneable.Clone() {
      return MemberwiseClone();
    }
  }
}
