using Microsoft.SharePoint.Administration;

namespace Codeless.SharePoint.Internal {
  internal static class TraceCategory {
    public static readonly SPDiagnosticsCategory General = new SPDiagnosticsCategory("General", TraceSeverity.Unexpected, EventSeverity.Error);
    public static readonly SPDiagnosticsCategory ModelProvisionVerbose = new SPDiagnosticsCategory("Model Provision", TraceSeverity.Monitorable, EventSeverity.Information);
    public static readonly SPDiagnosticsCategory ModelProvision = new SPDiagnosticsCategory("Model Provision", TraceSeverity.Unexpected, EventSeverity.Error);
    public static readonly SPDiagnosticsCategory ModelQuery = new SPDiagnosticsCategory("Model Query", TraceSeverity.Unexpected, EventSeverity.Error);
    public static readonly SPDiagnosticsCategory SiteConfig = new SPDiagnosticsCategory("Site Config", TraceSeverity.Unexpected, EventSeverity.Error);
  }
}
