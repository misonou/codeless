using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Codeless.SharePoint.ObjectModel {
  internal class SPModelProvisionMulticastEventReceiver : SPModelProvisionEventReceiver {
    private readonly List<SPModelProvisionEventReceiver> eventReceivers = new List<SPModelProvisionEventReceiver>();
    
    public int Count {
      get { return eventReceivers.Count; }
    }

    public SPModelProvisionEventReceiver this[int index] {
      get { return eventReceivers[index]; }
    }

    public void Add(SPModelProvisionEventReceiver eventReceiver) {
      if (eventReceiver != SPModelProvisionEventReceiver.Default) {
        SPModelProvisionMulticastEventReceiver multicastReceiver = eventReceiver as SPModelProvisionMulticastEventReceiver;
        if (multicastReceiver != null) {
          eventReceivers.AddRange(multicastReceiver.eventReceivers);
        } else {
          eventReceivers.Add(eventReceiver);
        }
      }
    }

    public override void OnContentTypeProvisioned(SPContentTypeProvisionEventArgs eventArgs) {
      foreach (SPModelProvisionEventReceiver eventReceiver in eventReceivers) {
        eventReceiver.OnContentTypeProvisioned(eventArgs);
      }
    }

    public override void OnContentTypeProvisioning(SPContentTypeProvisionEventArgs eventArgs) {
      foreach (SPModelProvisionEventReceiver eventReceiver in eventReceivers) {
        eventReceiver.OnContentTypeProvisioning(eventArgs);
        if (eventArgs.Cancel) {
          break;
        }
      }
    }

    public override void OnFieldProvisioned(SPFieldProvisionEventArgs eventArgs) {
      foreach (SPModelProvisionEventReceiver eventReceiver in eventReceivers) {
        eventReceiver.OnFieldProvisioned(eventArgs);
      }
    }

    public override void OnFieldProvisioning(SPFieldProvisionEventArgs eventArgs) {
      foreach (SPModelProvisionEventReceiver eventReceiver in eventReceivers) {
        eventReceiver.OnFieldProvisioning(eventArgs);
        if (eventArgs.Cancel) {
          break;
        }
      }
    }

    public override void OnListProvisioned(SPListProvisionEventArgs eventArgs) {
      foreach (SPModelProvisionEventReceiver eventReceiver in eventReceivers) {
        eventReceiver.OnListProvisioned(eventArgs);
      }
    }

    public override void OnListProvisioning(SPListProvisionEventArgs eventArgs) {
      foreach (SPModelProvisionEventReceiver eventReceiver in eventReceivers) {
        eventReceiver.OnListProvisioning(eventArgs);
        if (eventArgs.Cancel) {
          break;
        }
      }
    }

    public override void OnListViewProvisioned(SPListViewProvisionEventArgs eventArgs) {
      foreach (SPModelProvisionEventReceiver eventReceiver in eventReceivers) {
        eventReceiver.OnListViewProvisioned(eventArgs);
      }
    }

    public override void OnListViewProvisioning(SPListViewProvisionEventArgs eventArgs) {
      foreach (SPModelProvisionEventReceiver eventReceiver in eventReceivers) {
        eventReceiver.OnListViewProvisioning(eventArgs);
        if (eventArgs.Cancel) {
          break;
        }
      }
    }
  }
}
