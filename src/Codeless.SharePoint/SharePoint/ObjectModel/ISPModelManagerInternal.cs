using System.Collections.Generic;

namespace Codeless.SharePoint.ObjectModel {
  internal interface ISPModelManagerInternal : ISPModelManager {
    SPModelDescriptor Descriptor { get; }
    SPObjectCache ObjectCache { get; set; }
    IEnumerable<SPModelUsage> ContextLists { get; }

    SPModel TryCreateModel(ISPListItemAdapter item, bool readOnly);
    void SaveOnCommit(ISPListItemAdapter item);
  }
}
