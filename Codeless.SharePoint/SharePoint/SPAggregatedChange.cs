using Microsoft.SharePoint;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codeless.SharePoint {
  /// <summary>
  /// Represents a set of changes on the same persisted object in SharePoint.
  /// </summary>
  public class SPAggregatedChange : Collection<SPChange> {
    private readonly bool isReadonly;

    /// <summary>
    /// Instantiate an instance of the <see cref="SPAggregatedChange"/> class.
    /// </summary>
    public SPAggregatedChange() { }

    /// <summary>
    /// Instantiate an instance of the <see cref="SPAggregatedChange"/> class with the fixed set of changes.
    /// The collection is marked read-only and no further changes to the collection is allowed.
    /// </summary>
    /// <param name="items"></param>
    public SPAggregatedChange(IList<SPChange> items) {
      items.ForEach(this.Add);
      isReadonly = true;
    }

    /// <summary>
    /// Gets the object that uniquely identifies a persisted object in SharePoint.
    /// </summary>
    public object Key { get; private set; }

    /// <summary>
    /// Gets the <see cref="Guid"/> object that uniquely identifies a persisted object in SharePoint if available.
    /// </summary>
    public Guid UniqueId { get; private set; }

    /// <summary>
    /// Gets the object type of the persisted object.
    /// </summary>
    public SPChangeObjectType ObjectType { get; private set; }

    /// <summary>
    /// Gets the combination of changes that are made on the persisted object.
    /// </summary>
    public SPChangeFlags ChangeFlags { get; private set; }

    /// <summary>
    /// Overriden.
    /// </summary>
    /// <param name="index"></param>
    /// <param name="item"></param>
    protected override void InsertItem(int index, SPChange item) {
      if (isReadonly) {
        throw new InvalidOperationException("The collection is read-only.");
      }
      if (this.Count == 0) {
        this.Key = SPChangeMonitor.GetUniqueKey(item);
        this.ObjectType = SPChangeMonitor.GetChangeObjectType(item);
        this.UniqueId = (this.Key as Guid?).GetValueOrDefault();
      }
      this.ChangeFlags |= (SPChangeFlags)SPChangeMonitor.GetBitmaskValue(item.ChangeType);
      base.InsertItem(index, item);
    }

    /// <summary>
    /// Overriden.
    /// </summary>
    /// <param name="index"></param>
    /// <param name="item"></param>
    protected override void SetItem(int index, SPChange item) {
      if (isReadonly) {
        throw new InvalidOperationException("The collection is read-only.");
      }
      base.SetItem(index, item);
    }

    /// <summary>
    /// Overriden.
    /// </summary>
    /// <param name="index"></param>
    protected override void RemoveItem(int index) {
      if (isReadonly) {
        throw new InvalidOperationException("The collection is read-only.");
      }
      base.RemoveItem(index);
    }

    /// <summary>
    /// Overriden.
    /// </summary>
    protected override void ClearItems() {
      if (isReadonly) {
        throw new InvalidOperationException("The collection is read-only.");
      }
      base.ClearItems();
    }
  }
}
