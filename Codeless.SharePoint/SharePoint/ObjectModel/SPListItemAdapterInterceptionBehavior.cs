using Microsoft.Practices.Unity.InterceptionExtension;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;

namespace Codeless.SharePoint.ObjectModel {
  internal class SPListItemAdapterInterceptionBehavior : IInterceptionBehavior {
    private readonly SPModelCollection parentCollection;
    private readonly ISPListItemAdapter adapter;
    private readonly Dictionary<string, object> typedValues = new Dictionary<string, object>();

    public SPListItemAdapterInterceptionBehavior(ISPListItemAdapter adapter, SPModelCollection parentCollection) {
      CommonHelper.ConfirmNotNull(adapter, "adapter");
      CommonHelper.ConfirmNotNull(parentCollection, "parentCollection");
      this.parentCollection = parentCollection;
      this.adapter = adapter;
    }

    public bool WillExecute {
      get { return true; }
    }

    public IEnumerable<Type> GetRequiredInterfaces() {
      return Enumerable.Empty<Type>();
    }

    public IMethodReturn Invoke(IMethodInvocation input, GetNextInterceptionBehaviorDelegate getNext) {
      if ((input.MethodBase.Name.StartsWith("Get") && input.MethodBase.Name != "GetType") || input.MethodBase.Name.StartsWith("get_")) {
        string cacheKey = input.Arguments.Count > 0 ? (string)input.Arguments[0] : input.MethodBase.Name;
        object value;
        if (typedValues.TryGetValue(cacheKey, out value)) {
          if ((value != null && ((MethodInfo)input.MethodBase).ReturnType.IsAssignableFrom(value.GetType())) ||
              (value == null && !((MethodInfo)input.MethodBase).ReturnType.IsValueType)) {
            return input.CreateMethodReturn(value);
          }
        }
        IMethodReturn result = getNext()(input, getNext);
        if (result.Exception == null) {
          Type elementType;
          if (result.ReturnValue != null && result.ReturnValue.GetType().IsOf(typeof(ObservableCollection<>), out elementType)) {
            if (parentCollection.IsReadOnly) {
              try {
                result = input.CreateMethodReturn(typeof(ReadOnlyCollection<>).MakeGenericType(elementType).CreateInstance(result.ReturnValue));
              } catch (Exception ex) {
                return input.CreateExceptionMethodReturn(ex);
              }
            } else {
              ((INotifyCollectionChanged)result.ReturnValue).CollectionChanged += ((sender, e) => parentCollection.Manager.SaveOnCommit(adapter));
            }
          }
          typedValues[cacheKey] = result.ReturnValue;
        }
        return result;
      }
      if (input.MethodBase.Name.StartsWith("Set")) {
        if (parentCollection.IsReadOnly) {
          return input.CreateExceptionMethodReturn(new InvalidOperationException("Cannot set value to a read-only model"));
        }
        IMethodReturn result = getNext()(input, getNext);
        if (result.Exception == null) {
          string fieldName = (string)input.Arguments[0];
          typedValues[fieldName] = input.Arguments[1];
          parentCollection.Manager.SaveOnCommit(adapter);
        }
        return result;
      }
      return getNext()(input, getNext);
    }
  }
}
