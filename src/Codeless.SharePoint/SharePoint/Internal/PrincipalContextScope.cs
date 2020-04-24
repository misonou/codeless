using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Security.Principal;
using System.Threading;

namespace Codeless.SharePoint.Internal {
  internal sealed class PrincipalContextScope : IDisposable {
    [ThreadStatic]
    private static PrincipalContextScope currentScope;
    private readonly Stack<PrincipalContext> contextStack = new Stack<PrincipalContext>();
    private readonly Dictionary<string, PrincipalContext> contexts = new Dictionary<string, PrincipalContext>();

    public PrincipalContextScope(bool preferSSL) {
      if (Interlocked.CompareExchange(ref currentScope, this, null) != null) {
        throw new InvalidOperationException();
      }
      PrincipalContext pc;
      try {
        string domainName = WindowsIdentity.GetCurrent().Name.Split('\\')[0];
        pc = GetContextByDomainName(domainName, preferSSL);
      } catch {
        pc = new PrincipalContext(ContextType.Machine);
      }
      this.preferSSL = preferSSL;
      this.LocalContext = pc;
      contextStack.Push(pc);
      contexts.Add(pc.ConnectedServer.ToLowerInvariant(), pc);
    }

    public static PrincipalContextScope Current {
      get { return CommonHelper.AccessNotNull(currentScope, "CurrentScope"); }
    }

    private readonly bool preferSSL;

    public PrincipalContext LocalContext { get; private set; }

    public PrincipalContext CurrentContext {
      get { return contextStack.Peek(); }
    }

    public PrincipalContext LeaveContext() {
      return contextStack.Pop();
    }

    public void EnterContext(PrincipalContext context) {
      contextStack.Push(context);
    }

    public void Dispose() {
      Interlocked.Exchange(ref currentScope, null);
      foreach (PrincipalContext pc in contexts.Values) {
        try {
          pc.Dispose();
        } catch { }
      }
      this.LocalContext = null;
    }

    public PrincipalContext GetContextByDomainName(string domainName) {
      CommonHelper.ConfirmNotNull(domainName, "domainName");
      PrincipalContext cachedContext;
      if (!contexts.TryGetValue(domainName.ToLowerInvariant(), out cachedContext)) {
        try {
          PrincipalContext context = GetContextByDomainName(domainName, preferSSL);
          if (context.ConnectedServer != null) {
            cachedContext = context;
          }
        } catch {
        }
        contexts.Add(domainName.ToLowerInvariant(), cachedContext);
      }
      if (cachedContext == null) {
        throw new InvalidOperationException(String.Format("Unable to connect domain server for domain \"{0}\"", domainName));
      }
      return cachedContext;
    }

    private static PrincipalContext GetContextByDomainName(string domainName, bool preferSSL) {
      if (preferSSL) {
        return GetContextByDomainNameWithSSL(domainName) ?? new PrincipalContext(ContextType.Domain, domainName);
      }
      try {
        return new PrincipalContext(ContextType.Domain, domainName);
      } catch (PrincipalServerDownException) {
        PrincipalContext sslContext = GetContextByDomainNameWithSSL(domainName);
        if (sslContext != null) {
          return sslContext;
        }
        throw;
      }
    }

    private static PrincipalContext GetContextByDomainNameWithSSL(string domainName) {
      try {
        DirectoryEntry de = new DirectoryEntry("LDAP://" + domainName);
        de.AuthenticationType = AuthenticationTypes.Secure | AuthenticationTypes.SecureSocketsLayer;
        string dn = (string)de.Properties["distinguishName"].Value;
        return new PrincipalContext(ContextType.Domain, domainName + ":636", dn, ContextOptions.SecureSocketLayer | ContextOptions.Negotiate);
      } catch { }
      return null;
    }
  }
}
