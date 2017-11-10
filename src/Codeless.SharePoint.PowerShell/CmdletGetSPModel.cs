using Codeless.SharePoint.ObjectModel;
using Codeless.SharePoint.ObjectModel.Linq;
using IQToolkit;
using Microsoft.Office.Server.Search.Query;
using System;
using System.Linq;
using System.Linq.Dynamic;
using System.Management.Automation;

namespace Codeless.SharePoint.PowerShell {
  [Cmdlet(VerbsCommon.Get, "SPModel", DefaultParameterSetName = "Default")]
  public class CmdletGetSPModel : CmdletBaseSPModel {
    private IQueryable query;

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
        WriteObject(query, true);
      } catch (Exception ex) {
        ThrowTerminatingError(ex, ErrorCategory.NotSpecified);
      }
    }

    protected override void OnManagerResolved() {
      base.OnManagerResolved();
      query = this.GetType().GetMethod("ProcessQuery", true).MakeGenericMethod(this.Manager.Descriptor.ModelType, this.Descriptor.ModelType).Invoke<IQueryable>(this);
    }

    private IQueryable ProcessQuery<T, U>() {
      IQueryable<U> queryable;
      if (this.ParameterSetName == "SearchAny" || this.ParameterSetName == "SearchAll") {
        queryable = ((ISPModelManager)this.Manager).Query<U>(this.Search, this.All.IsPresent ? KeywordInclusion.AllKeywords : KeywordInclusion.AnyKeyword);
      } else {
        queryable = ((ISPModelManager)this.Manager).Query<U>();
      }
      if (this.Where != null) {
        queryable = queryable.Where(this.Where);
      }
      if (this.ParameterSetName == "ID") {
        queryable = queryable.Where(v => ((ISPModelMetaData)v).ID == this.ID.Value);
      } else if (this.ParameterSetName == "UniqueId") {
        queryable = queryable.Where(v => ((ISPModelMetaData)v).UniqueId == this.UniqueId.Value);
      }
      if (this.Order != null) {
        queryable = queryable.OrderBy(this.Order);
      }
      if (this.Limit.HasValue) {
        queryable = queryable.Take((int)this.Limit.Value);
      }
      return queryable;
    }
  }
}
