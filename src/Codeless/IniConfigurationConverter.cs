using System;
using System.ComponentModel;
using System.Globalization;

namespace Codeless {
  /// <summary>
  /// Provides a type converter to convert string to <see cref="IniConfiguration"/> objects.
  /// </summary>
  public sealed class IniConfigurationConverter : TypeConverter {
    /// <summary>
    /// Overriden. <see cref="TypeConverter.CanConvertFrom(ITypeDescriptorContext,Type)"/>.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="sourceType"></param>
    /// <returns></returns>
    public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType) {
      return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }

    /// <summary>
    /// Overriden. <see cref="TypeConverter.ConvertFrom(ITypeDescriptorContext,CultureInfo,object)"/>.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="culture"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value) {
      if (value is string) {
        return IniConfiguration.Parse((string)value);
      }
      return base.ConvertFrom(context, culture, value);
    }
  }
}
