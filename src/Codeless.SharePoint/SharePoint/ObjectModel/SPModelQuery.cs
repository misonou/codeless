using Codeless.SharePoint.Internal;
using Codeless.SharePoint.ObjectModel.Linq;
using IQToolkit;
using Microsoft.Office.Server.Search.Query;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Taxonomy;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Codeless.SharePoint.ObjectModel {
  internal class SPModelQuery {
    private static readonly List<ISPModelQueryFilter> filters = new List<ISPModelQueryFilter>() {
      new SPModelParameterizedQueryExpressionFilter(),
      new SPListQueryExpressionFilter(),
      new TaxonomyNullEqualityExpressionFilter()
    };

    private ISPModelManagerInternal manager;
    private SPModelDescriptor descriptor;
    private CamlExpression originalExpression;
    private CamlExpression expression;
    private CamlExpression expressionForCTFilter;
    private ReadOnlyCollection<string> selectProperties;
    private ReadOnlyCollection<string> selectPropertiesForSiteQuery;

    protected SPModelQuery() { }

    public SPModelQuery(ISPModelManagerInternal manager)
      : this(CommonHelper.ConfirmNotNull(manager, "manager"), manager.Descriptor.ModelType, Caml.Empty, 0, 0) { }

    public SPModelQuery(ISPModelManagerInternal manager, Type modelType, CamlExpression expression, int limit, int startRow) {
      CommonHelper.ConfirmNotNull(manager, "manager");
      CommonHelper.ConfirmNotNull(modelType, "modelType");
      this.Manager = manager;
      this.Descriptor = SPModelDescriptor.Resolve(modelType);
      this.ContentTypeFilterExpression = manager.Descriptor.GetContentTypeExpression(this.descriptor);
      this.Expression = expression;
      this.Offset = startRow;
      this.Limit = limit;
    }

    public ISPModelManagerInternal Manager {
      get {
        return manager;
      }
      protected set {
        if (manager != null) {
          throw new InvalidOperationException();
        }
        manager = value;
        expression = null;
      }
    }

    public SPModelDescriptor Descriptor {
      get {
        return descriptor;
      }
      protected set {
        if (descriptor != null) {
          throw new InvalidOperationException();
        }
        descriptor = value;
        expression = null;
        selectProperties = null;
      }
    }

    public CamlExpression Expression {
      get {
        if (expression == null) {
          expression = (originalExpression ?? Caml.Empty) + ContentTypeFilterExpression;
        }
        return expression;
      }
      set {
        originalExpression = value;
        expression = null;
      }
    }

    public CamlExpression ContentTypeFilterExpression {
      get {
        return expressionForCTFilter;
      }
      protected set {
        expressionForCTFilter = value;
        expression = null;
      }
    }

    public virtual ReadOnlyCollection<string> SelectProperties {
      get {
        if (selectProperties == null) {
          selectProperties = GetAllSelectProperties(descriptor);
        }
        return selectProperties;
      }
      set {
        selectProperties = value;
        selectPropertiesForSiteQuery = null;
      }
    }

    public virtual ReadOnlyCollection<string> SelectPropertiesForSiteQuery {
      get {
        if (selectPropertiesForSiteQuery == null) {
          selectPropertiesForSiteQuery = GetSiteQueryProperties();
        }
        return selectPropertiesForSiteQuery;
      }
    }

    public virtual int Offset { get; set; }
    public virtual int Limit { get; set; }
    public virtual bool ForceKeywordSearch { get; set; }
    public virtual string[] Keywords { get; set; }
    public virtual SearchRefiner[] Refiners { get; set; }
    public virtual KeywordInclusion KeywordInclusion { get; set; }

    protected SPModelQuery ApplyFilters() {
      CamlExpression expression = originalExpression;
      if (expression == Caml.False || expressionForCTFilter == Caml.False) {
        return this;
      }
      foreach (ISPModelQueryFilter filter in filters) {
        if (filter.ShouldTransformExpression(this)) {
          ISPModelQueryFilter clone = (ISPModelQueryFilter)filter.Clone();
          expression = clone.TransformExpression(this, expression);
          if (expression == Caml.False) {
            break;
          }
        }
      }
      if (expression != this.Expression) {
        SPModelQuery result = (SPModelQuery)MemberwiseClone();
        result.originalExpression = expression;
        result.expression = null;
        return result;
      }
      return this;
    }

    private ReadOnlyCollection<string> GetSiteQueryProperties() {
      Hashtable bindings = CamlExpression.EmptyBindings;
      if (this.Expression.Type == CamlExpressionType.Binded) {
        bindings = ((CamlBindedExpression)this.Expression).Bindings;
      }
      HashSet<string> viewFields = new HashSet<string>(this.SelectProperties);
      foreach (CamlFieldRefExpression f in ((CamlViewFieldsExpression)this.Expression.GetViewFieldsExpression()).Expressions) {
        viewFields.Add(f.FieldName.Bind(bindings));
      }
      return new ReadOnlyCollection<string>(viewFields.ToArray());
    }

    private static ReadOnlyCollection<string> GetAllSelectProperties(SPModelDescriptor descriptor) {
      List<string> viewFields = new List<string>();
      viewFields.AddRange(descriptor.RequiredViewFields);
      viewFields.AddRange(SPModel.RequiredViewFields);
      return new ReadOnlyCollection<string>(viewFields);
    }
  }
}
