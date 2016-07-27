using Codeless.SharePoint.ObjectModel;
using Microsoft.BusinessData.Infrastructure.SecureStore;
using Microsoft.Office.SecureStoreService.Server;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Administration;
using Microsoft.SharePoint.Administration.Claims;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Security.Principal;
using System.Web.Hosting;

namespace Codeless.SharePoint.Internal {
  [SPContentType("286EE8E5A7EC48F8B226410996A4DF07", "Site Config")]
  internal class SiteConfigEntry : SPModel, ISiteConfigEntry, ISecureSiteConfigEntry {
    private const string SecureValuePrefix = "PasswordContainer";

    [ThreadStatic]
    internal static bool IsInternalUpdate;

    private class Field {
      public const string SiteConfigValue = "SiteConfigValue";
      public const string SiteConfigValueHidden = "SiteConfigValueHidden";
      public const string SiteConfigCategory = "SiteConfigCategory";
      public const string SiteConfigDescription = "SiteConfigDescription";
      public const string SiteConfigUseDefaultValue = "SiteConfigUseDefaultValue";
    }

    private class FieldTitle {
      public const string SiteConfigTitle = "Key";
      public const string SiteConfigValue = "Value";
      public const string SiteConfigCategory = "Category";
      public const string SiteConfigDescription = "Description";
      public const string SiteConfigUseDefaultValue = "Use Default Value";
    }

    [SPTextField(SPBuiltInFieldName.Title,
      Title = FieldTitle.SiteConfigTitle,
      FormVisibility = SPFieldFormVisibility.Visible,
      ShowInListView = SPOption.True,
      Required = SPOption.True,
      ColumnOrder = 1)]
    public string Key {
      get { return this.Adapter.GetString(SPBuiltInFieldName.Title); }
      set { this.Adapter.SetString(SPBuiltInFieldName.Title, value); }
    }

    [SPNoteField(Field.SiteConfigValue,
      Title = FieldTitle.SiteConfigValue,
      FormVisibility = SPFieldFormVisibility.Visible,
      ShowInListView = SPOption.True,
      ColumnOrder = 4)]
    [SPNoteField(Field.SiteConfigValueHidden,
      FormVisibility = SPFieldFormVisibility.Hidden,
      IncludeInQuery = false,
      IncludeInViewFields = true)]
    public string Value {
      get {
        string hiddenValue = null;
        try {
          hiddenValue = this.Adapter.GetString(Field.SiteConfigValueHidden);
        } catch (ArgumentException) { }
        if (String.IsNullOrEmpty(hiddenValue)) {
          return this.Adapter.GetString(Field.SiteConfigValue);
        }
        if (hiddenValue.StartsWith(SecureValuePrefix)) {
          return String.Empty;
        }
        return hiddenValue.Substring(1);
      }
      set {
        this.Adapter.SetString(Field.SiteConfigValue, value);
      }
    }

    [SPBooleanField(Field.SiteConfigUseDefaultValue,
      Title = FieldTitle.SiteConfigUseDefaultValue,
      FormVisibility = SPFieldFormVisibility.Visible,
      ShowInListView = SPOption.True,
      ColumnOrder = 3)]
    public bool UseDefaultValue {
      get { return Adapter.GetBoolean(Field.SiteConfigUseDefaultValue); }
      set { Adapter.SetBoolean(Field.SiteConfigUseDefaultValue, value); }
    }

    [SPTextField(Field.SiteConfigCategory,
      Title = FieldTitle.SiteConfigCategory,
      FormVisibility = SPFieldFormVisibility.Visible,
      ShowInListView = SPOption.True,
      ColumnOrder = 2)]
    public string Category {
      get { return this.Adapter.GetString(Field.SiteConfigCategory); }
      set { this.Adapter.SetString(Field.SiteConfigCategory, value); }
    }

    [SPNoteField(Field.SiteConfigDescription,
      Title = FieldTitle.SiteConfigDescription,
      FormVisibility = SPFieldFormVisibility.Visible,
      ColumnOrder = 5)]
    public string Description {
      get { return this.Adapter.GetString(Field.SiteConfigDescription); }
      set { this.Adapter.SetString(Field.SiteConfigDescription, value); }
    }

    public SecureString SecureValue {
      get {
        SecureString returnValue = new SecureString();
        SPSecurity.RunWithElevatedPrivileges(() => {
          ISecureStore store;
          string applicationId;
          if (EnsureSecureStoreTargetApplication(false, out store, out applicationId)) {
            SecureStoreCredentialCollection cred;
            try {
              cred = store.GetCredentials(applicationId);
            } catch (SecureStoreCredentialsNotFoundException) {
              return;
            }
            ISecureStoreCredential password = cred.FirstOrDefault(v => v.CredentialType == SecureStoreCredentialType.Password);
            if (password != null) {
              returnValue = password.Credential.Copy();
            }
          }
        });
        return returnValue;
      }
      set {
        SPSecurity.RunWithElevatedPrivileges(() => {
          ISecureStore store;
          string applicationId;
          if (EnsureSecureStoreTargetApplication((value != null), out store, out applicationId)) {
            using (BypassValidateFormDigest()) {
              if (value != null) {
                SecureStoreCredentialCollection cred = new SecureStoreCredentialCollection(new[] { new SecureStoreCredential(value, SecureStoreCredentialType.Password) });
                foreach (SecureStoreServiceClaim user in GetServiceAccounts()) {
                  store.SetUserCredentials(applicationId, user, cred);
                }
              } else {
                store.DeleteCredentials(applicationId);
              }
            }
          }
        });
      }
    }

    protected override void OnAddingOrUpdating(SPModelEventArgs e) {
      base.OnAddingOrUpdating(e);
      string newValue = this.Adapter.GetString(Field.SiteConfigValue);
      if (e.OriginalValue == null || newValue != ((SiteConfigEntry)e.OriginalValue).Value) {
        if (this.Adapter.GetString(Field.SiteConfigValueHidden).StartsWith(SecureValuePrefix)) {
          using (SecureString result = ToSecureString(newValue)) {
            this.SecureValue = result;
            this.Adapter.SetString(Field.SiteConfigValue, new String('*', result.Length));
          }
          if (!IsInternalUpdate) {
            this.UseDefaultValue = false;
          }
        } else {
          this.Adapter.SetString(Field.SiteConfigValueHidden, String.Concat("=", newValue));
          if (e.EventType == SPModelEventType.Updating) {
            if (!IsInternalUpdate) {
              this.UseDefaultValue = false;
            }
          }
        }
      }
    }

    protected override void OnDeleting(SPModelEventArgs e) {
      base.OnDeleting(e);
      if (this.Adapter.GetString(Field.SiteConfigValueHidden).StartsWith(SecureValuePrefix)) {
        this.SecureValue = null;
      }
    }

    #region SecureStoreService helpers
    private bool EnsureSecureStoreTargetApplication(bool forceCreate, out ISecureStore store, out string applicationId) {
      store = GetSecureStore(this.Adapter.Site);
      applicationId = GetSecureStoreTargetApplicationID();
      try {
        store.GetApplication(applicationId);
        return true;
      } catch (SecureStoreServiceTargetApplicationNotFoundException) {
        if (forceCreate) {
          using (BypassValidateFormDigest()) {
            store.CreateApplication(new TargetApplication(
              applicationId: applicationId,
              friendlyName: String.Concat(this.Key, " (", this.Adapter.Web.Lists[this.Adapter.ListId].RootFolder.Url, ")"),
              contactEmail: "",
              ticketTimeoutInMinutes: 30,
              type: TargetApplicationType.Individual,
              credentialManagementUrl: new Uri("http://tempuri.org")),
              new[] { new TargetApplicationField("Password", true, SecureStoreCredentialType.Password) },
              new TargetApplicationClaims(GetServiceAccounts(), new SecureStoreServiceClaim[0], new SecureStoreServiceClaim[0]));
          }
          return true;
        }
      }
      return false;
    }

    private string GetSecureStoreTargetApplicationID() {
      string prefix = this.Adapter.Site.ID.ToString("N");
      string hiddenValue = this.Adapter.GetString(Field.SiteConfigValueHidden);
      if (hiddenValue.StartsWith(SecureValuePrefix)) {
        return String.Concat(prefix, hiddenValue.Substring(SecureValuePrefix.Length));
      }
      string newId = Guid.NewGuid().ToString("N");
      this.Adapter.SetString(Field.SiteConfigValueHidden, String.Concat(SecureValuePrefix, newId));
      return String.Concat(prefix, newId);
    }

    private static ISecureStore GetSecureStore(SPSite site) {
      SPServiceContext context = SPServiceContext.GetContext(site);
      SecureStoreServiceApplicationProxy proxy = (SecureStoreServiceApplicationProxy)context.GetDefaultProxy(typeof(SecureStoreServiceApplicationProxy));
      if (proxy == null) {
        throw new InvalidOperationException("This web application does not have connections to Secure store service application");
      }
      SecureStoreService service = proxy.Farm.Services.OfType<SecureStoreService>().FirstOrDefault();
      SecureStoreServiceApplication application = (SecureStoreServiceApplication)service.Applications.FirstOrDefault(v => v.IsConnected(proxy));
      if (!application.IsMasterSecretKeyPopulated()) {
        throw new InvalidOperationException("Master secret key not set for secure store service application");
      }
      return ((SecureStoreServiceProxy)proxy.Parent).GetSecureStore(context);
    }

    private static List<SecureStoreServiceClaim> GetServiceAccounts() {
      List<SecureStoreServiceClaim> users = new List<SecureStoreServiceClaim>();
      using (HostingEnvironment.Impersonate()) {
        users.Add(CreateServiceClaim(WindowsIdentity.GetCurrent().User));
      }
      foreach (SPManagedAccount managedAccount in new SPFarmManagedAccountCollection(SPFarm.Local)) {
        users.Add(CreateServiceClaim(managedAccount));
      }
      users.Add(CreateServiceClaim(SPFarm.Local.DefaultServiceAccount.SecurityIdentifier));
      return users;
    }

    private static SecureStoreServiceClaim CreateServiceClaim(SPManagedAccount account) {
      return new SecureStoreServiceClaim(SPClaimTypes.UserLogonName, "Windows", account.Username);
    }

    private static SecureStoreServiceClaim CreateServiceClaim(SecurityIdentifier sid) {
      return new SecureStoreServiceClaim(SPClaimTypes.UserLogonName, "Windows", sid.Translate(typeof(NTAccount)).Value);
    }

    private static SecureString ToSecureString(string value) {
      SecureString result = new SecureString();
      foreach (char c in value.ToCharArray()) {
        result.AppendChar(c);
      }
      return result;
    }

    private static IDisposable BypassValidateFormDigest() {
      if (System.Web.HttpContext.Current != null) {
        return Microsoft.SharePoint.WebControls.SPControl.GetContextWeb(System.Web.HttpContext.Current).GetAllowUnsafeUpdatesScope();
      }
      return null;
    }
    #endregion
  }
}
