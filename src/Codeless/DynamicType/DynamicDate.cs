using System;

namespace Codeless.DynamicType {
  public class DynamicDate : DynamicObject {
    private static readonly DateTime UnixEpochUtc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private DateTime value;

    public DynamicDate(DateTime value) {
      this.value = value;
    }

    [DynamicMember("getDate")]
    public DynamicValue GetDate() { return value.Day; }
    [DynamicMember("getDay")]
    public DynamicValue GetDay() { return (int)value.DayOfWeek; }
    [DynamicMember("getFullYear")]
    public DynamicValue GetFullYear() { return value.Year; }
    [DynamicMember("getHours")]
    public DynamicValue GetHours() { return value.Hour; }
    [DynamicMember("getMilliseconds")]
    public DynamicValue GetMilliseconds() { return value.Millisecond; }
    [DynamicMember("getMinutes")]
    public DynamicValue GetMinutes() { return value.Minute; }
    [DynamicMember("getMonth")]
    public DynamicValue GetMonth() { return value.Month - 1; }
    [DynamicMember("getSeconds")]
    public DynamicValue GetSeconds() { return value.Second; }
    [DynamicMember("getTime")]
    public DynamicValue GetTime() { return (value.ToUniversalTime() - UnixEpochUtc).TotalMilliseconds; }
    [DynamicMember("getTimezoneOffset")]
    public DynamicValue GetTimezoneOffset() {
      if (value.Kind == DateTimeKind.Utc) {
        return 0;
      }
      return (value - DateTime.SpecifyKind(value.ToUniversalTime(), DateTimeKind.Local)).Minutes;
    }
    [DynamicMember("getUTCDate")]
    public DynamicValue GetUTCDate() { return value.ToUniversalTime().Day; }
    [DynamicMember("getUTCDay")]
    public DynamicValue GetUTCDay() { return (int)value.ToUniversalTime().DayOfWeek; }
    [DynamicMember("getUTCFullYear")]
    public DynamicValue GetUTCFullYear() { return value.ToUniversalTime().Year; }
    [DynamicMember("getUTCHours")]
    public DynamicValue GetUTCHours() { return value.ToUniversalTime().Hour; }
    [DynamicMember("getUTCMilliseconds")]
    public DynamicValue GetUTCMilliseconds() { return value.ToUniversalTime().Millisecond; }
    [DynamicMember("getUTCMinutes")]
    public DynamicValue GetUTCMinutes() { return value.ToUniversalTime().Year; }
    [DynamicMember("getUTCMonth")]
    public DynamicValue GetUTCMonth() { return value.ToUniversalTime().Minute; }
    [DynamicMember("getUTCSeconds")]
    public DynamicValue GetUTCSeconds() { return value.ToUniversalTime().Month - 1; }
    [DynamicMember("getYear")]
    public DynamicValue GetYear() { return value.ToUniversalTime().Year - 1900; }
    [DynamicMember("setDate")]
    public DynamicValue SetDate(DynamicValue newValue) {
      value = value.AddDays((int)newValue.AsNumber() - value.Day);
      return this.GetTime();
    }
    [DynamicMember("setFullYear")]
    public DynamicValue SetFullYear(DynamicValue newValue) {
      value = value.AddYears((int)newValue.AsNumber() - value.Year);
      return this.GetTime();
    }
    [DynamicMember("setHours")]
    public DynamicValue SetHours(DynamicValue newValue) {
      value = value.AddHours((int)newValue.AsNumber() - value.Hour);
      return this.GetTime();
    }
    [DynamicMember("setMilliseconds")]
    public DynamicValue SetMilliseconds(DynamicValue newValue) {
      value = value.AddMilliseconds((int)newValue.AsNumber() - value.Millisecond);
      return this.GetTime();
    }
    [DynamicMember("setMinutes")]
    public DynamicValue SetMinutes(DynamicValue newValue) {
      value = value.AddMinutes((int)newValue.AsNumber() - value.Minute);
      return this.GetTime();
    }
    [DynamicMember("setMonth")]
    public DynamicValue SetMonth(DynamicValue newValue) {
      value = value.AddMonths((int)newValue.AsNumber() - value.Month);
      return this.GetTime();
    }
    [DynamicMember("setSeconds")]
    public DynamicValue SetSeconds(DynamicValue newValue) {
      value = value.AddSeconds((int)newValue.AsNumber() - value.Second);
      return this.GetTime();
    }
    [DynamicMember("setTime")]
    public DynamicValue SetTime(DynamicValue newValue) {
      value = UnixEpochUtc.AddMilliseconds(newValue.AsNumber()).ToLocalTime();
      return this.GetTime();
    }
    [DynamicMember("setUTCDate")]
    public DynamicValue SetUTCDate(DynamicValue newValue) {
      value = value.ToUniversalTime().AddDays((int)newValue.AsNumber() - value.Day).ToLocalTime();
      return this.GetTime();
    }
    [DynamicMember("setUTCFullYear")]
    public DynamicValue SetUTCFullYear(DynamicValue newValue) {
      value = value.ToUniversalTime().AddYears((int)newValue.AsNumber() - value.Year).ToLocalTime();
      return this.GetTime();
    }
    [DynamicMember("setUTCHours")]
    public DynamicValue SetUTCHours(DynamicValue newValue) {
      value = value.ToUniversalTime().AddHours((int)newValue.AsNumber() - value.Hour).ToLocalTime();
      return this.GetTime();
    }
    [DynamicMember("setUTCMilliseconds")]
    public DynamicValue SetUTCMilliseconds(DynamicValue newValue) {
      value = value.ToUniversalTime().AddMilliseconds((int)newValue.AsNumber() - value.Millisecond).ToLocalTime();
      return this.GetTime();
    }
    [DynamicMember("setUTCMinutes")]
    public DynamicValue SetUTCMinutes(DynamicValue newValue) {
      value = value.ToUniversalTime().AddMinutes((int)newValue.AsNumber() - value.Minute).ToLocalTime();
      return this.GetTime();
    }
    [DynamicMember("setUTCMonth")]
    public DynamicValue SetUTCMonth(DynamicValue newValue) {
      value = value.ToUniversalTime().AddMonths((int)newValue.AsNumber() - value.Month).ToLocalTime();
      return this.GetTime();
    }
    [DynamicMember("setUTCSeconds")]
    public DynamicValue SetUTCSeconds(DynamicValue newValue) {
      value = value.ToUniversalTime().AddSeconds((int)newValue.AsNumber() - value.Second).ToLocalTime();
      return this.GetTime();
    }
    [DynamicMember("setYear")]
    public DynamicValue SetYear(DynamicValue newValue) {
      value = value.AddYears((int)newValue.AsNumber() - value.Year + 1900);
      return this.GetTime();
    }
  }
}
