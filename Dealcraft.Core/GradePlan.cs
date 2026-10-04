using System;
using System.Collections.Generic;

namespace Dealcraft.Core;

/// <summary>
/// Which grade to hand over, and what every grade in reach would have come to.
/// </summary>
public sealed class GradePlan
{
    private GradePlan(
        DeliveryRequest request,
        bool canDeliver,
        int chosenQuality,
        int chosenUnits,
        string reason,
        IReadOnlyList<GradeOutcome> outcomes)
    {
        Request = request;
        CanDeliver = canDeliver;
        ChosenQuality = chosenQuality;
        ChosenUnits = chosenUnits;
        Reason = reason;
        Outcomes = outcomes;
    }

    public DeliveryRequest Request { get; }

    /// <summary>False when nothing in reach fills the order on its own.</summary>
    public bool CanDeliver { get; }

    /// <summary>Only meaningful when <see cref="CanDeliver"/>.</summary>
    public int ChosenQuality { get; }

    /// <summary>
    /// How many units to hand over: exactly what the contract asked for. The
    /// Generosity Bonus pays ten a unit for overshooting, which is not worth
    /// spending stock on without the player saying so.
    /// <para>
    /// A target, not a promise. Product travels in packages — a jar is one item
    /// worth several units — so the adapter hands over the fewest whole
    /// packages that reach this number and reports what that came to. It can
    /// overshoot; it never falls short, because a grade is only chosen when it
    /// has the units.
    /// </para>
    /// </summary>
    public int ChosenUnits { get; }

    /// <summary>Why this grade, or why none. Always populated; it goes in the log.</summary>
    public string Reason { get; }

    /// <summary>
    /// Every grade in reach, lowest first, whether or not it was chosen. This
    /// is the part the player reads: it says what the stock they are about to
    /// spend would have been worth spent differently.
    /// </summary>
    public IReadOnlyList<GradeOutcome> Outcomes { get; }

    /// <summary>The outcome for the grade that was chosen, if one was.</summary>
    public GradeOutcome? Chosen
    {
        get
        {
            if (!CanDeliver)
            {
                return null;
            }

            foreach (GradeOutcome outcome in Outcomes)
            {
                if (outcome.Quality == ChosenQuality)
                {
                    return outcome;
                }
            }

            return null;
        }
    }

    internal static GradePlan Deliver(
        DeliveryRequest request,
        int quality,
        int units,
        string reason,
        IReadOnlyList<GradeOutcome> outcomes) =>
        new(request, canDeliver: true, quality, units, reason, outcomes);

    internal static GradePlan Nothing(
        DeliveryRequest request,
        string reason,
        IReadOnlyList<GradeOutcome> outcomes) =>
        new(request, canDeliver: false, QualityTier.Lowest, 0, reason, outcomes ?? Array.Empty<GradeOutcome>());

    public override string ToString() =>
        CanDeliver
            ? $"{ChosenUnits} x {QualityTier.Name(ChosenQuality)} ({Reason})"
            : $"no delivery ({Reason})";
}
