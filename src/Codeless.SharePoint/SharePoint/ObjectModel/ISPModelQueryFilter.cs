using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Codeless.SharePoint.ObjectModel {
  internal interface ISPModelQueryFilter : ICloneable {
    bool ShouldTransformExpression(SPModelQuery query);
    CamlExpression TransformExpression(SPModelQuery query, CamlExpression expression);
  }
}
