using Microsoft.SharePoint;
using Microsoft.SharePoint.Taxonomy;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;

namespace Codeless.SharePoint {
  /// <summary>
  /// Provides field value accessors to a <see cref="DataRow"/> instance returned from Office search service.
  /// </summary>
  public class KeywordQueryResultAdapter : DataRowAdapter {
    /// <summary>
    /// Creates an adapter.
    /// </summary>
    /// <param name="site">Site collection object.</param>
    /// <param name="item">List item.</param>
    public KeywordQueryResultAdapter(SPSite site, DataRow item)
      : base(site, item) { }

    /// <summary>
    /// Creates an adapter with the given object cache.
    /// </summary>
    /// <param name="site">Site collection object.</param>
    /// <param name="item">List item.</param>
    /// <param name="objectCache">Object cache.</param>
    public KeywordQueryResultAdapter(SPSite site, DataRow item, SPObjectCache objectCache)
      : base(site, item, objectCache) { }

    /// <summary>
    /// Gets or sets values to the specified column.
    /// </summary>
    /// <param name="name">Field name.</param>
    /// <returns>Value of the specified column.</returns>
    protected override object this[string name] {
      get {
        string[] mappedNames = SearchServiceHelper.GetManagedPropertyNames(this.Site, name);
        if (mappedNames.Length > 0) {
          name = mappedNames[0];
        }
        object value = base[name];
        if (value != DBNull.Value) {
          return value;
        }
        return null;
      }
      set {
        base[name] = value;
      }
    }

    /// <summary>
    /// Gets the filename of the list item represented by the adapter.
    /// </summary>
    public override string Filename {
      get { return Path.GetFileName(this.ServerRelativeUrl); }
    }

    /// <summary>
    /// Gets the server-relative URL of the list item represented by the adapter.
    /// </summary>
    public override string ServerRelativeUrl {
      get { return new Uri(base[BuiltInManagedPropertyName.Path].ToString()).AbsolutePath.TrimEnd('/'); }
    }

    /// <summary>
    /// Gets the unique ID of the list item represented by the adapter.
    /// </summary>
    public override Guid UniqueId {
      get { return new Guid(base[BuiltInManagedPropertyName.UniqueID].ToString()); }
    }

    /// <summary>
    /// When overriden in derived classes, gets the parent site ID of the list item represented by the adapter.
    /// </summary>
    public override Guid WebId {
      get { return new Guid(base[BuiltInManagedPropertyName.WebId].ToString()); }
    }

    /// <summary>
    /// When overriden in derived classes, gets the parent list ID of the list item represented by the adapter.
    /// </summary>
    public override Guid ListId {
      get { return new Guid(this[BuiltInManagedPropertyName.ListID].ToString()); }
    }

    /// <summary>
    /// When overriden in derived classes, gets the list item ID of the list item represented by the adapter.
    /// </summary>
    public override int ListItemId {
      get { return Int32.Parse(base[BuiltInManagedPropertyName.ListItemID].ToString()); }
    }

    /// <summary>
    /// When overriden in derived classes, gets the last modified time of the list item represented by the adapter.
    /// </summary>
    public override DateTime LastModified {
      get { return GetDateTime(BuiltInManagedPropertyName.LastModifiedTime).GetValueOrDefault(); }
    }

    /// <summary>
    /// Gets the permissions of the list item represented by the adapter which is granted to the current user.
    /// </summary>
    public override SPBasePermissions EffectivePermissions {
      get { return this.ListItem.EffectiveBasePermissions; }
    }

    /// <summary>
    /// When overidden, determines whether the specified field is included in the data set.
    /// </summary>
    /// <param name="fieldName">Field name.</param>
    /// <returns>Returns *true* if the specified field is included in the data set.</returns>
    public override bool HasField(string fieldName) {
      string[] mappedNames = SearchServiceHelper.GetManagedPropertyNames(this.Site, fieldName);
      if (mappedNames.Length > 0) {
        fieldName = mappedNames[0];
      }
      return base.HasField(fieldName);
    }

    /// <summary>
    /// Gets value from a Taxonomy field.
    /// </summary>
    /// <param name="fieldName">Field name.</param>
    /// <param name="termStore">Term store object.</param>
    /// <returns>Value in the specified field.</returns>
    public override Term GetTaxonomy(string fieldName, TermStore termStore) {
      if (this.ListItemAdapater != null) {
        return this.ListItemAdapater.GetTaxonomy(fieldName, termStore);
      }
      IList<Term> terms = GetTaxonomyMultiInternal(fieldName, termStore);
      return terms.FirstOrDefault();
    }

    /// <summary>
    /// Gets a collection of <see cref="Term"/> objects referenced by the multiple taxonomy field.
    /// </summary>
    /// <param name="fieldName">Field name.</param>
    /// <param name="termStore">Term store object.</param>
    /// <returns>A collection of terms.</returns>
    protected override IList<Term> GetTaxonomyMultiInternal(string fieldName, TermStore termStore) {
      if (this.ListItemAdapater != null) {
        return this.ListItemAdapater.GetTaxonomyMulti(fieldName, termStore);
      }

      string value = (string)this[fieldName];
      List<Term> terms = new List<Term>();

      if (value != null) {
        TermSet termSet = null;
        List<Guid> parsedValues = new List<Guid>();
        foreach (string s in value.Split(';')) {
          if (s.StartsWith("GP0|#")) {
            Guid termId = new Guid(s.Substring(5));
            parsedValues.Add(termId);
          } else if (s.StartsWith("GTSet|#")) {
            if (termSet == null) {
              Guid termSetId = new Guid(s.Substring(7));
              termSet = termStore.GetTermSet(termSetId);
            }
          }
        }
        if (termSet != null) {
          foreach (Guid termId in parsedValues) {
            Term term = termSet.GetTerm(termId);
            if (term != null) {
              terms.Add(term);
            }
          }
        }
      }
      return terms;
    }
  }
}
