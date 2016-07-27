using Codeless.SharePoint.ObjectModel;
using Microsoft.SharePoint.PowerShell;
using System;
using System.Management.Automation;
using System.Reflection;

namespace Codeless.SharePoint.PowerShell {
  public abstract class CmdletBaseSPModel : CmdletBase {
    private bool webFromPipe;

    [Parameter(Mandatory = true, Position = 0)]
    [ValidateNotNullOrEmpty]
    public string TypeName { get; set; }

    [Parameter(Mandatory = true, ValueFromPipeline = true, Position = 1)]
    [ValidateNotNullOrEmpty]
    public SPWebPipeBind Web { get; set; }

    [Parameter]
    public string AssemblyName { get; set; }

    internal SPModelDescriptor Descriptor { get; private set; }
    internal ISPModelManagerInternal Manager { get; private set; }

    protected override void BeginProcessing() {
      base.BeginProcessing();
      try {
        if (this.AssemblyName != null) {
          Assembly.LoadWithPartialName(this.AssemblyName);
        }
        if (this.Web != null) {
          ResolveManager();
        } else {
          webFromPipe = true;
        }
      } catch (ArgumentException) {
        ThrowTerminatingError(new ArgumentException("TypeName"), ErrorCategory.InvalidArgument);
      }
    }

    protected override void ProcessRecord() {
      base.ProcessRecord();
      try {
        if (webFromPipe) {
          ResolveManager();
        }
      } catch (Exception ex) {
        ThrowTerminatingError(ex, ErrorCategory.NotSpecified);
      }
    }

    protected void ResolveManager() {
      SPModelDescriptor.RegisterReferencedAssemblies(this.Web.Read().Site);
      this.Descriptor = SPModelDescriptor.Resolve(this.TypeName);
      this.Manager = this.Descriptor.CreateManager(this.Web.Read());
      OnManagerResolved();
    }

    protected virtual void OnManagerResolved() { }
  }
}
