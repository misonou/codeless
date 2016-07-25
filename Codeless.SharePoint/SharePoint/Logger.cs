using Microsoft.Practices.EnterpriseLibrary.Logging;
using Microsoft.SharePoint.Administration;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using EntLibLogger = Microsoft.Practices.EnterpriseLibrary.Logging.Logger;

namespace Codeless.SharePoint {
  internal enum LoggerEventType {
    Information,
    Debug,
    Warning,
    Error
  }

  /// <summary>
  /// Specifies which logging channel a log entry should write to.
  /// </summary>
  [Flags]
  public enum LoggerTarget {
    /// <summary>
    /// Specifies a log entry should write to ULS trace log.
    /// </summary>
    UlsLog = 1,
    /// <summary>
    /// Specifies a log entry should write to console output.
    /// </summary>
    Console = 2,
    /// <summary>
    /// Specifies a log entry should write to Microsoft Enterprise Library Logging Block.
    /// </summary>
    EntLib = 4,
    /// <summary>
    /// Specifies a log entry should write to Windows event log.
    /// </summary>
    EventLog = 8
  }

  /// <summary>
  /// Specifies logging behaviors for subsequent calls to <see cref="Logger"/> in the attributed method.
  /// </summary>
  [AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Property | AttributeTargets.Method)]
  public sealed class LoggerOptionsAttribute : Attribute {
    /// <summary>
    /// Creates an instance of the <see cref="LoggerOptionsAttribute"/> class with default options.
    /// </summary>
    public LoggerOptionsAttribute()
      : this(LoggerTarget.UlsLog | LoggerTarget.EntLib) { }

    /// <summary>
    /// Creates an instance of the <see cref="LoggerOptionsAttribute"/> class with the specified logging targets.
    /// </summary>
    /// <param name="options"></param>
    public LoggerOptionsAttribute(LoggerTarget options) {
      this.All = options;
    }

    /// <summary>
    /// Creates an instance of the <see cref="LoggerOptionsAttribute"/> class with the specified category.
    /// </summary>
    /// <param name="category"></param>
    public LoggerOptionsAttribute(string category)
      : this() {
      this.Category = category;
    }

    /// <summary>
    /// Creates an instance of the <see cref="LoggerOptionsAttribute"/> class with the specified category and logging targets.
    /// </summary>
    /// <param name="category"></param>
    /// <param name="options"></param>
    public LoggerOptionsAttribute(string category, LoggerTarget options)
      : this(options) {
      this.Category = category;
    }

    /// <summary>
    /// Gets or sets the logging category.
    /// </summary>
    public string Category { get; set; }

    /// <summary>
    /// Gets or sets the default logging targets for all types of log entries.
    /// </summary>
    public LoggerTarget All { get; set; }

    /// <summary>
    /// Gets or sets the default logging targets for information log.
    /// </summary>
    public LoggerTarget Information { get; set; }

    /// <summary>
    /// Gets or sets the default logging targets for debug log.
    /// </summary>
    public LoggerTarget Debug { get; set; }

    /// <summary>
    /// Gets or sets the default logging targets for warning log.
    /// </summary>
    public LoggerTarget Warn { get; set; }

    /// <summary>
    /// Gets or sets the default logging targets for error log.
    /// </summary>
    public LoggerTarget Error { get; set; }

    internal LoggerOptionsAttribute Clone() {
      return (LoggerOptionsAttribute)MemberwiseClone();
    }

    internal LoggerTarget GetTargets(LoggerEventType eventType) {
      LoggerTarget options;
      switch (eventType) {
        case LoggerEventType.Error:
          options = this.Error;
          break;
        case LoggerEventType.Warning:
          options = this.Warn;
          break;
        case LoggerEventType.Debug:
          options = this.Debug;
          break;
        default:
          options = this.Information;
          break;
      }
      if (options > 0) {
        return options;
      }
      return this.All;
    }
  }

  /// <summary>
  /// Provides unified interface for writing logs to different logging systems.
  /// </summary>
  public static class Logger {
    private static readonly ConcurrentDictionary<int, ConcurrentDictionary<MethodBase, LoggerOptionsAttribute>> attributeCaches = new ConcurrentDictionary<int, ConcurrentDictionary<MethodBase, LoggerOptionsAttribute>>();

    /// <summary>
    /// Writes an information log.
    /// </summary>
    /// <param name="message"></param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Info(string message) {
      WriteLog(LoggerEventType.Information, GetCallingMethodOptions(null), message);
    }

    /// <summary>
    /// Writes an information log.
    /// </summary>
    /// <param name="format"></param>
    /// <param name="args"></param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Info(string format, params object[] args) {
      WriteLog(LoggerEventType.Information, GetCallingMethodOptions(null), String.Format(format, args));
    }
    
    /// <summary>
    /// Writes a debug log.
    /// </summary>
    /// <param name="message"></param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Debug(string message) {
      WriteLog(LoggerEventType.Debug, GetCallingMethodOptions(null), message);
    }
    
    /// <summary>
    /// Writes a debug log.
    /// </summary>
    /// <param name="format"></param>
    /// <param name="args"></param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Debug(string format, params object[] args) {
      WriteLog(LoggerEventType.Debug, GetCallingMethodOptions(null), String.Format(format, args));
    }

    /// <summary>
    /// Writes a warning log.
    /// </summary>
    /// <param name="message"></param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Warn(string message) {
      WriteLog(LoggerEventType.Warning, GetCallingMethodOptions(null), message);
    }
    
    /// <summary>
    /// Writes a warning log.
    /// </summary>
    /// <param name="format"></param>
    /// <param name="args"></param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Warn(string format, params object[] args) {
      WriteLog(LoggerEventType.Warning, GetCallingMethodOptions(null), String.Format(format, args));
    }
    
    /// <summary>
    /// Writes an error log.
    /// </summary>
    /// <param name="message"></param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Error(string message) {
      WriteLog(LoggerEventType.Error, GetCallingMethodOptions(null), message);
    }
    
    /// <summary>
    /// Writes an error log.
    /// </summary>
    /// <param name="format"></param>
    /// <param name="args"></param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Error(string format, params object[] args) {
      WriteLog(LoggerEventType.Error, GetCallingMethodOptions(null), String.Format(format, args));
    }
    
    /// <summary>
    /// Writes an error log.
    /// </summary>
    /// <param name="ex"></param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Error(Exception ex) {
      WriteLog(LoggerEventType.Error, GetCallingMethodOptions(null), GetFullExceptionMessage(ex));
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void WriteLog(LoggerEventType eventType, LoggerOptionsAttribute options, string message) {
      LoggerTarget targets = options.GetTargets(eventType);
      if (targets.HasFlag(LoggerTarget.Console)) {
        ConsoleColor originalColor = Console.ForegroundColor;
        try {
          Console.ForegroundColor = GetConsoleColor(eventType);
          Console.WriteLine(message);
        } finally {
          Console.ForegroundColor = originalColor;
        }
      }
      if (targets.HasFlag(LoggerTarget.EntLib)) {
        try {
          LogEntry entry = new LogEntry();
          entry.Message = message;
          entry.Priority = 10 - (int)eventType;
          entry.Severity = GetTraceEventType(eventType);
          entry.EventId = -1;
          if (!String.IsNullOrEmpty(options.Category)) {
            entry.Categories.Add(options.Category);
          }
          EntLibLogger.Write(entry);
        } catch { }
      }
      if (targets.HasFlag(LoggerTarget.UlsLog)) {
        SPDiagnosticsCategory dCat = new SPDiagnosticsCategory(options.Category, GetTraceSeverity(eventType), GetEventSeverity(eventType));
        SPDiagnosticsService.Local.WriteTrace(0, dCat, GetTraceSeverity(eventType), message);
      }
      if (targets.HasFlag(LoggerTarget.EventLog)) {
        if (!EventLog.SourceExists(options.Category)) {
          EventLog.CreateEventSource(options.Category, options.Category);
          while (!EventLog.SourceExists(options.Category)) {
            Thread.Sleep(100);
          }
        }
        EventLog.WriteEntry(options.Category, message, GetEventLogEntryType(eventType), 0);
      }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static LoggerOptionsAttribute GetCallingMethodOptions(string customCategory) {
      StackFrame[] frames = new StackTrace(2).GetFrames();
      int hashCode = frames.Aggregate(0, (v, a) => v ^ a.GetMethod().GetHashCode() + 13);
      ConcurrentDictionary<MethodBase, LoggerOptionsAttribute> dictionary = attributeCaches.EnsureKeyValue(hashCode);
      LoggerOptionsAttribute attribute = dictionary.EnsureKeyValue(frames[0].GetMethod(), () => GetCallingMethodOptions(dictionary, frames));
      attribute = attribute.Clone();
      attribute.Category = customCategory ?? attribute.Category ?? "General";
      return attribute;
    }

    private static LoggerOptionsAttribute GetCallingMethodOptions(ConcurrentDictionary<MethodBase, LoggerOptionsAttribute> dictionary, StackFrame[] frames) {
      foreach (StackFrame thisFrame in frames) {
        LoggerOptionsAttribute attribute;
        MethodBase method = thisFrame.GetMethod();
        if (dictionary.TryGetValue(method, out attribute)) {
          return attribute;
        }
        attribute = method.GetCustomAttribute<LoggerOptionsAttribute>(true);
        if (attribute != null) {
          dictionary.AddOrUpdate(method, attribute, (m, v) => attribute);
          return attribute;
        }
      }
      return new LoggerOptionsAttribute(LoggerTarget.EntLib | LoggerTarget.UlsLog);
    }

    private static EventLogEntryType GetEventLogEntryType(LoggerEventType eventType) {
      switch (eventType) {
        case LoggerEventType.Information:
        case LoggerEventType.Debug:
          return EventLogEntryType.Information;
        case LoggerEventType.Warning:
          return EventLogEntryType.Warning;
        case LoggerEventType.Error:
          return EventLogEntryType.Error;
        default:
          return EventLogEntryType.Information;
      }
    }

    private static TraceSeverity GetTraceSeverity(LoggerEventType eventType) {
      switch (eventType) {
        case LoggerEventType.Information:
          return TraceSeverity.Medium;
        case LoggerEventType.Debug:
          return TraceSeverity.Verbose;
        case LoggerEventType.Warning:
          return TraceSeverity.Monitorable;
        case LoggerEventType.Error:
          return TraceSeverity.Unexpected;
        default:
          return TraceSeverity.Medium;
      }
    }

    private static EventSeverity GetEventSeverity(LoggerEventType eventType) {
      switch (eventType) {
        case LoggerEventType.Information:
        case LoggerEventType.Debug:
          return EventSeverity.Verbose;
        case LoggerEventType.Warning:
          return EventSeverity.Warning;
        case LoggerEventType.Error:
          return EventSeverity.Error;
        default:
          return EventSeverity.Information;
      }
    }

    private static TraceEventType GetTraceEventType(LoggerEventType eventType) {
      switch (eventType) {
        case LoggerEventType.Information:
          return TraceEventType.Information;
        case LoggerEventType.Debug:
          return TraceEventType.Verbose;
        case LoggerEventType.Warning:
          return TraceEventType.Warning;
        case LoggerEventType.Error:
          return TraceEventType.Error;
        default:
          return TraceEventType.Information;
      }
    }

    private static ConsoleColor GetConsoleColor(LoggerEventType eventType) {
      switch (eventType) {
        case LoggerEventType.Debug:
          return ConsoleColor.Green;
        case LoggerEventType.Warning:
          return ConsoleColor.Yellow;
        case LoggerEventType.Error:
          return ConsoleColor.Red;
        default:
          return Console.ForegroundColor;
      }
    }

    private static string GetFullExceptionMessage(Exception ex) {
      StringBuilder sb = new StringBuilder();
      Stack<Exception> exceptions = new Stack<Exception>();
      for (Exception innerEx = ex; innerEx != null; innerEx = innerEx.InnerException) {
        exceptions.Push(innerEx);
      }
      while (exceptions.Count > 0) {
        Exception innerEx = exceptions.Pop();
        sb.AppendLine(String.Format("{0}: {1} {2}", innerEx.GetType().Name, innerEx.Message, innerEx.StackTrace));
      }
      return sb.ToString();
    }
  }
}
