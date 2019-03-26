using Microsoft.SharePoint;
using System;
using System.Diagnostics;

namespace Codeless.SharePoint.ObjectModel {
  /// <summary>
  /// Declares the attributed class to represent a content type.
  /// </summary>
  [Serializable]
  [AttributeUsage(AttributeTargets.Class)]
  [DebuggerDisplay("{Name}, ID = {contentTypeId}")]
  public sealed class SPContentTypeAttribute : Attribute {
    private SPContentTypeId contentTypeId;

    private SPContentTypeAttribute(string contentTypeId, string name, bool dummy) {
      CommonHelper.ConfirmNotNull(contentTypeId, "contentTypeId");
      this.ContentTypeIdString = contentTypeId;
      this.Name = name;
      this.Description = String.Empty;
    }

    /// <summary>
    /// Constructs an instance of <see cref="SPContentTypeAttribute"/> with a full or partial content type ID.
    /// If content type ID supplied is partial, it must be in the form of 32 consecutive characters of hexadecimal numerics (0-9, A-F).
    /// When the full content type ID is being resolved, the value will be concatenated to the resolved content type ID of the base class with the content type ID separator "00".
    /// If the base class is <see cref="SPModel"/>, "0x01" is assumed to be the parent content type ID.
    /// </summary>
    /// <param name="contentTypeId">Full or partial content type ID.</param>
    /// <param name="name">Name of the content type.</param>
    public SPContentTypeAttribute(string contentTypeId, string name)
      : this(CommonHelper.ConfirmNotNull(contentTypeId, "contentTypeId").StartsWith("0x") ? contentTypeId : String.Concat("00", contentTypeId), name, true) { }

    /// <summary>
    /// Constructs an instance of <see cref="SPContentTypeAttribute"/> with a two-digit partial content type ID.
    /// When the full content type ID is being resolved, the value will be formatted as a two-digit hexadecimal number an concatenated to the resolved content type ID of the base class.
    /// If the base class is <see cref="SPModel"/>, "0x01" is assumed to be the parent content type ID.
    /// </summary>
    /// <param name="specifier">A two-byte integer value which will be formatted as a two-digit hexadecimal number.</param>
    /// <param name="name">Name of the content type.</param>
    /// <exception cref="ArgumentException">Throws when <paramref name="specifier"/> equals to zero.</exception>
    public SPContentTypeAttribute(ushort specifier, string name)
      : this(specifier.ToString("X2"), name, true) {
      if (specifier == 0) {
        throw new ArgumentException("Two-digit content type specifier cannot be zero (\"00\")", "specifier");
      }
    }

    /// <summary>
    /// Constructs an instance of <see cref="SPContentTypeAttribute"/> with a parent content type ID and a partial content type ID.
    /// When the full content type ID is being resolved, the two content type ID will be concatenated with with the content type ID separator "00".
    /// </summary>
    /// <param name="parentContentTypeId">Parent content type ID.</param>
    /// <param name="guid">Partial content type ID.</param>
    /// <param name="name">Name of the content type.</param>
    public SPContentTypeAttribute(string parentContentTypeId, string guid, string name)
      : this(String.Concat(CommonHelper.ConfirmNotNull(parentContentTypeId, "parentContentTypeId"), "00", CommonHelper.ConfirmNotNull(guid, "guid")), name, true) { }

    /// <summary>
    /// Constructs an instance of <see cref="SPContentTypeAttribute"/> with a parent content type ID and a partial content type ID.
    /// When the full content type ID is being resolved, the value will be formatted as a two-digit hexadecimal number an concatenated to the parent content type ID specified.
    /// </summary>
    /// <param name="parentContentTypeId">Parent content type ID.</param>
    /// <param name="specifier">A two-byte integer value which will be formatted as a two-digit hexadecimal number.</param>
    /// <param name="name">Name of the content type.</param>
    public SPContentTypeAttribute(string parentContentTypeId, ushort specifier, string name)
      : this(String.Concat(CommonHelper.ConfirmNotNull(parentContentTypeId, "parentContentTypeId"), specifier.ToString("X2")), name, true) { }

    /// <summary>
    /// Gets the partial content type ID attributed to the class. 
    /// The partial content type ID is used to construct a full content type ID returned by <see cref="SPContentTypeAttribute.ContentTypeId"/>.
    /// </summary>
    public string ContentTypeIdString { get; private set; }

    /// <summary>
    /// Gets or sets the name of the associated content type. See <see cref="SPContentType.Name"/> for details.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the description of the associated content type. See <see cref="SPContentType.Description"/> for details.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the group name of the associated content type. See <see cref="SPContentType.Group"/> for details.
    /// </summary>
    public string Group { get; set; }

    /// <summary>
    /// Gets or sets whether the associated content type is hidden. See <see cref="SPContentType.Hidden"/> for details.
    /// </summary>
    public bool Hidden { get; set; }

    /// <summary>
    /// Gets or sets whether the content type is hidden in the "New Item" menu.
    /// </summary>
    public bool HiddenInList { get; set; }

    /// <summary>
    /// Gets or sets whether the attributed class represents a third-party content type.
    /// A third-party content type will not be provisioned, nor any modifications will be made to existing content type.
    /// </summary>
    public bool ExternalContentType { get; set; }

    /// <summary>
    /// Gets or sets the type of event receiver which model provision event is invoked with.
    /// </summary>
    public Type ProvisionEventReceiverType { get; set; }

    /// <summary>
    /// Gets a full content type ID resolved by type hierarchy.
    /// </summary>
    public SPContentTypeId ContentTypeId {
      get {
        if (contentTypeId == default(SPContentTypeId)) {
          throw new InvalidOperationException("Attributed class is not registered with SPModel.RegisterAssembly");
        }
        return contentTypeId;
      }
    }

    internal void SetFullContentTypeId(SPContentTypeId value) {
      this.contentTypeId = value;
    }

    internal SPContentTypeAttribute Clone() {
      return (SPContentTypeAttribute)this.MemberwiseClone();
    }
  }
}
