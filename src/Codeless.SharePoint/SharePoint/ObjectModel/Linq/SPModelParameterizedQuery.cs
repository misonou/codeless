using Codeless.SharePoint.Internal;
using IQToolkit;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;

namespace Codeless.SharePoint.ObjectModel.Linq {
  internal enum SPModelQueryExecuteMode {
    Select,
    First,
    FirstOrDefault,
    Single,
    SingleOrDefault,
    Count,
    Any
  }

  internal class SPModelParameterizedQuery : SPModelQuery {
    public delegate object ParameterEvaluator(object[] args);
    public delegate object ResultEvaluator(IEnumerable res, object[] args);

    public const int PIndexLimit = 0;
    public const int PIndexOffset = 1;
    public const int PIndexMax = 2;

    private static readonly MethodInfo mOfType = typeof(SPModelParameterizedQuery).GetMethod("OfType", true);
    private static readonly ConcurrentFactory<ParameterizedExpression, SPModelParameterizedQuery> cache = new ConcurrentFactory<ParameterizedExpression, SPModelParameterizedQuery>();
    private static readonly ReadOnlyCollection<string> emptyCollection = new ReadOnlyCollection<string>(new string[0]);

    private readonly ParameterizedExpression expression;
    private readonly SPModelQueryExecuteMode executeMode;
    private readonly ReadOnlyDictionary<string, ParameterEvaluator> evaluators;
    private readonly ResultEvaluator projector;
    private readonly IEnumerable emptyArray;
    private readonly string[] parameterNames = new string[PIndexMax];
    private object[] args;

    private SPModelParameterizedQuery(ParameterizedExpression expression, ISPModelManagerInternal manager) {
      SPModelQueryBuilder builder = SPModelQueryExpressionVisitor.Translate(expression, manager);
      this.Descriptor = builder.ModelType != null ? SPModelDescriptor.Resolve(builder.ModelType) : manager.Descriptor;
      this.Expression = builder.Expression;
      this.TaxonomyFields = new ReadOnlyCollection<string>(builder.TaxonomyFields.ToArray());

      this.expression = expression;
      this.executeMode = builder.ExecuteMode;
      this.emptyArray = Array.CreateInstance(this.Descriptor.ModelType, 0);
      if (builder.SelectExpression != null) {
        this.projector = ((Expression<ResultEvaluator>)builder.SelectExpression).Compile();
      } else {
        this.projector = (ResultEvaluator)Delegate.CreateDelegate(typeof(ResultEvaluator), mOfType.MakeGenericMethod(this.Descriptor.ModelType));
      }
      this.evaluators = new ReadOnlyDictionary<string, ParameterEvaluator>(builder.ParameterEvaluators);
      for (int i = 0; i < PIndexMax; i++) {
        parameterNames[i] = (string)builder.Parameters[i];
      }

      if (builder.ModelType != null) {
        this.ContentTypeFilterExpression = builder.ContentTypeIds.Aggregate(Caml.False, (v, a) => v | Caml.OfContentType(a));
      }
      if (!builder.SelectAllProperties) {
        List<string> properties = new List<string>(builder.SelectProperties);
        if (!properties.Contains(SPBuiltInFieldName.ContentTypeId)) {
          properties.Add(SPBuiltInFieldName.ContentTypeId);
        }
        this.SelectProperties = new ReadOnlyCollection<string>(properties);
      }
    }

    public static SPModelParameterizedQuery Create(Expression expression, ISPModelManagerInternal manager) {
      CommonHelper.ConfirmNotNull(expression, "expression");
      CommonHelper.ConfirmNotNull(manager, "manager");

      object[] args;
      ParameterizedExpression pq = ParameterizedExpression.Create(expression, out args);
      SPModelParameterizedQuery cached = cache.GetInstance(pq, p => new SPModelParameterizedQuery(pq, manager));
      return cached.BindParameters(args, manager);
    }

    public override ReadOnlyCollection<string> SelectProperties {
      get {
        if (executeMode == SPModelQueryExecuteMode.Count || executeMode == SPModelQueryExecuteMode.Any) {
          return emptyCollection;
        }
        return base.SelectProperties;
      }
      set {
        base.SelectProperties = value;
      }
    }

    public override int Limit {
      get {
        switch (executeMode) {
          case SPModelQueryExecuteMode.Any:
          case SPModelQueryExecuteMode.First:
          case SPModelQueryExecuteMode.FirstOrDefault:
            return 1;
          case SPModelQueryExecuteMode.Single:
          case SPModelQueryExecuteMode.SingleOrDefault:
            return 2;
        }
        return base.Limit;
      }
      set {
        base.Limit = value;
      }
    }

    public ReadOnlyCollection<string> TaxonomyFields { get; private set; }

    public object Execute() {
      if (this.Manager == null) {
        throw new InvalidOperationException();
      }
      if (this.Manager.ImplicitQueryMode == SPModelImplicitQueryMode.None) {
        return projector(emptyArray, args);
      }
      SPModelQuery query = ApplyFilters();
      if (query.Expression == Caml.False) {
        return projector(emptyArray, args);
      }
      if (executeMode == SPModelQueryExecuteMode.Count) {
        return this.Manager.GetCount(query);
      }
      SPModelCollection collection = this.Manager.GetItems(query);
      return projector(collection, args);
    }

    private SPModelParameterizedQuery BindParameters(object[] args, ISPModelManagerInternal manager) {
      CamlParameterBindingHashtable hashtable = new CamlParameterBindingHashtable(manager);
      for (int i = 0, count = args.Length; i < count; i++) {
        hashtable[expression.Parameters[i].Name] = args[i];
      }
      foreach (KeyValuePair<string, ParameterEvaluator> item in evaluators) {
        hashtable[item.Key] = item.Value(args);
      }

      SPModelParameterizedQuery other = (SPModelParameterizedQuery)MemberwiseClone();
      other.Expression = other.Expression.Bind(hashtable);
      other.ContentTypeFilterExpression = null;
      other.args = args;
      other.Manager = manager;
      if (parameterNames[PIndexOffset] != null) {
        other.Offset = Convert.ToInt32(hashtable[parameterNames[PIndexOffset]]);
      }
      if (parameterNames[PIndexLimit] != null) {
        other.Limit = Convert.ToInt32(hashtable[parameterNames[PIndexLimit]]);
      }
      return other;
    }

    private static object OfType<T>(IEnumerable result, object[] args) {
      return result.OfType<T>();
    }
  }
}
