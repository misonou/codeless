using Microsoft.SharePoint;
using Microsoft.SharePoint.Administration;
using Microsoft.SharePoint.Administration.Claims;
using System;
using System.Web.Profile;
using System.Web.Security;

namespace Codeless.SharePoint {
  /// <summary>
  /// Provides information on the current logged on user and authentication settings of the current web application.
  /// </summary>
  public sealed class ClaimsContext {
    private ClaimsContext(SPContext context) {
      SPWebApplication webApplication = context.Site.WebApplication;
      foreach (SPAlternateUrl mapping in webApplication.AlternateUrls) {
        SPIisSettings settings = webApplication.GetIisSettingsWithFallback(mapping.UrlZone);
        if (settings.UseFormsClaimsAuthenticationProvider) {
          this.FormsMembershipProvider = Membership.Providers[settings.FormsClaimsAuthenticationProvider.MembershipProvider];
          this.FormsRoleProvider = Roles.Providers[settings.FormsClaimsAuthenticationProvider.RoleProvider];
          break;
        }
      }

      SPUser currentUser = context.Web.CurrentUser;
      if (currentUser != null && SPClaimProviderManager.IsEncodedClaim(currentUser.LoginName)) {
        SPClaim claim = SPClaimProviderManager.Local.DecodeClaim(currentUser.LoginName);
        this.IsWindowsUser = claim.OriginalIssuer == "Windows";

        if (claim.OriginalIssuer.StartsWith("Forms:")) {
          if (this.FormsMembershipProvider != null && this.FormsMembershipProvider.Name.Equals(claim.OriginalIssuer.Substring(6), StringComparison.OrdinalIgnoreCase)) {
            this.FormsUser = this.FormsMembershipProvider.GetUser(claim.Value, false);
            if (this.FormsUser != null) {
              this.IsFormsUser = true;
              this.FormsUserId = claim.Value;
              this.FormsUserProfile = ProfileBase.Create(this.FormsUser.UserName);
            }
          }
        }
      }
      this.IsAnonymous = !this.IsFormsUser && !this.IsWindowsUser;
    }

    /// <summary>
    /// Indicates if current user is anonymous.
    /// </summary>
    public bool IsAnonymous { get; private set; }

    /// <summary>
    /// Indicates if current user is authenticated using Windows Authentication.
    /// </summary>
    public bool IsWindowsUser { get; private set; }

    /// <summary>
    /// Indicates if current user is authenticated using Form-Based Authentication.
    /// </summary>
    public bool IsFormsUser { get; private set; }

    /// <summary>
    /// Gets the Form-Based user ID if current user is authenticated using Form-Based Authentication.
    /// </summary>
    public string FormsUserId { get; private set; }

    /// <summary>
    /// Gets the profile of current user if current user is authenticated using Form-Based Authentication.
    /// </summary>
    public ProfileBase FormsUserProfile { get; private set; }

    /// <summary>
    /// Gets an underlying representation of current user if current user is authenticated using Form-Based Authentication.
    /// </summary>
    public MembershipUser FormsUser { get; private set; }

    /// <summary>
    /// Gets a membership provider used by the current web application.
    /// </summary>
    public MembershipProvider FormsMembershipProvider { get; private set; }

    /// <summary>
    /// Gets a role provider used by the current web application.
    /// </summary>
    public RoleProvider FormsRoleProvider { get; private set; }

    /// <summary>
    /// Gets a <see cref="ClaimsContext"/> instance associated with the current HTTP request.
    /// </summary>
    public static ClaimsContext Current {
      get {
        if (SPContext.Current != null) {
          return CommonHelper.HttpContextSingleton(() => new ClaimsContext(SPContext.Current));
        }
        return null;
      }
    }
  }
}
