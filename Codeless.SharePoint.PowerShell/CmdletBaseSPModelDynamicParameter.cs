using Codeless.SharePoint.ObjectModel;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Reflection;

namespace Codeless.SharePoint.PowerShell {
  public abstract class CmdletBaseSPModelDynamicParameter : CmdletBaseSPModel, IDynamicParameters {
    private RuntimeDefinedParameterDictionary modelParameters;
    
    protected void UpdateModelFromParameters(SPModel item) {
      foreach (RuntimeDefinedParameter parameter in modelParameters.Values) {
        if (parameter.IsSet) {
          PropertyInfo property = base.Descriptor.ModelType.GetProperty(parameter.Name);
          if (property.PropertyType.IsOf(typeof(IList))) {
            IList list = (IList)property.GetValue(item, null);
            foreach (object value in (IList)parameter.Value) {
              list.Add(value);
            }
          } else {
            property.SetValue(item, parameter.Value, null);
          }
        }
      }
    }

    object IDynamicParameters.GetDynamicParameters() {
      ResolveManager();
      SPModelDescriptor descriptor = SPModelDescriptor.Resolve(this.TypeName);
      RuntimeDefinedParameterDictionary parameters = new RuntimeDefinedParameterDictionary();
      foreach (PropertyInfo property in descriptor.ModelType.GetProperties()) {
        if (property.CanWrite || property.PropertyType.IsOf(typeof(IList<>))) {
          ParameterAttribute attribute = new ParameterAttribute();
          attribute.ParameterSetName = "__AllParameterSets";
          parameters.Add(property.Name, new RuntimeDefinedParameter(property.Name, property.PropertyType, new Collection<Attribute> { attribute }));
        }
      }
      modelParameters = parameters;
      return parameters;
    }
  }
}
