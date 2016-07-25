using Codeless.SharePoint.ObjectModel;
using System;
using System.Management.Automation;

namespace Codeless.SharePoint.PowerShell {
  [Cmdlet(VerbsCommon.New, "SPModel")]
  public class CmdletNewSPModel : CmdletBaseSPModelDynamicParameter {
    protected override void ProcessRecord() {
      base.ProcessRecord();
      try {
        SPModel item = (SPModel)this.Manager.Create(this.Descriptor.ModelType);
        UpdateModelFromParameters(item);
        this.Manager.CommitChanges();
        WriteObject(item);
      } catch (Exception ex) {
        ThrowTerminatingError(ex, ErrorCategory.NotSpecified);
      }
    }
  }
}
