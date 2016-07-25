using System;

namespace Codeless.SharePoint {
  /// <summary>
  /// Represents a date range for a recurrence.
  /// </summary>
  public struct SPRecurrenceDateRange : IEquatable<SPRecurrenceDateRange>, IComparable<SPRecurrenceDateRange> {
    /// <summary>
    /// Creates an <see cref="SPRecurrenceDateRange"/> instance.
    /// </summary>
    /// <param name="start">Start time of a date range.</param>
    /// <param name="end">End time of a date range.</param>
    public SPRecurrenceDateRange(DateTime start, DateTime end)
      : this() {
      StartDate = start;
      EndDate = end;
    }

    /// <summary>
    /// Start time of this recurrence.
    /// </summary>
    public DateTime StartDate { get; private set; }

    /// <summary>
    /// End time of this recurrence.
    /// </summary>
    public DateTime EndDate { get; private set; }

    /// <summary>
    /// Determines whether two specified date range have the same value.
    /// </summary>
    /// <param name="x">The first date range to compare.</param>
    /// <param name="y">The second date range to compare.</param>
    /// <returns>*true* if the value of <paramref name="x"/> is the same as the value of <paramref name="y"/>; otherwise, *false*.</returns>
    public static bool operator ==(SPRecurrenceDateRange x, SPRecurrenceDateRange y) {
      return x.StartDate == y.StartDate && x.EndDate == y.EndDate;
    }

    /// <summary>
    /// Determines whether two specified date range have different values.
    /// </summary>
    /// <param name="x">The first date range to compare.</param>
    /// <param name="y">The second date range to compare.</param>
    /// <returns>*true* if the value of <paramref name="x"/> is different to the value of <paramref name="y"/>; otherwise, *false*.</returns>
    public static bool operator !=(SPRecurrenceDateRange x, SPRecurrenceDateRange y) {
      return x.StartDate != y.StartDate || x.EndDate != y.EndDate;
    }

    /// <summary>
    /// Returns a value that indicates whether a date range is less than or equal to another date range.
    /// </summary>
    /// <param name="x">The first date range to compare.</param>
    /// <param name="y">The second date range to compare.</param>
    /// <returns>*true* if the value of <paramref name="x"/> is less than or equal to the value of <paramref name="y"/>; otherwise, *false*.</returns>
    public static bool operator <=(SPRecurrenceDateRange x, SPRecurrenceDateRange y) {
      return x.CompareTo(y) <= 0;
    }

    /// <summary>
    /// Returns a value that indicates whether a date range is less than another date range.
    /// </summary>
    /// <param name="x">The first date range to compare.</param>
    /// <param name="y">The second date range to compare.</param>
    /// <returns>*true* if the value of <paramref name="x"/> is less than the value of <paramref name="y"/>; otherwise, *false*.</returns>
    public static bool operator <(SPRecurrenceDateRange x, SPRecurrenceDateRange y) {
      return x.CompareTo(y) < 0;
    }

    /// <summary>
    /// Returns a value that indicates whether a date range is greater than or equal to another date range.
    /// </summary>
    /// <param name="x">The first date range to compare.</param>
    /// <param name="y">The second date range to compare.</param>
    /// <returns>*true* if the value of <paramref name="x"/> is greater than or equal to the value of <paramref name="y"/>; otherwise, *false*.</returns>
    public static bool operator >=(SPRecurrenceDateRange x, SPRecurrenceDateRange y) {
      return x.CompareTo(y) >= 0;
    }

    /// <summary>
    /// Returns a value that indicates whether a date range is greater than another date range.
    /// </summary>
    /// <param name="x">The first date range to compare.</param>
    /// <param name="y">The second date range to compare.</param>
    /// <returns>*true* if the value of <paramref name="x"/> is greater than the value of <paramref name="y"/>; otherwise, *false*.</returns>
    public static bool operator >(SPRecurrenceDateRange x, SPRecurrenceDateRange y) {
      return x.CompareTo(y) > 0;
    }

    /// <summary>
    /// Determines whether the period of this recurrence overlaps with the given recurrence.
    /// </summary>
    /// <param name="other">An <see cref="SPRecurrenceDateRange"/> instance to compare.</param>
    /// <returns>*true* if this recurrence overlaps with the given recurrence. otherwise *false*.</returns>
    public bool Overlaps(SPRecurrenceDateRange other) {
      return (Math.Max(this.StartDate.Ticks, other.StartDate.Ticks) < Math.Min(this.EndDate.Ticks, other.EndDate.Ticks));
    }

    /// <summary>
    /// Determines the equality of this instance to the given instance.
    /// Two date ranges are considered equal if and only if both start and end time are equal.
    /// </summary>
    /// <param name="other">An <see cref="SPRecurrenceDateRange"/> instance to compare.</param>
    /// <returns>*true* if this recurrence is the same with the given recurrence. otherwise *false*.</returns>
    public bool Equals(SPRecurrenceDateRange other) {
      return this == other;
    }

    /// <summary>
    /// Compares with another date range.
    /// A date range with later start date is considered greater. If start dates of two isntances are equal, a date range with later end date is considered greater.
    /// </summary>
    /// <param name="other">Date range to compare.</param>
    /// <returns>Returns -1 if the version number precedes the given one; 1 if the version number succedes the given one; or 0 if the version number is identical to the given one.</returns>
    public int CompareTo(SPRecurrenceDateRange other) {
      int result = this.StartDate.CompareTo(other.StartDate);
      if (result == 0) {
        return this.EndDate.CompareTo(other.EndDate);
      }
      return result;
    }

    /// <summary>
    /// Overriden. When <paramref name="obj"/> is an <see cref="SPRecurrenceDateRange"/> instance, the custom equality comparison is performed.
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override bool Equals(object obj) {
      if (obj is SPRecurrenceDateRange) {
        return Equals((SPRecurrenceDateRange)obj);
      }
      return base.Equals(obj);
    }

    /// <summary>
    /// Overriden. Computes hash code by the start and end time.
    /// </summary>
    /// <returns></returns>
    public override int GetHashCode() {
      return this.StartDate.GetHashCode() ^ this.EndDate.GetHashCode();
    }
  }
}
