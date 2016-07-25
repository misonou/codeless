using Codeless.SharePoint.ObjectModel;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Taxonomy;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Codeless.SharePoint {
  /// <summary>
  /// Defines methods to access values from a list item.
  /// </summary>
  public interface ISPListItemAdapter {
    /// <summary>
    /// Gets the title of the list item represented by the adapter.
    /// </summary>
    string Title { get; }

    /// <summary>
    /// Gets the filename of the list item represented by the adapter.
    /// </summary>
    string Filename { get; }

    /// <summary>
    /// Gets the server-relative URL of the list item represented by the adapter.
    /// </summary>
    string ServerRelativeUrl { get; }

    /// <summary>
    /// Gets the site collection associated with the list item represented by the adapter.
    /// </summary>
    SPSite Site { get; }

    /// <summary>
    /// Gets the parent site of the list item represented by the adapter.
    /// </summary>
    SPWeb Web { get; }

    /// <summary>
    /// Gets the unique ID of the list item represented by the adapter.
    /// </summary>
    Guid UniqueId { get; }

    /// <summary>
    /// Gets the parent site ID of the list item represented by the adapter.
    /// </summary>
    Guid WebId { get; }

    /// <summary>
    /// Gets the parent list ID of the list item represented by the adapter.
    /// </summary>
    Guid ListId { get; }

    /// <summary>
    /// Gets the list item ID of the list item represented by the adapter.
    /// </summary>
    int ListItemId { get; }

    /// <summary>
    /// Gets the list item represented by the adapter.
    /// </summary>
    SPListItem ListItem { get; }

    /// <summary>
    /// Gets the content type ID of the list item represented by the adapter.
    /// </summary>
    SPContentTypeId ContentTypeId { get; }

    /// <summary>
    /// Gets the last modified time of the list item represented by the adapter.
    /// </summary>
    DateTime LastModified { get; }

    /// <summary>
    /// Gets the permissions of the list item represented by the adapter which is granted to the current user.
    /// </summary>
    SPBasePermissions EffectivePermissions { get; }

    /// <summary>
    /// Gets the version number of the list item.
    /// </summary>
    SPItemVersion Version { get; }

    /// <summary>
    /// Determines whether the specified field is included in the data set.
    /// </summary>
    /// <param name="fieldName">Field name.</param>
    /// <returns>Returns *true* if the specified field is included in the data set.</returns>
    bool HasField(string fieldName);

    /// <summary>
    /// Gets value from a boolean field.
    /// If the field does not contain value or the string representation of the value does not form a boolean value, default value is returned.
    /// </summary>
    /// <param name="fieldName">Field name.</param>
    /// <returns>Value in the specified field.</returns>
    bool GetBoolean(string fieldName);

    /// <summary>
    /// Gets value from an integer field.
    /// If the field does not contain value or the string representation of the value does not form an integer value, default value is returned.
    /// </summary>
    /// <param name="fieldName">Field name.</param>
    /// <returns>Value in the specified field.</returns>
    int GetInteger(string fieldName);

    /// <summary>
    /// Gets value from a numeric field, such as Integer, Number and Currency field.
    /// If the field does not contain value or the string representation of the value does not form a double-precision value, default value is returned.
    /// </summary>
    /// <param name="fieldName">Field name.</param>
    /// <returns>Value in the specified field.</returns>
    double GetNumber(string fieldName);
    
    /// <summary>
    /// Gets value from text field, such as Text, Note and Publishing HTML field.
    /// If the field does not contain value, an empty string is returned.
    /// If the field is not a text column, a string representation of the value is returned.
    /// </summary>
    /// <param name="fieldName">Field name.</param>
    /// <returns>Value in the specified field.</returns>
    string GetString(string fieldName);

    /// <summary>
    /// Gets value from a GUID field.
    /// If the field does not contain value or the string representation of the value does not form a GUID value, default value is returned.
    /// </summary>
    /// <param name="fieldName">Field name.</param>
    /// <returns>Value in the specified field.</returns>
    Guid GetGuid(string fieldName);

    /// <summary>
    /// Gets value from a DateTime field.
    /// If the field does not contain value, *null* is returned.
    /// </summary>
    /// <param name="fieldName">Field name.</param>
    /// <returns>Value in the specified field.</returns>
    DateTime? GetDateTime(string fieldName);

    /// <summary>
    /// Gets value from an Integer, Text, Choice or MultiChoice field and returns as the equivalent value of the enum type.
    /// For a MultiChoice field, the returned value is the bitwise OR result of the enum values represented by each selected choice.
    /// </summary>
    /// <typeparam name="T">Enum type.</typeparam>
    /// <param name="fieldName">Field name.</param>
    /// <returns>Value in the specified field.</returns>
    T GetEnum<T>(string fieldName) where T : struct;

    /// <summary>
    /// Gets value from a Taxonomy field.
    /// </summary>
    /// <param name="fieldName">Field name.</param>
    /// <param name="termStore">Term store object.</param>
    /// <returns>Value in the specified field.</returns>
    Term GetTaxonomy(string fieldName, TermStore termStore);

    IList<Term> GetTaxonomyMulti(string fieldName, TermStore termStore);
    ReadOnlyCollection<Term> GetTaxonomyMultiReadOnly(string fieldName, TermStore termStore);

    /// <summary>
    /// Gets value from a URL field where URL returned can be absolute or relative.
    /// The URL is normalized to a server-relative path if it points to the same SharePoint web application.
    /// </summary>
    /// <param name="fieldName">Field name.</param>
    /// <returns>Value in the specified field.</returns>
    SPFieldUrlValue GetUrlFieldValue(string fieldName);

    /// <summary>
    /// Gets value from a Lookup field.
    /// </summary>
    /// <param name="fieldName">Field name.</param>
    /// <returns>Value in the specified field.</returns>
    string GetLookupFieldValue(string fieldName);

    IList<string> GetMultiLookupFieldValue(string fieldName);
    ReadOnlyCollection<string> GetMultiLookupFieldValueReadOnly(string fieldName);
    SPPrincipal GetUserFieldValue(string fieldName);
    IList<SPPrincipal> GetMultiUserFieldValue(string fieldName);
    ReadOnlyCollection<SPPrincipal> GetMultiUserFieldValueReadOnly(string fieldName);
    T GetModel<T>(string fieldName, SPModelCollection parentCollection);
    IList<T> GetModelCollection<T>(string fieldName, SPModelCollection parentCollection);
    ReadOnlyCollection<T> GetModelCollectionReadOnly<T>(string fieldName, SPModelCollection parentCollection);
    IList<string> GetMultiChoiceFieldValue(string fieldName);
    ReadOnlyCollection<string> GetMultiChoiceFieldValueReadOnly(string fieldName);

    void SetBoolean(string fieldName, bool value);
    void SetInteger(string fieldName, int value);
    void SetNumber(string fieldName, double value);
    void SetString(string fieldName, string value);
    void SetGuid(string fieldName, Guid value);
    void SetDateTime(string fieldName, DateTime? value);
    void SetEnum<T>(string fieldName, T value) where T : struct;
    void SetTaxonomy(string fieldName, Term value);
    void SetUrlFieldValue(string fieldName, SPFieldUrlValue value);
    void SetLookupFieldValue(string fieldName, string value);
    void SetUserFieldValue(string fieldName, SPPrincipal user);
    void SetModel<T>(string fieldName, T item);
  }
}
