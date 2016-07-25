using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Codeless.SharePoint {
  /// <summary>
  /// Represents a keyword search refiner.
  /// </summary>
  public sealed class SearchRefiner {
    private readonly List<SearchRefinement> refinements = new List<SearchRefinement>();

    /// <summary>
    /// Initialize an instance of the <see cref="SearchRefiner"/> class with the specified refiner property.
    /// </summary>
    /// <param name="propertyName"></param>
    public SearchRefiner(string propertyName) {
      this.PropertyName = propertyName;
      this.Refinements = refinements.AsReadOnly();
    }

    /// <summary>
    /// Initialize an instance of the <see cref="SearchRefiner"/> class with the specified refiner property and refinement value.
    /// </summary>
    /// <param name="propertyName"></param>
    /// <param name="refinementToken"></param>
    public SearchRefiner(string propertyName, string refinementToken)
      : this(propertyName) {
      this.RefinementToken = refinementToken;
    }

    /// <summary>
    /// Gets the refiner name.
    /// </summary>
    public string PropertyName { get; private set; }

    /// <summary>
    /// Gets or sets the refinement to be searched against.
    /// </summary>
    public string RefinementToken { get; set; }

    /// <summary>
    /// Gets the refinements associated with this refiner.
    /// </summary>
    public ReadOnlyCollection<SearchRefinement> Refinements { get; private set; }

    internal void AddRefinement(string name, string token, int count) {
      refinements.Add(new SearchRefinement { Name = name, Token = token, Count = count });
    }
  }

  /// <summary>
  /// Represents a keyword search refinement result.
  /// </summary>
  public sealed class SearchRefinement {
    /// <summary>
    /// Gets the display text of the refinement.
    /// </summary>
    public string Name { get; internal set; }

    /// <summary>
    /// Gets the refinement token.
    /// </summary>
    public string Token { get; internal set; }

    /// <summary>
    /// Gets the count of items that fall in this refinement.
    /// </summary>
    public int Count { get; internal set; }
  }
}
