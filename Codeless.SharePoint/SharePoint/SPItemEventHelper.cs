using Microsoft.SharePoint;
using System;
using System.Diagnostics;
using System.Reflection;

namespace Codeless.SharePoint {
  /// <summary>
  /// Provides helper methods to SharePoint event handling.
  /// </summary>
  public class SPItemEventHelper : SPItemEventReceiver {
    private class SPItemEventFiringScope : IDisposable {
      private readonly bool originalValue;
      private bool disposed = false;

      public SPItemEventFiringScope(bool enableEventFiring) {
        originalValue = instance.EventFiringEnabled;
        instance.EventFiringEnabled = enableEventFiring;
      }

      public void Dispose() {
        if (!disposed) {
          instance.EventFiringEnabled = originalValue;
          disposed = true;
        }
      }
    }

    private static readonly SPItemEventHelper instance = new SPItemEventHelper();

    private SPItemEventHelper()
      : base() { }

    /// <summary>
    /// Disables event firing and restores current settings when disposed.
    /// </summary>
    /// <returns>A disposble object.</returns>
    public static IDisposable GetEventFiringDisabledScope() {
      return new SPItemEventFiringScope(false);
    }

    /// <summary>
    /// Enables event firing and restores current settings when disposed.
    /// </summary>
    /// <returns>A disposble object.</returns>
    public static IDisposable GetEventFiringEnabledScope() {
      return new SPItemEventFiringScope(true);
    }

    /// <summary>
    /// Returns *true* if the current event is fired inside workflow; otherwise *false*.
    /// </summary>
    public static bool IsWorkflowFiredEvent {
      get {
        foreach (StackFrame sf in new StackTrace().GetFrames()) {
          MethodBase method = sf.GetMethod();
          if (method.Name == "Run" && method.ReflectedType.FullName == "System.Workflow.Runtime.Scheduler") {
            return true;
          }
        }
        return false;
      }
    }

    /// <summary>
    /// Returns *true* if the current event is fired by another event; otherwise *false*.
    /// </summary>
    public static bool IsNestedItemEvent {
      get {
        bool passFirstReceiver = false;
        foreach (StackFrame sf in new StackTrace().GetFrames()) {
          MethodBase method = sf.GetMethod();
          if (method.Name == "RunItemEventReceiver" && method.ReflectedType.FullName == "Microsoft.SharePoint.SPEventManager") {
            if (passFirstReceiver) {
              return true;
            }
            passFirstReceiver = true;
          }
        }
        return false;
      }
    }
  }
}
