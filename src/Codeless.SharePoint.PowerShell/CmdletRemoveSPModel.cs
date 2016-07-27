using Codeless.SharePoint.ObjectModel;
using System;
using System.Management.Automation;

namespace Codeless.SharePoint.PowerShell {
  [Cmdlet(VerbsCommon.Remove, "SPModel")]
  public class CmdletRemoveSPModel : CmdletBase {
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
    [ValidateNotNull]
    public SPModel Input { get; set; }

    protected override void ProcessRecord() {
      base.ProcessRecord();
      try {
        this.Input.Manager.Delete(this.Input);
      } catch (Exception ex) {
        ThrowTerminatingError(ex, ErrorCategory.NotSpecified);
      }
    }
  }
}
