using Codeless.SharePoint.Internal;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Taxonomy;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Codeless.SharePoint.ObjectModel.Linq {
  internal class TaxonomyNullEqualityExpressionFilter : SPModelQueryExpressionFilter {
    private ISPModelManagerInternal manager;
    private ReadOnlyCollection<string> transformFields;
    private List<int> deletedTerms;

    public override bool ShouldTransformExpression(SPModelQuery query) {
      SPModelParameterizedQuery pq = query as SPModelParameterizedQuery;
      return pq != null && pq.TaxonomyFields.Count > 0;
    }

    protected override void Initialize(SPModelQuery query) {
      this.manager = query.Manager;
      this.transformFields = ((SPModelParameterizedQuery)query).TaxonomyFields;
    }

    protected override CamlExpression VisitWhereBinaryComparisonExpression(CamlWhereBinaryComparisonExpression expression) {
      if (expression.Operator == CamlBinaryOperator.Eq || expression.Operator == CamlBinaryOperator.Neq) {
        if (transformFields.Contains(expression.FieldName.Bind(this.Bindings))) {
          try {
            expression.Value.Bind(this.Bindings);
          } catch (CamlParameterBindingNullException) {
            return UpdateExpression(expression, expression.FieldName, expression.Operator == CamlBinaryOperator.Eq);
          }
        }
      }
      return expression;
    }

    protected override CamlExpression VisitWhereUnaryComparisonExpression(CamlWhereUnaryComparisonExpression expression) {
      if (expression.Operator == CamlUnaryOperator.IsNull || expression.Operator == CamlUnaryOperator.IsNotNull) {
        if (transformFields.Contains(expression.FieldName.Bind(this.Bindings))) {
          return UpdateExpression(expression, expression.FieldName, expression.Operator == CamlUnaryOperator.IsNull);
        }
      }
      return expression;
    }

    private CamlExpression UpdateExpression(CamlExpression expression, CamlParameterBindingFieldRef field, bool equals) {
      ICollection<int> ids = GetDeletedTermIDs();
      if (ids.Count == 0) {
        return expression;
      }
      CamlExpression constraint = Caml.LookupIdEqualsAny(field, ids);
      return equals ? expression | constraint : expression & ~constraint;
    }

    private ICollection<int> GetDeletedTermIDs() {
      if (deletedTerms == null) {
        deletedTerms = new List<int>();
        SPList taxonomyHiddenList = SPExtensionHelper.GetTaxonomyHiddenList(manager.Site);
        TermStore termStore = manager.TermStore;
        SPQuery query = new SPQuery {
          Query = Caml.Equals("IdForTermStore", termStore.Id.ToString()).ToString(),
          ViewFields = Caml.ViewFields("IdForTerm").ToString()
        };
        foreach (SPListItem item in taxonomyHiddenList.GetItems(query)) {
          if (termStore.GetTerm(new Guid((string)item["IdForTerm"])) == null) {
            deletedTerms.Add(item.ID);
          }
        }
      }
      return deletedTerms;
    }
  }
}
