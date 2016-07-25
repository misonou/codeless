using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Web;

namespace Codeless {
  [DebuggerStepThrough]
  internal static class CommonHelper {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ConfirmNotNull<T>(T value, string argumentName) {
      if (Object.ReferenceEquals(value, null)) {
        throw new ArgumentNullException(argumentName);
      }
      return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T AccessNotNull<T>(T value, string argumentName) {
      if (Object.ReferenceEquals(value, null)) {
        throw new MemberAccessException(argumentName);
      }
      return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T TryCastOrDefault<T>(object value) where T : class {
      return value as T;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNullOrWhiteSpace(string value) {
      return String.IsNullOrWhiteSpace(value);
    }

    [DebuggerStepThrough]
    public static T HttpContextSingleton<T>() where T : new() {
      HttpContext context = HttpContext.Current;
      if (context != null) {
        return context.Items.EnsureKeyValue(typeof(T).GUID, ReflectionHelper.CreateInstance<T>);
      }
      return default(T);
    }

    [DebuggerStepThrough]
    public static T HttpContextSingleton<T>(Func<T> valueFactory) {
      CommonHelper.ConfirmNotNull(valueFactory, "valueFactory");
      HttpContext context = HttpContext.Current;
      if (context != null) {
        return context.Items.EnsureKeyValue(typeof(T).GUID, valueFactory);
      }
      return default(T);
    }
  }
}
