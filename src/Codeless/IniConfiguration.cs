using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Codeless {
  /// <summary>
  /// Specifies behaviors when parsing from or converting to an INI-formatted string.
  /// </summary>
  [Flags]
  public enum IniConfigurationFormat {
    /// <summary>
    /// Forces double-quoting of values when converting to an INI-formatted string.
    /// </summary>
    ForceQuote = 1,
    /// <summary>
    /// Sorts entries alphabetically by their keys when converting to an INI-formatted string.
    /// </summary>
    SortKey = 2,
    /// <summary>
    /// Writes comments when converting to an INI-formatted string.
    /// </summary>
    PreserveComment = 4
  }

  /// <summary>
  /// Represents a section in an INI-formatted string.
  /// </summary>
  public sealed class IniConfigurationSection : NameValueCollection {
    private readonly Dictionary<string, string> comments;
    private string sectionComment;

    internal IniConfigurationSection(string name) {
      this.Name = name;
      this.comments = new Dictionary<string, string>();
    }

    internal IniConfigurationSection(IniConfigurationSection collection)
      : base(collection) {
      this.Name = collection.Name;
      this.sectionComment = collection.sectionComment;
      this.comments = new Dictionary<string, string>(collection.comments);
    }

    /// <summary>
    /// Gets the name of the section represented by the instance.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// Gets the comment of the section represented by the instance.
    /// </summary>
    /// <returns>The comment of the section.</returns>
    public string GetComment() {
      return sectionComment;
    }

    /// <summary>
    /// Gets the comment associated with the specified key.
    /// </summary>
    /// <param name="key">The key of comment to get</param>
    /// <returns></returns>
    public string GetComment(string key) {
      CommonHelper.ConfirmNotNull(key, "key");
      string value;
      if (comments.TryGetValue(key, out value)) {
        return value;
      }
      return String.Empty;
    }

    /// <summary>
    /// Sets the comment of the section represented by the instance.
    /// </summary>
    /// <param name="value">The comment of the section to set.</param>
    public void SetComment(string value) {
      sectionComment = value;
    }

    /// <summary>
    /// Sets the comment associated with the specified key.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    public void SetComment(string key, string value) {
      CommonHelper.ConfirmNotNull(key, "key");
      comments[key] = value;
    }

    /// <summary>
    /// Removes the comment associated with the specified key.
    /// </summary>
    /// <param name="key"></param>
    public void RemoveComment(string key) {
      CommonHelper.ConfirmNotNull(key, "key");
      comments.Remove(key);
    }
  }

  /// <summary>
  /// Represents a collection of keys and values that is parsed from or to be converted to an INI-formatted string.
  /// </summary>
  public sealed class IniConfiguration : NameObjectCollectionBase, IFormattable {
    /// <summary>
    /// Instantiates a new instance of the <see cref="IniConfiguration"/> class that is empty.
    /// </summary>
    public IniConfiguration() {
      this.DefaultSection = new IniConfigurationSection("");
    }

    /// <summary>
    /// Instantiates a new instance of the <see cref="IniConfiguration"/> class with the specified keys and values.
    /// </summary>
    /// <param name="collection"></param>
    public IniConfiguration(NameValueCollection collection)
      : this() {
      this.DefaultSection.Add(collection);
    }

    /// <summary>
    /// Instantiates a new instance of the <see cref="IniConfiguration"/> class and copy all entries from the specified instance.
    /// </summary>
    /// <param name="other"></param>
    public IniConfiguration(IniConfiguration other)
      : this(other.DefaultSection) {
      foreach (IniConfigurationSection e in other) {
        BaseAdd(e.Name, new IniConfigurationSection(e));
      }
    }

    /// <summary>
    /// Gets a collection containing the sections.
    /// </summary>
    public ICollection<string> Sections {
      get { return BaseGetAllKeys(); }
    }

    /// <summary>
    /// Gets the default section where entries where rendered before any section identifiers in an INI-formatted string.
    /// </summary>
    public IniConfigurationSection DefaultSection { get; private set; }

    /// <summary>
    /// Adds a new section of the specified name to the collection.
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public IniConfigurationSection AddSection(string name) {
      CommonHelper.ConfirmNotNull(name, "name");
      if (BaseGet(name) != null) {
        throw new ArgumentException("The section of the specified name has already been added.", "name");
      }
      BaseAdd(name, new IniConfigurationSection(name));
      return (IniConfigurationSection)BaseGet(name);
    }

    /// <summary>
    /// Gets the section of the specified name.
    /// </summary>
    /// <param name="name">The name of the section to get.</param>
    /// <returns>The section of the specified name.</returns>
    public IniConfigurationSection GetSection(string name) {
      CommonHelper.ConfirmNotNull(name, "name");
      return (IniConfigurationSection)BaseGet(name);
    }

    /// <summary>
    /// Removes the section of the specified name from the collection.
    /// </summary>
    /// <param name="name">The name of the section to remove.</param>
    public void RemoveSection(string name) {
      CommonHelper.ConfirmNotNull(name, "name");
      BaseRemove(name);
    }

    /// <summary>
    /// Removes all entries and sections in the collection.
    /// </summary>
    public void Clear() {
      DefaultSection.Clear();
      BaseClear();
    }

    /// <summary>
    /// Parse an INI-formatted formatted string and creates a new instance of the <see cref="IniConfiguration"/> class with the parsed keys and values.
    /// </summary>
    /// <param name="data">An INI-formatted formatted string.</param>
    /// <returns>A key-value collection that contains key-value pairs parsed from the specified string.</returns>
    public static IniConfiguration Parse(string data) {
      IniConfiguration iniData = new IniConfiguration();
      IniConfigurationSection currentSection = iniData.DefaultSection;

      using (StringReader reader = new StringReader(data)) {
        StringBuilder cb = new StringBuilder();
        StringBuilder sb = new StringBuilder();
        while (reader.Peek() > 0) {
          sb.Clear();
          do {
            string line = reader.ReadLine();
            if (line.Length > 0) {
              if (line[line.Length - 1] != '\\') {
                sb.Append(line);
                break;
              }
              sb.Append(Environment.NewLine);
              sb.Append(line.Substring(0, line.Length - 1).TrimStart());
            }
          } while (reader.Peek() > 0);

          string entry = sb.ToString().Trim();
          if (entry.Length == 0) {
            continue;
          }
          if (entry[0] == ';' || entry[0] == '#') {
            cb.AppendLine(entry.Substring(1));
          } else if (entry[0] == '[' && entry[entry.Length - 1] == ']') {
            currentSection = iniData.AddSection(entry.Substring(1, entry.Length - 2));
            if (cb.Length > 0) {
              currentSection.SetComment(cb.ToString().TrimEnd());
            }
            cb.Clear();
          } else {
            int equalSignPos = entry.IndexOf('=');
            if (equalSignPos >= 0) {
              string key = entry.Substring(0, equalSignPos).TrimEnd();
              string value = Regex.Unescape(entry.Substring(equalSignPos + 1).TrimStart());
              if (value.Length > 1 && value[0] == '"' && value[value.Length - 1] == '"') {
                value = value.Substring(1, value.Length - 2);
              }
              currentSection.Add(key, value);
              if (cb.Length > 0) {
                currentSection.SetComment(key, cb.ToString().TrimEnd());
              }
            }
            cb.Clear();
          }
        }
        return iniData;
      }
    }

    /// <summary>
    /// Converts the collection to an INI-formatted formatted string with the default options.
    /// </summary>
    /// <returns>An INI-formatted formatted string that contains the keys and values in the collection.</returns>
    public override string ToString() {
      return ToString(0);
    }

    /// <summary>
    /// Converts the collection to an INI-formatted formatted string with the specified options.
    /// </summary>
    /// <param name="format"></param>
    /// <returns></returns>
    public string ToString(string format) {
      return ToString(format, null);
    }

    /// <summary>
    /// Converts the collection to an INI-formatted formatted string with the specified options.
    /// </summary>
    /// <param name="format"></param>
    /// <param name="formatProvider"></param>
    /// <returns></returns>
    public string ToString(string format, IFormatProvider formatProvider) {
      IniConfigurationFormat f = 0;
      if (format != null) {
        if (format.IndexOfAny(new[] { 'q', 'Q' }) >= 0) {
          f |= IniConfigurationFormat.ForceQuote;
        }
        if (format.IndexOfAny(new[] { 's', 'S' }) >= 0) {
          f |= IniConfigurationFormat.SortKey;
        }
        if (format.IndexOfAny(new[] { 'c', 'C' }) >= 0) {
          f |= IniConfigurationFormat.PreserveComment;
        }
      }
      return ToString(f);
    }

    /// <summary>
    /// Converts the collection to an INI-formatted formatted string with the specified options.
    /// </summary>
    /// <param name="format"></param>
    /// <returns>An INI-formatted formatted string that contains the keys and values in the collection.</returns>
    public string ToString(IniConfigurationFormat format) {
      StringBuilder sb = new StringBuilder();
      WriteSection(sb, this.DefaultSection, format);
      foreach (string section in this) {
        WriteSection(sb, (IniConfigurationSection)BaseGet(section), format);
      }
      return sb.ToString();
    }

    private static void WriteSection(StringBuilder sb, IniConfigurationSection section, IniConfigurationFormat format) {
      IniConfigurationSection n = new IniConfigurationSection(section);
      if (section.Name != String.Empty && sb.Length > 0) {
        sb.AppendLine();
      }
      if (format.HasFlag(IniConfigurationFormat.PreserveComment)) {
        WriteComment(sb, section.GetComment());
      }
      if (section.Name != String.Empty) {
        sb.Append('[');
        sb.Append(section.Name);
        sb.AppendLine("]");
      }
      string[] keys = new string[section.AllKeys.Length];
      Array.Copy(section.AllKeys, keys, keys.Length);
      if (format.HasFlag(IniConfigurationFormat.SortKey)) {
        Array.Sort(keys);
      }
      foreach (string key in keys) {
        if (format.HasFlag(IniConfigurationFormat.PreserveComment)) {
          WriteComment(sb, n.GetComment(key));
          n.RemoveComment(key);
        }
        foreach (string value in section.GetValues(key)) {
          var hasQuote = format.HasFlag(IniConfigurationFormat.ForceQuote) || value.IndexOf('"') >= 0;
          var quotedValue = hasQuote ? value.Replace("\"", "\\\"") : value;
          var isNewLine = false;
          sb.Append(key);
          sb.Append('=');
          if (hasQuote) {
            sb.Append('"');
          }
          foreach (string line in quotedValue.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)) {
            if (isNewLine) {
              sb.Append('\\');
              sb.AppendLine();
            } else {
              isNewLine = true;
            }
            sb.Append(line);
          }
          if (hasQuote) {
            sb.Append('"');
          }
          sb.AppendLine();
        }
      }
    }

    private static void WriteComment(StringBuilder sb, string comment) {
      if (!String.IsNullOrWhiteSpace(comment)) {
        foreach (string line in comment.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)) {
          sb.AppendLine(';' + line);
        }
      }
    }
  }
}
