using Microsoft.SharePoint;
using Microsoft.SharePoint.Administration.Claims;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Security.Principal;
using System.Web.Security;

namespace Codeless.SharePoint.Internal {
  #region Exceptions
  internal class PrincipalResolveException : Exception {
    public string IdentityValue { get; private set; }

    public PrincipalResolveException(string value, Exception innerException)
      : base(String.Format("Cannot resolve identity \"{0}\"", value), innerException) {
      this.IdentityValue = value;
    }
  }

  internal class ADPrincipalResolveException : PrincipalResolveException {
    public IdentityType IdentityType { get; private set; }

    public ADPrincipalResolveException(IdentityType identityType, string value, Exception innerException)
      : base(String.Format("Cannot resolve identity \"{0}\"", value), innerException) {
      this.IdentityType = identityType;
    }
  }
  #endregion

  internal class PrincipalResolver {
    private const string ClaimTypes_Role = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";
    private const string ClaimTypes_GroupSid = "http://schemas.microsoft.com/ws/2008/06/identity/claims/groupsid";

    private readonly bool throwOnException;
    private readonly HashSet<object> resolvedIdentities = new HashSet<object>();
    private SPPrincipal parentPrincipal;

    public PrincipalResolver(bool throwOnException) {
      this.throwOnException = throwOnException;
    }

    public IEnumerable<PrincipalInfo> Resolve(SPPrincipal member, IDisposable implicitContext) {
      if (member is SPGroup) {
        return Resolve((SPGroup)member, implicitContext);
      }
      return Resolve((SPUser)member, implicitContext);
    }

    public IEnumerable<PrincipalInfo> Resolve(SPGroup group, IDisposable implicitContext) {
      try {
        CommonHelper.ConfirmNotNull(group, "group");
        if (!resolvedIdentities.Contains(group.ID)) {
          resolvedIdentities.Add(group.ID);

          List<PrincipalInfo> userInfos = new List<PrincipalInfo>();
          foreach (SPUser user in group.Users) {
            userInfos.AddRange(Resolve(user, null));
          }
          return userInfos.ToArray();
        }
        return new PrincipalInfo[0];
      } finally {
        if (implicitContext != null) {
          implicitContext.Dispose();
        }
      }
    }

    public IEnumerable<PrincipalInfo> Resolve(SPUser user, IDisposable implicitContext) {
      try {
        CommonHelper.ConfirmNotNull(user, "user");
        if (!resolvedIdentities.Contains(user.ID)) {
          resolvedIdentities.Add(user.ID);
          parentPrincipal = user;
          if (SPClaimProviderManager.IsEncodedClaim(user.LoginName)) {
            SPClaim claim = SPClaimProviderManager.Local.DecodeClaim(user.LoginName);
            if (claim.OriginalIssuer == "Windows") {
              if (claim.ClaimType == SPClaimTypes.UserLogonName) {
                PrincipalInfo userInfo = ResolveActiveDirectoryUser(IdentityType.SamAccountName, claim.Value);
                if (userInfo != null) {
                  return new[] { userInfo };
                }
                return new PrincipalInfo[0];
              }
              if (claim.ClaimType == ClaimTypes_GroupSid) {
                return EnumerateActiveDirectoryGroup(IdentityType.SamAccountName, user.Name);
              }
            }
            if (claim.OriginalIssuer.StartsWith("Forms:")) {
              string providerName = claim.OriginalIssuer.Substring(6);
              if (claim.ClaimType == SPClaimTypes.UserLogonName) {
                PrincipalInfo userInfo = ResolveMembershipUser(providerName, claim.Value);
                if (userInfo != null) {
                  return new[] { userInfo };
                }
                return new PrincipalInfo[0];
              }
              if (claim.ClaimType == ClaimTypes_Role) {
                return EnumerateMembershipUsersInRole(providerName, claim.Value);
              }
            }
          }
          if (user.IsDomainGroup) {
            return EnumerateActiveDirectoryGroup(IdentityType.SamAccountName, user.LoginName);
          } 
          return EnumerateBySamAccountName(user.LoginName);
        }
        return new PrincipalInfo[0];
      } finally {
        if (implicitContext != null) {
          implicitContext.Dispose();
        }
      }
    }

    private PrincipalInfo ResolveActiveDirectoryUser(UserPrincipal user) {
      string providerUserId = user.DistinguishedName ?? user.SamAccountName;
      if (!resolvedIdentities.Contains(providerUserId)) {
        resolvedIdentities.Add(providerUserId);
        return new PrincipalInfo(user, parentPrincipal);
      }
      return null;
    }

    private PrincipalInfo ResolveActiveDirectoryUser(IdentityType identityType, string value) {
      if (identityType == IdentityType.SamAccountName) {
        return EnumerateBySamAccountName(value).FirstOrDefault();
      }
      try {
        UserPrincipal userPrincipal = UserPrincipal.FindByIdentity(PrincipalContextScope.Current.CurrentContext, identityType, value);
        if (userPrincipal != null) {
          using (userPrincipal) {
            return ResolveActiveDirectoryUser(userPrincipal);
          }
        }
      } catch (Exception ex) {
        return ExceptionHandler(new ADPrincipalResolveException(identityType, value, ex));
      }
      return null;
    }

    private PrincipalInfo ResolveMembershipUser(string providerName, string username) {
      try {
        MembershipUser user;
        if (providerName != null) {
          MembershipProvider provider = Membership.Providers[providerName];
          if (provider == null) {
            throw new ConfigurationErrorsException(String.Format("Membership provider \"{0}\"not found", providerName));
          }
          user = provider.GetUser(username, false);
        } else {
          user = Membership.GetUser(username, false);
        }
        if (user != null) {
          return new PrincipalInfo(user, parentPrincipal);
        }
      } catch (Exception ex) {
        return ExceptionHandler(new PrincipalResolveException(username, ex));
      }
      return null;
    }

    private IEnumerable<PrincipalInfo> EnumerateActiveDirectoryGroup(GroupPrincipal group) {
      List<PrincipalInfo> userInfos = new List<PrincipalInfo>();
      if (!resolvedIdentities.Contains(group.DistinguishedName)) {
        resolvedIdentities.Add(group.DistinguishedName);
        try {
          DirectoryEntry groupEntry = (DirectoryEntry)group.GetUnderlyingObject();
          foreach (string dn in groupEntry.Properties["member"]) {
            try {
              DirectoryEntry memberEntry = new DirectoryEntry("LDAP://" + dn);
              PropertyCollection userProps = memberEntry.Properties;
              object[] objectClass = (object[])userProps["objectClass"].Value;

              if (objectClass.Contains("group")) {
                userInfos.AddRange(EnumerateActiveDirectoryGroup(IdentityType.DistinguishedName, userProps["distinguishedName"].Value.ToString()));
                continue;
              }
              if (objectClass.Contains("foreignSecurityPrincipal")) {
                userInfos.AddRange(EnumerateForeignSecurityPrincipal(memberEntry));
                continue;
              }
              PrincipalInfo userInfo = ResolveActiveDirectoryUser(IdentityType.DistinguishedName, dn);
              if (userInfo != null) {
                userInfos.Add(userInfo);
              }
            } catch (Exception ex) {
              userInfos.Add(ExceptionHandler(new ADPrincipalResolveException(IdentityType.DistinguishedName, dn, ex)));
            }
          }
        } catch (ADPrincipalResolveException) {
          throw;
        } catch (Exception ex) {
          userInfos.Add(ExceptionHandler(new ADPrincipalResolveException(IdentityType.DistinguishedName, group.DistinguishedName, ex)));
        }
      }
      return userInfos;
    }

    private IEnumerable<PrincipalInfo> EnumerateActiveDirectoryGroup(IdentityType identityType, string value) {
      try {
        GroupPrincipal groupPrincipal = GroupPrincipal.FindByIdentity(PrincipalContextScope.Current.CurrentContext, identityType, value);
        if (groupPrincipal != null) {
          using (groupPrincipal) {
            return EnumerateActiveDirectoryGroup(groupPrincipal);
          }
        }
      } catch (ADPrincipalResolveException) {
        throw;
      } catch (Exception ex) {
        return new[] { ExceptionHandler(new ADPrincipalResolveException(identityType, value, ex)) };
      }
      return new PrincipalInfo[0];
    }

    private IEnumerable<PrincipalInfo> EnumerateForeignSecurityPrincipal(DirectoryEntry de) {
      SecurityIdentifier sid = new SecurityIdentifier((byte[])de.Properties["objectSid"].Value, 0);
      NTAccount account = (NTAccount)sid.Translate(typeof(NTAccount));
      return EnumerateBySamAccountName(account.ToString());
    }

    private IEnumerable<PrincipalInfo> EnumerateBySamAccountName(string samAccountName) {
      List<PrincipalInfo> userInfos = new List<PrincipalInfo>();
      string[] tokens = samAccountName.Split('\\');
      PrincipalContext context = null;
      try {
        if (tokens.Length == 1) {
          context = PrincipalContextScope.Current.LocalContext;
        } else if ("NT AUTHORITY".Equals(tokens[0], StringComparison.OrdinalIgnoreCase) || "SHAREPOINT".Equals(tokens[0], StringComparison.OrdinalIgnoreCase)) {
          context = null;
        } else {
          context = PrincipalContextScope.Current.GetContextByDomainName(tokens[0]);
        }
      } catch (Exception ex) {
        userInfos.Add(ExceptionHandler(new ADPrincipalResolveException(IdentityType.SamAccountName, samAccountName, ex)));
      }

      if (context != null) {
        PrincipalContextScope.Current.EnterContext(context);
        try {
          Principal principal = Principal.FindByIdentity(context, IdentityType.SamAccountName, tokens.Last());
          if (principal != null) {
            using (principal) {
              if (principal is GroupPrincipal) {
                userInfos.AddRange(EnumerateActiveDirectoryGroup((GroupPrincipal)principal));
              } else if (principal is UserPrincipal) {
                PrincipalInfo userInfo = ResolveActiveDirectoryUser((UserPrincipal)principal);
                if (userInfo != null) {
                  userInfos.Add(userInfo);
                }
              }
            }
          }
        } catch (ADPrincipalResolveException) {
          throw;
        } catch (Exception ex) {
          userInfos.Add(ExceptionHandler(new ADPrincipalResolveException(IdentityType.SamAccountName, samAccountName, ex)));
        } finally {
          PrincipalContextScope.Current.LeaveContext();
        }
      }
      return userInfos;
    }

    private IEnumerable<PrincipalInfo> EnumerateMembershipUsersInRole(string providerName, string roleName) {
      List<PrincipalInfo> userInfos = new List<PrincipalInfo>();
      try {
        RoleProvider provider = Roles.Providers[providerName];
        if (provider == null) {
          throw new ConfigurationErrorsException(String.Format("Role provider \"{0}\" not found", providerName));
        }
        foreach (string username in provider.GetUsersInRole(roleName)) {
          PrincipalInfo resolvedUser = ResolveMembershipUser(null, username);
          if (resolvedUser != null) {
            userInfos.Add(resolvedUser);
          }
        }
      } catch (PrincipalResolveException) {
        throw;
      } catch (Exception ex) {
        userInfos.Add(ExceptionHandler(new PrincipalResolveException(roleName, ex)));
      }
      return userInfos;
    }

    private PrincipalInfo ExceptionHandler(PrincipalResolveException exception) {
      if (throwOnException) {
        throw exception;
      }
      return new PrincipalInfo(exception.IdentityValue, exception, parentPrincipal);
    }
  }
}
