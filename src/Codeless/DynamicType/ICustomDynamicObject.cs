using System.Collections.Generic;

namespace Codeless.DynamicType {
  public interface ICustomDynamicObject {
    string TypeName { get; }
    IEnumerable<DynamicKey> GetKeys();
    bool GetValue(string key, out object value);
    void SetValue(string key, object value);
    void DeleteKey(string key);
  }
}
