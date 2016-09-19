using System;

namespace Codeless.SharePoint {
  /// <summary>
  /// Represents a version number of a list item.
  /// </summary>
  public struct SPItemVersion : IEquatable<SPItemVersion>, IComparable<SPItemVersion>, IConvertible {
    private readonly int version;

    /// <summary>
    /// Creates a representation of version number from a internal integer value stored by SharePoint.
    /// </summary>
    /// <param name="version">Internal version number.</param>
    public SPItemVersion(int version) {
      this.version = version;
    }
    
    /// <summary>
    /// Creates a representation of version number with the given major and minor version number.
    /// </summary>
    /// <param name="majorVersion">Major version number.</param>
    /// <param name="minorVersion">Minor version number.</param>
    /// <exception cref="System.ArgumentOutOfRangeException">Throws when input parameter <paramref name="minorVersion"/> does not fall between 0 and 511 inclusive.</exception>
    public SPItemVersion(int majorVersion, int minorVersion) {
      if (minorVersion > 0x1FF) {
        throw new ArgumentOutOfRangeException("Minor version must fall between 0 and 511 inclusive", "minorVersion");
      }
      this.version = (majorVersion << 9) | (minorVersion & 0x1FF);
    }

    /// <summary>
    /// Creates a representation of version number from a human-readeable version string.
    /// </summary>
    /// <param name="versionString">Version string.</param>
    /// <exception cref="System.ArgumentNullException">Throws when input parameter <paramref name="versionString"/> is null.</exception>
    /// <exception cref="System.ArgumentException">Throws when input version string does not contain a correct representation of a version number.</exception>
    public SPItemVersion(string versionString) {
      CommonHelper.ConfirmNotNull(versionString, "versionString");
      try {
        int dotPos = versionString.IndexOf('.');
        if (dotPos > 0) {
          int majorVersion = Int32.Parse(versionString.Substring(0, dotPos));
          int minorVersion = Int32.Parse(versionString.Substring(dotPos + 1));
          if (minorVersion <= 0x1FF) {
            this.version = (majorVersion << 9) | (minorVersion & 0x1FF);
            return;
          }
        }
      } catch { }
      throw new ArgumentException("Invalid version string", "versionString");
    }

    /// <summary>
    /// Returns the major version number represented by this instance.
    /// </summary>
    public int MajorVersion {
      get { return version >> 9; }
    }

    /// <summary>
    /// Returns the minor version number represented by this instance.
    /// </summary>
    public int MinorVersion {
      get { return version & 0x1FF; }
    }

    /// <summary>
    /// Returns *true* if the version number represents a major version.
    /// </summary>
    public bool IsMajorVersion {
      get { return this.MinorVersion == 0; }
    }

    /// <summary>
    /// Returns *true* if the version number represents a minor version.
    /// </summary>
    public bool IsMinorVersion {
      get { return this.MinorVersion != 0; }
    }

    /// <summary>
    /// Compares with another version number.
    /// Version numbers are compared against major version and then minor version
    /// </summary>
    /// <param name="other">Version number to compare.</param>
    /// <returns>Returns -1 if the version number precedes the given one; 1 if the version number succedes the given one; or 0 if the version number is identical to the given one.</returns>
    public int CompareTo(SPItemVersion other) {
      return version.CompareTo(other.version);
    }

    /// <summary>
    /// Determines whether the version number is identical to the given one.
    /// </summary>
    /// <param name="other">Version number to compare.</param>
    /// <returns>*true* if the version number is identical to the given one; otherwise *false*.</returns>
    public bool Equals(SPItemVersion other) {
      return version.Equals(other.version);
    }

    /// <summary>
    /// Determines whether this instance and a specified object, which must also be a <see cref="SPItemVersion"/> object, have the same value. 
    /// </summary>
    /// <param name="obj">The object to compare to this instance.</param>
    /// <returns>*true* if <paramref name="obj"/> is a <see cref="SPItemVersion"/> and its value is the same as this instance; otherwise, *false*.</returns>
    public override bool Equals(object obj) {
      if (obj is SPItemVersion) {
        return Equals((SPItemVersion)obj);
      }
      return base.Equals(obj);
    }

    /// <summary>
    /// Returns the hash code for this object.
    /// </summary>
    /// <returns>A 32-bit signed integer hash code.</returns>
    public override int GetHashCode() {
      return version.GetHashCode();
    }

    /// <summary>
    /// Returns a human-readable version string that represents the version number of this instance.
    /// </summary>
    /// <returns>A human-readable version string.</returns>
    public override string ToString() {
      return String.Concat(this.MajorVersion, ".", this.MinorVersion);
    }

    /// <summary>
    /// Determines whether two specified version number have the same value.
    /// </summary>
    /// <param name="x">The first version number to compare.</param>
    /// <param name="y">The second version number to compare.</param>
    /// <returns>*true* if the value of <paramref name="x"/> is the same as the value of <paramref name="y"/>; otherwise, *false*.</returns>
    public static bool operator ==(SPItemVersion x, SPItemVersion y) {
      return x.Equals(y);
    }

    /// <summary>
    /// Determines whether two specified version number have different values.
    /// </summary>
    /// <param name="x">The first version number to compare.</param>
    /// <param name="y">The second version number to compare.</param>
    /// <returns>*true* if the value of <paramref name="x"/> is different to the value of <paramref name="y"/>; otherwise, *false*.</returns>
    public static bool operator !=(SPItemVersion x, SPItemVersion y) {
      return !x.Equals(y);
    }

    /// <summary>
    /// Returns a value that indicates whether a version number is greater than or equal to another version number.
    /// </summary>
    /// <param name="x">The first version number to compare.</param>
    /// <param name="y">The second version number to compare.</param>
    /// <returns>*true* if the value of <paramref name="x"/> is greater than or equal to the value of <paramref name="y"/>; otherwise, *false*.</returns>
    public static bool operator >=(SPItemVersion x, SPItemVersion y) {
      return x.CompareTo(y) >= 0;
    }

    /// <summary>
    /// Returns a value that indicates whether a version number is less than or equal to another version number.
    /// </summary>
    /// <param name="x">The first version number to compare.</param>
    /// <param name="y">The second version number to compare.</param>
    /// <returns>*true* if the value of <paramref name="x"/> is less than or equal to the value of <paramref name="y"/>; otherwise, *false*.</returns>
    public static bool operator <=(SPItemVersion x, SPItemVersion y) {
      return x.CompareTo(y) <= 0;
    }

    /// <summary>
    /// Returns a value that indicates whether a version number is greater than another version number.
    /// </summary>
    /// <param name="x">The first version number to compare.</param>
    /// <param name="y">The second version number to compare.</param>
    /// <returns>*true* if the value of <paramref name="x"/> is greater than the value of <paramref name="y"/>; otherwise, *false*.</returns>
    public static bool operator >(SPItemVersion x, SPItemVersion y) {
      return x.CompareTo(y) > 0;
    }

    /// <summary>
    /// Returns a value that indicates whether a version number is less than another version number.
    /// </summary>
    /// <param name="x">The first version number to compare.</param>
    /// <param name="y">The second version number to compare.</param>
    /// <returns>*true* if the value of <paramref name="x"/> is less than the value of <paramref name="y"/>; otherwise, *false*.</returns>
    public static bool operator <(SPItemVersion x, SPItemVersion y) {
      return x.CompareTo(y) < 0;
    }

    #region IConvertible
    TypeCode IConvertible.GetTypeCode() {
      return TypeCode.Int32;
    }

    bool IConvertible.ToBoolean(IFormatProvider provider) {
      throw new InvalidCastException();
    }

    byte IConvertible.ToByte(IFormatProvider provider) {
      throw new InvalidCastException();
    }

    char IConvertible.ToChar(IFormatProvider provider) {
      throw new InvalidCastException();
    }

    DateTime IConvertible.ToDateTime(IFormatProvider provider) {
      throw new InvalidCastException();
    }

    decimal IConvertible.ToDecimal(IFormatProvider provider) {
      return version;
    }

    double IConvertible.ToDouble(IFormatProvider provider) {
      return version;
    }

    short IConvertible.ToInt16(IFormatProvider provider) {
      throw new InvalidCastException();
    }

    int IConvertible.ToInt32(IFormatProvider provider) {
      return version;
    }

    long IConvertible.ToInt64(IFormatProvider provider) {
      return version;
    }

    sbyte IConvertible.ToSByte(IFormatProvider provider) {
      throw new InvalidCastException();
    }

    float IConvertible.ToSingle(IFormatProvider provider) {
      return version;
    }

    string IConvertible.ToString(IFormatProvider provider) {
      return this.ToString();
    }

    object IConvertible.ToType(Type conversionType, IFormatProvider provider) {
      throw new InvalidCastException();
    }

    ushort IConvertible.ToUInt16(IFormatProvider provider) {
      throw new InvalidCastException();
    }

    uint IConvertible.ToUInt32(IFormatProvider provider) {
      return (uint)version;
    }

    ulong IConvertible.ToUInt64(IFormatProvider provider) {
      return (ulong)version;
    }
    #endregion
  }
}
