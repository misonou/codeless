using Codeless.SharePoint.ObjectModel;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Taxonomy;
using Microsoft.SharePoint.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Codeless.SharePoint {
  internal sealed class CamlParameterBindingNotFoundException : CamlException {
    public CamlParameterBindingNotFoundException(string parameterName) :
      base(String.Concat("Missing parameter ", parameterName)) { }
  }

  internal sealed class CamlParameterBindingEmptyCollectionException : CamlException {
    public CamlParameterBindingEmptyCollectionException(string parameterName) :
      base(String.Concat("Collection bound for parameter ", parameterName, " is empty")) { }
  }

  internal sealed class CamlParameterBindingIncorrectTypeException : CamlException {
    public CamlParameterBindingIncorrectTypeException(string parameterName, Type expectedType, Type actualType)
      : base(String.Concat("Type bound for ", parameterName, " expected to be ", expectedType.ToString(), ", but given ", actualType.ToString())) { }
  }

  /// <summary>
  /// Indicates the name of a parameter which its value can be binded after.
  /// </summary>
  public struct CamlParameterName {
    internal static readonly CamlParameterName NoBinding = default(CamlParameterName);

    internal readonly string Value;

    /// <summary>
    /// Creates an instance of the <see cref="CamlParameterName"/> class.
    /// </summary>
    /// <param name="value"></param>
    public CamlParameterName(string value) {
      CommonHelper.ConfirmNotNull(value, "value");
      this.Value = value;
    }

    /// <summary>
    /// Implicitly converts the name of a parameter specified by this instance to a string representation.
    /// </summary>
    /// <param name="p"></param>
    /// <returns></returns>
    public static implicit operator string(CamlParameterName p) {
      return p.Value;
    }
  }

  /// <summary>
  /// Exposes properties and methods related to a parameter in a CAML expression.
  /// </summary>
  public interface ICamlParameterBinding {
    /// <summary>
    /// Gets a boolean value indicating whether this instance binds to any given arguments.
    /// </summary>
    bool IsParameter { get; }
    /// <summary>
    /// Gets the name of this parameter. <see cref="CamlParameterName.NoBinding"/> is returned if this instance does not bind to any given arguments.
    /// </summary>
    CamlParameterName ParameterName { get; }
    /// <summary>
    /// Gets the value type this parameter representing.
    /// </summary>
    CamlValueType ValueType { get; }
    /// <summary>
    /// Binds a single value and returns a string representation of the value from a collection of parameter values.
    /// </summary>
    /// <param name="bindings"></param>
    /// <returns></returns>
    string Bind(Hashtable bindings);
    /// <summary>
    /// Bings a list of values and returns a string representation of the values from a collection of parameter values.
    /// </summary>
    /// <param name="bindings"></param>
    /// <returns></returns>
    IEnumerable<string> BindCollection(Hashtable bindings);
  }

  internal class CamlParameterBinding {
    public static ICamlParameterBinding GetValueBinding(SPSite parentSite, SPField field, object value) {
      bool includeTimeValue = (field.Type == SPFieldType.DateTime && ((SPFieldDateTime)field).DisplayFormat == SPDateTimeFieldFormatType.DateTime);
      return GetValueBinding(parentSite, field.Type, field.TypeAsString, includeTimeValue, typeof(object), value);
    }

    public static ICamlParameterBinding GetValueBinding(SPSite parentSite, SPFieldType fieldType, string fieldTypeAsString, bool includeTimeValue, Type enumType, object value) {
      try {
        Type valueType = value.GetType();
        Type enumeratedType = valueType.GetEnumeratedType();

        switch (fieldType) {
          case SPFieldType.Boolean:
            return new CamlParameterBindingBoolean(ResolveValue<bool>(value));
          case SPFieldType.DateTime:
            if (enumeratedType != null) {
              return new CamlParameterBindingDateTime(ResolveValueCollection<DateTime>(value), includeTimeValue);
            }
            return new CamlParameterBindingDateTime(ResolveValue<DateTime>(value), includeTimeValue);
          case SPFieldType.Guid:
            if (enumeratedType != null) {
              return new CamlParameterBindingGuid(ResolveValueCollection<Guid>(value));
            }
            return new CamlParameterBindingGuid(ResolveValue<Guid>(value));
          case SPFieldType.Counter:
          case SPFieldType.Integer:
            if (enumeratedType != null) {
              return new CamlParameterBindingInteger(ResolveValueCollection<int>(value));
            }
            return new CamlParameterBindingInteger(ResolveValue<int>(value));
          case SPFieldType.Currency:
          case SPFieldType.Number:
            if (enumeratedType != null) {
              return new CamlParameterBindingNumber(ResolveValueCollection<double>(value));
            }
            return new CamlParameterBindingNumber(ResolveValue<double>(value));
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
              return new CamlParameterBindingUrl(ResolveValueCollection<string>(value));
            }
            return new CamlParameterBindingUrl(ResolveValue<string>(value));
          case SPFieldType.Choice:
            if (enumType.IsOf<Enum>()) {
              return new CamlParameterBindingString(Enum.GetName(enumType, ResolveValue<int>(value)));
            }
            break;
          case SPFieldType.ModStat:
            if (enumeratedType != null) {
              return new CamlParameterBindingModStat(ResolveValueCollection<SPModerationStatusType>(value));
            }
            return new CamlParameterBindingModStat(ResolveValue<SPModerationStatusType>(value));
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
          return new CamlParameterBindingString(ResolveValueCollection(value, ResolveValueAsString));
        }
        return new CamlParameterBindingString(ResolveValueAsString(value));
      } catch (InvalidCastException) {
        throw new ArgumentException(String.Format("Supplied value cannot be converted to binding type '{0}'", fieldTypeAsString), "value");
      }
    }

    private static T ResolveValue<T>(object value) {
      if (typeof(T).IsSubclassOf(typeof(Enum)) && Type.GetTypeCode(value.GetType()) != TypeCode.String) {
        return (T)value;
      }
      return (T)Convert.ChangeType(value, typeof(T));
    }

    private static string ResolveValueAsString(object value) {
      if (value == null) {
        return String.Empty;
      }
      if (value is Guid) {
        return ((Guid)value).ToString("B");
      }
      return value.ToString();
    }

    private static IEnumerable<T> ResolveValueCollection<T>(object value) {
      return ResolveValueCollection(value, ResolveValue<T>);
    }

    private static IEnumerable<T> ResolveValueCollection<T>(object value, Func<object, T> converter) {
      if (value != null) {
        IEnumerable enumerable = CommonHelper.TryCastOrDefault<IEnumerable>(value);
        if (enumerable != null) {
          return enumerable.OfType<object>().Select(converter);
        }
        try {
          T typedValue = converter(value);
          return new[] { typedValue };
        } catch { }
      }
      return Enumerable.Empty<T>();
    }
  }

  /// <summary>
  /// Provides a base class representating a parameter in a CAML expression.
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public abstract class CamlParameterBinding<T> : ICamlParameterBinding {
    protected readonly List<T> Values = new List<T>();

    public CamlParameterBinding(T value)
      : this(CamlParameterName.NoBinding, value) { }

    public CamlParameterBinding(IEnumerable<T> value)
      : this(CamlParameterName.NoBinding, value) { }

    public CamlParameterBinding(CamlParameterName parameterName) {
      ParameterName = parameterName;
    }

    public CamlParameterBinding(CamlParameterName parameterName, T value)
      : this(parameterName) {
      Values.Add(value);
    }

    public CamlParameterBinding(CamlParameterName parameterName, IEnumerable<T> value)
      : this(parameterName) {
      CommonHelper.ConfirmNotNull(value, "value");
      Values.AddRange(value);
      if (Values.Count == 0) {
        throw new ArgumentException("value", "Collection is empty");
      }
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
      T value = GetValuesFromBindingsOrDefault(bindings, false).First();
      return Format(value);
    }

    public virtual IEnumerable<string> BindCollection(Hashtable bindings) {
      CommonHelper.ConfirmNotNull(bindings, "bindings");
      foreach (T item in GetValuesFromBindingsOrDefault(bindings, true)) {
        yield return Format(item);
      }
    }

    protected virtual string Format(T value) {
      return value.ToString();
    }

    protected IEnumerable<T> GetValuesFromBindingsOrDefault(Hashtable bindings, bool acceptMultipleValues) {
      if (ParameterName != CamlParameterName.NoBinding) {
        object bindingValue = bindings[ParameterName.Value];
        if (bindingValue != null) {
          if (bindingValue is T) {
            return new[] { (T)bindingValue };
          }
          if (acceptMultipleValues) {
            if (bindingValue is IEnumerable<T>) {
              IEnumerable<T> typedValue = (IEnumerable<T>)bindingValue;
              if (typedValue.Any()) {
                return typedValue;
              }
              throw new CamlParameterBindingEmptyCollectionException(ParameterName);
            }
            throw new CamlParameterBindingIncorrectTypeException(ParameterName, typeof(IEnumerable<T>), bindingValue.GetType());
          }
          throw new CamlParameterBindingIncorrectTypeException(ParameterName, typeof(T), bindingValue.GetType());
        }
        throw new CamlParameterBindingNotFoundException(ParameterName);
      }
      if (acceptMultipleValues) {
        return Values;
      } else {
        return Values.Take(1);
      }
    }
  }

  public sealed class CamlParameterBindingFieldRef : CamlParameterBinding<string> {
    private CamlParameterBindingFieldRef(string value)
      : base(value) { }

    private CamlParameterBindingFieldRef(CamlParameterName parameterName)
      : base(parameterName) { }

    public override bool Equals(object obj) {
      if (obj is CamlParameterBindingFieldRef) {
        CamlParameterBindingFieldRef x = (CamlParameterBindingFieldRef)obj;
        if (this.ParameterName != CamlParameterName.NoBinding) {
          return x.ParameterName != CamlParameterName.NoBinding && this.ParameterName.Value.Equals(x.ParameterName.Value);
        }
        return x.ParameterName == CamlParameterName.NoBinding && this.Values[0].Equals(x.Values[0]);
      }
      return base.Equals(obj);
    }

    public override int GetHashCode() {
      if (ParameterName != CamlParameterName.NoBinding) {
        return ParameterName.Value.GetHashCode();
      }
      return Values[0].GetHashCode();
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

    internal CamlParameterBindingBooleanString(CamlParameterName parameterName, bool value)
      : base(parameterName, value) { }

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

    public CamlParameterBindingString(CamlParameterName parameterName, string value)
      : base(parameterName, value) { }

    public CamlParameterBindingString(CamlParameterName parameterName, IEnumerable<string> value)
      : base(parameterName, value) { }
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

    public CamlParameterBindingUrl(CamlParameterName parameterName, string value)
      : base(parameterName, value) { }

    public CamlParameterBindingUrl(CamlParameterName parameterName, IEnumerable<string> value)
      : base(parameterName, value) { }

    public override CamlValueType ValueType {
      get { return CamlValueType.URL; }
    }
  }

  internal class CamlParameterBindingBoolean : CamlParameterBinding<bool> {
    public CamlParameterBindingBoolean(bool value)
      : base(value) { }

    public CamlParameterBindingBoolean(CamlParameterName parameterName)
      : base(parameterName) { }

    public CamlParameterBindingBoolean(CamlParameterName parameterName, bool value)
      : base(parameterName, value) { }

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

    public CamlParameterBindingLookup(CamlParameterName parameterName, int value)
      : base(parameterName, value) { }

    public CamlParameterBindingLookup(CamlParameterName parameterName, IEnumerable<int> value)
      : base(parameterName, value) { }

    public CamlParameterBindingLookup(CamlParameterName parameterName, SPPrincipal value)
      : base(parameterName, value.ID) { }

    public CamlParameterBindingLookup(CamlParameterName parameterName, IEnumerable<SPPrincipal> value)
      : base(parameterName, value.Select(u => u.ID)) { }

    public CamlParameterBindingLookup(CamlParameterName parameterName, SPListItem value)
      : base(parameterName, value.ID) { }

    public CamlParameterBindingLookup(CamlParameterName parameterName, IEnumerable<SPListItem> value)
      : base(parameterName, value.Select(u => u.ID)) { }

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

    public CamlParameterBindingInteger(CamlParameterName parameterName, int value)
      : base(parameterName, value) { }

    public CamlParameterBindingInteger(CamlParameterName parameterName, IEnumerable<int> value)
      : base(parameterName, value) { }

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

    public CamlParameterBindingNumber(CamlParameterName parameterName, double value)
      : base(parameterName, value) { }

    public CamlParameterBindingNumber(CamlParameterName parameterName, IEnumerable<double> value)
      : base(parameterName, value) { }

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

    public CamlParameterBindingGuid(CamlParameterName parameterName, Guid value)
      : base(parameterName, value) { }

    public CamlParameterBindingGuid(CamlParameterName parameterName, IEnumerable<Guid> value)
      : base(parameterName, value) { }

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

    public CamlParameterBindingDateTime(CamlParameterName parameterName, DateTime value, bool includeTimeValue)
      : base(parameterName, value) {
      this.IncludeTimeValue = includeTimeValue;
    }

    public CamlParameterBindingDateTime(CamlParameterName parameterName, IEnumerable<DateTime> value, bool includeTimeValue)
      : base(parameterName, value) {
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

    public CamlParameterBindingContentTypeId(CamlParameterName parameterName, SPContentTypeId value)
      : base(parameterName, value) { }

    public CamlParameterBindingContentTypeId(CamlParameterName parameterName, IEnumerable<SPContentTypeId> value)
      : base(parameterName, value) { }

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

    public CamlParameterBindingModStat(CamlParameterName parameterName, SPModerationStatusType value)
      : base(parameterName, value) { }

    public CamlParameterBindingModStat(CamlParameterName parameterName, IEnumerable<SPModerationStatusType> value)
      : base(parameterName, value) { }

    public override CamlValueType ValueType {
      get { return CamlValueType.ModStat; }
    }
  }
  #endregion
}
