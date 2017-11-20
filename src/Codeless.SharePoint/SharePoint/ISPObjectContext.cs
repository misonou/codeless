using Microsoft.SharePoint;
using Microsoft.SharePoint.Taxonomy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Codeless.SharePoint {
  /// <summary>
  /// Represents data source of SharePoint objects.
  /// </summary>
  public interface ISPObjectContext {
    /// <summary>
    /// Gets the site collection where persisted SharePoint objects to be retrieved from.
    /// </summary>
    SPSite Site { get; }
    /// <summary>
    /// Gets the term store where persisted objects of the Managed Metadata Service to be retrieved from.
    /// </summary>
    TermStore TermStore { get; }
  }
}
