using Microsoft.SharePoint;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Codeless.SharePoint.ObjectModel {
  internal class SPListQueryExpressionFilter : SPModelQueryExpressionFilter {
    private IList<string> allowedFields;

    public override bool ShouldTransformExpression(SPModelQuery query) {
      return !query.ForceKeywordSearch && query.Manager.ImplicitQueryMode == SPModelImplicitQueryMode.ListQuery;
    }

    protected override void Initialize(SPModelQuery query) {
      SPList list = query.Manager.ContextLists.First().EnsureList(query.Manager.ObjectCache).List;
      this.allowedFields = list.Fields.OfType<SPField>().Select(v => v.InternalName).ToArray();
    }

    protected override CamlExpression VisitGroupByFieldRefExpression(CamlGroupByFieldRefExpression expression) {
      return IsFieldAllowed(expression.FieldName.Bind(this.Bindings)) ? expression : Caml.Empty;
    }

    protected override CamlExpression VisitOrderByFieldRefExpression(CamlOrderByFieldRefExpression expression) {
      return IsFieldAllowed(expression.FieldName.Bind(this.Bindings)) ? expression : Caml.Empty;
    }

    protected override CamlExpression VisitViewFieldsFieldRefExpression(CamlViewFieldsFieldRefExpression expression) {
      return IsFieldAllowed(expression.FieldName.Bind(this.Bindings)) ? expression : Caml.Empty;
    }

    protected override CamlExpression VisitWhereBinaryComparisonExpression(CamlWhereBinaryComparisonExpression expression) {
      return IsFieldAllowed(expression.FieldName.Bind(this.Bindings)) ? expression : Caml.False;
    }

    protected override CamlExpression VisitWhereUnaryComparisonExpression(CamlWhereUnaryComparisonExpression expression) {
      return IsFieldAllowed(expression.FieldName.Bind(this.Bindings)) ? expression : Caml.False;
    }

    private bool IsFieldAllowed(string fieldName) {
      return allowedFields.Contains(fieldName);
    }
  }
}
