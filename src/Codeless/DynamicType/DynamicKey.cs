using System;

namespace Codeless.DynamicType {
  public class DynamicKey : IEquatable<DynamicKey> {
    public DynamicKey(string name) {
      this.Name = name;
    }

    public string Name { get; private set; }

    public bool Equals(DynamicKey other) {
      if (other != null) {
        return this.Name.Equals(other.Name);
      }
      return false;
    }

    public override bool Equals(object obj) {
      if (obj is DynamicKey) {
        return Equals((DynamicKey)obj);
      }
      return base.Equals(obj);
    }

    public override int GetHashCode() {
      return this.Name.GetHashCode();
    }

    public override string ToString() {
      return this.Name;
    }
  }
}
