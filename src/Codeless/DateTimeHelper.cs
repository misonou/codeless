using System;
using System.Diagnostics;

namespace Codeless {
  /// <summary>
  /// Provides conversions between ECMAScript and Unix timestamps to and from <see cref="DateTime"/> objects.
  /// </summary>
  public static class DateTimeHelper {
    private static readonly DateTime UnixEpochUtc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Converts a JavaScript timestamp to a <see cref="DateTime"/> object.
    /// </summary>
    /// <param name="timestamp">A JavaScript timestamp.</param>
    /// <param name="kind">Kind of <see cref="DateTime"/> object.</param>
    /// <returns>A <see cref="DateTime"/> object representing the same moment of that of the supplied timestamp.</returns>
    [DebuggerStepThrough]
    public static DateTime FromJavaScriptTimestamp(long timestamp, DateTimeKind kind) {
      DateTime d = UnixEpochUtc.AddMilliseconds(timestamp);
      if (kind == DateTimeKind.Local) {
        return d.ToLocalTime();
      }
      return d;
    }

    /// <summary>
    /// Converts a Unix timestamp to a <see cref="DateTime"/> object.
    /// </summary>
    /// <param name="timestamp">A Unix timestamp.</param>
    /// <param name="kind">Kind of <see cref="DateTime"/> object.</param>
    /// <returns>A <see cref="DateTime"/> object representing the same moment of that of the supplied timestamp.</returns>
    [DebuggerStepThrough]
    public static DateTime FromUnixTimestamp(long timestamp, DateTimeKind kind) {
      DateTime d = UnixEpochUtc.AddSeconds(timestamp);
      if (kind == DateTimeKind.Local) {
        return d.ToLocalTime();
      }
      return d;
    }

    /// <summary>
    /// Converts a <see cref="DateTime"/> object to a JavaScript timestamp.
    /// </summary>
    /// <param name="d">A <see cref="DateTime"/> object.</param>
    /// <returns>A JavaScript timestamp representing the same moment of that of the <see cref="DateTime"/> object.</returns>
    [DebuggerStepThrough]
    public static long ToJavaScriptTimestamp(this DateTime d) {
      if (d.Kind == DateTimeKind.Utc) {
        return Convert.ToInt64((d - UnixEpochUtc).TotalMilliseconds);
      }
      return Convert.ToInt64((d.ToUniversalTime() - UnixEpochUtc).TotalMilliseconds);
    }

    /// <summary>
    /// Converts a <see cref="DateTime"/> object to a Unix timestamp.
    /// </summary>
    /// <param name="d">A <see cref="DateTime"/> object.</param>
    /// <returns>A Unix timestamp representing the same moment of that of the <see cref="DateTime"/> object.</returns>
    [DebuggerStepThrough]
    public static long ToUnixTimestamp(this DateTime d) {
      if (d.Kind == DateTimeKind.Utc) {
        return Convert.ToInt64((d - UnixEpochUtc).TotalSeconds);
      }
      return Convert.ToInt64((d.ToUniversalTime() - UnixEpochUtc).TotalSeconds);
    }
  }
}
