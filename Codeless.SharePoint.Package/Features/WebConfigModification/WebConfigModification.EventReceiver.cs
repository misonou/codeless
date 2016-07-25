using Microsoft.SharePoint;
using Microsoft.SharePoint.Administration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Codeless.SharePoint.Package.Features {
  [Guid("eec35c1d-9fa9-4424-8b9f-03eb46273c16")]
  public class WebConfigModificationEventReceiver : SPFeatureReceiver {
    public override void FeatureInstalled(SPFeatureReceiverProperties properties) {
      using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("WebConfigModifications.xml")) {
        using (XmlReader reader = XmlReader.Create(s)) {
          ApplyWebConfigModifications(reader, SPFarm.Local.Solutions[properties.Definition.SolutionId].DeployedWebApplications);
        }
      }
    }

    public override void FeatureUninstalling(SPFeatureReceiverProperties properties) {
      foreach (SPWebApplication app in SPFarm.Local.Solutions[properties.Definition.SolutionId].DeployedWebApplications) {
        foreach (SPWebConfigModification mod in app.WebConfigModifications.Where(v => v.Owner == "Codeless.SharePoint")) {
          app.WebConfigModifications.Remove(mod);
        }
        app.Update();
        app.WebService.ApplyWebConfigModifications();
      }
    }

    private const string NS = "http://sharepoint.codeless.org/webconfigmod";

    private static void ApplyWebConfigModifications(XmlReader reader, ICollection<SPWebApplication> apps) {
      SPWebConfigModification[] mods = GetWebConfigModifications(reader);
      foreach (SPWebApplication app in apps) {
        foreach (SPWebConfigModification mod in mods) {
          app.WebConfigModifications.Add(mod);
        }
        app.Update();
        app.WebService.ApplyWebConfigModifications();
      }
    }

    private static SPWebConfigModification[] GetWebConfigModifications(XmlReader reader) {
      XDocument doc = XDocument.Load(reader);
      XAttribute ownerAttr = doc.Root.Attribute($"{{{NS}}}:owner");
      return GetWebConfigModifications(ownerAttr.Value, doc.Root, "/").ToArray();
    }

    private static IEnumerable<SPWebConfigModification> GetWebConfigModifications(string owner, XElement node, string path) {
      List<string> keys = new List<string>();
      XAttribute keyAttr = node.Attribute($"{{{NS}}}:key");
      if (keyAttr != null) {
        keys.AddRange(keyAttr.Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
      } else {
        keys.AddRange(node.Attributes().Where(v => v.Name.Namespace.NamespaceName != NS).Select(v => v.Name.LocalName));
      }
      StringBuilder sb = new StringBuilder(node.Name.LocalName);
      foreach (XAttribute attr in node.Attributes()) {
        if (keys.Contains(attr.Name.LocalName)) {
          sb.AppendFormat("[@{0}='{1}']", attr.Name.LocalName, attr.Value);
        }
      }
      XElement clone = new XElement(node.Name);
      foreach (XAttribute attr in node.Attributes()) {
        if (attr.Name.Namespace.NamespaceName != NS) {
          clone.Add(new XAttribute(attr));
        }
      }
      XmlReader reader = clone.CreateReader();
      reader.Read();
      string xmlValue = reader.ReadOuterXml();

      yield return new SPWebConfigModification {
        Path = path,
        Name = sb.ToString(),
        Owner = owner,
        Type = node.Elements().Count() == 0 ? SPWebConfigModification.SPWebConfigModificationType.EnsureChildNode : SPWebConfigModification.SPWebConfigModificationType.EnsureSection,
        Value = xmlValue
      };
      if (keys.Count > 0) {
        foreach (XAttribute attr in node.Attributes()) {
          if (!keys.Contains(attr.Name.LocalName)) {
            yield return new SPWebConfigModification {
              Path = path,
              Name = attr.Name.LocalName,
              Owner = owner,
              Type = SPWebConfigModification.SPWebConfigModificationType.EnsureAttribute,
              Value = attr.Value
            };
          }
        }
      }
      string childPath = String.Concat(path, "/", node.Name);
      foreach (XElement elm in node.Elements()) {
        foreach (SPWebConfigModification m in GetWebConfigModifications(owner, elm, childPath)) {
          yield return m;
        }
      }
    }
  }
}
