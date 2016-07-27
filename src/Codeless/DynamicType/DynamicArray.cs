using System;
using System.Collections.Generic;
using System.Linq;

namespace Codeless.DynamicType {
  public class DynamicArray : DynamicObject {
    private readonly SortedDictionary<int, object> sparseList = new SortedDictionary<int, object>();
    private int indexOffset = 0;

    public DynamicArray() {
      this.Length = 0;
    }

    public override string TypeName {
      get { return "Array"; }
    }

    [DynamicMember("length")]
    public DynamicValue Length { get; set; }
    
    [DynamicMember("unshift")]
    public DynamicValue Unshift(params DynamicValue[] values) {
      indexOffset -= values.Length;
      for (int i = 0; i < values.Length; i++) {
        sparseList[i + indexOffset] = values[i];
      }
      this.Length += values.Length;
      return this.Length;
    }

    public override IEnumerable<DynamicKey> GetKeys() {
      foreach (int i in Enumerable.Range(0, (int)this.Length)) {
        yield return new DynamicKey(i.ToString());
      }
      foreach (DynamicKey key in base.GetKeys()) {
        yield return key;
      }
    }

    public override bool GetValue(string key, out object value) {
      int index;
      if (Int32.TryParse(key, out index)) {
        if (sparseList.TryGetValue(index + indexOffset, out value)) {
          return true;
        }
        value = index + indexOffset > this.Length ? DynamicValue.Undefined : DynamicValue.Null;
        return true;
      }
      return base.GetValue(key, out value);
    }

    public override bool SetValue(string key, object value) {
      int index;
      if (Int32.TryParse(key, out index)) {
        sparseList[index + indexOffset] = value;
        this.Length = Math.Max(this.Length, index + 1);
        return true;
      }
      return base.SetValue(key, value);
    }

    public override void DeleteKey(string key) {
      int index;
      if (Int32.TryParse(key, out index)) {
        if (sparseList.ContainsKey(index + indexOffset)) {
          sparseList.Remove(index + indexOffset);
        }
        return;
      }
      base.DeleteKey(key);
    }
  }
}
