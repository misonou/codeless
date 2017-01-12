using Codeless.SharePoint.Internal;
using Microsoft.SharePoint;
using System;
using System.Collections.Specialized;
using System.Diagnostics;

namespace Codeless.SharePoint.ObjectModel {
  /// <summary>
  /// Specifies the location of the lookup list.
  /// </summary>
  public enum SPFieldLookupSource {
    /// <summary>
    /// Does not specify the location of the lookup list.
    /// </summary>
    None,
    /// <summary>
    /// Lookup the same list.
    /// </summary>
    Self,
    /// <summary>
    /// Lookup a list that is in the same site.
    /// </summary>
    SiteList,
    /// <summary>
    /// Lookup a list that is in the root site.
    /// </summary>
    SiteCollectionList
  }

  /// <summary>
  /// Specifies whether a column should be shown.
  /// </summary>
  public enum SPFieldFormVisibility {
    /// <summary>
    /// Keep the current configuration.
    /// </summary>
    Unspecified,
    /// <summary>
    /// The column is hidden in all places.
    /// </summary>
    Hidden,
    /// <summary>
    /// The column is hidden in create and edit form.
    /// </summary>
    DisplayOnly,
    /// <summary>
    /// The column is hidden only in edit form.
    /// </summary>
    ExceptEditForm,
    /// <summary>
    /// The column is hidden only in create form.
    /// </summary>
    ExceptNewForm,
    /// <summary>
    /// The column is visible in all places.
    /// </summary>
    Visible
  }

  internal enum SPFieldProvisionMode {
    Default,
    FieldLink,
    None
  }

  /// <summary>
  /// Represents a SharePoint column that is referenced by an SPModel class.
  /// </summary>
  [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = true)]
  [DebuggerDisplay("{InternalName}")]
  public abstract class SPFieldAttribute : Attribute, IEquatable<SPFieldAttribute> {
    /// <summary>
    /// Initializes a new instance of the <see cref="SPFieldAttribute"/> class with the specified internal name and column type.
    /// </summary>
    /// <param name="internalName">The internal name of a column.</param>
    /// <param name="fieldType">The field type of a column.</param>
    public SPFieldAttribute(string internalName, SPFieldType fieldType)
      : this(internalName, fieldType.ToString()) {
      this.Type = fieldType;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SPFieldAttribute"/> class with the specified internal name and a custom column type.
    /// </summary>
    /// <param name="internalName">The internal name of a column.</param>
    /// <param name="fieldType">The field type of a column.</param>
    public SPFieldAttribute(string internalName, string fieldType) {
      CommonHelper.ConfirmNotNull(internalName, "internalName");
      CommonHelper.ConfirmNotNull(fieldType, "fieldType");
      this.InternalName = internalName;
      this.ListFieldInternalName = internalName;
      this.TypeAsString = fieldType;
      this.IncludeInQuery = true;
      this.FormVisibility = SPFieldFormVisibility.Visible;
    }

    /// <summary>
    /// Gets or sets the unique ID of the column when a fixed unique ID is desirable.
    /// </summary>
    public Guid ID { get; set; }
    /// <summary>
    /// Gets the type of the column.
    /// </summary>
    public SPFieldType Type { get; private set; }
    /// <summary>
    /// Gets the custom type of the column.
    /// </summary>
    public string TypeAsString { get; private set; }
    /// <summary>
    /// Gets the internal name of the column.
    /// </summary>
    public string InternalName { get; private set; }
    /// <summary>
    /// Gets or sets the internal name of the column when added to a list.
    /// </summary>
    public string ListFieldInternalName { get; protected set; }

    /// <summary>
    /// Gets or sets whether this column is associated with the attributed property when generating queries through LINQ interface. 
    /// If this property is set to *false*, the column will not be retrieved in queries unless <see cref="IncludeInViewFields"/> is set to *true*.
    /// </summary>
    public bool IncludeInQuery { get; set; }
    /// <summary>
    /// Gets or sets whether this column should be retrieved when <see cref="IncludeInQuery"/> is set to *false*.
    /// </summary>
    public bool IncludeInViewFields { get; set; }
    /// <summary>
    /// Gets ot sets the column order in content types and list views.
    /// </summary>
    public int ColumnOrder { get; set; }
    /// <summary>
    /// Gets or sets whether this column is shown in list views.
    /// </summary>
    public SPOption ShowInListView { get; set; }
    /// <summary>
    /// Gets or sets whether this column should be crawled by Office search.
    /// </summary>
    public SPOption NoCrawl { get; set; }

    internal SPFieldProvisionMode ProvisionMode { get; set; }

    /// <summary>
    /// Gets or sets the display name of the column.
    /// </summary>
    public string Title { get; set; }
    /// <summary>
    /// Gets or sets the description of the column.
    /// </summary>
    public string Description { get; set; }
    /// <summary>
    /// Gets or sets the group name of the column.
    /// </summary>
    public string Group { get; set; }
    /// <summary>
    /// Gets or sets the default value of the column.
    /// </summary>
    public virtual object DefaultValue { get; set; }
    /// <summary>
    /// Gets or sets the default formula of the column.
    /// </summary>
    public string DefaultFormula { get; set; }

    /// <summary>
    /// Sets where this column should be shown.
    /// </summary>
    public SPFieldFormVisibility FormVisibility {
      get {
        if (this.Hidden == SPOption.True) {
          return SPFieldFormVisibility.Hidden;
        }
        if (this.ReadOnlyField == SPOption.True) {
          return SPFieldFormVisibility.DisplayOnly;
        }
        if (this.ShowInEditForm == SPOption.True && this.ShowInNewForm == SPOption.True) {
          return SPFieldFormVisibility.Visible;
        }
        if (this.ShowInEditForm == SPOption.True) {
          return SPFieldFormVisibility.ExceptNewForm;
        }
        if (this.ShowInNewForm == SPOption.True) {
          return SPFieldFormVisibility.ExceptEditForm;
        }
        return SPFieldFormVisibility.Unspecified;
      }
      set {
        if (value == SPFieldFormVisibility.Unspecified) {
          this.ShowInDisplayForm = SPOption.Unspecified;
          this.ShowInEditForm = SPOption.Unspecified;
          this.ShowInNewForm = SPOption.Unspecified;
          this.ShowInViewForms = SPOption.Unspecified;
          this.ShowInVersionHistory = SPOption.Unspecified;
          this.Hidden = SPOption.Unspecified;
          this.ReadOnlyField = SPOption.Unspecified;
        } else {
          this.ShowInDisplayForm = (value != SPFieldFormVisibility.Hidden) ? SPOption.True : SPOption.False;
          this.ShowInEditForm = (value != SPFieldFormVisibility.Hidden && value != SPFieldFormVisibility.DisplayOnly && value != SPFieldFormVisibility.ExceptEditForm) ? SPOption.True : SPOption.False;
          this.ShowInNewForm = (value != SPFieldFormVisibility.Hidden && value != SPFieldFormVisibility.DisplayOnly && value != SPFieldFormVisibility.ExceptNewForm) ? SPOption.True : SPOption.False;
          this.ShowInViewForms = (value != SPFieldFormVisibility.Hidden) ? SPOption.True : SPOption.False;
          this.ShowInVersionHistory = (value != SPFieldFormVisibility.Hidden) ? SPOption.True : SPOption.False;
          this.Hidden = (value == SPFieldFormVisibility.Hidden) ? SPOption.True : SPOption.False;
          this.ReadOnlyField = (value == SPFieldFormVisibility.Hidden || value == SPFieldFormVisibility.DisplayOnly) ? SPOption.True : SPOption.False;
        }
      }
    }

    /// <summary>
    /// Gets whether this column will be shown in item display form.
    /// </summary>
    public SPOption ShowInDisplayForm { get; private set; }
    /// <summary>
    /// Gets whether this column will be shown in item edit form.
    /// </summary>
    public SPOption ShowInEditForm { get; private set; }
    /// <summary>
    /// Gets whether this column will be shown in item new form.
    /// </summary>
    public SPOption ShowInNewForm { get; private set; }
    /// <summary>
    /// Gets whether this column will be shown in view forms.
    /// </summary>
    public SPOption ShowInViewForms { get; private set; }
    /// <summary>
    /// Gets whether this column will be shown in version history dialog.
    /// </summary>
    public SPOption ShowInVersionHistory { get; private set; }
    /// <summary>
    /// Gets whether this column is a hidden column.
    /// </summary>
    public SPOption Hidden { get; private set; }
    /// <summary>
    /// Gets whether this column can be edited by users.
    /// </summary>
    public SPOption ReadOnlyField { get; private set; }

    /// <summary>
    /// Gets or sets whether this column is required.
    /// </summary>
    public SPOption Required { get; set; }
    /// <summary>
    /// Gets or sets whether this column is indexed.
    /// </summary>
    public SPOption Indexed { get; set; }
    /// <summary>
    /// Gets or sets whether this column has unique value for each item in a list.
    /// </summary>
    public SPOption EnforceUniqueValues { get; set; }

    /// <summary>
    /// Determines if this instance refers to the same column as by the other instance compared by the internal name.
    /// </summary>
    /// <param name="other">Instance to compare.</param>
    /// <returns>*true* if this instance refers to the same column; otherwise *false*.</returns>
    public bool Equals(SPFieldAttribute other) {
      return this.InternalName.Equals(other.InternalName);
    }

    /// <summary>
    /// Overriden.
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override bool Equals(object obj) {
      SPFieldAttribute other = CommonHelper.TryCastOrDefault<SPFieldAttribute>(obj);
      if (other != null) {
        return Equals(other);
      }
      return false;
    }

    /// <summary>
    /// Overriden.
    /// </summary>
    /// <returns></returns>
    public override int GetHashCode() {
      return this.InternalName.GetHashCode();
    }

    internal SPFieldAttribute Clone() {
      return (SPFieldAttribute)this.MemberwiseClone();
    }

    internal SPFieldAttribute Clone(SPFieldProvisionMode provisionMode) {
      SPFieldAttribute value = this.Clone();
      if (provisionMode > value.ProvisionMode) {
        value.ProvisionMode = provisionMode;
      }
      return value;
    }
  }

  #region Implemented attributes
  /// <summary>
  /// Represents an existing column that is referenced by an SPModel class.
  /// </summary>
  public class SPBuiltInFieldAttribute : SPFieldAttribute {
    /// <summary>
    /// Initializes a new instance of the <see cref="SPBuiltInFieldAttribute"/> class with the specified internal name.
    /// </summary>
    /// <param name="internalName">The internal name of a column.</param>
    public SPBuiltInFieldAttribute(string internalName)
      : base(internalName, GetFieldType(internalName)) {
      this.FormVisibility = SPFieldFormVisibility.Unspecified;
    }

    /// <summary>
    /// Gets or sets the internal name used when the column is added to a list.
    /// </summary>
    public string Alias {
      get { return this.ListFieldInternalName; }
      set { this.ListFieldInternalName = value; }
    }

    /// <summary>
    /// A no-op.
    /// </summary>
    public new string Group {
      get { return null; }
    }

    /// <summary>
    /// A no-op.
    /// </summary>
    public new string TypeAsString {
      get { return null; }
    }

    private static SPFieldType GetFieldType(string internalName) {
      SPFieldType value;
      if (KnownFields.FieldTypeDictionary.TryGetValue(internalName, out value)) {
        return value;
      }
      return SPFieldType.Invalid;
    }
  }

  /// <summary>
  /// Represents an integer column that is referenced by an SPModel class.
  /// </summary>
  public class SPIntegerFieldAttribute : SPFieldAttribute {
    /// <summary>
    /// Initializes a new instance of the <see cref="SPIntegerFieldAttribute"/> class with the specified internal name.
    /// </summary>
    /// <param name="internalName">The internal name of a column.</param>
    public SPIntegerFieldAttribute(string internalName)
      : base(internalName, SPFieldType.Integer) {
      this.MinimumValue = Int32.MinValue;
      this.MaximumValue = Int32.MaxValue;
    }

    /// <summary>
    /// Gets or sets the display format.
    /// </summary>
    public SPNumberFormatTypes DisplayFormat { set; get; }
    /// <summary>
    /// Gets or sets the maximum value allowed.
    /// </summary>
    public double MaximumValue { set; get; }
    /// <summary>
    /// Gets or sets the minimum value allowed.
    /// </summary>
    public double MinimumValue { set; get; }
    /// <summary>
    /// Gets or sets whether value is shown as a percentage.
    /// </summary>
    public SPOption ShowAsPercentage { set; get; }
  }

  /// <summary>
  /// Represents a text column that is referenced by an SPModel class.
  /// </summary>
  public class SPTextFieldAttribute : SPFieldAttribute {
    /// <summary>
    /// Initializes a new instance of the <see cref="SPTextFieldAttribute"/> class with the specified internal name.
    /// </summary>
    /// <param name="internalName">The internal name of a column.</param>
    public SPTextFieldAttribute(string internalName)
      : base(internalName, SPFieldType.Text) { }
  }

  /// <summary>
  /// Represents a multiline text column that is referenced by an SPModel class.
  /// </summary>
  public class SPNoteFieldAttribute : SPFieldAttribute {
    /// <summary>
    /// Initializes a new instance of the <see cref="SPNoteFieldAttribute"/> class with the specified internal name.
    /// </summary>
    /// <param name="internalName">The internal name of a column.</param>
    public SPNoteFieldAttribute(string internalName)
      : base(internalName, SPFieldType.Note) {
      this.NumberOfLines = 10;
    }

    /// <summary>
    /// Gets or sets the number of lines of the editing field.
    /// </summary>
    public int NumberOfLines { get; set; }
    /// <summary>
    /// Gets or sets whether the rich-text funtionalities is restricted.
    /// </summary>
    public SPOption RestrictedMode { get; set; }
    /// <summary>
    /// Gets or sets whether the rich-text mode is on.
    /// </summary>
    public SPOption RichText { get; set; }
    /// <summary>
    /// Gets or sets whether values longer than 255 characters is accepted.
    /// </summary>
    public SPOption UnlimitedLengthInDocumentLibrary { get; set; }
    /// <summary>
    /// Gets or sets the rich-text formatting mode.
    /// </summary>
    public SPRichTextMode RichTextMode { get; set; }
  }

  /// <summary>
  /// Represents a Date and Time column that is referenced by an SPModel class.
  /// </summary>
  public class SPDateTimeFieldAttribute : SPFieldAttribute {
    /// <summary>
    /// Initializes a new instance of the <see cref="SPDateTimeFieldAttribute"/> class with the specified internal name.
    /// </summary>
    /// <param name="internalName">The internal name of a column.</param>
    public SPDateTimeFieldAttribute(string internalName)
      : base(internalName, SPFieldType.DateTime) { }

    /// <summary>
    /// Gets or sets the calendar type.
    /// </summary>
    public SPCalendarType CalendarType { set; get; }
    /// <summary>
    /// Gets or sets the format to use in displaying dates and times.
    /// </summary>
    public SPDateTimeFieldFormatType DisplayFormat { set; get; }
  }

  /// <summary>
  /// Represents a choice column that is referenced by an SPModel class.
  /// </summary>
  public class SPChoiceFieldAttribute : SPFieldAttribute {
    private readonly StringCollection choices = new StringCollection();
    private readonly Type enumType;

    /// <summary>
    /// Initializes a new instance of the <see cref="SPChoiceFieldAttribute"/> class with the specified internal name.
    /// </summary>
    /// <param name="internalName">The internal name of a column.</param>
    public SPChoiceFieldAttribute(string internalName, Type enumType)
      : base(internalName, SPFieldType.Choice) {
      CommonHelper.ConfirmNotNull(enumType, "enumType");
      foreach (string choice in Enum.GetNames(enumType)) {
        this.choices.Add(choice);
      }
      this.enumType = enumType;
      this.DefaultValue = 0;
    }

    /// <summary>
    /// Gets or sets how options for how to display selections in a choice field.
    /// </summary>
    public SPChoiceFormatType EditFormat { set; get; }
    /// <summary>
    /// Gets the list of choices.
    /// </summary>
    public StringCollection Choices { get { return choices; } }

    /// <summary>
    /// Gets or sets the default value.
    /// </summary>
    public override object DefaultValue {
      get {
        return base.DefaultValue;
      }
      set {
        string defaultValue = Enum.GetName(enumType, value);
        base.DefaultValue = choices.Contains(defaultValue) ? defaultValue : String.Empty;
      }
    }
  }

  /// <summary>
  /// Represents a multiple choice column that is referenced by an SPModel class.
  /// </summary>
  public class SPMultiChoiceFieldAttribute : SPFieldAttribute {
    private readonly StringCollection choices;

    /// <summary>
    /// Initializes a new instance of the <see cref="SPMultiChoiceFieldAttribute"/> class with the specified internal name.
    /// </summary>
    /// <param name="internalName">The internal name of a column.</param>
    public SPMultiChoiceFieldAttribute(string internalName)
      : base(internalName, "MultiChoice") { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SPMultiChoiceFieldAttribute"/> class with the specified internal name and available choices.
    /// </summary>
    /// <param name="internalName">The internal name of a column.</param>
    /// <param name="choices">A list of available choices.</param>
    public SPMultiChoiceFieldAttribute(string internalName, string[] choices)
      : base(internalName, "MultiChoice") {
      CommonHelper.ConfirmNotNull(choices, "choices");
      this.choices = new StringCollection();
      foreach (string choice in choices) {
        this.choices.Add(choice);
      }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SPMultiChoiceFieldAttribute"/> class with the specified internal name and available choices enumerated by an Enum type.
    /// </summary>
    /// <param name="internalName">The internal name of a column.</param>
    /// <param name="enumType">An Enum type.</param>
    public SPMultiChoiceFieldAttribute(string internalName, Type enumType)
      : base(internalName, "MultiChoice") {
      CommonHelper.ConfirmNotNull(enumType, "enumType");
      this.choices = new StringCollection();
      foreach (string choice in Enum.GetNames(enumType)) {
        if ((int)Enum.Parse(enumType, choice) != 0) {
          this.choices.Add(choice);
        }
      }
    }

    /// <summary>
    /// Gets the list of choices.
    /// </summary>
    public StringCollection Choices { get { return choices; } }
  }

  /// <summary>
  /// Represents a calculated column that is referenced by an SPModel class.
  /// </summary>
  public class SPCalculatedFieldAttribute : SPFieldAttribute {
    /// <summary>
    /// Initializes a new instance of the <see cref="SPCalculatedFieldAttribute"/> class with the specified internal name, result type and formula.
    /// </summary>
    /// <param name="internalName">The internal name of a column.</param>
    /// <param name="outputType">The result type of the formula.</param>
    /// <param name="formula">The formula of the calculated column.</param>
    public SPCalculatedFieldAttribute(string internalName, SPFieldType outputType, string formula)
      : base(internalName, SPFieldType.Calculated) {
      Formula = formula;
      OutputType = outputType;
    }

    /// <summary>
    /// Gets or sets the formula.
    /// </summary>
    public string Formula { get; set; }
    /// <summary>
    /// Gets or sets the result type of the formula.
    /// </summary>
    public SPFieldType OutputType { get; set; }
    /// <summary>
    /// Gets or sets the format to use in displaying dates and times if the result type is a Date and Time value.
    /// </summary>
    public SPDateTimeFieldFormatType DateFormat { set; get; }
    /// <summary>
    /// Gets or sets the format to use in displaying numbers if the result type is a number.
    /// </summary>
    public SPNumberFormatTypes DisplayFormat { get; set; }
    /// <summary>
    /// Gets or sets whether value is shown as a percentage if the result type is a number.
    /// </summary>
    public SPOption ShowAsPercentage { set; get; }
    /// <summary>
    /// Gets or sets the currency used if the result type is a number.
    /// </summary>
    public int CurrencyLocaleId { set; get; }
  }

  /// <summary>
  /// Represents a lookup column that is referenced by an SPModel class.
  /// </summary>
  public class SPLookupFieldAttribute : SPFieldAttribute {
    /// <summary>
    /// Initializes a new instance of the <see cref="SPLookupFieldAttribute"/> class with the specified internal name.
    /// </summary>
    /// <param name="internalName">The internal name of a column.</param>
    public SPLookupFieldAttribute(string internalName)
      : base(internalName, SPFieldType.Lookup) {
      this.LookupField = "Title";
    }

    /// <summary>
    /// Gets or sets the column to be displayed.
    /// </summary>
    public string LookupField { get; set; }
    /// <summary>
    /// Gets or sets the foreign list URL.
    /// </summary>
    public string LookupListUrl { get; set; }
    /// <summary>
    /// Gets or sets the location of the foreign list.
    /// </summary>
    public SPFieldLookupSource LookupSource { get; set; }
    /// <summary>
    /// Gets or sets whether multiple values are allowed.
    /// </summary>
    public SPOption AllowMultipleValues { get; set; }
  }

  /// <summary>
  /// Represents a Yes/No column that is referenced by an SPModel class.
  /// </summary>
  public class SPBooleanFieldAttribute : SPFieldAttribute {
    /// <summary>
    /// Initializes a new instance of the <see cref="SPBooleanFieldAttribute"/> class with the specified internal name.
    /// </summary>
    /// <param name="internalName">The internal name of a column.</param>
    public SPBooleanFieldAttribute(string internalName)
      : base(internalName, SPFieldType.Boolean) { }

    /// <summary>
    /// Gets or sets the default value. To set the default value, supplies *true* or *false*.
    /// </summary>
    public override object DefaultValue {
      get { return base.DefaultValue; }
      set { base.DefaultValue = (true.Equals(value) ? "1" : "0"); }
    }
  }

  /// <summary>
  /// Represents a number column that is referenced by an SPModel class.
  /// </summary>
  public class SPNumberFieldAttribute : SPFieldAttribute {
    /// <summary>
    /// Initializes a new instance of the <see cref="SPNumberFieldAttribute"/> class with the specified internal name.
    /// </summary>
    /// <param name="internalName">The internal name of a column.</param>
    public SPNumberFieldAttribute(string internalName)
      : base(internalName, SPFieldType.Number) {
      this.MinimumValue = -9.00719925E15;
      this.MaximumValue = 9.00719925E15;
    }

    /// <summary>
    /// Gets or sets the display format.
    /// </summary>
    public SPNumberFormatTypes DisplayFormat { set; get; }
    /// <summary>
    /// Gets or sets the maximum value allowed.
    /// </summary>
    public double MaximumValue { set; get; }
    /// <summary>
    /// Gets or sets the minimum value allowed.
    /// </summary>
    public double MinimumValue { set; get; }
    /// <summary>
    /// Gets or sets whether value is shown as a percentage.
    /// </summary>
    public SPOption ShowAsPercentage { set; get; }
  }

  /// <summary>
  /// Represents a currency column that is referenced by an SPModel class.
  /// </summary>
  public class SPCurrencyFieldAttribute : SPFieldAttribute {
    /// <summary>
    /// Initializes a new instance of the <see cref="SPCurrencyFieldAttribute"/> class with the specified internal name.
    /// </summary>
    /// <param name="internalName">The internal name of a column.</param>
    public SPCurrencyFieldAttribute(string internalName)
      : base(internalName, SPFieldType.Currency) { }

    /// <summary>
    /// Gets or sets the currency used.
    /// </summary>
    public int CurrencyLocaleId { set; get; }
    /// <summary>
    /// Gets or sets whether value is shown as a percentage if the result type is a number.
    /// </summary>
    public SPOption ShowAsPercentage { set; get; }
  }

  /// <summary>
  /// Represents a URL column that is referenced by an SPModel class.
  /// </summary>
  public class SPUrlFieldAttribute : SPFieldAttribute {
    /// <summary>
    /// Initializes a new instance of the <see cref="SPUrlFieldAttribute"/> class with the specified internal name.
    /// </summary>
    /// <param name="internalName">The internal name of a column.</param>
    public SPUrlFieldAttribute(string internalName)
      : base(internalName, SPFieldType.URL) { }

    /// <summary>
    /// Gets or sets the format used.
    /// </summary>
    public SPUrlFieldFormatType DisplayFormat { set; get; }
  }

  /// <summary>
  /// Represents a people column that is referenced by an SPModel class.
  /// </summary>
  public class SPUserFieldAttribute : SPFieldAttribute {
    /// <summary>
    /// Initializes a new instance of the <see cref="SPUserFieldAttribute"/> class with the specified internal name.
    /// </summary>
    /// <param name="internalName">The internal name of a column.</param>
    public SPUserFieldAttribute(string internalName)
      : base(internalName, SPFieldType.User) { }

    /// <summary>
    /// Gets or sets the SharePoint group ID.
    /// </summary>
    public int SelectionGroup { set; get; }
    /// <summary>
    /// Gets or sets the selection mode.
    /// </summary>
    public SPFieldUserSelectionMode SelectionMode { set; get; }
    /// <summary>
    /// Gets or sets whether entering multiple values is allowed.
    /// </summary>
    public SPOption AllowMultipleValues { get; set; }
  }

  /// <summary>
  /// Represents a GUID column that is referenced by an SPModel class.
  /// </summary>
  public class SPGuidFieldAttribute : SPFieldAttribute {
    /// <summary>
    /// Initializes a new instance of the <see cref="SPGuidFieldAttribute"/> class with the specified internal name.
    /// </summary>
    /// <param name="internalName">The internal name of a column.</param>
    public SPGuidFieldAttribute(string internalName)
      : base(internalName, SPFieldType.Guid) { }
  }

  /// <summary>
  /// Represents a managed metadata column that is referenced by an SPModel class.
  /// </summary>
  public class TaxonomyFieldAttribute : SPFieldAttribute {
    /// <summary>
    /// Initializes a new instance of the <see cref="TaxonomyFieldAttribute"/> class with the specified internal name.
    /// </summary>
    /// <param name="internalName">The internal name of a column.</param>
    public TaxonomyFieldAttribute(string internalName)
      : this(internalName, String.Empty, String.Empty, String.Empty) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="TaxonomyFieldAttribute"/> class with the specified internal name and the term set information.
    /// </summary>
    /// <param name="internalName">The internal name of a column.</param>
    public TaxonomyFieldAttribute(string internalName, string termSetId, string groupName, string termSetName)
      : base(internalName, "TaxonomyFieldType") {
      DefaultGroupName = groupName;
      DefaultTermSetName = termSetName;
      TermSetId = termSetId;
    }

    /// <summary>
    /// Gets or sets the group name to be created if the term set does not exist.
    /// </summary>
    public string DefaultGroupName { get; set; }
    /// <summary>
    /// Gets or sets the term set name to be created if the term set does not exist.
    /// </summary>
    public string DefaultTermSetName { get; set; }
    /// <summary>
    /// Gets or sets the term set unique identifier.
    /// </summary>
    public string TermSetId { get; set; }
    /// <summary>
    /// Gets or sets whether entering multiple values is allowed.
    /// </summary>
    public SPOption AllowMultipleValues { get; set; }
  }

  /// <summary>
  /// Represents an HTML column that is referenced by an SPModel class.
  /// </summary>
  public class PublishingHtmlFieldAttribute : SPFieldAttribute {
    /// <summary>
    /// Initializes a new instance of the <see cref="PublishingHtmlFieldAttribute"/> class with the specified internal name.
    /// </summary>
    /// <param name="internalName">The internal name of a column.</param>
    public PublishingHtmlFieldAttribute(string internalName)
      : base(internalName, "HTML") {
      this.RichText = SPOption.True;
      this.RichTextMode = SPRichTextMode.FullHtml;
    }

    /// <summary>
    /// Gets or sets whether rich-text formatting is allowed.
    /// </summary>
    public SPOption RichText { set; get; }
    /// <summary>
    /// Gets or sets whether rich-text formatting is restricted.
    /// </summary>
    public SPOption RestrictedMode { set; get; }
    /// <summary>
    /// Gets or sets the rich-text formatting.
    /// </summary>
    public SPRichTextMode RichTextMode { set; get; }
  }

  /// <summary>
  /// Represents a publishing image column that is referenced by an SPModel class.
  /// </summary>
  public class PublishingImageFieldAttribute : SPFieldAttribute {
    /// <summary>
    /// Initializes a new instance of the <see cref="PublishingImageFieldAttribute"/> class with the specified internal name.
    /// </summary>
    /// <param name="internalName">The internal name of a column.</param>
    public PublishingImageFieldAttribute(string internalName)
      : base(internalName, "Image") { }
  }
  #endregion
}
