using System;
using System.Reflection;

namespace Codeless.SharePoint.ObjectModel.Linq {
  internal struct SPModelFieldAssociation : IEquatable<SPModelFieldAssociation> {
    private readonly SPModelDescriptor descriptor;
    private readonly SPFieldAttribute attribute;
    private readonly PropertyInfo queryProperty;

    public SPModelFieldAssociation(SPModelDescriptor descriptor, SPFieldAttribute attribute, PropertyInfo queryProperty) {
      CommonHelper.ConfirmNotNull(descriptor, "descriptor");
      CommonHelper.ConfirmNotNull(attribute, "attribute");
      this.descriptor = descriptor;
      this.attribute = attribute;
      this.queryProperty = queryProperty;
    }

    public SPModelDescriptor Descriptor {
      get { return descriptor; }
    }

    public SPFieldAttribute Attribute {
      get { return attribute; }
    }

    public PropertyInfo QueryProperty {
      get { return queryProperty; }
    }

    public bool Equals(SPModelFieldAssociation other) {
      return descriptor.Equals(other.descriptor) && attribute.Equals(other.attribute);
    }

    public override bool Equals(object obj) {
      if (obj is SPModelFieldAssociation) {
        return Equals((SPModelFieldAssociation)obj);
      }
      return base.Equals(obj);
    }

    public override int GetHashCode() {
      return descriptor.GetHashCode() ^ attribute.GetHashCode();
    }
  }
}
