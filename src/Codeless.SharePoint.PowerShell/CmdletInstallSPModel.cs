using Codeless.SharePoint.ObjectModel;
using System;
using System.Management.Automation;

namespace Codeless.SharePoint.PowerShell {
  [Cmdlet(VerbsLifecycle.Install, "SPModel")]
  public class CmdletInstallSPModel : CmdletBaseSPModel {
    [Parameter]
    public string ListUrl { get; set; }

    [Parameter]
    public SwitchParameter SuppressList { get; set; }

    [Parameter]
    public SwitchParameter Force { get; set; }
    
    protected override void OnManagerResolved() {
      base.OnManagerResolved();
      try {
        SPModelProvisionOptions opts = SPModelProvisionOptions.None;
        SPModelListProvisionOptions options;
        if (this.ListUrl != null) {
          options = new SPModelListProvisionOptions(this.ListUrl);
        } else {
          options = SPModelListProvisionOptions.Default;
        }
        if (this.Force.IsPresent) {
          opts = opts | SPModelProvisionOptions.ForceProvisionContentType;
        }
        if (this.SuppressList.IsPresent) {
          opts = opts | SPModelProvisionOptions.SuppressListCreation;
        }
        this.Descriptor.Provision(this.Web.Read(), opts, options);
      } catch (Exception ex) {
        ThrowTerminatingError(ex, ErrorCategory.NotSpecified);
      }
    }
  }
}
