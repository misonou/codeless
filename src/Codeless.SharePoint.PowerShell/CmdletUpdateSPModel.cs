using Codeless.SharePoint.ObjectModel;
using Microsoft.SharePoint.PowerShell;
using System;
using System.Management.Automation;

namespace Codeless.SharePoint.PowerShell {
  [Cmdlet(VerbsData.Update, "SPModel")]
  public class CmdletUpdateSPModel : CmdletBaseSPModelDynamicParameter {
    [Parameter(Mandatory = true, ValueFromPipeline = true, Position = 0)]
    [ValidateNotNull]
    public SPModel Input { get; set; }
    
    [Parameter]
    public new SPWebPipeBind Web { get; set; }

    protected override void ProcessRecord() {
      base.ProcessRecord();
      try {
        UpdateModelFromParameters(this.Input);
        this.Input.Manager.CommitChanges(this.Input);
      } catch (Exception ex) {
        ThrowTerminatingError(ex, ErrorCategory.NotSpecified);
      }
    }
  }
}
