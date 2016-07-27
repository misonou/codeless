namespace Codeless.SharePoint.ObjectModel {
  internal interface ISPModelManagerInternal : ISPModelManager {
    SPModelDescriptor Descriptor { get; }
    SPObjectCache ObjectCache { get; }
    SPModel TryCreateModel(ISPListItemAdapter item, bool readOnly);
    void SaveOnCommit(ISPListItemAdapter item);
  }
}
