using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Xml.Linq;

namespace Codeless.SharePoint {
  /// <summary>
  /// Represents type of an event item. This value is stored in the -EventType- field of an event item.
  /// </summary>
  public enum SPRecurrenceEventType {
    /// <summary>
    /// A non-recurrence event.
    /// </summary>
    None = 0,
    /// <summary>
    /// A recurrence event.
    /// </summary>
    Master = 1,
    /// <summary>
    /// Deletion exception to a recurrence event.
    /// </summary>
    Deleted = 3,
    /// <summary>
    /// Reschedule exception to a recurrence event.
    /// </summary>
    Rescheduled = 4
  }

  /// <summary>
  /// Enumerates recurrence according to recurrence rule of an event item.
  /// </summary>
  public abstract class SPRecurrenceEnumerator {
    /// <summary>
    /// Specifies which day(s) of week should be enumerated.
    /// </summary>
    protected enum EnumeratedDayOfWeek {
      /// <summary>
      /// Only Sunday should be enumerated.
      /// </summary>
      Sunday = 0x01,
      /// <summary>
      /// Only Monday should be enumerated.
      /// </summary>
      Monday = 0x02,
      /// <summary>
      /// Only Tuesday should be enumerated.
      /// </summary>
      Tuesday = 0x04,
      /// <summary>
      /// Only Wednesday should be enumerated.
      /// </summary>
      Wednesday = 0x08,
      /// <summary>
      /// Only Thursday should be enumerated.
      /// </summary>
      Thursday = 0x10,
      /// <summary>
      /// Only Friday should be enumerated.
      /// </summary>
      Friday = 0x20,
      /// <summary>
      /// Only Saturday should be enumerated.
      /// </summary>
      Saturday = 0x40,
      /// <summary>
      /// All seven days in a week should be enumerated.
      /// </summary>
      AllDays = 0x7F,
      /// <summary>
      /// Only from Monday to Friday should be enumerated.
      /// </summary>
      Weekdays = 0x7F & ~(0x41),
      /// <summary>
      /// Only Saturday and Sunday should be enumerated.
      /// </summary>
      WeekendDays = 0x41
    };

    /// <summary>
    /// Specifies which occurrence should be enumerated. Occurence in question is up to derived classes which can be day in month, week in month or others.
    /// </summary>
    protected enum EnumeratedOccurenceInPeriod {
      /// <summary>
      /// Only the first occurrence should be enumerated.
      /// </summary>
      First = 1,
      /// <summary>
      /// Only the second occurrence should be enumerated.
      /// </summary>
      Second = 2,
      /// <summary>
      /// Only the third occurrence should be enumerated.
      /// </summary>
      Third = 3,
      /// <summary>
      /// Only the forth occurrence should be enumerated.
      /// </summary>
      Fourth = 4,
      /// <summary>
      /// Only the last occurrence should be enumerated.
      /// </summary>
      Last = -1
    };

    /// <summary>
    /// Mapping table of <see cref="EnumeratedDayOfWeek"/> as represented in recurrence XML.
    /// </summary>
    protected static readonly ReadOnlyDictionary<string, EnumeratedDayOfWeek> EnumeratedDayOfWeekDictionary = (new Dictionary<string, EnumeratedDayOfWeek> {
      { "su", EnumeratedDayOfWeek.Sunday },
      { "mo", EnumeratedDayOfWeek.Monday },
      { "tu", EnumeratedDayOfWeek.Tuesday },
      { "we", EnumeratedDayOfWeek.Wednesday },
      { "th", EnumeratedDayOfWeek.Thursday },
      { "fr", EnumeratedDayOfWeek.Friday },
      { "sa", EnumeratedDayOfWeek.Saturday },
      { "day", EnumeratedDayOfWeek.AllDays },
      { "weekday", EnumeratedDayOfWeek.Weekdays },
      { "weekend_day", EnumeratedDayOfWeek.WeekendDays }
    }).AsReadOnly();

    /// <summary>
    /// Mapping table of <see cref="EnumeratedOccurenceInPeriod"/> as represented in recurrence XML.
    /// </summary>
    protected static readonly ReadOnlyDictionary<string, EnumeratedOccurenceInPeriod> EnumeratedOccurenceInPeriodDictionary = (new Dictionary<string, EnumeratedOccurenceInPeriod> {
      { "first", EnumeratedOccurenceInPeriod.First },
      { "second", EnumeratedOccurenceInPeriod.Second },
      { "third", EnumeratedOccurenceInPeriod.Third },
      { "fourth", EnumeratedOccurenceInPeriod.Fourth },
      { "last", EnumeratedOccurenceInPeriod.Last }
    }).AsReadOnly();

    private readonly XElement element;
    private readonly EnumeratedDayOfWeek enumeratedDayOfWeek;
    private readonly DateTime eventStart;
    private readonly DateTime eventEnd;

    /// <summary>
    /// Constructor of <see cref="SPRecurrenceEnumerator"/>.
    /// </summary>
    /// <param name="element">Input recurrence XML.</param>
    /// <param name="eventStart">Recurrence start date defined by master recurrence item.</param>
    /// <param name="eventEnd">Recurrence end date defined by master recurrence item.</param>
    /// <exception cref="System.ArgumentNullException">Throws when input parameter <paramref name="element"/> is null.</exception>
    public SPRecurrenceEnumerator(XElement element, DateTime eventStart, DateTime eventEnd) {
      CommonHelper.ConfirmNotNull(element, "element");
      this.element = element;
      this.eventStart = eventStart;
      this.eventEnd = eventEnd;

      if (element.Name.LocalName == "monthly" || element.Name.LocalName == "yearly") {
        this.enumeratedDayOfWeek = EnumeratedDayOfWeek.AllDays;
      } else {
        foreach (KeyValuePair<string, EnumeratedDayOfWeek> entry in EnumeratedDayOfWeekDictionary) {
          if (GetFlag(entry.Key)) {
            this.enumeratedDayOfWeek |= entry.Value;
          }
        }
      }
    }

    /// <summary>
    /// When overridden in derived classes, computes the first base date from the recurrence type and recurrence start date as defined.
    /// </summary>
    /// <param name="now">Recurrence start date.</param>
    /// <returns>First base date regards of recurrence type.</returns>
    protected abstract DateTime GetFirstBaseDate(DateTime now);

    /// <summary>
    /// When overridden in derived classes, computes the next base date from recurrence type and current base date, depending on the recurrence type.
    /// </summary>
    /// <param name="now">Current base date.</param>
    /// <returns>Next base date regards of recurrence type.</returns>
    protected abstract DateTime GetNextBaseDate(DateTime now);

    /// <summary>
    /// When overridden in derived classes, enumerates dates from current base date to next base date according to recurrence rule.
    /// </summary>
    /// <param name="baseDate">Current base date.</param>
    /// <returns>Enumerable sequence of recurrences from current base date to next base date.</returns>
    protected abstract IEnumerable<DateTime> EnumerateDates(DateTime baseDate);

    /// <summary>
    /// Enumerates recurrences in specified period according to the recurrence rule.
    /// </summary>
    /// <param name="rangeStart">Start date.</param>
    /// <param name="rangeEnd">End date.</param>
    /// <returns>Enumerable sequence of recurrences in specified period.</returns>
    public IEnumerable<SPRecurrenceDateRange> Enumerate(DateTime rangeStart, DateTime rangeEnd) {
      DateTime baseStart = GetFirstBaseDate(eventStart);
      while (baseStart < rangeEnd) {
        foreach (DateTime dt in EnumerateDates(baseStart)) {
          DateTime st = dt.Date.Add(eventStart.TimeOfDay);
          DateTime et = dt.Date.Add(eventEnd.TimeOfDay);
          if (et < rangeStart || dt.Date < eventStart.Date) {
            continue;
          }
          if (st > rangeEnd || dt.Date > eventEnd.Date) {
            yield break;
          }
          yield return new SPRecurrenceDateRange(st, et);
        }
        baseStart = GetNextBaseDate(baseStart);
      }
    }

    /// <summary>
    /// Creates an instance of <see cref="SPRecurrenceEnumerator"/> from recurrence XML.
    /// </summary>
    /// <param name="value">Input recurrence XML.</param>
    /// <param name="eventStart">Recurrence start date defined by master recurrence item.</param>
    /// <param name="eventEnd">Recurrence end date defined by master recurrence item.</param>
    /// <returns>A <see cref="SPRecurrenceEnumerator"/> instance which recurrence rule are defined by the input XML.</returns>
    public static SPRecurrenceEnumerator ParseRecurrenceData(string value, DateTime eventStart, DateTime eventEnd) {
      XDocument xml = XDocument.Parse(value);
      XElement element = xml.Descendants("repeat").First().Descendants().First();
      switch (element.Name.ToString()) {
        case "daily":
          return new DailyRecurrenceEnumerator(element, eventStart, eventEnd);
        case "weekly":
          return new WeeklyRecurrenceEnumerator(element, eventStart, eventEnd);
        case "monthly":
        case "monthlyByDay":
          return new MonthlyRecurrenceEnumerator(element, eventStart, eventEnd);
        case "yearly":
        case "yearlyByDay":
          return new YearlyRecurrenceEnumerator(element, eventStart, eventEnd);
      }
      return null;
    }

    /// <summary>
    /// Gets the attribute value as an interger from recurrence XML.
    /// </summary>
    /// <param name="attributeName">Attribute name.</param>
    /// <returns>Integer value parsed from XML. 0 if the value does not represent a valid integer.</returns>
    protected int GetInteger(string attributeName) {
      return Math.Max(1, ((int?)element.Attribute(attributeName)).GetValueOrDefault());
    }

    /// <summary>
    /// Gets the attribute value as an boolean from recurrence XML.
    /// </summary>
    /// <param name="attributeName">Attribute name.</param>
    /// <returns>Boolean value parsed from XML. False if the value does not represent a valid integer.</returns>
    protected bool GetFlag(string attributeName) {
      return ((bool?)element.Attribute(attributeName)).GetValueOrDefault();
    }

    /// <summary>
    /// Determines whether the specified date should be enumerated.
    /// </summary>
    /// <param name="date">Date to be checked.</param>
    /// <returns>*true* if the specified date meets recurrence rule and should be enumerated; otherwise *false*.</returns>
    protected bool ShouldEnumerate(DateTime date) {
      return enumeratedDayOfWeek.HasFlag((EnumeratedDayOfWeek)(1 << (int)date.DayOfWeek));
    }
  }

  internal class DailyRecurrenceEnumerator : SPRecurrenceEnumerator {
    private readonly int dayFrequency;
    private readonly bool weekday;

    public DailyRecurrenceEnumerator(XElement element, DateTime eventStart, DateTime eventEnd)
      : base(element, eventStart, eventEnd) {
      this.dayFrequency = GetInteger("dayFrequency");
      this.weekday = GetFlag("weekday");
    }

    protected override DateTime GetFirstBaseDate(DateTime now) {
      return weekday ? now.AddDays(-(int)now.DayOfWeek) : now;
    }

    protected override DateTime GetNextBaseDate(DateTime now) {
      return weekday ? now.AddDays(7) : now.AddDays(dayFrequency);
    }

    protected override IEnumerable<DateTime> EnumerateDates(DateTime baseDate) {
      if (weekday) {
        for (int i = 1; i < 6; i++) {
          yield return baseDate.AddDays(i);
        }
      } else {
        yield return baseDate;
      }
    }
  }

  internal class WeeklyRecurrenceEnumerator : SPRecurrenceEnumerator {
    private readonly int weekFrequency;

    public WeeklyRecurrenceEnumerator(XElement element, DateTime eventStart, DateTime eventEnd)
      : base(element, eventStart, eventEnd) {
      this.weekFrequency = GetInteger("weekFrequency");
    }

    protected override DateTime GetFirstBaseDate(DateTime now) {
      return now.AddDays(-(int)now.DayOfWeek);
    }

    protected override DateTime GetNextBaseDate(DateTime now) {
      return now.AddDays(weekFrequency * 7);
    }

    protected override IEnumerable<DateTime> EnumerateDates(DateTime baseDate) {
      for (int i = 0; i < 7; i++) {
        DateTime dt = baseDate.AddDays(i);
        if (ShouldEnumerate(dt)) {
          yield return dt;
        }
      }
    }
  }

  internal class MonthlyRecurrenceEnumerator : SPRecurrenceEnumerator {
    private readonly bool monthlyByDay;
    private readonly int monthDay;
    private readonly int monthFrequency;
    private readonly EnumeratedOccurenceInPeriod dayIndex;

    public MonthlyRecurrenceEnumerator(XElement element, DateTime eventStart, DateTime eventEnd)
      : base(element, eventStart, eventEnd) {
      if ("monthlyByDay".Equals(element.Name.LocalName)) {
        this.monthlyByDay = true;
        this.dayIndex = EnumeratedOccurenceInPeriodDictionary[(string)element.Attribute("weekdayOfMonth")];
      } else {
        this.monthlyByDay = false;
        this.monthDay = GetInteger("day");
      }
      this.monthFrequency = GetInteger("monthFrequency");
    }

    protected override DateTime GetFirstBaseDate(DateTime now) {
      return new DateTime(now.Year, now.Month, 1);
    }

    protected override DateTime GetNextBaseDate(DateTime now) {
      return now.AddMonths(monthFrequency);
    }

    protected override IEnumerable<DateTime> EnumerateDates(DateTime baseDate) {
      if (monthlyByDay) {
        DateTime date;
        if (dayIndex != EnumeratedOccurenceInPeriod.Last) {
          date = baseDate.Date.AddDays(-1);
          for (int i = 0; i < (int)dayIndex; ) {
            date = date.AddDays(1);
            if (ShouldEnumerate(date)) i++;
          }
        } else {
          date = baseDate.AddMonths(1).AddDays(-1);
          while (!ShouldEnumerate(date)) {
            date = date.AddDays(-1);
          }
        }
        yield return date;
      } else {
        yield return new DateTime(baseDate.Year, baseDate.Month, Math.Min(monthDay, DateTime.DaysInMonth(baseDate.Year, baseDate.Month)));
      }
    }
  }

  internal class YearlyRecurrenceEnumerator : SPRecurrenceEnumerator {
    private readonly bool yearlyByDay;
    private readonly int month;
    private readonly int day;
    private readonly int yearFrequency;
    private readonly EnumeratedOccurenceInPeriod dayIndex;

    public YearlyRecurrenceEnumerator(XElement element, DateTime eventStart, DateTime eventEnd)
      : base(element, eventStart, eventEnd) {
      if ("yearlyByDay".Equals(element.Name.LocalName)) {
        this.yearlyByDay = true;
        this.dayIndex = EnumeratedOccurenceInPeriodDictionary[(string)element.Attribute("weekdayOfMonth")];
      } else {
        this.yearlyByDay = false;
        this.day = GetInteger("day");
      }
      this.month = GetInteger("month");
      this.yearFrequency = GetInteger("yearFrequency");
    }

    protected override DateTime GetFirstBaseDate(DateTime now) {
      return new DateTime(now.Year, 1, 1);
    }

    protected override DateTime GetNextBaseDate(DateTime now) {
      return now.AddYears(yearFrequency);
    }

    protected override IEnumerable<DateTime> EnumerateDates(DateTime baseDate) {
      if (yearlyByDay) {
        DateTime date;
        if (dayIndex != EnumeratedOccurenceInPeriod.Last) {
          date = new DateTime(baseDate.Year, month, 1).AddDays(-1);
          for (int i = 0; i < (int)dayIndex; ) {
            date = date.AddDays(1);
            if (ShouldEnumerate(date)) i++;
          }
        } else {
          date = baseDate.AddMonths(month + 1).AddDays(-1);
          while (!ShouldEnumerate(date)) {
            date = date.AddDays(-1);
          }
        }
        yield return date;
      } else {
        yield return new DateTime(baseDate.Year, month, day);
      }
    }
  }
}
