using Microsoft.SharePoint;
using System;
using System.Collections.Generic;

namespace Codeless.SharePoint.ObjectModel {
  /// <summary>
  /// Provides event data to FieldProvisioning and FieldProvisioned events.
  /// </summary>
  public class SPFieldProvisionEventArgs : EventArgs {
    /// <summary>
    /// Gets a site collection that a column is being provisioned to.
    /// </summary>
    public SPSite Site { get; internal set; }
    /// <summary>
    /// Gets a list that a list column is being provisioned to. If it is a site column, *null* is returned.
    /// </summary>
    public SPList ParentList { get; internal set; }
    /// <summary>
    /// Gets a definition object for the column being provisioned.
    /// </summary>
    public SPFieldAttribute Definition { get; internal set; }
    /// <summary>
    /// Indicates if an existing column is modified during provision.
    /// </summary>
    public bool TargetModified { get; internal set; }
    /// <summary>
    /// Gets or sets whether to cancel operation. Setting this property has no effects in FieldProvisioned event.
    /// </summary>
    public bool Cancel { get; set; }
  }

  /// <summary>
  /// Provides event data to ContentTypeProvisioning and ContentTypeProvisioned events.
  /// </summary>
  public class SPContentTypeProvisionEventArgs : EventArgs {
    /// <summary>
    /// Gets a site collection that a content type is being provisioned to.
    /// </summary>
    public SPSite Site { get; internal set; }
    /// <summary>
    /// Gets a list that a list content type is being provisioned to. If it is a site content type, *null* is returned.
    /// </summary>
    public SPList ParentList { get; internal set; }
    /// <summary>
    /// Gets a definition object for the content type being provisioned.
    /// </summary>
    public SPContentTypeAttribute Definition { get; internal set; }
    /// <summary>
    /// Gets a list of ordered field names. The list can be manuplated for custom ordering.
    /// </summary>
    public List<string> FieldOrder { get; internal set; }
    /// <summary>
    /// Indicates if an existing content type is modified during provision.
    /// </summary>
    public bool TargetModified { get; internal set; }
    /// <summary>
    /// Gets or sets whether to cancel operation. Setting this property has no effects in ContentTypeProvisioned event.
    /// </summary>
    public bool Cancel { get; set; }
  }

  /// <summary>
  /// Provides event data to ListProvisioning and ListProvisioned events.
  /// </summary>
  public class SPListProvisionEventArgs : EventArgs {
    /// <summary>
    /// Gets a site that a list is being provisioned to.
    /// </summary>
    public SPWeb Web { get; internal set; }
    /// <summary>
    /// Gets a list that is being provisioned.
    /// </summary>
    public SPList List { get; internal set; }
    /// <summary>
    /// Gets a definition object for the list being provisioned.
    /// </summary>
    public SPListAttribute Definition { get; internal set; }
    /// <summary>
    /// Indicates if an existing list is modified during provision.
    /// </summary>
    public bool TargetModified { get; internal set; }
    /// <summary>
    /// Gets or sets whether to cancel operation. Setting this property has no effects in ContentTypeProvisioned event.
    /// </summary>
    public bool Cancel { get; set; }
  }

  /// <summary>
  /// Provides event data to ListViewProvisioning and ListViewProvisioned events.
  /// </summary>
  public class SPListViewProvisionEventArgs : EventArgs {
    /// <summary>
    /// Gets a site that a list view is being provisioned to.
    /// </summary>
    public SPWeb Web { get; internal set; }
    /// <summary>
    /// Gets a list view that is being provisioned.
    /// </summary>
    public SPView View { get; internal set; }
    /// <summary>
    /// Gets or sets a query text for the list view being provisioned.
    /// </summary>
    public string Query { get; set; }
    /// <summary>
    /// Gets a list of included fields for the list view being provisioned. The list can be manuplated.
    /// </summary>
    public List<string> IncludedFields { get; internal set; }
    /// <summary>
    /// Gets a list of excluded fields for the list view being provisioned. The list can be manuplated.
    /// </summary>
    public List<string> ExcludedFields { get; internal set; }
    /// <summary>
    /// Indicates if an existing list view is modified during provision.
    /// </summary>
    public bool TargetModified { get; internal set; }
    /// <summary>
    /// Gets or sets whether to cancel operation. Setting this property has no effects in ContentTypeProvisioned event.
    /// </summary>
    public bool Cancel { get; set; }
  }

  /// <summary>
  /// Provides a base class for handling model provisioning events.
  /// </summary>
  public class SPModelProvisionEventReceiver {
    private static readonly SPModelProvisionEventReceiver defaultEventReceiver = new SPModelProvisionEventReceiver();

    /// <summary>
    /// Gets a default model provisioning handler which does nothing on all events.
    /// </summary>
    public static SPModelProvisionEventReceiver Default {
      get { return defaultEventReceiver; }
    }

    /// <summary>
    /// Called when a list view is being provisioned.
    /// </summary>
    /// <param name="eventArgs">Event data.</param>
    public virtual void OnListViewProvisioning(SPListViewProvisionEventArgs eventArgs) { }

    /// <summary>
    /// Called when a list view is provisioned.
    /// </summary>
    /// <param name="eventArgs">Event data.</param>
    public virtual void OnListViewProvisioned(SPListViewProvisionEventArgs eventArgs) { }

    /// <summary>
    /// Called when a list is being provisioned.
    /// </summary>
    /// <param name="eventArgs">Event data.</param>
    public virtual void OnListProvisioning(SPListProvisionEventArgs eventArgs) { }

    /// <summary>
    /// Called when a list is provisioned.
    /// </summary>
    /// <param name="eventArgs">Event data.</param>
    public virtual void OnListProvisioned(SPListProvisionEventArgs eventArgs) { }

    /// <summary>
    /// Called when a content type is being provisioned.
    /// </summary>
    /// <param name="eventArgs">Event data.</param>
    public virtual void OnContentTypeProvisioning(SPContentTypeProvisionEventArgs eventArgs) { }

    /// <summary>
    /// Called when a content type is provisioned.
    /// </summary>
    /// <param name="eventArgs">Event data.</param>
    public virtual void OnContentTypeProvisioned(SPContentTypeProvisionEventArgs eventArgs) { }

    /// <summary>
    /// Called when a site column is being provisioned.
    /// </summary>
    /// <param name="eventArgs">Event data.</param>
    public virtual void OnFieldProvisioning(SPFieldProvisionEventArgs eventArgs) { }

    /// <summary>
    /// Called when a site column is provisioned.
    /// </summary>
    /// <param name="eventArgs">Event data.</param>
    public virtual void OnFieldProvisioned(SPFieldProvisionEventArgs eventArgs) { }
  }
}
