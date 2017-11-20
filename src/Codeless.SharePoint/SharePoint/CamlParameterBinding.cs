using Codeless.SharePoint.ObjectModel;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Taxonomy;
using Microsoft.SharePoint.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Codeless.SharePoint {
  #region Exception
  internal sealed class CamlParameterBindingNotFoundException : CamlException {
    public CamlParameterBindingNotFoundException(string parameterName) :
      base(String.Concat("Missing parameter ", parameterName, ".")) { }
  }

  internal sealed class CamlParameterBindingEmptyCollectionException : CamlException {
    public CamlParameterBindingEmptyCollectionException(string parameterName) :
      base(String.Concat("Collection bound for parameter ", parameterName, " is empty.")) { }
  }

  internal sealed class CamlParameterBindingNullException : CamlException {
    public CamlParameterBindingNullException(string parameterName)
      : base(String.Concat("Parameter ", parameterName, " cannot be null.")) { }
  }

  internal sealed class CamlParameterBindingMultipleValuesException : CamlException {
    public CamlParameterBindingMultipleValuesException(string parameterName)
      : base(String.Concat("Parameter ", parameterName, " has multiple values.")) { }
  }
  #endregion

  public delegate object CamlParameterValueBinder(object obj, Hashtable bindings);

  internal class CamlParameterBinding {
    private static readonly ParameterExpression p1 = Expression.Parameter(typeof(object), "p1");
    private static readonly ParameterExpression p2 = Expression.Parameter(typeof(Hashtable), "p2");
    private static readonly ConcurrentFactory<PropertyInfo, CamlParameterValueBinder> propertyBinders = new ConcurrentFactory<PropertyInfo, CamlParameterValueBinder>();
    private static readonly ConcurrentFactory<Type, CamlParameterValueBinder> enumBinder = new ConcurrentFactory<Type, CamlParameterValueBinder>();
    private static readonly CamlParameterValueBinder bindSPPrincipalID = BindSPPrincipalID;
    private static readonly CamlParameterValueBinder bindSPListItemID = BindSPPrincipalID;
    private static readonly CamlParameterValueBinder bindSPModelID = BindSPPrincipalID;
    private static readonly CamlParameterValueBinder bindTermWssIdFromGuid = BindTermWssIdFromGuid;
    private static readonly CamlParameterValueBinder bindTermWssId = BindTermWssId;

    public static ICamlParameterBinding GetValueBinding(SPSite parentSite, SPField field, object value) {
      bool includeTimeValue = (field.Type == SPFieldType.DateTime && ((SPFieldDateTime)field).DisplayFormat == SPDateTimeFieldFormatType.DateTime);
      return GetValueBinding(parentSite, field.Type, field.TypeAsString, includeTimeValue, typeof(object), value);
    }

    public static ICamlParameterBinding GetValueBinding(SPSite parentSite, SPFieldType fieldType, string fieldTypeAsString, bool includeTimeValue, Type memberType, object value) {
      try {
        Type valueType = value.GetType();
        Type enumeratedType = valueType.GetEnumeratedType();

        switch (fieldType) {
          case SPFieldType.Boolean:
            return new CamlParameterBindingBoolean(Convert<bool>(value));
          case SPFieldType.DateTime:
            if (enumeratedType != null) {
              return new CamlParameterBindingDateTime(ResolveValueCollection(value, Convert<DateTime>), includeTimeValue);
            }
            return new CamlParameterBindingDateTime(Convert<DateTime>(value), includeTimeValue);
          case SPFieldType.Guid:
            if (enumeratedType != null) {
              return new CamlParameterBindingGuid(ResolveValueCollection(value, Convert<Guid>));
            }
            return new CamlParameterBindingGuid(Convert<Guid>(value));
          case SPFieldType.Counter:
          case SPFieldType.Integer:
            if (enumeratedType != null) {
              return new CamlParameterBindingInteger(ResolveValueCollection(value, Convert<int>));
            }
            return new CamlParameterBindingInteger(Convert<int>(value));
          case SPFieldType.Currency:
          case SPFieldType.Number:
            if (enumeratedType != null) {
              return new CamlParameterBindingNumber(ResolveValueCollection(value, Convert<double>));
            }
            return new CamlParameterBindingNumber(Convert<double>(value));
          case SPFieldType.Lookup:
          case SPFieldType.User:
            if (enumeratedType != null) {
              if (enumeratedType.IsOf<SPModel>()) {
                return new CamlParameterBindingLookup(((IEnumerable)value).OfType<SPModel>().Select(v => v.Adapter.ListItemId));
              }
              if (enumeratedType.IsOf<int>()) {
                return new CamlParameterBindingLookup(((IEnumerable)value).OfType<int>());
              }
              if (enumeratedType.IsOf<SPPrincipal>()) {
                return new CamlParameterBindingLookup(((IEnumerable)value).OfType<SPPrincipal>());
              }
              if (enumeratedType.IsOf<SPListItem>()) {
                return new CamlParameterBindingLookup(((IEnumerable)value).OfType<SPListItem>());
              }
              if (enumeratedType.IsOf<Guid>()) {
                return new CamlParameterBindingGuid(((IEnumerable)value).OfType<Guid>());
              }
            }
            if (valueType.IsOf<SPModel>()) {
              return new CamlParameterBindingLookup(((SPModel)value).Adapter.ListItemId);
            }
            if (valueType.IsOf<int>()) {
              return new CamlParameterBindingLookup((int)value);
            }
            if (valueType.IsOf<SPPrincipal>()) {
              return new CamlParameterBindingLookup((SPPrincipal)value);
            }
            if (valueType.IsOf<SPListItem>()) {
              return new CamlParameterBindingLookup((SPListItem)value);
            }
            if (valueType.IsOf<Guid>()) {
              return new CamlParameterBindingGuid((Guid)value);
            }
            break;
          case SPFieldType.URL:
            if (enumeratedType != null) {
              return new CamlParameterBindingUrl(ResolveValueCollection(value, ConvertToString));
            }
            return new CamlParameterBindingUrl(ConvertToString(value));
          case SPFieldType.Choice:
            if (memberType.IsEnum) {
              return new CamlParameterBindingString(Enum.GetName(memberType, Convert<int>(value)));
            }
            break;
          case SPFieldType.ModStat:
            if (enumeratedType != null) {
              return new CamlParameterBindingModStat(ResolveValueCollection(value, ConvertToEnum<SPModerationStatusType>));
            }
            return new CamlParameterBindingModStat(ConvertToEnum<SPModerationStatusType>(value));
        }
        switch (fieldTypeAsString) {
          case "TaxonomyFieldType":
          case "TaxonomyFieldTypeMulti":
            if (enumeratedType != null) {
              if (enumeratedType.IsOf<int>()) {
                return new CamlParameterBindingLookup(((IEnumerable)value).OfType<int>());
              }
              if (enumeratedType.IsOf<Guid>()) {
                TaxonomySession session = new TaxonomySession(parentSite);
                int[] wssIds = ((IEnumerable)value).OfType<Guid>().SelectMany(v => session.GetTerm(v) == null ? new int[0] : session.GetTerm(v).GetWssIds(parentSite, true)).ToArray();
                if (wssIds.Length > 0) {
                  return new CamlParameterBindingLookup(wssIds);
                }
                return new CamlParameterBindingLookup(0);
              }
              if (enumeratedType.IsOf<Term>()) {
                int[] wssIds = ((IEnumerable)value).OfType<Term>().SelectMany(v => v.GetWssIds(parentSite, true)).ToArray();
                if (wssIds.Length > 0) {
                  return new CamlParameterBindingLookup(wssIds);
                }
                return new CamlParameterBindingLookup(0);
              }
            }
            if (valueType.IsOf<int>()) {
              return new CamlParameterBindingLookup((int)value);
            }
            if (valueType.IsOf<Guid>()) {
              TaxonomySession session = new TaxonomySession(parentSite);
              Term term = session.GetTerm((Guid)value);
              if (term != null) {
                IList<int> wssIds = term.GetWssIds(parentSite, true);
                if (wssIds.Count > 0) {
                  return new CamlParameterBindingLookup(wssIds);
                }
              }
              return new CamlParameterBindingLookup(0);
            }
            if (valueType.IsOf<Term>()) {
              IList<int> wssIds = ((Term)value).GetWssIds(parentSite, true);
              if (wssIds.Count > 0) {
                return new CamlParameterBindingLookup(wssIds);
              }
              return new CamlParameterBindingLookup(0);
            }
            break;
        }
        if (enumeratedType != null) {
          return new CamlParameterBindingString(ResolveValueCollection(value, ConvertToString));
        }
        return new CamlParameterBindingString(ConvertToString(value));
      } catch (InvalidCastException) {
        throw new ArgumentException(String.Format("Supplied value cannot be converted to binding type '{0}'", fieldTypeAsString), "value");
      }
    }

    public static ICamlParameterBinding GetValueBinding(SPSite parentSite, SPFieldType fieldType, string fieldTypeAsString, bool includeTimeValue, Type memberType, CamlParameterName parameterName, PropertyInfo property) {
      Type valueType = memberType.GetEnumeratedType() ?? memberType;
      CamlParameterValueBinder binder = null;
      if (property != null) {
        binder = propertyBinders.EnsureKeyValue(property, CreateBinderFromPropertyInfo);
      }
      switch (fieldType) {
        case SPFieldType.Boolean:
          return new CamlParameterBindingBoolean(parameterName, binder);
        case SPFieldType.DateTime:
          return new CamlParameterBindingDateTime(parameterName, binder, includeTimeValue);
        case SPFieldType.Guid:
          return new CamlParameterBindingGuid(parameterName, binder);
        case SPFieldType.Counter:
        case SPFieldType.Integer:
          return new CamlParameterBindingInteger(parameterName, binder);
        case SPFieldType.Currency:
        case SPFieldType.Number:
          return new CamlParameterBindingNumber(parameterName, binder);
        case SPFieldType.Lookup:
        case SPFieldType.User:
          if (valueType.IsOf<SPModel>()) {
            return new CamlParameterBindingLookup(parameterName, bindSPModelID);
          }
          if (valueType.IsOf<int>()) {
            return new CamlParameterBindingLookup(parameterName, binder);
          }
          if (valueType.IsOf<SPPrincipal>()) {
            return new CamlParameterBindingLookup(parameterName, bindSPPrincipalID);
          }
          if (valueType.IsOf<SPListItem>()) {
            return new CamlParameterBindingLookup(parameterName, bindSPListItemID);
          }
          if (valueType.IsOf<Guid>()) {
            return new CamlParameterBindingGuid(parameterName, binder);
          }
          break;
        case SPFieldType.URL:
          return new CamlParameterBindingUrl(parameterName, binder);
        case SPFieldType.Choice:
          if (memberType.IsEnum) {
            return new CamlParameterBindingString(parameterName, enumBinder.EnsureKeyValue(memberType, CreateBinderFromEnumType));
          }
          break;
        case SPFieldType.ModStat:
          return new CamlParameterBindingModStat(parameterName, binder);
      }
      switch (fieldTypeAsString) {
        case "TaxonomyFieldType":
        case "TaxonomyFieldTypeMulti":
          if (valueType.IsOf<int>()) {
            return new CamlParameterBindingLookup(parameterName, binder);
          }
          if (valueType.IsOf<Guid>()) {
            return new CamlParameterBindingLookup(parameterName, bindTermWssIdFromGuid);
          }
          if (valueType.IsOf<Term>()) {
            return new CamlParameterBindingLookup(parameterName, bindTermWssId);
          }
          break;
      }
      return new CamlParameterBindingString(parameterName, binder);
    }

    public static T Convert<T>(object value) {
      if (value == null) {
        return default(T);
      }
      if (value is T) {
        return (T)value;
      }
      return (T)System.Convert.ChangeType(value, typeof(T));
    }

    public static T ConvertToEnum<T>(object value) {
      if (value == null) {
        return default(T);
      }
      if (value is string) {
        return (T)Enum.Parse(typeof(T), value.ToString());
      }
      return (T)value;
    }

    public static string ConvertToString(object value) {
      if (value == null) {
        return String.Empty;
      }
      if (value is Guid) {
        return ((Guid)value).ToString("B");
      }
      return value.ToString();
    }

    private static IEnumerable<T> ResolveValueCollection<T>(object value, Func<object, T> converter) {
      if (value != null) {
        if (value.GetType() != typeof(string)) {
          IEnumerable enumerable = value as IEnumerable;
          if (enumerable != null) {
            return enumerable.OfType<object>().Select(converter);
          }
        }
        return new[] { converter(value) };
      }
      return Enumerable.Empty<T>();
    }

    private static object BindSPPrincipalID(object obj, Hashtable bindings) {
      return ((SPPrincipal)obj).ID;
    }

    private static object BindSPListItemID(object obj, Hashtable bindings) {
      return ((SPListItem)obj).ID;
    }

    private static object BindSPModelID(object obj, Hashtable bindings) {
      return ((SPModel)obj).Adapter.ListItemId;
    }

    private static object BindTermWssIdFromGuid(object obj, Hashtable bindings) {
      ISPObjectContext context = bindings as ISPObjectContext;
      if (context == null) {
        throw new InvalidOperationException();
      }
      Term t = context.TermStore.GetTerm((Guid)obj);
      return t == null ? new int[0] : t.GetWssIds(context.Site, true);
    }

    private static object BindTermWssId(object obj, Hashtable bindings) {
      ISPObjectContext context = bindings as ISPObjectContext;
      if (context == null) {
        throw new InvalidOperationException();
      }
      return ((Term)obj).GetWssIds(context.Site, true);
    }

    private static CamlParameterValueBinder CreateBinderFromPropertyInfo(PropertyInfo selector) {
      Expression body = Expression.Convert(Expression.Property(Expression.Convert(p1, selector.DeclaringType), selector), typeof(object));
      return Expression.Lambda<CamlParameterValueBinder>(body, p1, p2).Compile();
    }

    private static CamlParameterValueBinder CreateBinderFromEnumType(Type enumType) {
      Expression body = Expression.Convert(Expression.Call(typeof(Enum).GetMethod("GetName"), Expression.Constant(enumType), p1), typeof(object));
      return Expression.Lambda<CamlParameterValueBinder>(body, p1, p2).Compile();
    }
  }

  /// <summary>
  /// Provides a base class representating a parameter in a CAML expression.
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public abstract class CamlParameterBinding<T> : ICamlParameterBinding {
    private static readonly Func<object, T> converter;
    private readonly CamlParameterValueBinder binder;
    private readonly IList<T> values;

    static CamlParameterBinding() {
      if (typeof(T) == typeof(string)) {
        converter = v => (T)(object)CamlParameterBinding.ConvertToString(v);
      } else if (typeof(T).IsEnum) {
        converter = CamlParameterBinding.ConvertToEnum<T>;
      } else {
        converter = CamlParameterBinding.Convert<T>;
      }
    }

    public CamlParameterBinding(T value) {
      CommonHelper.ConfirmNotNull(value, "value");
      this.values = new[] { value };
    }

    public CamlParameterBinding(IEnumerable<T> value) {
      CommonHelper.ConfirmNotNull(value, "value");
      this.values = value.ToArray();
      if (values.Count == 0) {
        throw new ArgumentException("value", "Collection is empty");
      }
    }

    internal CamlParameterBinding(CamlParameterName parameterName) {
      this.ParameterName = parameterName;
    }

    internal CamlParameterBinding(CamlParameterName parameterName, CamlParameterValueBinder binder) {
      this.ParameterName = parameterName;
      this.binder = binder;
    }

    public bool IsParameter {
      get { return ParameterName != CamlParameterName.NoBinding; }
    }

    public CamlParameterName ParameterName { get; private set; }

    public virtual CamlValueType ValueType {
      get { return CamlValueType.Text; }
    }

    public virtual string Bind(Hashtable bindings) {
      CommonHelper.ConfirmNotNull(bindings, "bindings");
      IEnumerator<T> values = EnumerateValues(bindings).GetEnumerator();
      T value;
      if (!values.MoveNext()) {
        throw new CamlParameterBindingNotFoundException(ParameterName);
      }
      value = values.Current;
      if (values.MoveNext()) {
        throw new CamlParameterBindingMultipleValuesException(ParameterName);
      }
      return Format(value);
    }

    public virtual IEnumerable<string> BindCollection(Hashtable bindings) {
      CommonHelper.ConfirmNotNull(bindings, "bindings");
      IEnumerator<T> values = EnumerateValues(bindings).GetEnumerator();
      if (!values.MoveNext()) {
        throw new CamlParameterBindingEmptyCollectionException(ParameterName);
      }
      do {
        yield return Format(values.Current);
      } while (values.MoveNext());
    }

    protected virtual string Format(T value) {
      return value.ToString();
    }

    private IEnumerable<T> EnumerateValues(Hashtable bindings) {
      if (values != null) {
        return values;
      }
      object bindingValue = bindings[ParameterName.Value];
      if (bindingValue == null) {
        if (!bindings.ContainsKey(ParameterName.Value)) {
          throw new CamlParameterBindingNotFoundException(ParameterName);
        }
        throw new CamlParameterBindingNullException(ParameterName);
      }
      if (bindingValue.GetType() != typeof(string)) {
        IEnumerable enumerable = bindingValue as IEnumerable;
        if (enumerable != null) {
          return enumerable.OfType<object>().Select(v => ConvertValue(v, bindings));
        }
      }
      return new[] { ConvertValue(bindingValue, bindings) };
    }

    private T ConvertValue(object value, Hashtable bindings) {
      if (binder != null) {
        value = binder(value, bindings);
      }
      return converter(value);
    }
  }

  public sealed class CamlParameterBindingFieldRef : CamlParameterBinding<string> {
    private CamlParameterBindingFieldRef(string value)
      : base(value) { }

    private CamlParameterBindingFieldRef(CamlParameterName parameterName)
      : base(parameterName) { }

    public override bool Equals(object obj) {
      if (obj is CamlParameterBindingFieldRef) {
        CamlParameterBindingFieldRef other = (CamlParameterBindingFieldRef)obj;
        if (this.ParameterName != CamlParameterName.NoBinding) {
          return this.ParameterName.Value.Equals(other.ParameterName.Value);
        }
        return other.ParameterName == CamlParameterName.NoBinding && Bind(CamlExpression.EmptyBindings) == other.Bind(CamlExpression.EmptyBindings);
      }
      return base.Equals(obj);
    }

    public override int GetHashCode() {
      if (ParameterName != CamlParameterName.NoBinding) {
        return ParameterName.Value.GetHashCode();
      }
      return Bind(CamlExpression.EmptyBindings).GetHashCode();
    }

    public static implicit operator CamlParameterBindingFieldRef(string value) {
      try {
        return new CamlParameterBindingFieldRef(value);
      } catch (ArgumentNullException) {
        throw new InvalidCastException("Cannot cast NULL to CamlParameterBindingFieldRef");
      }
    }

    public static implicit operator CamlParameterBindingFieldRef(CamlParameterName parameterName) {
      return new CamlParameterBindingFieldRef(parameterName);
    }
  }

  public sealed class CamlParameterBindingOrder : CamlParameterBinding<CamlOrder> {
    private CamlParameterBindingOrder(CamlOrder value)
      : base(value) { }

    private CamlParameterBindingOrder(CamlParameterName parameterName)
      : base(parameterName) { }

    protected override string Format(CamlOrder value) {
      if (value == CamlOrder.Ascending) {
        return Caml.BooleanString.True;
      }
      return Caml.BooleanString.False;
    }

    public static implicit operator CamlParameterBindingOrder(CamlOrder value) {
      return new CamlParameterBindingOrder(value);
    }

    public static implicit operator CamlParameterBindingOrder(CamlParameterName parameterName) {
      return new CamlParameterBindingOrder(parameterName);
    }
  }

  public sealed class CamlParameterBindingBooleanString : CamlParameterBinding<bool> {
    internal CamlParameterBindingBooleanString(bool value)
      : base(value) { }

    internal CamlParameterBindingBooleanString(CamlParameterName parameterName)
      : base(parameterName) { }

    public override CamlValueType ValueType {
      get { return CamlValueType.Boolean; }
    }

    protected override string Format(bool value) {
      return value ? Caml.BooleanString.True : Caml.BooleanString.False;
    }
  }

  #region Internal Implementation
  internal class CamlParameterBindingString : CamlParameterBinding<string> {
    public CamlParameterBindingString(string value)
      : base(value) {
      CommonHelper.ConfirmNotNull(value, "value");
    }

    public CamlParameterBindingString(IEnumerable<string> value)
      : base(value) { }

    public CamlParameterBindingString(CamlParameterName parameterName)
      : base(parameterName) { }

    public CamlParameterBindingString(CamlParameterName parameterName, CamlParameterValueBinder binder)
      : base(parameterName, binder) { }
  }

  internal class CamlParameterBindingUrl : CamlParameterBinding<string> {
    public CamlParameterBindingUrl(string value)
      : base(value) {
      CommonHelper.ConfirmNotNull(value, "value");
    }

    public CamlParameterBindingUrl(IEnumerable<string> value)
      : base(value) { }

    public CamlParameterBindingUrl(CamlParameterName parameterName)
      : base(parameterName) { }

    public CamlParameterBindingUrl(CamlParameterName parameterName, CamlParameterValueBinder binder)
      : base(parameterName, binder) { }

    public override CamlValueType ValueType {
      get { return CamlValueType.URL; }
    }
  }

  internal class CamlParameterBindingBoolean : CamlParameterBinding<bool> {
    public CamlParameterBindingBoolean(bool value)
      : base(value) { }

    public CamlParameterBindingBoolean(CamlParameterName parameterName)
      : base(parameterName) { }

    public CamlParameterBindingBoolean(CamlParameterName parameterName, CamlParameterValueBinder binder)
      : base(parameterName, binder) { }

    public override CamlValueType ValueType {
      get { return CamlValueType.Integer; }
    }

    protected override string Format(bool value) {
      return value ? "1" : "0";
    }
  }

  internal class CamlParameterBindingLookup : CamlParameterBinding<int> {
    public CamlParameterBindingLookup(int value)
      : base(value) { }

    public CamlParameterBindingLookup(IEnumerable<int> value)
      : base(value) { }

    public CamlParameterBindingLookup(SPPrincipal value)
      : base(value.ID) { }

    public CamlParameterBindingLookup(IEnumerable<SPPrincipal> value)
      : base(value.Select(u => u.ID)) { }

    public CamlParameterBindingLookup(SPListItem value)
      : base(value.ID) { }

    public CamlParameterBindingLookup(IEnumerable<SPListItem> value)
      : base(value.Select(u => u.ID)) { }

    public CamlParameterBindingLookup(CamlParameterName parameterName)
      : base(parameterName) { }

    public CamlParameterBindingLookup(CamlParameterName parameterName, CamlParameterValueBinder binder)
      : base(parameterName, binder) { }

    public override CamlValueType ValueType {
      get { return CamlValueType.Lookup; }
    }
  }

  internal class CamlParameterBindingInteger : CamlParameterBinding<int> {
    public CamlParameterBindingInteger(int value)
      : base(value) { }

    public CamlParameterBindingInteger(IEnumerable<int> value)
      : base(value) { }

    public CamlParameterBindingInteger(CamlParameterName parameterName)
      : base(parameterName) { }

    public CamlParameterBindingInteger(CamlParameterName parameterName, CamlParameterValueBinder binder)
      : base(parameterName, binder) { }

    public override CamlValueType ValueType {
      get { return CamlValueType.Integer; }
    }
  }

  internal class CamlParameterBindingNumber : CamlParameterBinding<double> {
    public CamlParameterBindingNumber(double value)
      : base(value) { }

    public CamlParameterBindingNumber(IEnumerable<double> value)
      : base(value) { }

    public CamlParameterBindingNumber(CamlParameterName parameterName)
      : base(parameterName) { }

    public CamlParameterBindingNumber(CamlParameterName parameterName, CamlParameterValueBinder binder)
      : base(parameterName, binder) { }

    public override CamlValueType ValueType {
      get { return CamlValueType.Number; }
    }
  }

  internal class CamlParameterBindingGuid : CamlParameterBinding<Guid> {
    public CamlParameterBindingGuid(Guid value)
      : base(value) { }

    public CamlParameterBindingGuid(IEnumerable<Guid> value)
      : base(value) { }

    public CamlParameterBindingGuid(CamlParameterName parameterName)
      : base(parameterName) { }

    public CamlParameterBindingGuid(CamlParameterName parameterName, CamlParameterValueBinder binder)
      : base(parameterName, binder) { }

    public override CamlValueType ValueType {
      get { return CamlValueType.Guid; }
    }

    protected override string Format(Guid value) {
      return value.ToString("D");
    }
  }

  internal class CamlParameterBindingDateTime : CamlParameterBinding<DateTime> {
    public CamlParameterBindingDateTime(DateTime value, bool includeTimeValue)
      : base(value) {
      this.IncludeTimeValue = includeTimeValue;
    }

    public CamlParameterBindingDateTime(IEnumerable<DateTime> value, bool includeTimeValue)
      : base(value) {
      this.IncludeTimeValue = includeTimeValue;
    }

    public CamlParameterBindingDateTime(CamlParameterName parameterName, bool includeTimeValue)
      : base(parameterName) {
      this.IncludeTimeValue = includeTimeValue;
    }

    public CamlParameterBindingDateTime(CamlParameterName parameterName, CamlParameterValueBinder binder, bool includeTimeValue)
      : base(parameterName, binder) {
      this.IncludeTimeValue = includeTimeValue;
    }

    public bool IncludeTimeValue { get; private set; }

    public override CamlValueType ValueType {
      get { return CamlValueType.DateTime; }
    }

    protected override string Format(DateTime value) {
      if (this.IncludeTimeValue) {
        return SPUtility.CreateISO8601DateTimeFromSystemDateTime(value);
      }
      return SPUtility.CreateISO8601DateTimeFromSystemDateTime(value.Date);
    }
  }

  internal class CamlParameterBindingContentTypeId : CamlParameterBinding<SPContentTypeId> {
    public CamlParameterBindingContentTypeId(SPContentTypeId value)
      : base(value) { }

    public CamlParameterBindingContentTypeId(IEnumerable<SPContentTypeId> value)
      : base(value) { }

    public CamlParameterBindingContentTypeId(CamlParameterName parameterName)
      : base(parameterName) { }

    public CamlParameterBindingContentTypeId(CamlParameterName parameterName, CamlParameterValueBinder binder)
      : base(parameterName, binder) { }

    public override CamlValueType ValueType {
      get { return CamlValueType.ContentTypeId; }
    }
  }

  internal class CamlParameterBindingModStat : CamlParameterBinding<SPModerationStatusType> {
    public CamlParameterBindingModStat(SPModerationStatusType value)
      : base(value) { }

    public CamlParameterBindingModStat(IEnumerable<SPModerationStatusType> value)
      : base(value) { }

    public CamlParameterBindingModStat(CamlParameterName parameterName)
      : base(parameterName) { }

    public CamlParameterBindingModStat(CamlParameterName parameterName, CamlParameterValueBinder binder)
      : base(parameterName, binder) { }

    public override CamlValueType ValueType {
      get { return CamlValueType.ModStat; }
    }
  }
  #endregion
}
