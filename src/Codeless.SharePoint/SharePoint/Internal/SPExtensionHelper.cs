using Microsoft.SharePoint;
using Microsoft.SharePoint.Taxonomy;
using Microsoft.SharePoint.Workflow;
using System;
using System.Collections.Generic;

namespace Codeless.SharePoint.Internal {
  internal static class SPExtensionHelper {
    public delegate SPWorkflowAssociation SPWorkflowAssociationCreator(SPWorkflowTemplate template, string name, SPList taskList, SPList historyList);

    public static SPWorkflowAssociation EnsureWorkflowAssociation(SPWorkflowAssociationCollection collection, Guid workflowBaseId, SPWorkflowAssociationCreator createDelegate, out bool associationUpdated) {
      CommonHelper.ConfirmNotNull(collection, "collection");
      CommonHelper.ConfirmNotNull(createDelegate, "createDelegate");
      associationUpdated = false;

      SPWeb targetWeb = collection.ParentWeb;
      SPWorkflowAssociation wfAssoc = collection.GetAssociationByBaseIDSafe(workflowBaseId);

      if (wfAssoc == null) {
        SPWorkflowTemplate wfTemplate = targetWeb.WorkflowTemplates[workflowBaseId];
        if (wfTemplate == null) {
          throw new ArgumentOutOfRangeException("workflowBaseId", "Workflow template with the specified base ID does not exist in this site");
        }
        SPList taskList = EnsureList(targetWeb, SPListTemplateType.Tasks, SPResource.GetString("DefaultWorkflowTaskListName", new object[0]));
        SPList historyList = EnsureList(targetWeb, SPListTemplateType.WorkflowHistory, SPResource.GetString("DefaultWorkflowHistoryListName", new object[0]));
        wfAssoc = createDelegate(wfTemplate, wfTemplate.Name, taskList, historyList);
        collection.Add(wfAssoc);
        associationUpdated = true;
      }
      if (!wfAssoc.Enabled) {
        wfAssoc.Enabled = true;
        collection.Update(wfAssoc);
        associationUpdated = true;
      }
      return wfAssoc;
    }

    public static SPList EnsureList(SPWeb targetWeb, SPListTemplateType templateType, string defaultTitle) {
      CommonHelper.ConfirmNotNull(targetWeb, "targetWeb");
      CommonHelper.ConfirmNotNull(defaultTitle, "defaultTitle");
      foreach (SPList list in targetWeb.Lists) {
        if (list.BaseTemplate == templateType) {
          return list;
        }
      }
      Guid listId = targetWeb.Lists.Add(defaultTitle, String.Empty, templateType);
      return targetWeb.Lists[listId];
    }

    public static bool UpdateTaxonomyFieldValue(SPSite targetSite, TermSet termSet, TaxonomyFieldValue fieldValue, Dictionary<Guid, TaxonomyFieldValue> mappedValues) {
      CommonHelper.ConfirmNotNull(targetSite, "targetSite");
      CommonHelper.ConfirmNotNull(termSet, "termSet");
      CommonHelper.ConfirmNotNull(fieldValue, "fieldValue");
      CommonHelper.ConfirmNotNull(mappedValues, "mappedValues");

      Guid originalGuid = new Guid(fieldValue.TermGuid);
      TaxonomyFieldValue newValue;
      if (mappedValues.TryGetValue(originalGuid, out newValue)) {
        if (newValue != null && newValue.TermGuid != originalGuid.ToString()) {
          fieldValue.TermGuid = newValue.TermGuid;
          fieldValue.WssId = newValue.WssId;
          return true;
        }
        return false;
      }
      Term originalTerm = termSet.GetTerm(originalGuid);
      if (originalTerm != null) {
        mappedValues.Add(originalGuid, null);
        return false;
      }
      TermCollection matchedTerms = termSet.GetTerms(fieldValue.Label, false);
      if (matchedTerms.Count > 0) {
        mappedValues.Add(originalGuid, fieldValue);
        fieldValue.TermGuid = matchedTerms[0].Id.ToString();
        fieldValue.WssId = matchedTerms[0].EnsureWssId(targetSite, false);
        return true;
      }
      mappedValues.Add(originalGuid, null);
      return false;
    }

    public static SPList GetTaxonomyHiddenList(SPSite site) {
      CommonHelper.ConfirmNotNull(site, "site");
       if (!site.RootWeb.Properties.ContainsKey("TaxonomyHiddenList")) {
         throw new InvalidOperationException(String.Format("Managed metadata feature id not activated at site {0}", site.Url));
       }
       using (new SPSecurity.SuppressAccessDeniedRedirectInScope()) {
         Guid taxonomyHiddenListId = new Guid(site.RootWeb.Properties["TaxonomyHiddenList"]);
         try {
           return site.RootWeb.Lists[taxonomyHiddenListId];
         } catch (UnauthorizedAccessException) {
           if (ClaimsContext.Current.IsAnonymous) {
             site.WithElevatedPrivileges(elevatedSite => {
               SPList elevatedList = elevatedSite.RootWeb.Lists[taxonomyHiddenListId];
               elevatedList.AnonymousPermMask64 = SPBasePermissions.ViewListItems | SPBasePermissions.OpenItems | SPBasePermissions.ViewVersions | SPBasePermissions.Open | SPBasePermissions.UseClientIntegration;
               elevatedList.Update();
             });
             return site.RootWeb.Lists[taxonomyHiddenListId];
           } else {
             throw;
           }
         }
       }
    }
  }
}
