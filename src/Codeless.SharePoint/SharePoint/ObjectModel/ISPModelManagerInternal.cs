using System.Collections.Generic;

namespace Codeless.SharePoint.ObjectModel {
  internal interface ISPModelManagerInternal : ISPModelManager {
    SPModelDescriptor Descriptor { get; }
    SPObjectCache ObjectCache { get; set; }
    SPModelImplicitQueryMode ImplicitQueryMode { get; }
    IEnumerable<SPModelUsage> ContextLists { get; }

    SPModel TryCreateModel(ISPListItemAdapter item, bool readOnly);
    void SaveOnCommit(SPModel item);
  }
}
