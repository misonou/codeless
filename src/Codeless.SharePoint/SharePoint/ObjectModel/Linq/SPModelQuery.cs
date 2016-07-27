using IQToolkit;
using System.Linq.Expressions;

namespace Codeless.SharePoint.ObjectModel.Linq {
  internal class SPModelQuery<T> : Query<T> {
    public SPModelQuery(SPModelQueryProvider<T> provider)
      : base(provider) { }

    public SPModelQuery(SPModelQueryProvider<T> provider, Expression expression)
      : base(provider, expression) { }
  }
}
