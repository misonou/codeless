using System.Management.Automation;

namespace Codeless.SharePoint.PowerShell {
  [Cmdlet(VerbsCommon.Get, "SPModelManager")]
  public class CmdletGetSPModelManager : CmdletBaseSPModel {
    protected override void ProcessRecord() {
      base.ProcessRecord();
      WriteObject(this.Manager);
    }
  }
}
