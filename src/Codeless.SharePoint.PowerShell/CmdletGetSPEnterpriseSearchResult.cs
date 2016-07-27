using Microsoft.Office.Server.Search.Query;
using Microsoft.SharePoint.PowerShell;
using System;
using System.Data;
using System.Linq;
using System.Management.Automation;

namespace Codeless.SharePoint.PowerShell {
  [Cmdlet(VerbsCommon.Get, "SPEnterpriseSearchResult")]
  public class CmdletGetSPEnterpriseSearchResult : CmdletBase {
    [Parameter(ValueFromPipeline = true, Mandatory = true, Position = 0)]
    public SPSitePipeBind Site { get; set; }

    [Parameter(Mandatory = true, Position = 1)]
    public string Query { get; set; }

    [Parameter(ValueFromRemainingArguments = true)]
    public string[] Select { get; set; }

    protected override void ProcessRecord() {
      base.ProcessRecord();
      try {
        KeywordQuery query = new KeywordQuery(this.Site.Read());
        query.QueryText = this.Query;
        query.SelectProperties.AddRange(this.Select);

        SearchExecutor executor = new SearchExecutor();
        ResultTableCollection result = executor.ExecuteQuery(query);
        ResultTable resultTable = result.Filter("TableType", KnownTableTypes.RelevantResults).FirstOrDefault();
        if (resultTable == null) {
          throw new Exception("Search executor did not return result table of type RelevantResults");
        }
        DataTable dataTable = new DataTable();
        dataTable.Load(resultTable);

        foreach (DataRow row in dataTable.Rows) {
          PSObject obj = new PSObject();
          foreach (DataColumn column in dataTable.Columns) {
            obj.Members.Add(new PSNoteProperty(column.Caption, row[column]));
          }
          WriteObject(obj);
        }
      } catch (Exception ex) {
        ThrowTerminatingError(ex, ErrorCategory.NotSpecified);
      }
    }
  }
}
