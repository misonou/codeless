
using System;
using System.ComponentModel;

namespace Codeless.SharePoint {
  /// <summary>
  /// Converts string values stored in site collections to objects of type <typeparamref name="T"/>.
  /// </summary>
  /// <typeparam name="T">Value type.</typeparam>
  public abstract class SiteConfigTypeConverter<T> : TypeConverter {
    /// <summary>
    /// Converts the specified text to an object of type <typeparamref name="T"/>.
    /// </summary>
    /// <param name="value">The text representation of the object to convert.</param>
    /// <returns>An object of type <typeparamref name="T"/> that represents the converted text.</returns>
    protected new abstract T ConvertFromString(string value);

    /// <summary>
    /// Overridden.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="sourceType"></param>
    /// <returns></returns>
    public sealed override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType) {
      return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }

    #region Sealed methods
    /// <summary>
    /// Overridden.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="destinationType"></param>
    /// <returns></returns>
    public sealed override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType) {
      return base.CanConvertTo(context, destinationType);
    }

    /// <summary>
    /// Overridden.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="culture"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public sealed override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value) {
      if (value is string) {
        return ConvertFromString(value.ToString());
      }
      return base.ConvertFrom(context, culture, value);
    }

    /// <summary>
    /// Overridden.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="culture"></param>
    /// <param name="value"></param>
    /// <param name="destinationType"></param>
    /// <returns></returns>
    public sealed override object ConvertTo(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destinationType) {
      return base.ConvertTo(context, culture, value, destinationType);
    }

    /// <summary>
    /// Overridden.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="propertyValues"></param>
    /// <returns></returns>
    public sealed override object CreateInstance(ITypeDescriptorContext context, System.Collections.IDictionary propertyValues) {
      return base.CreateInstance(context, propertyValues);
    }

    /// <summary>
    /// Overridden.
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public sealed override bool GetCreateInstanceSupported(ITypeDescriptorContext context) {
      return base.GetCreateInstanceSupported(context);
    }

    /// <summary>
    /// Overridden.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="value"></param>
    /// <param name="attributes"></param>
    /// <returns></returns>
    public sealed override PropertyDescriptorCollection GetProperties(ITypeDescriptorContext context, object value, Attribute[] attributes) {
      return base.GetProperties(context, value, attributes);
    }
    
    /// <summary>
    /// Overridden.
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public sealed override bool GetPropertiesSupported(ITypeDescriptorContext context) {
      return base.GetPropertiesSupported(context);
    }
    
    /// <summary>
    /// Overridden.
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public sealed override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context) {
      return base.GetStandardValues(context);
    }

    /// <summary>
    /// Overridden.
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public sealed override bool GetStandardValuesExclusive(ITypeDescriptorContext context) {
      return base.GetStandardValuesExclusive(context);
    }

    /// <summary>
    /// Overridden.
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public sealed override bool GetStandardValuesSupported(ITypeDescriptorContext context) {
      return base.GetStandardValuesSupported(context);
    }

    /// <summary>
    /// Overridden.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public sealed override bool IsValid(ITypeDescriptorContext context, object value) {
      return base.IsValid(context, value);
    }
    #endregion
  }
}
