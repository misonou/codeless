using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Codeless {
  /// <summary>
  /// Provides methods for enumeration.
  /// </summary>
  public static class EnumerableHelper {
    /// <summary>
    /// Selects all descendant objects in a tree-like data structure.
    /// </summary>
    /// <typeparam name="T">Type of objects to select.</typeparam>
    /// <param name="source">An object representing a node in a tree-like data structure.</param>
    /// <param name="selector">An delegate to select the child nodes of a given node.</param>
    /// <returns>An enumerable which enumerates all descendant nodes of the specified node.</returns>
    [DebuggerStepThrough]
    public static IEnumerable<T> Descendants<T>(T source, Func<T, IEnumerable<T>> selector) {
      CommonHelper.ConfirmNotNull(selector, "selector");
      foreach (T item in selector(source)) {
        yield return item;
        foreach (T childItem in Descendants(item, selector)) {
          yield return childItem;
        }
      }
    }

    /// <summary>
    /// Selects all ancestor objects in a tree-like data structure.
    /// </summary>
    /// <typeparam name="T">Type of objects to select.</typeparam>
    /// <param name="source">An object representing a node in a tree-like data structure.</param>
    /// <param name="selector">An delegate to select the arent node of a given node.</param>
    /// <returns>An enumerable which enumerates all ancestor nodes of the specified node.</returns>
    [DebuggerStepThrough]
    public static IEnumerable<T> Ancestors<T>(T source, Func<T, T> selector) {
      CommonHelper.ConfirmNotNull(selector, "selector");
      for (T current = source; current != null; current = selector(current)) {
        yield return current;
      }
    }
    
    [DebuggerStepThrough]
    public static IEnumerable<T> ForEach<T>(this IEnumerable<T> source, Action<T> action) {
      CommonHelper.ConfirmNotNull(source, "source");
      CommonHelper.ConfirmNotNull(action, "action");
      foreach (T item in source) {
        action(item);
      }
      return source;
    }
  }
}
