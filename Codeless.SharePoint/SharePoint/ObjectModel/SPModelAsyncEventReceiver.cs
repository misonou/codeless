using Microsoft.SharePoint;
using System;

namespace Codeless.SharePoint.ObjectModel {
  internal class SPModelAsyncEventReceiver : SPModelEventReceiver {
    public override void ItemAdded(SPItemEventProperties properties) {
      if (properties.ListItem != null && properties.List.BaseType != SPBaseType.DocumentLibrary) {
        HandleEvent(properties, SPModelEventType.AddedAsync);
      }
    }

    public override void ItemUpdated(SPItemEventProperties properties) {
      if (properties.ListItem != null) {
        if (properties.List.BaseType == SPBaseType.DocumentLibrary && Boolean.TrueString.Equals(properties.ListItem.Properties[InitializeKey])) {
          HandleEvent(properties, SPModelEventType.AddedAsync);
        } else {
          HandleEvent(properties, SPModelEventType.UpdatedAsync);
        }
      }
    }

    public override void ItemDeleted(SPItemEventProperties properties) {
      // there is no OnDeletedAsync as we cannot get the reference of the SPModel in another thread
    }
  }
}
