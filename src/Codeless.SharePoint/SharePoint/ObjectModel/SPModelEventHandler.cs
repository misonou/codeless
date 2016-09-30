using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Codeless.SharePoint.ObjectModel {
  internal interface ISPModelEventHandler {
    void HandleEvent(SPModel item, SPModelEventArgs e);
  }

  public abstract class SPModelEventHandler<T> : ISPModelEventHandler where T : class {
    /// <summary>
    /// Invoked when the underlying list item is being added to a list.
    /// </summary>
    /// <param name="item">Instance of the model class representing a list item.</param>
    /// <param name="e">Event data.</param>
    public virtual void OnAdding(T item, SPModelEventArgs e) { }

    /// <summary>
    /// Invoked when the underlying list item is added to a list.
    /// </summary>
    /// <param name="item">Instance of the model class representing a list item.</param>
    /// <param name="e">Event data.</param>
    public virtual void OnAdded(T item, SPModelEventArgs e) { }

    /// <summary>
    /// Invoked asynchronously when the underlying list item is added to a list.
    /// </summary>
    /// <param name="item">Instance of the model class representing a list item.</param>
    /// <param name="e">Event data.</param>
    public virtual void OnAddedAsync(T item, SPModelEventArgs e) { }

    /// <summary>
    /// Invoked when the underlying list item is being updated.
    /// </summary>
    /// <param name="item">Instance of the model class representing a list item.</param>
    /// <param name="e">Event data.</param>
    public virtual void OnUpdating(T item, SPModelEventArgs e) { }

    /// <summary>
    /// Invoked when the underlying list item is updated.
    /// </summary>
    /// <param name="item">Instance of the model class representing a list item.</param>
    /// <param name="e">Event data.</param>
    public virtual void OnUpdated(T item, SPModelEventArgs e) { }

    /// <summary>
    /// Invoked asynchronously when the underlying list item is updated.
    /// </summary>
    /// <param name="item">Instance of the model class representing a list item.</param>
    /// <param name="e">Event data.</param>
    public virtual void OnUpdatedAsync(T item, SPModelEventArgs e) { }

    /// <summary>
    /// Invoked when the underlying list item is being added to a list or being updated.
    /// </summary>
    /// <param name="item">Instance of the model class representing a list item.</param>
    /// <param name="e">Event data.</param>
    public virtual void OnAddingOrUpdating(T item, SPModelEventArgs e) { }

    /// <summary>
    /// Invoked when the underlying list item is added to a list or updated.
    /// </summary>
    /// <param name="item">Instance of the model class representing a list item.</param>
    /// <param name="e">Event data.</param>
    public virtual void OnAddedOrUpdated(T item, SPModelEventArgs e) { }

    /// <summary>
    /// Invoked asynchronously when the underlying list item is added to a list or updated.
    /// </summary>
    /// <param name="item">Instance of the model class representing a list item.</param>
    /// <param name="e">Event data.</param>
    public virtual void OnAddedOrUpdatedAsync(T item, SPModelEventArgs e) { }

    /// <summary>
    /// Invoked when the underlying list item is being deleted.
    /// </summary>
    /// <param name="item">Instance of the model class representing a list item.</param>
    /// <param name="e">Event data.</param>
    public virtual void OnDeleting(T item, SPModelEventArgs e) { }

    /// <summary>
    /// Invoked when the underlying list item is deleted.
    /// </summary>
    /// <param name="item">Instance of the model class representing a list item.</param>
    /// <param name="e">Event data.</param>
    public virtual void OnDeleted(T item, SPModelEventArgs e) { }

    /// <summary>
    /// Invoked when the underlying list item is being published.
    /// </summary>
    /// <param name="item">Instance of the model class representing a list item.</param>
    /// <param name="e">Event data.</param>
    public virtual void OnPublishing(T item, SPModelEventArgs e) { }

    /// <summary>
    /// Invoked when the underlying list item is published.
    /// </summary>
    /// <param name="item">Instance of the model class representing a list item.</param>
    /// <param name="e">Event data.</param>
    public virtual void OnPublished(T item, SPModelEventArgs e) { }
    
    void ISPModelEventHandler.HandleEvent(SPModel item, SPModelEventArgs e) {
      T typedItem = CommonHelper.TryCastOrDefault<T>(item);
      switch (e.EventType) {
        case SPModelEventType.Adding:
          OnAdding(typedItem, e);
          OnAddingOrUpdating(typedItem, e);
          return;
        case SPModelEventType.Added:
          OnAdded(typedItem, e);
          OnAddedOrUpdated(typedItem, e);
          return;
        case SPModelEventType.AddedAsync:
          OnAddedAsync(typedItem, e);
          OnAddedOrUpdatedAsync(typedItem, e);
          return;
        case SPModelEventType.Updating:
          OnUpdating(typedItem, e);
          OnAddingOrUpdating(typedItem, e);
          return;
        case SPModelEventType.Updated:
          OnUpdated(typedItem, e);
          OnAddedOrUpdated(typedItem, e);
          return;
        case SPModelEventType.UpdatedAsync:
          OnUpdatedAsync(typedItem, e);
          OnAddedOrUpdatedAsync(typedItem, e);
          return;
        case SPModelEventType.Deleting:
          OnDeleting(typedItem, e);
          return;
        case SPModelEventType.Deleted:
          OnDeleted(typedItem, e);
          return;
        case SPModelEventType.Publishing:
          OnPublishing(typedItem, e);
          return;
        case SPModelEventType.Published:
          OnPublished(typedItem, e);
          return;
      }
    }
  }
}
