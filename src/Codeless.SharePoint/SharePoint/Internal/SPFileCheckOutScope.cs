using Microsoft.SharePoint;
using System;

namespace Codeless.SharePoint.Internal {
  internal class SPFileCheckOutScope : IDisposable {
    private readonly SPFile file;
    private readonly string comment;
    private readonly bool publishOnDispose;
    private bool disposed;

    public SPFileCheckOutScope(SPFile file, bool checkOutBeforeUse, bool publishOnDispose, string comment) {
      CommonHelper.ConfirmNotNull(file, "file");
      this.file = file;
      this.comment = comment;
      this.publishOnDispose = publishOnDispose;

      if (checkOutBeforeUse && file.CheckOutType == SPFile.SPCheckOutType.None) {
        file.CheckOut();
      }
    }

    public void Dispose() {
      if (!disposed) {
        SPFile file = this.file.Web.GetFile(this.file.UniqueId);
        if (file.Item != null) {
          if (file.CheckOutType != SPFile.SPCheckOutType.None) {
            file.CheckIn(comment, file.Item.ParentList.EnableMinorVersions ? SPCheckinType.MinorCheckIn : SPCheckinType.MajorCheckIn);
          }
          if (publishOnDispose && file.Item.Level != SPFileLevel.Published) {
            if (file.Item.ParentList.EnableModeration) {
              file.Approve(comment);
            }
            if (file.Item.ParentList.EnableMinorVersions) {
              file.Publish(comment);
            }
            file.Item.EnsureApproved();
          }
        }
        disposed = true;
      }
    }
  }
}
