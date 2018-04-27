using System;
using System.Reflection;

namespace Codeless.SharePoint.ObjectModel.Linq {
  internal class SPModelQueryExpressionScope {
    public delegate CamlExpression ExpressionGenerator(CamlParameterBindingFieldRef fieldRef, ICamlParameterBinding parameter);

    private SPModelQueryExpressionVisitor visitor;
    private CamlExpression expression;

    public SPModelQueryExpressionScope(SPModelQueryExpressionVisitor visitor) {
      CommonHelper.ConfirmNotNull(visitor, "visitor");
      this.visitor = visitor;
    }

    public MemberInfo Member { get; set; }
    public Type MemberType { get; set; }
    public SPModelFieldAssociationCollection FieldAssociations { get; set; }
    public SPModelQueryFieldInfo Field { get; set; }
    public string ParameterName { get; set; }

    public CamlExpression Expression {
      get {
        if (expression != null) {
          return expression;
        }
        if (this.MemberType == typeof(bool) && this.ParameterName == null) {
          return GetExpression(s => Caml.Equals(s.FieldRef, true));
        }
        return null;
      }
      set {
        expression = value;
      }
    }

    public void Reset() {
      this.Expression = null;
      this.Field = default(SPModelQueryFieldInfo);
      this.FieldAssociations = null;
      this.Member = null;
      this.MemberType = null;
      this.ParameterName = null;
    }

    public void CopyTo(SPModelQueryExpressionScope other) {
      CommonHelper.ConfirmNotNull(other, "other");
      other.Expression = this.Expression;
      other.FieldAssociations = this.FieldAssociations;
      other.Field = this.Field;
      other.Member = this.Member;
      other.MemberType = this.MemberType;
      other.ParameterName = this.ParameterName;
    }

    public CamlExpression GetExpression(ExpressionGenerator expressionFactory) {
      return GetExpression(s => expressionFactory(s.FieldRef, GetValueBinding(s)));
    }

    public CamlExpression GetExpression(ExpressionGenerator expressionFactory, bool checkOrderable) {
      return GetExpression(s => expressionFactory(s.FieldRef, GetValueBinding(s)), true);
    }

    public CamlExpression GetExpression(Func<SPModelQueryFieldInfo, CamlExpression> expressionFactory) {
      return GetExpression(expressionFactory, false);
    }

    public CamlExpression GetExpression(Func<SPModelQueryFieldInfo, CamlExpression> expressionFactory, bool checkOrderable) {
      CommonHelper.AccessNotNull(expressionFactory, "expressionFactory");

      if (this.FieldAssociations == null) {
        CommonHelper.AccessNotNull(this.Field.FieldRef, "FieldRef");
        return expressionFactory(this.Field);
      }
      if (!this.FieldAssociations.Queryable) {
        throw new Exception(String.Format("Member '{0}' must have exactly one SPFieldAttribute with IncludeInQuery set to true", this.Member.Name));
      }
      if (this.FieldAssociations.Fields.Count > 1 && checkOrderable) {
        throw new Exception(String.Format("Member '{0}' cannot be used in ordering", this.Member.Name));
      }
      CamlExpression expression = Caml.False;
      foreach (SPModelFieldAssociation association in this.FieldAssociations) {
        SPModelQueryFieldInfo fieldInfo = new SPModelQueryFieldInfo(visitor.Manager.Site, association);
        if (this.FieldAssociations.Fields.Count == 1) {
          return expressionFactory(fieldInfo);
        }
        expression |= (association.Descriptor.GetContentTypeExpression(visitor.Manager.Descriptor) + expressionFactory(fieldInfo));
      }
      return expression;
    }

    public ICamlParameterBinding GetValueBinding(SPModelQueryFieldInfo s) {
      return CamlParameterBinding.GetValueBinding(visitor.Manager.Site, s.FieldType, s.FieldTypeAsString, s.IncludeTimeValue, this.MemberType, new CamlParameterName(this.ParameterName), s.QueryProperty);
    }
  }
}
