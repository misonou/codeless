using Codeless.SharePoint.Internal;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Administration;
using Microsoft.SharePoint.Utilities;
using System;
using System.Reflection;
using System.Threading;
using System.Web.Hosting;

namespace Codeless.SharePoint.ObjectModel {
  internal class SPPreviousEventDataCollectionAdapter : SPItemEventDataCollectionAdapter {
    private static readonly MethodInfo GetRawValueMethod = typeof(SPListItemCollection).GetMethod("GetRawValue", true, typeof(string), typeof(int));
    private readonly SPListItemCollection internalCollection;

    public SPPreviousEventDataCollectionAdapter(SPItemEventProperties properties, SPListItemCollection internalCollection)
      : base(properties) {
      CommonHelper.ConfirmNotNull(internalCollection, "internalCollection");
      this.internalCollection = internalCollection;
      if (GetRawValueMethod == null) {
        throw new MissingMethodException("GetRawValue");
      }
    }

    protected override object this[string name] {
      get { return GetRawValueMethod.Invoke<object>(internalCollection, name, 0); }
      set { throw new InvalidOperationException("Cannot set value to deleted model"); }
    }
  }

  internal class SPModelEventReceiver : SPItemEventReceiver {
    protected const string InitializeKey = "urn:codeless.sharepoint";

    [ThreadStatic]
    private static SPListItemCollection previousItemData;

    [ThreadStatic]
    private static bool isPublishingEvent;

    public override void ItemAdded(SPItemEventProperties properties) {
      if (properties.ListItem != null && properties.List.BaseType != SPBaseType.DocumentLibrary) {
        HandleEvent(properties, SPModelEventType.Added);
      }
    }

    public override void ItemAdding(SPItemEventProperties properties) {
      if (properties.List.BaseType != SPBaseType.DocumentLibrary) {
        HandleEvent(properties, SPModelEventType.Adding);
      }
    }

    public override void ItemUpdated(SPItemEventProperties properties) {
      if (properties.ListItem != null) {
        if (properties.List.BaseType == SPBaseType.DocumentLibrary && Boolean.TrueString.Equals(properties.ListItem.Properties[InitializeKey])) {
          HandleEvent(properties, SPModelEventType.Added);
        } else if (isPublishingEvent) {
          HandleEvent(properties, SPModelEventType.Published);
        } else {
          HandleEvent(properties, SPModelEventType.Updated);
        }
      }
    }

    public override void ItemUpdating(SPItemEventProperties properties) {
      if (properties.ListItem != null) {
        if (properties.List.BaseType == SPBaseType.DocumentLibrary && !properties.ListItem.Properties.ContainsKey(InitializeKey)) {
          HandleEvent(properties, SPModelEventType.Adding);
        } else if ((SPFileLevel)((int?)properties.AfterProperties["vti_level"]).GetValueOrDefault() == SPFileLevel.Draft &&
            (SPFileLevel)((int?)properties.BeforeProperties["vti_level"]).GetValueOrDefault() == SPFileLevel.Draft &&
            (properties.ListItem.ModerationInformation == null || properties.ListItem.ModerationInformation.Status == SPModerationStatusType.Pending)) {
          HandleEvent(properties, SPModelEventType.Publishing);
          isPublishingEvent = true;
        } else {
          HandleEvent(properties, SPModelEventType.Updating);
          isPublishingEvent = false;
        }
        if (properties.List.BaseType == SPBaseType.DocumentLibrary) {
          bool currentState;
          if (!Boolean.TryParse((string)properties.AfterProperties[InitializeKey], out currentState) || currentState) {
            properties.AfterProperties[InitializeKey] = (currentState ^ true).ToString();
          }
        }
      }
    }

    public override void ItemDeleting(SPItemEventProperties properties) {
      if (properties.ListItem != null) {
        HandleEvent(properties, SPModelEventType.Deleting);
      }
    }

    public override void ItemDeleted(SPItemEventProperties properties) {
      if (properties.ListItemId != 0) {
        HandleEvent(properties, SPModelEventType.Deleted);
      }
    }

    protected void HandleEvent(SPItemEventProperties properties, SPModelEventType eventType) {
      using (new SPMonitoredScope(String.Format("SPModel Event Receiver ({0})", eventType))) {
        try {
          if (properties.AfterProperties != null && properties.BeforeProperties != null) {
            SPListItemCollection itemData = Interlocked.Exchange(ref previousItemData, null);
            ISPListItemAdapter adapter;
            ISPListItemAdapter previousAdapter;
            if (eventType == SPModelEventType.Deleted) {
              adapter = new SPPreviousEventDataCollectionAdapter(properties, itemData);
              previousAdapter = adapter;
            } else if (eventType == SPModelEventType.Adding || eventType == SPModelEventType.Updating || eventType == SPModelEventType.Deleting || eventType == SPModelEventType.Publishing) {
              adapter = new SPItemEventDataCollectionAdapter(properties);
              previousAdapter = eventType == SPModelEventType.Adding ? null : new SPListItemAdapter(properties.ListItem);
            } else {
              adapter = new SPListItemAdapter(properties.ListItem);
              previousAdapter = itemData != null ? new SPPreviousEventDataCollectionAdapter(properties, itemData) : adapter;
            }

            SPModel currentItem = SPModel.TryCreate(adapter);
            if (currentItem != null) {
              currentItem.HandleEvent(new SPModelEventArgs(eventType, currentItem, previousAdapter, properties));
              if (eventType == SPModelEventType.Updating || eventType == SPModelEventType.Deleting) {
                previousItemData = properties.ListItem.ListItems;
              }
            }
          }
        } catch (Exception ex) {
          SPDiagnosticsService.Local.WriteTrace(TraceCategory.General, ex);
          if (HostingEnvironment.IsHosted) {
            properties.Status = SPEventReceiverStatus.CancelWithError;
            properties.ErrorMessage = ex.Message;
          } else {
            throw;
          }
        }
      }
    }
  }
}
