using System.Collections.Generic;

namespace Dealcraft.Core;

/// <summary>
/// What the log says about a deal the automation has just scheduled.
/// </summary>
/// <remarks>
/// User story 7 asks for the real clock time a scheduled deal starts and ends,
/// so the host can plan the route rather than guess the window; user story 10
/// asks for every automated action to be auditable afterwards. Both are the
/// same three lines. The window's nominal hours and the contract's own timings
/// are reported separately because they are two readings, and it is the
/// contract's that the player has to turn up for.
/// </remarks>
public static class ScheduledDealReport
{
    public static IEnumerable<string> Describe(
        string customerName,
        DealWindowHours window,
        ContractTimings timings)
    {
        string who = string.IsNullOrWhiteSpace(customerName) ? "A customer" : customerName;

        yield return $"{who}'s deal scheduled into {window.Name}.";
        yield return $"{window.Name} runs {window.Range}.";

        if (!timings.Available)
        {
            yield return "The game did not report timings for this contract.";
            yield break;
        }

        yield return
            $"The contract runs {GameClock.Spell(timings.SoftStartTime)} to "
            + $"{GameClock.Spell(timings.EndTime)}, and the customer is there from "
            + $"{GameClock.Spell(timings.HardStartTime)}.";
    }
}
