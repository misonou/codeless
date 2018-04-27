using Codeless.SharePoint.Internal;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Administration;
using Microsoft.SharePoint.Navigation;
using Microsoft.SharePoint.Taxonomy;
using Microsoft.SharePoint.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;

namespace Codeless.SharePoint.ObjectModel {
  /// <summary>
  /// Specifies the value when provisioning a <see cref="Boolean"/> property.
  /// </summary>
  public enum SPOption {
    /// <summary>
    /// Does not update the <see cref="Boolean"/> property.
    /// </summary>
    Unspecified,
    /// <summary>
    /// Updates the <see cref="Boolean"/> property to *true*.
    /// </summary>
    True,
    /// <summary>
    /// Updates the <see cref="Boolean"/> property to *false*.
    /// </summary>
    False
  }

  internal sealed class SPModelProvisionHelper : IDisposable {
    private const string CTDocNamespaceUri = "http://sharepoint.codeless.org/ct";

    private static readonly Map<string, string> FieldLinkPropertyMapping = new Map<string, string> {
      { "Title", "DisplayName" },
      { "ReadOnlyField", "ReadOnly" }
    };

    private static readonly Dictionary<string, string> LinkWithMenuFieldMapping = new Dictionary<string, string> {
      { SPBuiltInFieldName.Title, SPBuiltInFieldName.LinkTitle },
      { SPBuiltInFieldName.FileLeafRef, SPBuiltInFieldName.LinkFilename }
    };

    private static readonly MethodInfo TrySetFromNullableMethod = typeof(SPModelProvisionHelper).GetMethod("TrySetFromNullable", true);
    private static readonly MethodInfo TrySetToNullableMethod = typeof(SPModelProvisionHelper).GetMethod("TrySetToNullable", true);
    private static readonly MethodInfo GetFieldAttributeValueMethod = typeof(SPField).GetMethod("GetFieldAttributeValue", true, typeof(string));
    private static readonly MethodInfo SetFieldAttributeValueMethod = typeof(SPField).GetMethod("SetFieldAttributeValue", true);
    private static readonly Guid CTypesFeatureID = new Guid("695b6570-a48b-4a8e-8ea5-26ea7fc1d162");

    private readonly SPModelProvisionEventReceiver eventReceiver;
    private TermStore termStore;
    private AssertionCollection assertions;

    public SPModelProvisionHelper(Guid siteID, SPModelProvisionEventReceiver eventReceiver) {
      CommonHelper.ConfirmNotNull(eventReceiver, "eventReceiver");
      this.TargetSite = new SPSite(siteID, SPUserToken.SystemAccount);
      this.TargetSiteId = siteID;
      this.TargetSiteUrl = this.TargetSite.Url;
      this.TargetSite.RootWeb.AllowUnsafeUpdates = true;

      TaxonomySession session = new TaxonomySession(this.TargetSite);
      this.termStore = session.DefaultKeywordsTermStore;
      this.eventReceiver = eventReceiver;
      this.ObjectCache = new SPObjectCache(this.TargetSite);
      this.ObjectCache.AddWeb(this.TargetSite.RootWeb);
    }

    public SPSite TargetSite { get; private set; }
    public Guid TargetSiteId { get; private set; }
    public string TargetSiteUrl { get; private set; }
    public SPObjectCache ObjectCache { get; private set; }

    public SPContentType EnsureContentType(SPContentTypeAttribute definition) {
      CommonHelper.ConfirmNotNull(definition, "definition");
      SPContentType contentType = this.ObjectCache.GetContentType(definition.ContentTypeId);
      if (contentType == null) {
        SPContentTypeCollection contentTypes = this.TargetSite.RootWeb.ContentTypes;
        contentType = contentTypes[definition.ContentTypeId];
        if (contentType == null) {
          contentType = new SPContentType(definition.ContentTypeId, contentTypes, definition.Name);
          contentTypes.Add(contentType);
        } else if (contentType.FeatureId == CTypesFeatureID) {
          throw new SPModelProvisionException(String.Format("System content type cannot be provisioned. Consider derive child content type or set ExternalContentType to true. {0}.", definition.ContentTypeIdString));
        }
        this.ObjectCache.AddContentType(contentType);
      }
      return contentType;
    }

    public SPList EnsureList(SPWeb targetWeb, SPListAttribute definition, out List<SPContentTypeId> contentTypesToRemove) {
      CommonHelper.ConfirmNotNull(targetWeb, "targetWeb");
      CommonHelper.ConfirmNotNull(definition, "definition");
      if (String.IsNullOrEmpty(definition.Url)) {
        throw new ArgumentException("List URL cannot be empty.", "definition");
      }
      contentTypesToRemove = new List<SPContentTypeId>();

      string serverRelativeUrl = SPUrlUtility.CombineUrl(targetWeb.ServerRelativeUrl, definition.Url);
      SPList list = this.ObjectCache.TryGetList(serverRelativeUrl);
      if (list == null) {
        using (targetWeb.GetAllowUnsafeUpdatesScope()) {
          string listTitle = definition.Title ?? definition.Url.TrimStart('/').Replace('/', ' ');
          Guid listId;
          try {
            listId = targetWeb.Lists.Add(listTitle, definition.Description, definition.Url, String.Empty, (int)definition.ListTemplateType, "100");
          } catch (SPException ex) {
            throw new SPModelProvisionException(String.Format("Unable to create list at {0}", serverRelativeUrl), ex);
          }
          list = targetWeb.Lists[listId];

          if (definition.OnQuickLaunch && definition.ListTemplateType == SPListTemplateType.GenericList) {
            AddToQuickLaunch(list);
          }
          list.ContentTypesEnabled = true;
          contentTypesToRemove.AddRange(list.ContentTypes.OfType<SPContentType>().Select(v => v.Id));
          contentTypesToRemove.Remove(contentTypesToRemove.FirstOrDefault(v => v.IsChildOf(SPBuiltInContentTypeId.Folder)));
        }
        this.ObjectCache.AddList(list);
      }
      return list;
    }

    public bool UpdateField(SPField field, SPFieldAttribute definition) {
      CommonHelper.ConfirmNotNull(field, "field");
      CommonHelper.ConfirmNotNull(definition, "definition");
      if (definition.ProvisionMode == SPFieldProvisionMode.None || (definition.ProvisionMode == SPFieldProvisionMode.FieldLink && field.ParentList == null)) {
        return false;
      }
      using (CreateTraceScope(field)) {
        if (field.ParentList == null && definition.Type == SPFieldType.Lookup && !field.IsBuiltIn()) {
          bool needUpdate = false;
          if (((SPLookupFieldAttribute)definition).LookupSource != SPFieldLookupSource.None) {
            needUpdate |= SetFieldAttribute(field, "X-LookupSource", ((SPLookupFieldAttribute)definition).LookupSource.ToString());
            needUpdate |= SetFieldAttribute(field, "X-LookupListUrl", ((SPLookupFieldAttribute)definition).LookupListUrl);
          } else {
            needUpdate |= SetFieldAttribute(field, "X-LookupSource", String.Empty);
            needUpdate |= SetFieldAttribute(field, "X-LookupListUrl", String.Empty);
          }
          if (needUpdate) {
            field.Update();
          }
        }
        if ((field.ParentList == null && field.IsBuiltIn()) || (field.ParentList != null && field.IsSystemField())) {
          return false;
        }
        SPFieldProvisionEventArgs eventArgs = new SPFieldProvisionEventArgs();
        eventArgs.Site = this.TargetSite;
        eventArgs.ParentList = field.ParentList;
        eventArgs.Definition = definition.Clone();
        eventReceiver.OnFieldProvisioning(eventArgs);

        if (!eventArgs.Cancel) {
          if (eventArgs.Definition is TaxonomyFieldAttribute) {
            TaxonomyFieldAttribute taxFieldDefinition = (TaxonomyFieldAttribute)eventArgs.Definition;
            if (!String.IsNullOrEmpty(taxFieldDefinition.TermSetId)) {
              Guid termSetId = Guid.Empty;
              try {
                termSetId = new Guid(taxFieldDefinition.TermSetId);
              } catch (FormatException) { }
              if (termSetId != Guid.Empty && termStore != null) {
                try {
                  termStore.EnsureTermSet(termSetId, taxFieldDefinition.DefaultGroupName, taxFieldDefinition.DefaultTermSetName);
                } catch (UnauthorizedAccessException) {
                  throw new SPModelProvisionException("Unable to create term set because of insufficient permission.");
                }
                eventArgs.TargetModified |= CopyProperties(new { SspId = termStore.Id, TermSetId = termSetId, AnchorId = Guid.Empty }, field);
              }
            }
          }
          if (!(eventArgs.Definition is IAllowMultipleValue) || ((IAllowMultipleValue)eventArgs.Definition).FieldObjectType != field.GetType() || ((IAllowMultipleValue)eventArgs.Definition).AllowMultipleValues != SPOption.Unspecified) {
            if (CopyProperties(new { TypeAsString = eventArgs.Definition.TypeAsString }, field)) {
              try {
                field.Update();
                eventArgs.TargetModified = true;
              } catch (Exception ex) {
                WriteTrace(ex.Message);
              }
            }
          }
          eventArgs.TargetModified |= CopyProperties(eventArgs.Definition, field);
          if (eventArgs.TargetModified) {
            if (field.ParentList == null) {
              field.Update();
            }
          }
          eventReceiver.OnFieldProvisioned(eventArgs);
        }
        return eventArgs.TargetModified;
      }
    }

    public bool UpdateContentType(SPContentType contentType, SPContentTypeAttribute definition, SPFieldAttribute[] fieldDefinitions, string checksum) {
      CommonHelper.ConfirmNotNull(contentType, "contentType");
      CommonHelper.ConfirmNotNull(definition, "definition");
      CommonHelper.ConfirmNotNull(fieldDefinitions, "fieldDefinitions");

      Dictionary<SPFieldAttribute, SPField> fieldDictionary = new Dictionary<SPFieldAttribute, SPField>();
      foreach (SPFieldAttribute fieldDefinition in fieldDefinitions) {
        if (!fieldDictionary.ContainsKey(fieldDefinition)) {
          SPField field = EnsureField(fieldDefinition);
          if (contentType.ParentList == null) {
            UpdateField(field, fieldDefinition);
          }
          fieldDictionary.Add(fieldDefinition, field);
        }
      }

      string[] listColumnOrder = MergeFieldOrder(contentType, fieldDictionary.Keys, true);
      string[] siteColumnOrder = MergeFieldOrder(contentType, fieldDictionary.Keys, false);

      SPContentTypeProvisionEventArgs eventArgs = new SPContentTypeProvisionEventArgs();
      eventArgs.Site = this.TargetSite;
      eventArgs.ParentList = contentType.ParentList;
      eventArgs.Definition = definition.Clone();
      eventArgs.FieldOrder = new List<string>((contentType.ParentList == null) ? siteColumnOrder : listColumnOrder);
      eventReceiver.OnContentTypeProvisioning(eventArgs);

      if (!eventArgs.Cancel) {
        using (CreateTraceScope(contentType)) {
          SPFieldLinkCollection fieldLinks = contentType.FieldLinks;
          bool fieldLinkAdded = false;

          List<Guid> fieldLinkToRemove = new List<Guid>();
          foreach (SPFieldLink fieldRef in fieldLinks) {
            if (!listColumnOrder.Contains(fieldRef.Name) && !fieldDefinitions.Any(v => v.ListFieldInternalName == fieldRef.Name)) {
              WriteTrace("Remove FieldLink \"{0}\"", fieldRef.Name);
              fieldLinkToRemove.Add(fieldRef.Id);
            }
          }
          if (fieldLinkToRemove.Count > 0) {
            fieldLinkToRemove.ForEach(fieldLinks.Delete);
            eventArgs.TargetModified = true;
          }

          foreach (KeyValuePair<SPFieldAttribute, SPField> entry in fieldDictionary) {
            if (!SPModel.RequiredViewFields.Contains(entry.Value.InternalName) || entry.Value.InternalName == SPBuiltInFieldName.Title) {
              SPFieldLink fieldRef = fieldLinks[entry.Value.Id];
              if (fieldRef == null) {
                WriteTrace("Add FieldLink \"{0}\"", entry.Value.InternalName);
                if (contentType.ParentList != null && entry.Value.Type == SPFieldType.Lookup) {
                  EnsureListLookupField(contentType.ParentList, (SPFieldLookup)entry.Value, true);
                }
                using (contentType.ParentWeb.GetAllowUnsafeUpdatesScope()) {
                  fieldLinks.Add(new SPFieldLink(entry.Value));
                }
                fieldRef = fieldLinks[entry.Value.Id];
                fieldLinkAdded = true;
                eventArgs.TargetModified = true;
              }
              if (entry.Value.IsBuiltIn() || entry.Key.ProvisionMode != SPFieldProvisionMode.None) {
                using (CreateTraceScope(fieldRef)) {
                  eventArgs.TargetModified |= CopyProperties(entry.Key, fieldRef);
                }
              }
            }
          }

          string[] currentOrder = fieldLinks.OfType<SPFieldLink>().Select(v => (contentType.ParentList == null) ? this.TargetSite.RootWeb.Fields[v.Id].InternalName : v.Name).ToArray();
          string[] newOrder = eventArgs.FieldOrder.Intersect(currentOrder).ToArray();
          if (!currentOrder.Intersect(newOrder).SequenceEqual(newOrder)) {
            WriteTrace("Reorder FieldLinkCollection \"{0}\" (Original: \"{1}\")", String.Join(",", newOrder), String.Join(",", currentOrder));
            if (fieldLinkAdded) {
              // list content type need to be updated to correctly reorder newly added columns
              using (contentType.ParentWeb.GetAllowUnsafeUpdatesScope()) {
                contentType.Update();
              }
            }
            fieldLinks.Reorder(newOrder);
            eventArgs.TargetModified = true;
          }
          eventArgs.TargetModified |= CopyProperties(eventArgs.Definition, contentType);
        }
        using ((contentType.ParentWeb ?? this.TargetSite.RootWeb).GetAllowUnsafeUpdatesScope()) {
          if (eventArgs.TargetModified) {
            contentType.Update();
          }
          SetContentTypeChecksum(contentType, checksum);
        }
        eventReceiver.OnContentTypeProvisioned(eventArgs);
      }
      return eventArgs.TargetModified;
    }

    public bool UpdateList(SPList list, SPListAttribute definition, SPContentTypeAttribute contentTypeDefinition, SPFieldAttribute[] contentTypeFields, SPFieldAttribute[] hiddenFields, IList<SPContentTypeId> contentTypesToRemove, string checksum) {
      CommonHelper.ConfirmNotNull(list, "list");
      CommonHelper.ConfirmNotNull(definition, "definition");
      CommonHelper.ConfirmNotNull(contentTypeDefinition, "contentTypeDefinition");
      CommonHelper.ConfirmNotNull(contentTypeFields, "contentTypeFields");
      CommonHelper.ConfirmNotNull(hiddenFields, "hiddenFields");
      CommonHelper.ConfirmNotNull(contentTypesToRemove, "contentTypesToRemove");
      definition = definition.Clone();

      SPList cachedList = this.ObjectCache.AddList(list);
      SPListProvisionEventArgs eventArgs = new SPListProvisionEventArgs();
      eventArgs.Web = cachedList.ParentWeb;
      eventArgs.List = cachedList;
      eventArgs.Definition = definition;
      eventReceiver.OnListProvisioning(eventArgs);

      if (!eventArgs.Cancel) {
        assertions = new AssertionCollection(cachedList);
        try {
          List<SPField> listFieldsToUpdate = new List<SPField>();
          using (CreateTraceScope(cachedList)) {
            // register item event receivers to trigger model events based on content types
            EnsureListEventReceivers(cachedList.EventReceivers);

            // add fields to list before adding content type
            // content type with lookup fields cannot be added before those fields are added
            foreach (SPFieldAttribute field in contentTypeFields) {
              SPField listField = EnsureListField(cachedList, field, true);
              if (UpdateField(listField, field)) {
                listFieldsToUpdate.Add(listField);
              }
            }

            // add fields that are not contained in content type 
            // but are required in view fields of cross-list query of multiple unrelated content types
            foreach (SPFieldAttribute field in hiddenFields) {
              if (!SPModel.RequiredViewFields.Contains(field.InternalName)) {
                EnsureListField(cachedList, field, false);
              }
            }

            SPContentType listContentType = cachedList.ContentTypes.OfType<SPContentType>().FirstOrDefault(v => v.Id.Parent == contentTypeDefinition.ContentTypeId);
            if (listContentType == null) {
              SPContentType siteContentType = this.ObjectCache.GetContentType(contentTypeDefinition.ContentTypeId);
              try {
                using (cachedList.ParentWeb.GetAllowUnsafeUpdatesScope()) {
                  listContentType = cachedList.ContentTypes.Add(siteContentType);
                  UpdateContentType(listContentType, contentTypeDefinition, contentTypeFields, checksum);
                }
              } catch (SPException ex) {
                throw new SPModelProvisionException(String.Format("Unable to add content type '{0}' to list '{1}' because there is another content type with the same display name", contentTypeDefinition.Name, list.RootFolder.Url), ex);
              }
            }
            this.ObjectCache.AddContentType(listContentType);

            try {
              SPFieldIndex index = cachedList.FieldIndexes[SPBuiltInFieldId.ContentTypeId];
            } catch (ArgumentException) {
              SPField contentTypeIdField = cachedList.Fields[SPBuiltInFieldId.ContentTypeId];
              if (CopyProperties(new { Indexed = true }, contentTypeIdField)) {
                listFieldsToUpdate.Add(contentTypeIdField);
              }
              cachedList.FieldIndexes.Add(contentTypeIdField);
            }

            // remove unwanted content type that are most likely exist when list is created
            foreach (SPContentTypeId id in contentTypesToRemove) {
              cachedList.ContentTypes.Delete(id);
            }
            eventArgs.TargetModified |= CopyProperties(definition, cachedList);

            using (cachedList.ParentWeb.GetAllowUnsafeUpdatesScope()) {
              foreach (SPField field in listFieldsToUpdate) {
                field.Update();
              }
              if (eventArgs.TargetModified) {
                cachedList.Update();
              }

              SPFolder rootFolder = cachedList.RootFolder;
              List<SPContentType> visibleContentTypes = new List<SPContentType>(rootFolder.ContentTypeOrder);
              int index = visibleContentTypes.FindIndex(v => v.Id == listContentType.Id);
              if (contentTypeDefinition.HiddenInList ^ (index < 0)) {
                if (contentTypeDefinition.HiddenInList) {
                  visibleContentTypes.RemoveAt(index);
                } else {
                  visibleContentTypes.Add(listContentType);
                }
                rootFolder.UniqueContentTypeOrder = visibleContentTypes;
                rootFolder.Update();
              }
              SetContentTypeChecksum(list, contentTypeDefinition.ContentTypeId, checksum);
            }
            eventReceiver.OnListProvisioned(eventArgs);
          }

          string[] includedFields = contentTypeFields.Where(v => v.ShowInListView == SPOption.True).OrderBy(v => v.ColumnOrder).Select(v => v.ListFieldInternalName).ToArray();
          string[] excludedFields = contentTypeFields.Where(v => v.ShowInListView == SPOption.False).Concat(hiddenFields).Select(v => v.ListFieldInternalName).ToArray();
          UpdateListViews(cachedList, definition.DefaultViewQuery, includedFields, excludedFields);

          assertions.Assert();
        } finally {
          assertions = null;
        }
      }
      return eventArgs.TargetModified;
    }

    public bool UpdateListViews(SPList list, string defaultViewQuery, IList<string> includedFields, IList<string> excludedFields) {
      CommonHelper.ConfirmNotNull(list, "list");
      CommonHelper.ConfirmNotNull(includedFields, "includedFields");
      CommonHelper.ConfirmNotNull(excludedFields, "excludedFields");
      string listUrl = String.Concat(list.RootFolder.Url, "/");
      bool viewUpdated = false;

      using (list.ParentWeb.GetAllowUnsafeUpdatesScope()) {
        foreach (SPView view in list.Views.OfType<SPView>().ToArray()) {
          // only provision to views lying under root rolder of the parent list
          // views located in other URL (i.e. views associated with list view web part) are ignored
          if (view.Type == "HTML" && !view.Hidden && view.Url.StartsWith(listUrl)) {
            SPView cachedView = this.ObjectCache.AddView(view);
            SPListViewProvisionEventArgs eventArgs = new SPListViewProvisionEventArgs();
            eventArgs.Web = list.ParentWeb;
            eventArgs.View = cachedView;
            eventArgs.Query = defaultViewQuery;
            eventArgs.IncludedFields = new List<string>(includedFields);
            eventArgs.ExcludedFields = new List<string>(excludedFields);
            eventReceiver.OnListViewProvisioning(eventArgs);

            if (!eventArgs.Cancel) {
              using (CreateTraceScope(cachedView)) {
                eventArgs.TargetModified |= UpdateListViewFieldCollection(cachedView, eventArgs.IncludedFields, eventArgs.ExcludedFields);
                if (defaultViewQuery != null && (cachedView.DefaultView || cachedView.MobileDefaultView)) {
                  eventArgs.TargetModified |= CopyProperties(eventArgs, cachedView);
                }
              }
              if (eventArgs.TargetModified) {
                viewUpdated = true;
                cachedView.Update();
              }
              eventReceiver.OnListViewProvisioned(eventArgs);
            }
          }
        }
      }
      return viewUpdated;
    }

    public string GetContentTypeChecksum(SPContentType contentType) {
      string xml = contentType.XmlDocuments[CTDocNamespaceUri];
      if (xml == null) {
        return null;
      }
      XmlDocument doc = new XmlDocument();
      doc.LoadXml(xml);
      XmlNodeList elm = doc.GetElementsByTagName("CheckSum");
      return elm[0].FirstChild.Value;
    }

    public string GetContentTypeChecksum(SPList list, SPContentTypeId ctId) {
      string serializedData = (string)list.RootFolder.Properties["ContentTypeChecksum"];
      if (serializedData == null) {
        return null;
      }
      string[] parts = serializedData.Split(';');
      int index = Array.IndexOf(parts, ctId.ToString());
      return index >= 0 ? parts[index + 1] : null;
    }

    public void Dispose() {
      this.TargetSite.Dispose();
    }

    private SPField EnsureField(SPFieldAttribute definition) {
      SPField field = this.ObjectCache.TryGetField(definition.InternalName);
      if (field == null) {
        if (definition is SPBuiltInFieldAttribute) {
          throw new SPModelProvisionException(String.Format("Field '{0}' does not exist", definition.InternalName));
        }
        if (definition.InternalName.Length > 32) {
          throw new SPModelProvisionException(String.Format("Field internal name '{0}' too long", definition.InternalName));
        }
        if (Regex.IsMatch("[^a-zA-Z0-9_]", definition.InternalName)) {
          throw new SPModelProvisionException(String.Format("Field internal name '{0}' contains invalid characters", definition.InternalName));
        }
        Guid fieldId = definition.ID;
        if (fieldId == Guid.Empty) {
          fieldId = Guid.NewGuid();
        }

        string fieldType = definition.TypeAsString;
        if (String.IsNullOrEmpty(fieldType)) {
          fieldType = definition.Type.ToString();
        }
        if (this.TargetSite.RootWeb.FieldTypeDefinitionCollection[fieldType] == null) {
          throw new SPModelProvisionException(String.Format("Unknown field type '{1}' when provisioning site column '{0}'", definition.InternalName, fieldType));
        }

        SPFieldCollection fieldCollection = this.TargetSite.RootWeb.Fields;
        string fieldXml = String.Format("<Field ID=\"{0}\" Name=\"{1}\" DisplayName=\"{1}\" Type=\"{2}\" CanToggleHidden=\"TRUE\" Overwrite=\"TRUE\" />", fieldId, definition.InternalName, fieldType);
        fieldCollection.AddFieldAsXml(fieldXml);
        field = fieldCollection.GetFieldByInternalName(definition.InternalName);
        this.ObjectCache.AddField(field);
      }
      return field;
    }

    private SPField EnsureListField(SPList parentList, SPFieldAttribute definition, bool attachLookupList) {
      SPField siteField = EnsureField(definition);
      SPField listField;
      bool hasField = parentList.ContentTypes[0].Fields.Contains(siteField.Id);

      if (siteField.Type == SPFieldType.Lookup) {
        listField = EnsureListLookupField(parentList, (SPFieldLookup)siteField, attachLookupList);
      } else {
        listField = this.ObjectCache.GetField(parentList.ParentWeb.ID, parentList.ID, siteField.Id);
        if (listField == null) {
          using (parentList.ParentWeb.GetAllowUnsafeUpdatesScope()) {
            parentList.Fields.Add(siteField);
            listField = parentList.Fields[siteField.Id];
          }
        }
      }
      if (!hasField) {
        // delete the list field that is automatically added to the default content type
        // if it belongs to the default content type it will be added back later
        SPContentType defaultContentType = parentList.ContentTypes[0];
        if (defaultContentType.FieldLinks[siteField.Id] != null) {
          defaultContentType.FieldLinks.Delete(siteField.Id);
          defaultContentType.Update();
        }
      }
      if (!listField.IsSystemField()) {
        string depValue = GetFieldAttribute(listField, "X-DependentField");
        bool isDepField = !attachLookupList && depValue != "FALSE" && !parentList.ContentTypes.OfType<SPContentType>().Any(v => v.Fields.Contains(listField.Id));
        if (isDepField || depValue != String.Empty) {
          bool needUpdate = false;
          needUpdate |= SetFieldAttribute(listField, "X-DependentField", isDepField ? "TRUE" : "FALSE");
          needUpdate |= CopyProperties(new { Hidden = isDepField, ReadOnlyField = isDepField }, listField);
          if (needUpdate) {
            listField.Update();
          }
        }
      }
      this.ObjectCache.AddField(listField);
      return listField;
    }

    private SPField EnsureListLookupField(SPList parentList, SPFieldLookup siteLookupField, bool attachLookupList) {
      Guid lookupListId = Guid.Empty;
      Guid lookupWebId = parentList.ParentWeb.ID;
      SPFieldLookupSource customLookupSource;
      bool customLookupField = Enum<SPFieldLookupSource>.TryParse(GetFieldAttribute(siteLookupField, "X-LookupSource"), out customLookupSource);

      if (attachLookupList && customLookupField) {
        if (customLookupSource == SPFieldLookupSource.Self) {
          lookupListId = parentList.ID;
        } else {
          try {
            SPWeb lookupWeb = parentList.ParentWeb;
            if (customLookupSource == SPFieldLookupSource.SiteCollectionList) {
              lookupWeb = lookupWeb.Site.RootWeb;
            }
            string customLookupListUrl = GetFieldAttribute(siteLookupField, "X-LookupListUrl");
            SPList lookupList = lookupWeb.GetListSafe(SPUrlUtility.CombineUrl(lookupWeb.ServerRelativeUrl, customLookupListUrl));
            lookupListId = lookupList.ID;
            lookupWebId = lookupWeb.ID;
          } catch (FileNotFoundException) { }
        }
      }

      SPField listLookupField = this.ObjectCache.GetField(parentList.ParentWeb.ID, parentList.ID, siteLookupField.Id);
      if (listLookupField == null) {
        using (parentList.ParentWeb.GetAllowUnsafeUpdatesScope()) {
          XmlDocument fieldSchemaXml = new XmlDocument();
          fieldSchemaXml.LoadXml(siteLookupField.SchemaXmlWithResourceTokens);
          fieldSchemaXml.DocumentElement.SetAttribute("WebId", lookupWebId.ToString("B"));
          fieldSchemaXml.DocumentElement.SetAttribute("List", lookupListId.ToString("B"));
          fieldSchemaXml.DocumentElement.SetAttribute("DisplayName", siteLookupField.InternalName);
          parentList.Fields.AddFieldAsXml(fieldSchemaXml.InnerXml);

          listLookupField = parentList.Fields.GetField(siteLookupField.InternalName);
          listLookupField.Title = siteLookupField.Title;
          listLookupField.Update();

          this.ObjectCache.AddField(listLookupField);
          return listLookupField;
        }
      }

      if (attachLookupList && customLookupField) {
        using (CreateTraceScope(listLookupField)) {
          bool needUpdate = false;
          needUpdate |= SetFieldAttribute(listLookupField, "WebId", lookupWebId.ToString("B"));
          needUpdate |= SetFieldAttribute(listLookupField, "List", lookupListId.ToString("B"));
          if (needUpdate) {
            using (listLookupField.ParentList.ParentWeb.GetAllowUnsafeUpdatesScope()) {
              listLookupField.Update();
            }
          }
        }
      }
      return listLookupField;
    }

    private string[] MergeFieldOrder(SPContentType contentType, ICollection<SPFieldAttribute> fieldDefinitions, bool listContentType) {
      string[] thisOrder = fieldDefinitions.OrderBy(v => v.ColumnOrder).Select(v => listContentType ? v.ListFieldInternalName : v.InternalName).ToArray();
      List<string> parentOrder = new List<string>();
      foreach (SPFieldLink field in contentType.Parent.FieldLinks) {
        string fieldName = listContentType ? field.Name : this.ObjectCache.GetField(field.Id).InternalName;
        if (!thisOrder.Contains(fieldName)) {
          parentOrder.Add(fieldName);
        }
      }
      parentOrder.AddRange(thisOrder);
      return parentOrder.ToArray();
    }

    private void SetContentTypeChecksum(SPContentType contentType, string checksum) {
      if (contentType.XmlDocuments[CTDocNamespaceUri] != null) {
        contentType.XmlDocuments.Delete(CTDocNamespaceUri);
      }
      XmlDocument doc = new XmlDocument();
      doc.LoadXml("<Root xmlns=\"" + CTDocNamespaceUri + "\"><CheckSum>" + checksum + "</CheckSum></Root>");
      contentType.XmlDocuments.Add(doc);
      contentType.Update();
    }

    private void SetContentTypeChecksum(SPList list, SPContentTypeId ctId, string checksum) {
      Hashtable ht = new Hashtable();
      string serializedData = (string)list.RootFolder.Properties["ContentTypeChecksum"];
      if (serializedData != null) {
        string[] parts = serializedData.Split(';');
        for (int i = 0; i < parts.Length; i = i + 2) {
          ht[parts[i]] = parts[i + 1];
        }
      }
      ht[ctId.ToString()] = checksum;

      StringBuilder sb = new StringBuilder();
      foreach (string key in ht.Keys) {
        sb.Append(';');
        sb.Append(key);
        sb.Append(';');
        sb.Append(ht[key]);
      }
      sb.Remove(0, 1);
      list.RootFolder.Properties["ContentTypeChecksum"] = sb.ToString();
      list.RootFolder.Update();
    }

    private static void AddToQuickLaunch(SPList list) {
      bool added = false;
      string parentNodeUrl = SPUrlUtility.CombineUrl(list.ParentWeb.ServerRelativeUrl, "/_layouts/viewlsts.aspx?BaseType=") + (int)list.BaseType;
      SPNavigationNode newNode = new SPNavigationNode(list.Title, list.DefaultViewUrl, false);
      SPNavigationNodeCollection nodes = list.ParentWeb.Navigation.QuickLaunch;
      foreach (SPNavigationNode node in nodes) {
        if (node.Url.Equals(parentNodeUrl, StringComparison.OrdinalIgnoreCase)) {
          node.Children.AddAsLast(newNode);
          added = true;
          break;
        }
      }
      if (!added) {
        nodes.AddAsLast(newNode);
      }
    }

    private static void EnsureListEventReceivers(SPEventReceiverDefinitionCollection collection) {
      using (collection.Web.GetAllowUnsafeUpdatesScope()) {
        foreach (SPEventReceiverType t in new[] { SPEventReceiverType.ItemAdding, SPEventReceiverType.ItemUpdating, SPEventReceiverType.ItemDeleting }) {
          collection.EnsureEventReceiver(t, typeof(SPModelEventReceiver), SPEventReceiverSynchronization.Synchronous);
        }
        foreach (SPEventReceiverType t in new[] { SPEventReceiverType.ItemAdded, SPEventReceiverType.ItemUpdated, SPEventReceiverType.ItemDeleted }) {
          collection.EnsureEventReceiver(t, typeof(SPModelEventReceiver), SPEventReceiverSynchronization.Synchronous);
          collection.EnsureEventReceiver(t, typeof(SPModelAsyncEventReceiver), SPEventReceiverSynchronization.Asynchronous);
        }
      }
    }

    #region Get-setters
    private static bool UpdateListViewFieldCollection(SPView view, IList<string> includedFields, IList<string> excludedFields) {
      SPViewFieldCollection viewFields = view.ViewFields;
      StringCollection existingViewFields = viewFields.ToStringCollection();
      includedFields = new List<string>((includedFields ?? new string[0]).Distinct());
      excludedFields = new List<string>((excludedFields ?? new string[0]).Distinct());

      foreach (string v in excludedFields) {
        if (includedFields.Contains(v)) {
          includedFields.Remove(v);
        }
      }
      foreach (KeyValuePair<string, string> mapping in LinkWithMenuFieldMapping) {
        if (view.ParentList.Fields.ContainsField(mapping.Value)) {
          int index = includedFields.IndexOf(mapping.Key);
          if (index >= 0) {
            includedFields[index] = mapping.Value;
            excludedFields.Add(mapping.Key);
          }
          int index2 = excludedFields.IndexOf(mapping.Key);
          if (index2 >= 0 && index < 0 && !includedFields.Contains(mapping.Value)) {
            excludedFields.Add(mapping.Value);
          }
          if (includedFields.Contains(mapping.Value) && !excludedFields.Contains(mapping.Key)) {
            excludedFields.Add(mapping.Key);
          }
        }
      }
      foreach (string v in new[] { SPBuiltInFieldName.LinkFilename, SPBuiltInFieldName.LinkTitle }) {
        if (!includedFields.Contains(v) && !excludedFields.Contains(v) && viewFields.Exists(v)) {
          includedFields.Insert(0, v);
        }
      }
      foreach (string v in new[] { SPBuiltInFieldName.Edit, SPBuiltInFieldName.DocIcon }) {
        if (includedFields.Contains(v)) {
          includedFields.Remove(v);
          includedFields.Insert(0, v);
        }
      }
      if (!excludedFields.Contains(SPBuiltInFieldName.DocIcon)) {
        if (view.ParentList.BaseType == SPBaseType.DocumentLibrary) {
          if (!includedFields.Contains(SPBuiltInFieldName.DocIcon)) {
            includedFields.Insert(0, SPBuiltInFieldName.DocIcon);
          }
        } else {
          excludedFields.Add(SPBuiltInFieldName.DocIcon);
        }
      }

      int currentIndex = 0;
      foreach (string internalName in includedFields) {
        try {
          SPField field = view.ParentList.Fields.GetFieldByInternalName(internalName);
        } catch (ArgumentException) {
          continue;
        }
        int index = existingViewFields.IndexOf(internalName);
        if (index < 0) {
          viewFields.Add(internalName);
        }
        if (index < 0 || index < currentIndex) {
          viewFields.MoveFieldTo(internalName, currentIndex);
        }
        currentIndex = Math.Min(viewFields.Count, Math.Max(index, currentIndex) + 1);
      }
      foreach (string internalName in excludedFields) {
        if (existingViewFields.Contains(internalName)) {
          viewFields.Delete(internalName);
        }
      }

      StringCollection currentViewFields = viewFields.ToStringCollection();
      if (!existingViewFields.Cast<string>().SequenceEqual(currentViewFields.Cast<string>())) {
        string[] oldValues = new string[existingViewFields.Count];
        string[] newValues = new string[currentViewFields.Count];
        existingViewFields.CopyTo(oldValues, 0);
        currentViewFields.CopyTo(newValues, 0);
        WriteTrace("Update ViewFieldCollection \"{0}\" (Original: \"{1}\")", String.Join(",", newValues), String.Join(",", oldValues));
        return true;
      }
      return false;
    }

    private static string GetFieldAttribute(SPField field, string attribute) {
      if (GetFieldAttributeValueMethod == null) {
        throw new MissingMethodException("GetFieldAttributeValue");
      }
      object value = GetFieldAttributeValueMethod.Invoke<object>(field, attribute);
      if (value == null) {
        return String.Empty;
      }
      return value.ToString();
    }

    private bool SetFieldAttribute(SPField field, string attribute, string value) {
      if (value == null) {
        value = String.Empty;
      }
      string oldValue = GetFieldAttribute(field, attribute);
      if (value == oldValue) {
        return false;
      }
      if (SetFieldAttributeValueMethod == null) {
        throw new MissingMethodException("SetFieldAttributeValue");
      }
      if (assertions != null) {
        FieldAttributeAssertion assertion = new FieldAttributeAssertion(field, attribute, oldValue, value);
        if (assertions.IsFailedBefore(assertion)) {
          WriteTrace("SKIPPED: Set Schema {0} = \"{1}\" (Original: \"{2}\")", attribute, value, oldValue);
          return false;
        }
        assertions.Add(assertion);
      }
      SetFieldAttributeValueMethod.Invoke<object>(field, attribute, value);
      WriteTrace("Set Schema {0} = \"{1}\" (Original: \"{2}\")", attribute, value, oldValue);
      return true;
    }

    private static HashSet<string> GetIgnoredProperties(object source, object target) {
      HashSet<string> collection = new HashSet<string>();
      if (source is SPListAttribute) {
        collection.Add("Url");
        collection.Add("ListTemplateType");
      }
      if (source is SPFieldAttribute) {
        collection.Add("Type");
        collection.Add("TypeAsString");
      }
      if (source is TaxonomyFieldAttribute) {
        collection.Add("TermSetId");
      }
      if (target is SPList) {
        collection.Add("RootWebOnly");
      }
      return collection;
    }

    private bool CopyProperties(object source, object target) {
      bool propertiesUpdated = false;
      Map<string, string>.Indexer<string, string> propertyMap = null;
      if (source is SPFieldLink) {
        propertyMap = FieldLinkPropertyMapping.Reverse;
      } else if (target is SPFieldLink) {
        propertyMap = FieldLinkPropertyMapping.Forward;
      }

      HashSet<string> ignoredProperties = GetIgnoredProperties(source, target);
      foreach (PropertyInfo sourceProperty in source.GetType().GetProperties()) {
        if (ignoredProperties.Contains(sourceProperty.Name)) {
          continue;
        }
        string targetPropertyName = sourceProperty.Name;
        if (propertyMap != null) {
          string mappedPropertyName;
          if (propertyMap.TryGetValue(targetPropertyName, out mappedPropertyName)) {
            targetPropertyName = mappedPropertyName;
          }
        }
        PropertyInfo targetProperty = target.GetType().GetProperty(targetPropertyName);
        if (targetProperty != null) {
          try {
            object sourceValue = sourceProperty.GetValue<object>(source);
            if (sourceValue is StringCollection) {
              StringCollection sourceCollection = (StringCollection)sourceValue;
              StringCollection targetCollection = targetProperty.GetValue<StringCollection>(target);
              if (sourceCollection.Count != targetCollection.Count || sourceCollection.Cast<string>().Except(targetCollection.Cast<string>()).Any()) {
                string[] oldValues = new string[targetCollection.Count];
                string[] newValues = new string[sourceCollection.Count];
                targetCollection.CopyTo(oldValues, 0);
                sourceCollection.CopyTo(newValues, 0);
                targetCollection.Clear();
                targetCollection.AddRange(newValues);
                WriteTrace("Set Property {0} = \"{1}\" (Original: \"{2}\")", targetProperty.Name, String.Join(",", newValues), String.Join(",", oldValues));
                propertiesUpdated = true;
              }
            } else if (targetProperty.CanWrite && sourceValue != null) {
              if (sourceValue is SPOption) {
                if (targetProperty.Name == "Hidden" && target is SPField) {
                  SetFieldAttribute((SPField)target, "CanToggleHidden", "TRUE");
                }
                propertiesUpdated |= TrySetFromNullable(targetProperty, target, ConvertToNullableBoolean((SPOption)sourceValue));
              } else {
                Type sourceUnderlyingType = Nullable.GetUnderlyingType(sourceProperty.PropertyType);
                Type targetUnderlyingType = Nullable.GetUnderlyingType(targetProperty.PropertyType);
                if ((sourceUnderlyingType == null) ^ (targetUnderlyingType != null)) {
                  object currentValue = targetProperty.GetValue<object>(target);
                  try {
                    sourceValue = Convert.ChangeType(sourceValue, targetProperty.PropertyType);
                  } catch (InvalidCastException) {
                    continue;
                  }
                  if ((sourceValue == null && currentValue != null) || !sourceValue.Equals(currentValue)) {
                    propertiesUpdated |= TrySetValue(targetProperty, target, sourceValue);
                  }
                } else if (sourceUnderlyingType != null) {
                  propertiesUpdated |= TrySetFromNullableMethod.MakeGenericMethod(sourceUnderlyingType).Invoke<bool>(null, targetProperty, target, sourceValue);
                } else {
                  propertiesUpdated |= TrySetToNullableMethod.MakeGenericMethod(targetUnderlyingType).Invoke<bool>(null, targetProperty, target, sourceValue);
                }
              }
            }
          } catch (Exception ex) {
            throw new SPModelProvisionException(String.Format("Unable to update property '{0}.{1}'", targetProperty.DeclaringType, targetProperty.Name), ex);
          }
        }
      }
      return propertiesUpdated;
    }

    private bool TrySetToNullable<T>(PropertyInfo targetProperty, object target, T value) where T : struct {
      object currentValue = targetProperty.GetValue<object>(target);
      if (!value.Equals(currentValue)) {
        return TrySetValue(targetProperty, target, (T?)value);
      }
      return false;
    }

    private bool TrySetFromNullable<T>(PropertyInfo targetProperty, object target, T? value) where T : struct {
      if (targetProperty.Name == "Indexed" && targetProperty.GetGetMethod() == null) {
        targetProperty = targetProperty.DeclaringType.BaseType.GetProperty("Indexed");
      }
      object currentValue = targetProperty.GetValue<object>(target);
      if (!value.Equals(currentValue)) {
        if (targetProperty.PropertyType == typeof(T?)) {
          return TrySetValue(targetProperty, target, value);
        } else if (value.HasValue) {
          return TrySetValue(targetProperty, target, value.Value);
        }
      }
      return false;
    }

    private bool TrySetValue(PropertyInfo targetProperty, object target, object value) {
      object oldValue = targetProperty.GetValue<object>(target);
      if (assertions != null) {
        PropertyAssertion assertion = new PropertyAssertion(target, targetProperty, oldValue, value);
        if (assertions.IsFailedBefore(assertion)) {
          WriteTrace("SKIPPED: Set Property {0} = \"{1}\" (Original: \"{2}\")", targetProperty.Name, value, oldValue);
          return false;
        }
        assertions.Add(assertion);
      }
      targetProperty.SetValue<object>(target, value);
      object newValue = targetProperty.GetValue<object>(target);
      if (Assertion.CompareEquality(value, newValue)) {
        WriteTrace("Set Property {0} = \"{1}\" (Original: \"{2}\")", targetProperty.Name, value, oldValue);
        return true;
      }
      return false;
    }

    private static bool? ConvertToNullableBoolean(SPOption v) {
      switch (v) {
        case SPOption.False:
          return new bool?(false);
        case SPOption.True:
          return new bool?(true);
        default:
          return new bool?();
      }
    }
    #endregion

    #region Assertion
    [Serializable]
    [DebuggerDisplay("{OldValue},{NewValue}")]
    private struct AssertionValue {
      public AssertionValue(object oldvalue, object newValue) {
        OldValue = oldvalue;
        NewValue = newValue;
      }
      public readonly object OldValue;
      public readonly object NewValue;
    }

    private abstract class Assertion {
      public Assertion(object target, object oldvalue, object newValue) {
        this.Target = target;
        this.Value = new AssertionValue(oldvalue, newValue);
      }

      public object Id { get { return GetId(); } }
      public object Target { get; private set; }
      public AssertionValue Value { get; private set; }
      public AssertionValue HashValue { get { return new AssertionValue(this.Value.OldValue == null ? 0 : this.Value.OldValue.GetHashCode(), this.Value.NewValue == null ? 0 : this.Value.NewValue.GetHashCode()); } }

      protected abstract object GetId();
      protected abstract object GetValue();

      public bool Assert() {
        return CompareEquality(GetValue(), this.Value.NewValue);
      }

      public static bool CompareEquality(object x, object y) {
        return ReferenceEquals(x, y) || (x != null && y != null && x.Equals(y));
      }
    }

    private sealed class PropertyAssertion : Assertion {
      public PropertyAssertion(object target, PropertyInfo property, object oldvalue, object newValue) :
        base(target, oldvalue, newValue) {
        this.Property = property;
      }

      public PropertyInfo Property { get; set; }

      protected override object GetId() {
        Type type = this.Target.GetType();
        if (type.IsOf(typeof(SPField))) {
          return String.Format("P:{0:N}:{1}.{2}", ((SPField)this.Target).Id, this.Property.DeclaringType, this.Property.Name);
        }
        if (type.IsOf(typeof(SPContentType))) {
          return String.Format("P:{0}:{1}.{2}", ((SPContentType)this.Target).Id, this.Property.DeclaringType, this.Property.Name);
        }
        if (type.IsOf(typeof(SPList))) {
          return String.Format("P:{0}:{1}.{2}", ((SPList)this.Target).RootFolder.Url, this.Property.DeclaringType, this.Property.Name);
        }
        if (type.IsOf(typeof(SPView))) {
          return String.Format("P:{0}:{1}.{2}", ((SPView)this.Target).Url, this.Property.DeclaringType, this.Property.Name);
        }
        throw new NotSupportedException(String.Format("Type '{0}' not supported", type.FullName));
      }

      protected override object GetValue() {
        return this.Property.GetValue<object>(this.Target);
      }
    }

    private sealed class FieldAttributeAssertion : Assertion {
      public FieldAttributeAssertion(SPField target, string attribute, object oldvalue, object newValue) :
        base(target, oldvalue, newValue) {
        this.AttributeName = attribute;
      }

      public string AttributeName { get; set; }

      protected override object GetId() {
        return String.Format("S:{0:N}:{1}", ((SPField)this.Target).Id, this.AttributeName);
      }

      protected override object GetValue() {
        return GetFieldAttribute((SPField)this.Target, this.AttributeName);
      }
    }

    private class AssertionCollection : Collection<Assertion> {
      private readonly SPList list;
      private readonly Hashtable hashtable = new Hashtable();
      private readonly BinaryFormatter formatter = new BinaryFormatter();

      public AssertionCollection(SPList list) {
        CommonHelper.ConfirmNotNull(list, "list");
        try {
          byte[] serializedData = Convert.FromBase64String(list.RootFolder.Properties.EnsureKeyValue("FailedAssertions", () => String.Empty));
          if (serializedData.Length > 0) {
            using (MemoryStream ms = new MemoryStream(serializedData)) {
              this.hashtable = (Hashtable)formatter.Deserialize(ms);
            }
          }
        } catch {
          WriteTrace("Assertion data damaged");
        }
        this.list = list;
      }

      public bool IsFailedBefore(Assertion assertion) {
        CommonHelper.ConfirmNotNull(assertion, "assertion");
        if (hashtable.ContainsKey(assertion.Id)) {
          AssertionValue x = assertion.HashValue;
          AssertionValue y = (AssertionValue)hashtable[assertion.Id];
          return Assertion.CompareEquality(x.OldValue, y.OldValue) && Assertion.CompareEquality(x.NewValue, y.NewValue);
        }
        return false;
      }

      public void Assert() {
        bool failed = false;
        foreach (Assertion assertion in this) {
          if (!assertion.Assert()) {
            failed = true;
            hashtable[assertion.Id] = assertion.HashValue;
            WriteTrace("ASSERTION FAILED: {0}, OldValue={1}, NewValue={2}", assertion.Id, assertion.Value.OldValue, assertion.Value.NewValue);
          }
        }
        if (failed) {
          using (MemoryStream ms = new MemoryStream()) {
            formatter.Serialize(ms, hashtable);
            ms.Seek(0, SeekOrigin.Begin);
            string serializedData = Convert.ToBase64String(ms.ToArray());
            list.RootFolder.Properties["FailedAssertions"] = serializedData;
            list.RootFolder.Update();
          }
        }
      }
    }
    #endregion

    #region Trace Helpers
    [ThreadStatic]
    private static LinkedList<TraceScope> inTraceScopes;
    [ThreadStatic]
    private static Stack<TraceScope> outTraceScopes;

    private static void WriteTrace(string fmt, params object[] args) {
      LazyInitializer.EnsureInitialized(ref inTraceScopes);
      LazyInitializer.EnsureInitialized(ref outTraceScopes);
      while (inTraceScopes.Count > 0) {
        SPDiagnosticsService.Local.WriteTrace(TraceCategory.ModelProvisionVerbose, "Entering " + inTraceScopes.First.Value);
        outTraceScopes.Push(inTraceScopes.First.Value);
        inTraceScopes.RemoveFirst();
      }
      SPDiagnosticsService.Local.WriteTrace(TraceCategory.ModelProvisionVerbose, String.Format(fmt, args));
    }

    private class TraceScope : IDisposable {
      private readonly string traceMessage;

      public TraceScope(SPModelProvisionHelper helper, string fmt, params object[] args) {
        this.traceMessage = String.Format(fmt, args) + " [" + (helper.GetHashCode() & 0xFFFF).ToString("X4") + "]";
        LazyInitializer.EnsureInitialized(ref inTraceScopes);
        inTraceScopes.AddLast(this);
      }

      public void Dispose() {
        LazyInitializer.EnsureInitialized(ref outTraceScopes);
        if (outTraceScopes.Count > 0 && outTraceScopes.Peek() == this) {
          SPDiagnosticsService.Local.WriteTrace(0, TraceCategory.ModelProvisionVerbose, TraceSeverity.Monitorable, "Leaving " + outTraceScopes.Pop());
        } else {
          inTraceScopes.RemoveLast();
        }
      }

      public override string ToString() {
        return traceMessage;
      }
    }

    private IDisposable CreateTraceScope(SPField field) {
      if (field.ParentList != null) {
        return new TraceScope(this, "Field \"{0}\" ({1}/{2})", field.InternalName, field.ParentList.ParentWeb.Url, field.ParentList.RootFolder.Url);
      } else {
        return new TraceScope(this, "Field \"{0}\" ({1})", field.InternalName, this.TargetSiteUrl);
      }
    }

    private IDisposable CreateTraceScope(SPContentType contentType) {
      if (contentType.ParentList != null) {
        return new TraceScope(this, "ContentType \"{0}\" ({1}/{2})", contentType.Name, contentType.ParentList.ParentWeb.Url, contentType.ParentList.RootFolder.Url);
      } else {
        return new TraceScope(this, "ContentType \"{0}\" ({1})", contentType.Name, this.TargetSiteUrl);
      }
    }

    private IDisposable CreateTraceScope(SPFieldLink fieldLink) {
      return new TraceScope(this, "FieldLink \"{0}\" ({1})", fieldLink.Name, this.TargetSiteUrl);
    }

    private IDisposable CreateTraceScope(SPList list) {
      return new TraceScope(this, "List {0}/{1}", list.ParentWeb.Url, list.RootFolder.Url);
    }

    private IDisposable CreateTraceScope(SPView view) {
      return new TraceScope(this, "View {0}/{1}", view.ParentList.ParentWeb.Url, view.Url);
    }
    #endregion

    #region Map
    public class Map<T1, T2> : IEnumerable {
      private Dictionary<T1, T2> forward = new Dictionary<T1, T2>();
      private Dictionary<T2, T1> reverse = new Dictionary<T2, T1>();

      public Map() {
        this.Forward = new Indexer<T1, T2>(forward);
        this.Reverse = new Indexer<T2, T1>(reverse);
      }

      public class Indexer<T3, T4> : IReadOnlyDictionary<T3, T4> {
        private Dictionary<T3, T4> dictionary;

        internal Indexer(Dictionary<T3, T4> dictionary) {
          this.dictionary = dictionary;
        }

        public T4 this[T3 index] {
          get { return dictionary[index]; }
        }

        public int Count {
          get { return dictionary.Count; }
        }

        public bool ContainsKey(T3 key) {
          return dictionary.ContainsKey(key);
        }

        public bool TryGetValue(T3 key, out T4 value) {
          return dictionary.TryGetValue(key, out value);
        }

        public IEnumerable<T3> Keys {
          get { return dictionary.Keys; }
        }

        public IEnumerable<T4> Values {
          get { return dictionary.Values; }
        }

        IEnumerator IEnumerable.GetEnumerator() {
          return dictionary.GetEnumerator();
        }

        IEnumerator<KeyValuePair<T3, T4>> IEnumerable<KeyValuePair<T3, T4>>.GetEnumerator() {
          return dictionary.GetEnumerator();
        }
      }

      public void Add(T1 t1, T2 t2) {
        forward.Add(t1, t2);
        reverse.Add(t2, t1);
      }

      public Indexer<T1, T2> Forward { get; private set; }

      public Indexer<T2, T1> Reverse { get; private set; }

      IEnumerator IEnumerable.GetEnumerator() {
        return forward.GetEnumerator();
      }
    }
    #endregion
  }
}
