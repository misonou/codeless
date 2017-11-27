using System;
using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using System.Security.Principal;
using System.Threading;

namespace Codeless.SharePoint.Internal {
  internal sealed class PrincipalContextScope : IDisposable {
    [ThreadStatic]
    private static PrincipalContextScope currentScope;
    private readonly Stack<PrincipalContext> contextStack = new Stack<PrincipalContext>();
    private readonly Dictionary<string, PrincipalContext> contexts = new Dictionary<string, PrincipalContext>();

    public PrincipalContextScope() {
      if (Interlocked.CompareExchange(ref currentScope, this, null) != null) {
        throw new InvalidOperationException();
      }
      PrincipalContext pc;
      try {
        string domainName = WindowsIdentity.GetCurrent().Name.Split('\\')[0];
        pc = new PrincipalContext(ContextType.Domain, domainName);
      } catch {
        pc = new PrincipalContext(ContextType.Machine);
      }
      this.LocalContext = pc;
      contextStack.Push(pc);
      contexts.Add(pc.ConnectedServer.ToLowerInvariant(), pc);
    }

    public static PrincipalContextScope Current {
      get { return CommonHelper.AccessNotNull(currentScope, "CurrentScope"); }
    }

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
          PrincipalContext context = new PrincipalContext(ContextType.Domain, domainName);
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
  }
}
