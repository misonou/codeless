using Codeless.SharePoint.ObjectModel;
using Codeless.SharePoint.ObjectModel.Linq;
using Microsoft.Office.Server.Search.Query;
using System;
using System.Linq;
using System.Linq.Dynamic;
using System.Management.Automation;

namespace Codeless.SharePoint.PowerShell {
  [Cmdlet(VerbsCommon.Get, "SPModel", DefaultParameterSetName = "Default")]
  public class CmdletGetSPModel : CmdletBaseSPModel {
    private CamlExpression query;

    [Parameter(ParameterSetName = "ID", Mandatory = true)]
    public int? ID { get; set; }

    [Parameter(ParameterSetName = "UniqueId", Mandatory = true)]
    public Guid? UniqueId { get; set; }

    [Parameter(ParameterSetName = "SearchAll", ValueFromRemainingArguments = true)]
    [Parameter(ParameterSetName = "SearchAny", ValueFromRemainingArguments = true)]
    public string[] Search { get; set; }

    [Parameter(ParameterSetName = "SearchAll", Mandatory = true)]
    public SwitchParameter All { get; set; }

    [Parameter(ParameterSetName = "SearchAny", Mandatory = true)]
    public SwitchParameter Any { get; set; }

    [Parameter(ParameterSetName = "ID")]
    [Parameter(ParameterSetName = "SearchAll")]
    [Parameter(ParameterSetName = "SearchAny")]
    [Parameter(ParameterSetName = "Default")]
    public string Where { get; set; }

    [Parameter(ParameterSetName = "ID")]
    [Parameter(ParameterSetName = "SearchAll")]
    [Parameter(ParameterSetName = "SearchAny")]
    [Parameter(ParameterSetName = "Default")]
    public string Order { get; set; }

    [Parameter(ParameterSetName = "ID")]
    [Parameter(ParameterSetName = "SearchAll")]
    [Parameter(ParameterSetName = "SearchAny")]
    [Parameter(ParameterSetName = "Default")]
    public uint? Limit { get; set; }

    protected override void ProcessRecord() {
      base.ProcessRecord();
      try {
        SPModelCollection result;
        if (this.ParameterSetName == "ID") {
          result = base.Manager.GetItems(query + Caml.Equals(SPBuiltInFieldName.ID, this.ID.Value), this.Limit.GetValueOrDefault(100));
        } else if (this.ParameterSetName == "UniqueId") {
          result = base.Manager.GetItems(Caml.Equals(SPBuiltInFieldName.UniqueId, this.UniqueId.Value), 1u);
        } else if (this.ParameterSetName == "SearchAny" || this.ParameterSetName == "SearchAll") {
          result = base.Manager.GetItems(query, this.Limit.GetValueOrDefault(100), this.Search, this.All.IsPresent ? KeywordInclusion.AllKeywords : KeywordInclusion.AnyKeyword);
        } else {
          result = base.Manager.GetItems(query, this.Limit.GetValueOrDefault(100));
        }
        WriteObject(result, true);
      } catch (Exception ex) {
        ThrowTerminatingError(ex, ErrorCategory.NotSpecified);
      }
    }

    protected override void OnManagerResolved() {
      base.OnManagerResolved();
      query = this.GetType().GetMethod("ProcessQuery", true).MakeGenericMethod(this.Manager.Descriptor.ModelType).Invoke< CamlExpression>(this);
    }

    private CamlExpression ProcessQuery<T>() {
      if (this.Where != null || this.Order != null) {
        IQueryable<T> queryable = ((SPModelManagerBase<T>)this.Manager).Query();
        if (this.Where != null) {
          queryable = queryable.Where(this.Where);
        }
        if (this.Order != null) {
          queryable = queryable.OrderBy(this.Order);
        }
        SPModelQueryExpressionTranslateResult result = ((SPModelQueryProvider<T>)queryable.Provider).Translate(queryable.Expression);
        return result.Expression;
      }
      return null;
    }
  }
}
