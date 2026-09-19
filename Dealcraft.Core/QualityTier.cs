namespace Dealcraft.Core;

/// <summary>
/// The five product grades, as ranks rather than as a game type.
///
/// The adapter passes <c>(int)EQuality</c> in and the core never sees the enum,
/// but the ladder itself is a decision concept — "one tier above what the
/// contract asked for" is a sentence about ranks — so the ordering and the
/// spellings live here.
/// </summary>
public static class QualityTier
{
    public const int Trash = 0;
    public const int Poor = 1;
    public const int Standard = 2;
    public const int Premium = 3;
    public const int Heavenly = 4;

    /// <summary>The lowest and highest ranks the game has.</summary>
    public const int Lowest = Trash;

    public const int Highest = Heavenly;

    /// <summary>
    /// Whether this rank is one the game knows. A grade outside the ladder is
    /// left out of every plan rather than ranked against the others on a guess.
    /// </summary>
    public static bool IsKnown(int quality) => quality >= Lowest && quality <= Highest;

    /// <summary>The grade's name, as the game spells it.</summary>
    public static string Name(int quality) => quality switch
    {
        Trash => "Trash",
        Poor => "Poor",
        Standard => "Standard",
        Premium => "Premium",
        Heavenly => "Heavenly",
        _ => $"quality {quality}",
    };
}
