using System;
using System.Management.Automation;

namespace Codeless.SharePoint.PowerShell {
  public abstract class CmdletBase : PSCmdlet {
    protected void ThrowTerminatingError(Exception ex, ErrorCategory category) {
      ThrowTerminatingError(new ErrorRecord(ex, String.Empty, category, null));
    }
  }
}
