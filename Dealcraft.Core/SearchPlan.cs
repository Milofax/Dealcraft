namespace Dealcraft.Core;

/// <summary>
/// How the curve will be searched, and what it will cost. The cost is part of
/// the plan because every probe is a call into the live game, and the mod has
/// to be able to say beforehand how many it intends to make.
/// </summary>
public readonly struct SearchPlan
{
    private SearchPlan(bool viable, OfferBounds bounds, int worstCaseProbes, string problem)
    {
        IsViable = viable;
        Bounds = bounds;
        WorstCaseProbes = worstCaseProbes;
        Problem = problem;
    }

    /// <summary>False when the screen offers nothing that can be searched.</summary>
    public bool IsViable { get; }

    /// <summary>Only meaningful when <see cref="IsViable"/>.</summary>
    public OfferBounds Bounds { get; }

    /// <summary>
    /// The most times the customer can be asked while building the whole curve
    /// over <see cref="Bounds"/>.
    /// </summary>
    public int WorstCaseProbes { get; }

    /// <summary>Why there is no plan. Empty unless there is none.</summary>
    public string Problem { get; }

    public static SearchPlan Searchable(OfferBounds bounds, int worstCaseProbes) =>
        new(viable: true, bounds, worstCaseProbes, string.Empty);

    public static SearchPlan Impossible(string problem) =>
        new(viable: false, default, worstCaseProbes: 0, problem);
}
