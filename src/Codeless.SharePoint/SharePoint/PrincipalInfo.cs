using Codeless.SharePoint.Internal;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Administration.Claims;
using System;
using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using System.Security.Principal;
using System.Web.Hosting;
using System.Web.Security;

namespace Codeless.SharePoint {
  /// <summary>
  /// Provides information of identities resolved from SharePoint users and groups.
  /// </summary>
  public sealed class PrincipalInfo : IEquatable<PrincipalInfo> {
    internal PrincipalInfo(UserPrincipal principal, SPPrincipal parentPrincipal)
      : this(principal, CommonHelper.ConfirmNotNull(principal, "principal").DistinguishedName ?? principal.SamAccountName, parentPrincipal) {
      this.DisplayName = principal.DisplayName ?? principal.DistinguishedName ?? principal.SamAccountName;
      this.EmailAddress = principal.EmailAddress;
      this.ProviderType = SPIdentityProviderTypes.Windows;
      this.EncodedClaim = SPClaimProviderManager.Local.ConvertIdentifierToClaim(((NTAccount)principal.Sid.Translate(typeof(NTAccount))).Value, SPIdentifierTypes.WindowsSamAccountName).ToEncodedString();
    }

    internal PrincipalInfo(MembershipUser principal, SPPrincipal parentPrincipal)
      : this(principal, CommonHelper.ConfirmNotNull(principal, "principal").ProviderUserKey, parentPrincipal) {
      this.DisplayName = principal.UserName;
      this.EmailAddress = principal.Email;
      this.ProviderType = SPIdentityProviderTypes.Forms;
      this.EncodedClaim = SPClaimProviderManager.Local.ConvertIdentifierToClaim(principal.UserName, SPIdentifierTypes.FormsUser).ToEncodedString();
    }

    internal PrincipalInfo(string rawName, Exception exception, SPPrincipal parentPrincipal) {
      this.DisplayName = rawName;
      this.IsResolved = false;
      this.Exception = exception;
      this.ParentPrincipal = parentPrincipal;
    }

    internal PrincipalInfo(object userObject, object providerUserId, SPPrincipal parentPrincipal) {
      CommonHelper.ConfirmNotNull(userObject, "userObject");
      CommonHelper.ConfirmNotNull(providerUserId, "providerUserId");
      CommonHelper.ConfirmNotNull(parentPrincipal, "parentPrincipal");
      this.ObjectType = userObject.GetType();
      this.ProviderUserId = providerUserId;
      this.IsResolved = true;
      this.ParentPrincipal = parentPrincipal;
    }

    /// <summary>
    /// Gets the type of the underlying object representing the resolved identity.
    /// </summary>
    [Obsolete("Use PrincipalInfo.ProviderType to differentiate user types.")]
    public Type ObjectType { get; private set; }

    /// <summary>
    /// Gets a unique identifier of the resolved identity used by the identity provider.
    /// </summary>
    [Obsolete("Use PrincipalInfo.EncodedClaim for more consistent way to identify the resolved user.")]
    public object ProviderUserId { get; private set; }

    /// <summary>
    /// Gets the type of provider that resolve this user. 
    /// See members of <see cref="SPIdentityProviderTypes"/> for possible values.
    /// </summary>
    public string ProviderType { get; private set; }

    /// <summary>
    /// Gets the encoded claim-based logon name on SharePoint for the resolved principal.
    /// </summary>
    public string EncodedClaim { get; private set; }

    /// <summary>
    /// Gets the display name of the resolved identity.
    /// </summary>
    public string DisplayName { get; private set; }

    /// <summary>
    /// Gets the email address of the resolved identity.
    /// </summary>
    public string EmailAddress { get; private set; }

    /// <summary>
    /// Indicates if the SharePoint user referenced by <see cref="ParentPrincipal"/> resolves to a valid identity.
    /// </summary>
    public bool IsResolved { get; private set; }

    /// <summary>
    /// Gets a SharePoint user which the resolved identity is represented in a SharePoint site collection.
    /// </summary>
    public SPPrincipal ParentPrincipal { get; private set; }

    /// <summary>
    /// Gets the exception occurred if the SharePoint user referenced by <see cref="ParentPrincipal"/> cannot be resolved.
    /// </summary>
    public Exception Exception { get; private set; }

    /// <summary>
    /// Determines the equality of this instance to the given instance.
    /// </summary>
    /// <param name="other">Object to compare.</param>
    /// <returns></returns>
    public bool Equals(PrincipalInfo other) {
      if (other != null && other.IsResolved && this.IsResolved) {
        return this.EncodedClaim == other.EncodedClaim;
      }
      return false;
    }

    /// <summary>
    /// Overriden. When <paramref name="obj"/> is a <see cref="PrincipalInfo"/> instance, the custom equality comparison is performed.
    /// </summary>
    /// <param name="obj">Object to compare.</param>
    /// <returns></returns>
    public override bool Equals(object obj) {
      PrincipalInfo other = CommonHelper.TryCastOrDefault<PrincipalInfo>(obj);
      if (other != null) {
        return Equals(other);
      }
      return base.Equals(obj);
    }

    /// <summary>
    /// Overriden.
    /// </summary>
    /// <returns></returns>
    public override int GetHashCode() {
      if (IsResolved) {
        return EncodedClaim.GetHashCode();
      }
      return 0;
    }

    /// <summary>
    /// Creates a resolvation scope that resolved identities are cached until the object is disposed. 
    /// Same identity referenced by different SharePoint users are returned once only, even by subsequent calls to <see cref="Resolve"/> or <see cref="ResolveEmailAddresses(SPPrincipal)"/>.
    /// If no scope is created before calling <see cref="Resolve"/> or <see cref="ResolveEmailAddresses(SPPrincipal)"/>, an implicit scope is created during the call and is disposed after the call.
    /// </summary>
    /// <returns>A disposable object.</returns>
    public static IDisposable CreatePrincipalContextScope() {
      return new PrincipalContextScope();
    }

    /// <summary>
    /// Enumerates identities referenced by the specified SharePoint user or group, and optionally throw exception when error is encountered.
    /// If <paramref name="throwOnException"/> is set to *false*, a <see cref="PrincipalInfo"/> object that <see cref="IsResolved"/> is set to *true* is returned when error is encountered.
    /// To eliminate duplication on subequent calls, first call <see cref="CreatePrincipalContextScope"/>.
    /// </summary>
    /// <param name="member">A SharePoint user or group to be resolved.</param>
    /// <param name="throwOnException">Whether to throw exception when error is encountered.</param>
    /// <returns>A enumerable object containing resolved identities.</returns>
    public static IEnumerable<PrincipalInfo> Resolve(SPPrincipal member, bool throwOnException) {
      CommonHelper.ConfirmNotNull(member, "member");
      using (HostingEnvironment.Impersonate()) {
        IDisposable implicitScope = null;
        try {
          PrincipalContextScope.Current.GetType();
        } catch (MemberAccessException) {
          implicitScope = CreatePrincipalContextScope();
        }
        PrincipalResolver resolver = new PrincipalResolver(throwOnException);
        return resolver.Resolve(member, implicitScope);
      }
    }

    /// <summary>
    /// Enumerates email addresses from identities referenced by the specified SharePoint user or group.
    /// For SharePoint users that fail to be resolved, no exception will be thrown.
    /// To eliminate duplication on subequent calls, first call <see cref="CreatePrincipalContextScope"/>.
    /// </summary>
    /// <param name="member">A SharePoint user or group to be resolved.</param>
    /// <returns>A enumerable object containing resolved email addresses.</returns>
    public static IEnumerable<string> ResolveEmailAddresses(SPPrincipal member) {
      CommonHelper.ConfirmNotNull(member, "member");
      IDisposable implicitScope = null;
      try {
        PrincipalContextScope.Current.GetType();
      } catch (MemberAccessException) {
        implicitScope = CreatePrincipalContextScope();
      }
      try {
        foreach (PrincipalInfo info in PrincipalInfo.Resolve(member, true)) {
          if (info.IsResolved && !CommonHelper.IsNullOrWhiteSpace(info.EmailAddress)) {
            yield return info.EmailAddress;
          }
        }
      } finally {
        if (implicitScope != null) {
          implicitScope.Dispose();
        }
      }
    }

    /// <summary>
    /// Enumerates email addresses from identities referenced by the specified SharePoint users or groups.
    /// For SharePoint users that fail to be resolved, no exception will be thrown.
    /// To eliminate duplication on subequent calls, first call <see cref="CreatePrincipalContextScope"/>.
    /// </summary>
    /// <param name="members">A list of SharePoint users or groups to be resolved.</param>
    /// <returns>A enumerable object containing resolved email addresses.</returns>
    public static IEnumerable<string> ResolveEmailAddresses(IEnumerable<SPPrincipal> members) {
      CommonHelper.ConfirmNotNull(members, "members");
      IDisposable implicitScope = null;
      try {
        PrincipalContextScope.Current.GetType();
      } catch (MemberAccessException) {
        implicitScope = CreatePrincipalContextScope();
      }
      try {
        foreach (SPPrincipal member in members) {
          foreach (PrincipalInfo info in PrincipalInfo.Resolve(member, true)) {
            if (info.IsResolved && !CommonHelper.IsNullOrWhiteSpace(info.EmailAddress)) {
              yield return info.EmailAddress;
            }
          }
        }
      } finally {
        if (implicitScope != null) {
          implicitScope.Dispose();
        }
      }
    }
  }
}