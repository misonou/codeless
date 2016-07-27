using System;
using System.Collections.Generic;

namespace Codeless {
  /// <summary>
  /// Provides strongly-typed methods to any Enum type.
  /// </summary>
  /// <typeparam name="T">An enumeration type.</typeparam>
  public static class Enum<T> where T : struct {
    /// <summary>
    /// Converts the string representation of the name or numeric value of one or more
    /// enumerated constants to an equivalent enumerated object of type <typeparamref name="T"/>.
    /// </summary>
    /// <param name="value">A string containing the name or value to convert.</param>
    /// <returns>An object of type <typeparamref name="T"/> whose value is represented by value.</returns>
    public static T Parse(string value) {
      return Parse(value, false);
    }

    /// <summary>
    /// Converts the string representation of the name or numeric value of one or more
    /// enumerated constants to an equivalent enumerated object of type <typeparamref name="T"/>. 
    /// A parameter specifies whether the operation is case-insensitive.
    /// </summary>
    /// <param name="value">A string containing the name or value to convert.</param>
    /// <param name="ignoreCase">true to ignore case; false to regard case.</param>
    /// <returns>An object of type <typeparamref name="T"/> whose value is represented by value.</returns>
    public static T Parse(string value, bool ignoreCase) {
      return (T)Enum.Parse(typeof(T), value, ignoreCase);
    }

    /// <summary>
    /// Converts the string representation of the name or numeric value of one or more
    /// enumerated constants to an equivalent enumerated object.
    /// </summary>
    /// <param name="value">The string representation of the enumeration name or underlying value to convert.</param>
    /// <returns>The enumeration type to which to convert value if the value parameter was converted successfully; otherwise null.</returns>
    public static T? TryParse(string value) {
      return TryParse(value, false);
    }

    /// <summary>
    /// Converts the string representation of the name or numeric value of one or more
    /// enumerated constants to an equivalent enumerated object of type <typeparamref name="T"/>. 
    /// A parameter specifies whether the operation is case-insensitive.
    /// </summary>
    /// <param name="value">The string representation of the enumeration name or underlying value to convert.</param>
    /// <param name="ignoreCase">true to ignore case; false to regard case.</param>
    /// <returns>The enumeration type to which to convert value if the value parameter was converted successfully; otherwise null.</returns>
    public static T? TryParse(string value, bool ignoreCase) {
      T result;
      if (!String.IsNullOrEmpty(value) && TryParse(value, ignoreCase, out result)) {
        return result;
      }
      return null;
    }

    /// <summary>
    /// Converts the string representation of the name or numeric value of one or more
    /// enumerated constants to an equivalent enumerated object. The return value indicates
    /// whether the conversion succeeded.
    /// </summary>
    /// <param name="value">The string representation of the enumeration name or underlying value to convert.</param>
    /// <param name="result">
    /// When this method returns, result contains an object of type TEnum whose value
    /// is represented by value if the parse operation succeeds. If the parse operation
    /// fails, result contains the default value of the underlying type of TEnum. Note
    /// that this value need not be a member of the TEnum enumeration. This parameter
    /// is passed uninitialized.
    /// </param>
    /// <returns>true if the value parameter was converted successfully; otherwise, false.</returns>
    public static bool TryParse(string value, out T result) {
      return TryParse(value, false, out result);
    }

    /// <summary>
    /// Converts the string representation of the name or numeric value of one or more
    /// enumerated constants to an equivalent enumerated object. A parameter specifies
    /// whether the operation is case-sensitive. The return value indicates whether the
    /// conversion succeeded.
    /// </summary>
    /// <param name="value">The string representation of the enumeration name or underlying value to convert.</param>
    /// <param name="ignoreCase">true to ignore case; false to consider case.</param>
    /// <param name="result">
    /// When this method returns, result contains an object of type TEnum whose value
    /// is represented by value if the parse operation succeeds. If the parse operation
    /// fails, result contains the default value of the underlying type of TEnum. Note
    /// that this value need not be a member of the TEnum enumeration. This parameter
    /// is passed uninitialized.
    /// </param>
    /// <returns>true if the value parameter was converted successfully; otherwise, false.</returns>
    public static bool TryParse(string value, bool ignoreCase, out T result) {
      try {
        result = Parse(value, ignoreCase);
        return true;
      } catch (ArgumentException) {
        result = default(T);
        return false;
      }
    }

    /// <summary>
    /// Gets all values of the Enum type <typeparamref name="T"/>.
    /// </summary>
    /// <returns>An enumerable object that enumerate all values defined in the Enum type.</returns>
    public static IEnumerable<T> GetValues() {
      foreach (object value in Enum.GetValues(typeof(T))) {
        yield return (T)value;
      }
    }
    
    #region Strongly-Typed Enum Extender
    /// <summary>
    /// See <see cref="Enum.Format(Type, object, string)"/>.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="format"></param>
    /// <returns></returns>
    public static string Format(object value, string format) {
      return Enum.Format(typeof(T), value, format);
    }
    
    /// <summary>
    /// See <see cref="Enum.GetName(Type, object)"/>.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static string GetName(object value) {
      return Enum.GetName(typeof(T), value);
    }

    /// <summary>
    /// See <see cref="Enum.GetNames(Type)"/>.
    /// </summary>
    /// <returns></returns>
    public static IEnumerable<string> GetNames() {
      return Enum.GetNames(typeof(T));
    }

    /// <summary>
    /// See <see cref="Enum.GetUnderlyingType(Type)"/>.
    /// </summary>
    /// <returns></returns>
    public static Type GetUnderlyingType() {
      return Enum.GetUnderlyingType(typeof(T));
    }

    /// <summary>
    /// See <see cref="Enum.IsDefined(Type, object)"/>.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool IsDefined(object value) {
      return Enum.IsDefined(typeof(T), value);
    }

    /// <summary>
    /// See <see cref="Enum.ToObject(Type, object)"/>.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static T ToObject(object value) {
      return (T)Enum.ToObject(typeof(T), value);
    }

    /// <summary>
    /// See <see cref="Enum.ToObject(Type, byte)"/>.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static T ToObject(byte value) {
      return (T)Enum.ToObject(typeof(T), value);
    }

    /// <summary>
    /// See <see cref="Enum.ToObject(Type, sbyte)"/>.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static T ToObject(sbyte value) {
      return (T)Enum.ToObject(typeof(T), value);
    }

    /// <summary>
    /// See <see cref="Enum.ToObject(Type, int)"/>.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static T ToObject(int value) {
      return (T)Enum.ToObject(typeof(T), value);
    }

    /// <summary>
    /// See <see cref="Enum.ToObject(Type, uint)"/>.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static T ToObject(uint value) {
      return (T)Enum.ToObject(typeof(T), value);
    }

    /// <summary>
    /// See <see cref="Enum.ToObject(Type, long)"/>.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static T ToObject(long value) {
      return (T)Enum.ToObject(typeof(T), value);
    }

    /// <summary>
    /// See <see cref="Enum.ToObject(Type, ulong)"/>.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static T ToObject(ulong value) {
      return (T)Enum.ToObject(typeof(T), value);
    }

    /// <summary>
    /// See <see cref=" Enum.ToObject(Type, short)"/>.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static T ToObject(short value) {
      return (T)Enum.ToObject(typeof(T), value);
    }

    /// <summary>
    /// See <see cref="Enum.ToObject(Type, ushort)"/>.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static T ToObject(ushort value) {
      return (T)Enum.ToObject(typeof(T), value);
    }
    #endregion
  }
}
