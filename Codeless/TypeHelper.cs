using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace Codeless {
  /// <summary>
  /// Provides extension methods to <see cref="Type"/> objects.
  /// </summary>
  public static class TypeHelper {
    /// <summary>
    /// Gets a default value for a specified type. It is equivalent to <code>default(T)</code> if the type in known at compile time.
    /// </summary>
    /// <param name="type">Type which its default value is returned.</param>
    /// <exception cref="ArgumentException">Throws if the specified type is a generic type, or does not have a public parameterless constructor, or is not public.</exception>
    /// <returns>A default value for the specified type.</returns>
    public static object GetDefaultValue(this Type type) {
      CommonHelper.ConfirmNotNull(type, "type");
      if (type == null || !type.IsValueType || type == typeof(void)) {
        return null;
      }
      if (type.ContainsGenericParameters) {
        throw new ArgumentException("Type <" + type + "> is a generic value type", "type");
      }
      if (type.IsPrimitive || !type.IsNotPublic) {
        try {
          return Activator.CreateInstance(type);
        } catch (Exception ex) {
          throw new ArgumentException("Type <" + type + "> does not contain public parameterless constructor", "type", ex);
        }
      }
      throw new ArgumentException("The supplied value type <" + type + "> is not a publicly-visible type, so the default value cannot be retrieved");
    }

    /// <summary>
    /// Gets the element type of an enumerable type that is not of the type <see cref="String"/>.
    /// </summary>
    /// <param name="type">An enumerable type.</param>
    /// <returns>The element type if the specified type implements the interface <see cref="IEnumerable"/> or <see cref="IEnumerable{T}"/>; otherwise *null*.</returns>
    public static Type GetEnumeratedType(this Type type) {
      CommonHelper.ConfirmNotNull(type, "type");
      if (type == typeof(string)) {
        return null;
      }
      Type typeArgument;
      if (type.IsOf(typeof(IEnumerable<>), out typeArgument)) {
        return typeArgument;
      }
      if (type.IsOf<IEnumerable>()) {
        return typeof(object);
      }
      return null;
    }

    /// <summary>
    /// Gets the specified field.
    /// </summary>
    /// <param name="type">The type of field to search.</param>
    /// <param name="name">The string containing the name of the data field to get.</param>
    /// <param name="nonPublic">A boolean value specifying whether to return non-public field.</param>
    /// <returns>An object representing the field that matches the specified requirements, if found; otherwise, null.</returns>
    [DebuggerStepThrough]
    public static FieldInfo GetField(this Type type, string name, bool nonPublic) {
      CommonHelper.ConfirmNotNull(type, "type");
      return type.GetField(name, ReflectionHelper.ALL & (nonPublic ? (BindingFlags)~0 : ~BindingFlags.NonPublic));
    }

    /// <summary>
    /// Gets the specified property.
    /// </summary>
    /// <param name="type">The type of property to search.</param>
    /// <param name="name">The string containing the name of the data property to get.</param>
    /// <param name="nonPublic">A boolean value specifying whether to return non-public property.</param>
    /// <returns>An object representing the property that matches the specified requirements, if found; otherwise, null.</returns>
    [DebuggerStepThrough]
    public static PropertyInfo GetProperty(this Type type, string name, bool nonPublic) {
      CommonHelper.ConfirmNotNull(type, "type");
      return type.GetProperty(name, ReflectionHelper.ALL & (nonPublic ? (BindingFlags)~0 : ~BindingFlags.NonPublic));
    }

    /// <summary>
    /// Gets the specified method.
    /// </summary>
    /// <param name="type">The type of method to search</param>
    /// <param name="name">The string containing the name of the method to get.</param>
    /// <param name="nonPublic">A boolean value specifying whether to return non-public method.</param>
    /// <returns>An object representing the method that matches the specified requirements, if found; otherwise, null</returns>
    [DebuggerStepThrough]
    public static MethodInfo GetMethod(this Type type, string name, bool nonPublic) {
      CommonHelper.ConfirmNotNull(type, "type");
      return type.GetMethod(name, ReflectionHelper.ALL & (nonPublic ? (BindingFlags)~0 : ~BindingFlags.NonPublic));
    }

    /// <summary>
    /// Gets the specified method that has the specified types of parameters.
    /// </summary>
    /// <param name="type">The type of method to search</param>
    /// <param name="name">The string containing the name of the method to get.</param>
    /// <param name="nonPublic">A boolean value specifying whether to return non-public method.</param>
    /// <param name="parameterTypes">A collection containing the types of parameters.</param>
    /// <returns>An object representing the method that matches the specified requirements, if found; otherwise, null</returns>
    [DebuggerStepThrough]
    public static MethodInfo GetMethod(this Type type, string name, bool nonPublic, params Type[] parameterTypes) {
      CommonHelper.ConfirmNotNull(type, "type");
      return type.GetMethod(name, ReflectionHelper.ALL & (nonPublic ? (BindingFlags)~0 : ~BindingFlags.NonPublic), null, parameterTypes, null);
    }

    /// <summary>
    /// Determines if the specified type is equals to, or is a subclass of, or implemented the type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">Type to test.</typeparam>
    /// <param name="type">Type to test against.</param>
    /// <returns>*true* if the given type is equals to, or is a subclass of, or implemented the type <typeparamref name="T"/>.</returns>
    [DebuggerStepThrough]
    public static bool IsOf<T>(this Type type) {
      Type[] value;
      return IsOf(type, typeof(T), out value);
    }

    /// <summary>
    /// Determines if the specified type is equals to, or is a subclass of, or implemented the other type.
    /// </summary>
    /// <param name="type">Type to test against.</param>
    /// <param name="other">Type to test.</param>
    /// <returns>*true* if the given type is equals to, or is a subclass of, or implemented the type supplied in <paramref name="other"/>.</returns>
    [DebuggerStepThrough]
    public static bool IsOf(this Type type, Type other) {
      Type[] value;
      return IsOf(type, other, out value);
    }

    /// <summary>
    /// Determines if the specified type is equals to, or is a subclass of, or implemented the other type, and returns the first generic type argument.
    /// </summary>
    /// <param name="type">Type to test against.</param>
    /// <param name="other">Type to test.</param>
    /// <param name="typeArgument">If the specified type is a generic type or interface, the first generic type argument of the generic type of interface is set to this variable; otherwise, *null* is set.</param>
    /// <returns>*true* if the given type is equals to, or is a subclass of, or implemented the type supplied in <paramref name="other"/>.</returns>
    [DebuggerStepThrough]
    public static bool IsOf(this Type type, Type other, out Type typeArgument) {
      Type[] value;
      typeArgument = null;
      if (IsOf(type, other, out value)) {
        if (value.Length > 0) {
          typeArgument = value[0];
        }
        return true;
      }
      return false;
    }

    /// <summary>
    /// Determines if the specified type is equals to, or is a subclass of, or implemented the other type, and return the generic type arguments.
    /// </summary>
    /// <param name="type">Type to test against.</param>
    /// <param name="other">Type to test.</param>
    /// <param name="typeArguments">If the specified type is a generic type or interface, the generic type arguments of the generic type of interface is set to this variable as an array; otherwise, an empty array is set.</param>
    /// <returns>*true* if the given type is equals to, or is a subclass of, or implemented the type supplied in <paramref name="other"/>.</returns>
    public static bool IsOf(this Type type, Type other, out Type[] typeArguments) {
      CommonHelper.ConfirmNotNull(type, "type");
      CommonHelper.ConfirmNotNull(other, "other");
      typeArguments = type.GetGenericArguments();

      if (type.Equals(other) || type.IsEquivalentTo(other)) {
        return true;
      }
      if (other.IsArray || other.IsByRef || other.IsPointer) {
        return !(other.IsArray ^ type.IsArray) &&
               !(other.IsByRef ^ type.IsByRef) &&
               !(other.IsPointer ^ type.IsPointer) &&
               type.GetElementType().IsOf(other.GetElementType(), out typeArguments);
      }
      if (!other.IsGenericType) {
        if (other.IsInterface) {
          return type.GetInterface(other.Namespace + '.' + other.Name) != null;
        }
        return type.IsSubclassOf(other);
      }
      if (other.IsInterface) {
        if (IsOfGenericType(type, other, out typeArguments)) {
          return true;
        }
        foreach (Type interfaceType in type.GetInterfaces()) {
          if (IsOfGenericType(interfaceType, other, out typeArguments)) {
            return true;
          }
        }
      } else {
        for (Type baseType = type; baseType != null; baseType = baseType.BaseType) {
          if (IsOfGenericType(baseType, other, out typeArguments)) {
            return true;
          }
        }
      }
      return false;
    }

    private static bool IsOfGenericType(Type type, Type other, out Type[] typeArguments) {
      if (type.IsGenericType && type.GetGenericTypeDefinition() == other.GetGenericTypeDefinition()) {
        typeArguments = type.GetGenericArguments();
        if (other.GetGenericTypeDefinition() != other) {
          Type[] typeArguments1 = other.GetGenericTypeDefinition().GetGenericArguments();
          Type[] typeArguments2 = other.GetGenericArguments();
          for (var i = typeArguments1.Length - 1; i >= 0; i--) {
            if (!typeArguments2[i].IsGenericParameter) {
              if (typeArguments1[i].GenericParameterAttributes.HasFlag(GenericParameterAttributes.Covariant)) {
                if (!typeArguments[i].IsOf(typeArguments2[i])) {
                  return false;
                }
              } else if (typeArguments1[i].GenericParameterAttributes.HasFlag(GenericParameterAttributes.Contravariant)) {
                if (!typeArguments2[i].IsOf(typeArguments[i])) {
                  return false;
                }
              } else if (typeArguments[i] != typeArguments2[i]) {
                return false;
              }
            }
          }
        }
        return true;
      }
      typeArguments = new Type[0];
      return false;
    }
  }
}
