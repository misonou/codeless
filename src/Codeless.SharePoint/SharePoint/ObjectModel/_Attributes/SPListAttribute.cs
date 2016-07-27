using Microsoft.SharePoint;
using System;
using System.Diagnostics;

namespace Codeless.SharePoint.ObjectModel {
  /// <summary>
  /// Specifies which items users are allowed to read.
  /// </summary>
  public enum SPListReadSecurity {
    /// <summary>
    /// Users can read all items.
    /// </summary>
    All = 1,
    /// <summary>
    /// Users can read items that were created by the user.
    /// </summary>
    Owner = 2
  }

  /// <summary>
  /// Specifies which items users are allowed to create and edit.
  /// </summary>
  public enum SPListWriteSecurity {
    /// <summary>
    /// Users can create and edit all items.
    /// </summary>
    All = 1,
    /// <summary>
    /// Users can create items and edit items that were created by the user.
    /// </summary>
    Owner = 2,
    /// <summary>
    /// Users cannot create or edit items.
    /// </summary>
    None = 4
  }

  /// <summary>
  /// Obsolete. Previously specifies the behavior of <see cref="SPModelManager{T}"/> on this list defintion.
  /// </summary> 
  [Obsolete]
  public enum SPListProvisionMode {
    /// <summary>
    /// Obsolete.
    /// </summary>
    Manual,
    /// <summary>
    /// Obsolete.
    /// </summary>
    AutomaticInNonHosted,
    /// <summary>
    /// Obsolete.
    /// </summary>
    Automatic
  }

  /// <summary>
  /// Represents the definition of a list to be created during provisioning.
  /// </summary>
  [AttributeUsage(AttributeTargets.Class)]
  [DebuggerDisplay("{Url}")]
  public class SPListAttribute : Attribute {
    /// <summary>
    /// Initializes a new instance of the <see cref="SPListAttribute"/> class.
    /// </summary>
    public SPListAttribute()
      : this(String.Empty) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SPListAttribute"/> class with the specified location.
    /// </summary>
    /// <param name="url"></param>
    public SPListAttribute(string url)
      : this(url, SPListTemplateType.GenericList) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SPListAttribute"/> class with the specified location and list type.
    /// </summary>
    /// <param name="url"></param>
    /// <param name="listTemplateType"></param>
    public SPListAttribute(string url, SPListTemplateType listTemplateType) {
      this.Url = url;
      this.ListTemplateType = listTemplateType;
      this.ReadSecurity = SPListReadSecurity.All;
      this.WriteSecurity = SPListWriteSecurity.All;
      this.DraftVersionVisibility = DraftVisibilityType.Author;
      this.Description = String.Empty;
      this.Direction = "ltr";
    }

    /// <summary>
    /// Gets or sets the site-relative URL where the list to be created.
    /// </summary>
    public string Url { get; set; }
    /// <summary>
    /// Gets the type of the list.
    /// </summary>
    public SPListTemplateType ListTemplateType { get; private set; }
    /// <summary>
    /// Obsolete.
    /// </summary>
    [Obsolete]
    public SPListProvisionMode ProvisionMode { get; set; }

    /// <summary>
    /// Gets or sets the title of the list.
    /// </summary>
    public string Title { get; set; }
    /// <summary>
    /// Gets or sets the description of the list.
    /// </summary>
    public string Description { get; set; }
    /// <summary>
    /// Gets or sets the text direction of the list.
    /// </summary>
    public string Direction { get; set; }
    /// <summary>
    /// Gets or sets the query expression of the default view of the list.
    /// </summary>
    public string DefaultViewQuery { get; set; }

    /// <summary>
    /// Gets or sets whether attachment is enabled.
    /// </summary>
    public SPOption EnableAttachments { get; set; }
    /// <summary>
    /// Gets or sets whether folder is allowed in the list.
    /// </summary>
    public SPOption EnableFolderCreation { get; set; }
    /// <summary>
    /// Gets or sets whether to create a draft version when items are edited.
    /// </summary>
    public SPOption EnableMinorVersions { get; set; }
    /// <summary>
    /// Gets or sets whether content approval is required.
    /// </summary>
    public SPOption EnableModeration { get; set; }
    /// <summary>
    /// Gets or sets whether to create a major version when created when items are edited.
    /// </summary>
    public SPOption EnableVersioning { get; set; }
    /// <summary>
    /// Gets or sets who can see draft items.
    /// </summary>
    public DraftVisibilityType DraftVersionVisibility { get; set; }

    /// <summary>
    /// Obsolete.
    /// </summary>
    [Obsolete]
    public bool RootWebOnly { get; set; }
    /// <summary>
    /// Gets or sets whether this list is linked from Quick Launch area.
    /// </summary>
    public bool OnQuickLaunch { get; set; }
    /// <summary>
    /// Gets or sets what items can be read by uses.
    /// </summary>
    public SPListReadSecurity ReadSecurity { get; set; }
    /// <summary>
    /// Gets or sets what items can be created or edited by uses.
    /// </summary>
    public SPListWriteSecurity WriteSecurity { get; set; }

    internal SPListAttribute Clone() {
      return (SPListAttribute)this.MemberwiseClone();
    }

    internal SPListAttribute Clone(string url) {
      SPListAttribute other = this.Clone();
      other.Url = url;
      return other;
    }
  }
}
