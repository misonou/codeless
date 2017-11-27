using Codeless.SharePoint.ObjectModel;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Taxonomy;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Codeless.SharePoint {
  internal class CamlParameterBindingHashtable : Hashtable, ISPObjectContext {
    private readonly ISPModelManagerInternal manager;

    public CamlParameterBindingHashtable(ISPModelManager manager) {
      CommonHelper.ConfirmNotNull(manager, "manager");
      this.manager = (ISPModelManagerInternal)manager;
    }

    public SPSite Site {
      get { return manager.Site; }
    }

    public TermStore TermStore {
      get { return manager.TermStore; }
    }
  }
}
