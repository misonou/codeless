using Microsoft.SharePoint;
using System;
using System.Threading;

namespace Codeless.SharePoint.ObjectModel {
  /// <summary>
  /// Represents the type of an SPModel event.
  /// </summary>
  public enum SPModelEventType {
    /// <summary>
    /// Respresents a synchronous event which the underlying list item is being added to a list.
    /// </summary>
    Adding,
    /// <summary>
    /// Respresents a synchronous event which the underlying list item is added to a list.
    /// </summary>
    Added,
    /// <summary>
    /// Respresents an asynchronous event which the underlying list item is added to a list.
    /// </summary>
    AddedAsync,
    /// <summary>
    /// Respresents a synchronous event which the underlying list item is being updated.
    /// </summary>
    Updating,
    /// <summary>
    /// Respresents a synchronous event which the underlying list item is updated.
    /// </summary>
    Updated,
    /// <summary>
    /// Respresents an asynchronous event which the underlying list item is updated.
    /// </summary>
    UpdatedAsync,
    /// <summary>
    /// Respresents a synchronous event which the underlying list item is being deleted from a lsit.
    /// </summary>
    Deleting,
    /// <summary>
    /// Respresents a synchronous event which the underlying list item is deleted from a lsit.
    /// </summary>
    Deleted,
    /// <summary>
    /// Respresents a synchronous event which the underlying list item is being published.
    /// </summary>
    Publishing,
    /// <summary>
    /// Respresents a synchronous event which the underlying list item is published.
    /// </summary>
    Published
  }

  /// <summary>
  /// Provides data to an SPModel event.
  /// </summary>
  public class SPModelEventArgs : EventArgs {
    private readonly SPItemEventProperties properties;
    private readonly ISPListItemAdapter previousAdapter;
    private readonly ISPModelManagerInternal manager;
    private readonly SPModelEventType eventType;
    private SPModel originalValue;

    internal SPModelEventArgs(SPModelEventType eventType, SPModel model, ISPListItemAdapter previousAdapter, SPItemEventProperties properties) {
      CommonHelper.ConfirmNotNull(model, "model");
      CommonHelper.ConfirmNotNull(properties, "properties");
      this.properties = properties;
      this.previousAdapter = previousAdapter;
      this.manager = model.ParentCollection.Manager;
      this.eventType = eventType;
    }

    /// <summary>
    /// Gets the parent site collection of the underlying list item which triggered this event.
    /// </summary>
    public SPSite Site {
      get { return properties.Web.Site; }
    }

    /// <summary>
    /// Gets the parent site of the underlying list item which triggered this event.
    /// </summary>
    public SPWeb Web {
      get { return properties.Web; }
    }

    /// <summary>
    /// Gets the parent list of the underlying list item which triggered this event.
    /// </summary>
    public SPList List {
      get { return properties.List; }
    }

    /// <summary>
    /// Gets the underlying list item which triggered this event.
    /// </summary>
    public SPListItem ListItem {
      get { return properties.ListItem; }
    }

    /// <summary>
    /// Gets the properties of the underlying SPItem event.
    /// </summary>
    public SPItemEventProperties Properties {
      get { return properties; }
    }

    /// <summary>
    /// Gets a read-only model object containing original values of the underlying list item.
    /// The type of the model object returned may not be the same as the object that this event is invoked on, as the value of Content Type ID field can be changed.
    /// </summary>
    public SPModel OriginalValue {
      get {
        if (previousAdapter != null) {
          return LazyInitializer.EnsureInitialized(ref originalValue, () => manager.TryCreateModel(previousAdapter, true));
        }
        return null;
      }
    }

    /// <summary>
    /// Disables event firing on subsequent updates to list items until disposing the returned object.
    /// </summary>
    /// <returns>A disposable object.</returns>
    public IDisposable GetEventFiringDisabledScope() {
      return SPItemEventHelper.GetEventFiringDisabledScope();
    }

    /// <summary>
    /// Enables event firing on subsequent updates to list items until disposing the returned object.
    /// </summary>
    /// <returns>A disposable object.</returns>
    public IDisposable GetEventFiringEnabledScope() {
      return SPItemEventHelper.GetEventFiringEnabledScope();
    }

    /// <summary>
    /// Gets the type of this event.
    /// </summary>
    public SPModelEventType EventType {
      get { return eventType; }
    }

    /// <summary>
    /// Indicates whether this event is triggered inside a workflow.
    /// </summary>
    public bool IsWorkflowFiredEvent {
      get { return SPItemEventHelper.IsWorkflowFiredEvent; }
    }

    /// <summary>
    /// Indicates whether this event is triggered by another event.
    /// </summary>
    public bool IsNestedItemEvent {
      get { return SPItemEventHelper.IsNestedItemEvent; }
    }

    /// <summary>
    /// Cancels the action with the specified message.
    /// </summary>
    /// <param name="message">Error message.</param>
    public void PreventAction(string message) {
      PreventAction(message, SPEventReceiverStatus.CancelWithError);
    }

    /// <summary>
    /// Cancels the action with the specified message and event status.
    /// </summary>
    /// <param name="message">Error message.</param>
    /// <param name="status">Cancellation status.</param>
    public void PreventAction(string message, SPEventReceiverStatus status) {
      properties.Status = status;
      properties.ErrorMessage = message;
    }
  }
}
