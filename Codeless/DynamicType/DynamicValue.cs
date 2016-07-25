using Codeless;
using Codeless;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Codeless.DynamicType {
  public enum DynamicValueType {
    Undefined,
    Object,
    Number,
    String,
    Boolean,
    Function
  }

  public class DynamicValueIndexingException : Exception {
    internal DynamicValueIndexingException(string message)
      : base(message) { }
  }

  public class DynamicValueInvocationException : Exception {
    internal DynamicValueInvocationException(string message)
      : base(message) { }

    internal DynamicValueInvocationException(string message, Exception innerException)
      : base(message, innerException) { }
  }

  [DebuggerDisplay("{Value}")]
  public struct DynamicValue : IEquatable<DynamicValue>, IConvertible {
    private static readonly object undefined = new object();
    public static readonly DynamicValue Null = new DynamicValue(null);
    public static readonly DynamicValue Undefined = new DynamicValue(undefined);
    private readonly ICustomDynamicObject implementedInterface;
    private readonly object value;

    public DynamicValue(object value)
      : this() {
      if (value is DynamicValue) {
        this.value = ((DynamicValue)value).Value;
      } else if (value == null || (System.Type.GetTypeCode(value.GetType()) != TypeCode.Object && System.Type.GetTypeCode(value.GetType()) != TypeCode.DateTime) || value is ICustomDynamicObject || value is MethodInfo[]) {
        this.value = value;
      } else {
        this.value = new DynamicNativeObject(value);
      }
      this.implementedInterface = (this.value as ICustomDynamicObject);
    }

    public DynamicValue this[string index] {
      get {
        if (implementedInterface != null) {
          object value;
          if (implementedInterface.GetValue(index, out value)) {
            return new DynamicValue(value);
          }
          return DynamicValue.Undefined;
        }
        if (!this.IsEvallable) {
          throw new DynamicValueIndexingException("Cannot index to undefined or null object");
        }
        return DynamicValue.Undefined;
      }
      set {
        if (implementedInterface != null) {
          implementedInterface.SetValue(index, value);
        }
      }
    }

    public DynamicValueType Type {
      get {
        if (this.Value == undefined) {
          return DynamicValueType.Undefined;
        }
        if (!this.IsEvallable) {
          return DynamicValueType.Object;
        }
        switch (System.Type.GetTypeCode(this.Value.GetType())) {
          case TypeCode.Boolean:
            return DynamicValueType.Boolean;
          case TypeCode.Byte:
          case TypeCode.Char:
          case TypeCode.Decimal:
          case TypeCode.Double:
          case TypeCode.Int16:
          case TypeCode.Int32:
          case TypeCode.Int64:
          case TypeCode.SByte:
          case TypeCode.Single:
          case TypeCode.UInt16:
          case TypeCode.UInt32:
          case TypeCode.UInt64:
            return DynamicValueType.Number;
          case TypeCode.String:
            return DynamicValueType.String;
          default:
            if (this.Value.GetType() == typeof(MethodInfo[])) {
              return DynamicValueType.Function;
            }
            return DynamicValueType.Object;
        }
      }
    }

    public object Value {
      get { return value; }
    }

    public bool IsEvallable {
      get { return (this.Value != null && this.Value != undefined); }
    }

    public bool AsBool() {
      if (implementedInterface != null) {
        return true;
      }
      if (!this.IsEvallable || "".Equals(this.Value) || (0).Equals(this.Value) || false.Equals(this.Value)) {
        return false;
      }
      return true;
    }

    public double AsNumber() {
      if (implementedInterface != null) {
        return 1;
      }
      if (this.IsEvallable) {
        switch (this.Type) {
          case DynamicValueType.Boolean:
            return ((bool)this.Value ? 1 : 0);
          case DynamicValueType.Number:
            return Convert.ToDouble(this.Value);
          case DynamicValueType.String:
            double doubleValue;
            if (Double.TryParse((string)this.Value, out doubleValue)) {
              return doubleValue;
            }
            return Double.NaN;
          case DynamicValueType.Function:
            return ((MethodInfo[])this.Value).Max(v => v.GetParameters().Length);
          case DynamicValueType.Object:
            return 1;
        }
      }
      return Double.NaN;
    }

    public string AsString() {
      switch (this.Type) {
        case DynamicValueType.Boolean:
          return ((bool)this.Value ? "true" : "false");
        case DynamicValueType.Undefined:
          return "undefined";
        case DynamicValueType.Object:
          if (implementedInterface != null) {
            return "[object " + implementedInterface.TypeName + "]";
          } else if (this.IsEvallable) {
            return "[object Object]";
          } else {
            return "null";
          }
        default:
          return this.Value.ToString();
      }
    }

    [Obsolete]
    public IEnumerable<DynamicValue> AsArray() {
      if (this.Value is IEnumerable) {
        foreach (object item in (IEnumerable)this.Value) {
          yield return new DynamicValue(item);
        }
      } else if (this.IsEvallable) {
        yield return this;
      }
    }

    public DynamicKey[] GetKeys() {
      if (implementedInterface != null) {
        return implementedInterface.GetKeys().ToArray();
      }
      return new DynamicKey[0];
    }

    [Obsolete]
    public double GetLength() {
      return +this;
    }

    public static bool IsMethodCallable(MethodInfo method) {
      foreach (ParameterInfo parameter in method.GetParameters()) {
        if (parameter.ParameterType == typeof(DynamicValue[])) {
          return true;
        }
        if (parameter.ParameterType != typeof(DynamicValue) && System.Type.GetTypeCode(parameter.ParameterType) == TypeCode.Object) {
          return false;
        }
      }
      return true;
    }

    public DynamicValue Invoke(string methodName, params DynamicValue[] args) {
      return this[methodName].Invoke(this, args);
    }

    public DynamicValue Invoke(DynamicValue obj, params DynamicValue[] args) {
      if (!this.IsEvallable) {
        throw new DynamicValueInvocationException("Cannot invoke to undefined or null object.");
      }
      if (this.Type != DynamicValueType.Function) {
        throw new DynamicValueInvocationException("Cannot invoke to non-function value.");
      }

      MethodInfo[] methods = (MethodInfo[])this.Value;
      MethodInfo method = methods.FirstOrDefault(v => v.GetParameters().Length == args.Length);
      if (method == null) {
        method = methods.FirstOrDefault(v => v.GetParameters().Length > 0 && v.GetParameters()[0].ParameterType == typeof(DynamicValue[]));
      }
      if (method == null) {
        method = methods.OrderBy(v => v.GetParameters().Length).FirstOrDefault(v => v.GetParameters().Length > args.Length);
      }
      if (method == null) {
        method = methods.OrderByDescending(v => v.GetParameters().Length).FirstOrDefault(v => v.GetParameters().Length < args.Length);
      }
      if (method == null) {
        throw new DynamicValueInvocationException("No suitable overload of methods to invoke to.");
      }

      List<DynamicValue> argList = new List<DynamicValue>(args);
      object[] parametersValues = new object[method.GetParameters().Length];
      for (int i = 0; i < method.GetParameters().Length; i++) {
        Type parameterType = method.GetParameters()[i].ParameterType;
        if (parameterType == typeof(DynamicValue[])) {
          parametersValues[i] = argList.ToArray();
        } else if (parameterType == typeof(DynamicValue)) {
          if (argList.Count > i) {
            parametersValues[i] = argList[i];
          } else {
            parametersValues[i] = DynamicValue.Undefined;
          }
        } else {
          if (argList.Count > i) {
            parametersValues[i] = Convert.ChangeType(argList[i], parameterType);
          } else {
            parametersValues[i] = parameterType.GetDefaultValue();
          }
        }
      }
      try {
        object invocationTarget = obj.Value;
        if (invocationTarget is DynamicNativeObject) {
          invocationTarget = ((DynamicNativeObject)invocationTarget).obj;
        }
        object intermediateValue = method.Invoke(invocationTarget, parametersValues);
        if (method.ReturnType != typeof(void)) {
          return new DynamicValue(intermediateValue);
        }
        return DynamicValue.Undefined;
      } catch (Exception ex) {
        throw new DynamicValueInvocationException(ex.Message, ex);
      }
    }

    public static implicit operator bool(DynamicValue value) {
      return value.AsBool();
    }

    public static implicit operator string(DynamicValue value) {
      return value.AsString();
    }

    public static implicit operator double(DynamicValue value) {
      return value.AsNumber();
    }

    public static implicit operator DynamicValue(bool value) {
      return new DynamicValue(value);
    }

    public static implicit operator DynamicValue(string value) {
      return new DynamicValue(value);
    }

    public static implicit operator DynamicValue(double value) {
      return new DynamicValue(value);
    }

    public static implicit operator DynamicValue(DynamicObject value) {
      return new DynamicValue(value);
    }

    public static implicit operator DynamicValue(object[] value) {
      return new DynamicValue(value);
    }

    public static bool operator ==(DynamicValue x, DynamicValue y) {
      return x.Equals(y);
    }

    public static bool operator !=(DynamicValue x, DynamicValue y) {
      return !x.Equals(y);
    }

    public static bool operator <(DynamicValue x, DynamicValue y) {
      if (x.Type == DynamicValueType.Number && y.Type == DynamicValueType.Number) {
        return x.AsNumber() < y.AsNumber();
      }
      return x.AsString().CompareTo(y.AsString()) < 0;
    }

    public static bool operator >(DynamicValue x, DynamicValue y) {
      if (x.Type == DynamicValueType.Number && y.Type == DynamicValueType.Number) {
        return x.AsNumber() > y.AsNumber();
      }
      return x.AsString().CompareTo(y.AsString()) > 0;
    }

    public static bool operator <=(DynamicValue x, DynamicValue y) {
      if (x.Type == DynamicValueType.Number && y.Type == DynamicValueType.Number) {
        return x.AsNumber() <= y.AsNumber();
      }
      return x.AsString().CompareTo(y.AsString()) <= 0;
    }

    public static bool operator >=(DynamicValue x, DynamicValue y) {
      if (x.Type == DynamicValueType.Number && y.Type == DynamicValueType.Number) {
        return x.AsNumber() >= y.AsNumber();
      }
      return x.AsString().CompareTo(y.AsString()) >= 0;
    }

    public static bool operator true(DynamicValue x) {
      return x;
    }

    public static bool operator false(DynamicValue x) {
      return !x;
    }

    public static DynamicValue operator +(DynamicValue x) {
      return x.AsNumber();
    }

    public static DynamicValue operator -(DynamicValue x) {
      return -x.AsNumber();
    }

    public static DynamicValue operator +(DynamicValue x, DynamicValue y) {
      if (x.Type == DynamicValueType.Number && y.Type == DynamicValueType.Number) {
        return x.AsNumber() + y.AsNumber();
      }
      return x.AsString() + y.AsString();
    }

    public static DynamicValue operator -(DynamicValue x, DynamicValue y) {
      return x.AsNumber() - y.AsNumber();
    }

    public static DynamicValue operator *(DynamicValue x, DynamicValue y) {
      return x.AsNumber() * y.AsNumber();
    }

    public static DynamicValue operator /(DynamicValue x, DynamicValue y) {
      return x.AsNumber() / y.AsNumber();
    }

    public static DynamicValue operator %(DynamicValue x, DynamicValue y) {
      return x.AsNumber() % y.AsNumber();
    }

    public static DynamicValue operator &(DynamicValue x, DynamicValue y) {
      return (long)x.AsNumber() & (long)y.AsNumber();
    }

    public static DynamicValue operator |(DynamicValue x, DynamicValue y) {
      return (long)x.AsNumber() | (long)y.AsNumber();
    }

    public static DynamicValue operator <<(DynamicValue x, int y) {
      return (long)x.AsNumber() << y;
    }

    public static DynamicValue operator >>(DynamicValue x, int y) {
      return (long)x.AsNumber() >> y;
    }

    public bool Equals(DynamicValue other) {
      if (this.Value == null && other.Value == null) {
        return true;
      }
      if (this.Value == null || other.Value == null) {
        return false;
      }
      return this.Value.Equals(other.Value);
    }

    public override bool Equals(object obj) {
      if (obj is DynamicValue) {
        return Equals((DynamicValue)obj);
      }
      return base.Equals(obj);
    }

    public override int GetHashCode() {
      if (this.Value == null) {
        return 0;
      }
      return this.Value.GetHashCode();
    }

    public override string ToString() {
      return AsString();
    }

    #region IConvertible
    TypeCode IConvertible.GetTypeCode() {
      return TypeCode.Object;
    }

    bool IConvertible.ToBoolean(IFormatProvider provider) {
      return this.AsBool();
    }

    byte IConvertible.ToByte(IFormatProvider provider) {
      return (byte)(((long)this.AsNumber()) & ((1 << sizeof(byte) * 8) - 1));
    }

    char IConvertible.ToChar(IFormatProvider provider) {
      return (char)(((long)this.AsNumber()) & ((1 << sizeof(char) * 8) - 1));
    }

    DateTime IConvertible.ToDateTime(IFormatProvider provider) {
      throw new InvalidCastException();
    }

    decimal IConvertible.ToDecimal(IFormatProvider provider) {
      throw new InvalidCastException();
    }

    double IConvertible.ToDouble(IFormatProvider provider) {
      return this.AsNumber();
    }

    short IConvertible.ToInt16(IFormatProvider provider) {
      return (short)(((ulong)this.AsNumber()) & (((ulong)1 << sizeof(short) * 8) - 1));
    }

    int IConvertible.ToInt32(IFormatProvider provider) {
      return (int)(((ulong)this.AsNumber()) & (((ulong)1 << sizeof(int) * 8) - 1));
    }

    long IConvertible.ToInt64(IFormatProvider provider) {
      return (long)this.AsNumber();
    }

    sbyte IConvertible.ToSByte(IFormatProvider provider) {
      return (sbyte)(((ulong)this.AsNumber()) & (((ulong)1 << sizeof(sbyte) * 8) - 1));
    }

    float IConvertible.ToSingle(IFormatProvider provider) {
      try {
        return (float)this.AsNumber();
      } catch (OverflowException) {
        return Single.NaN;
      }
    }

    string IConvertible.ToString(IFormatProvider provider) {
      return this.AsString();
    }

    object IConvertible.ToType(Type conversionType, IFormatProvider provider) {
      throw new InvalidCastException();
    }

    ushort IConvertible.ToUInt16(IFormatProvider provider) {
      return (ushort)(((ulong)this.AsNumber()) & (((ulong)1 << sizeof(ushort) * 8) - 1));
    }

    uint IConvertible.ToUInt32(IFormatProvider provider) {
      return (uint)(((ulong)this.AsNumber()) & (((ulong)1 << sizeof(uint) * 8) - 1));
    }

    ulong IConvertible.ToUInt64(IFormatProvider provider) {
      return (ulong)this.AsNumber();
    }
    #endregion
  }
}
