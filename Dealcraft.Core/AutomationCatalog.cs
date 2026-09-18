using System.Collections.Generic;

namespace Dealcraft.Core;

/// <summary>
/// What the shared settings say. One entry per MelonPreferences entry, each
/// carrying what the file currently says, one short line, and whether it needs
/// the player to be host.
/// </summary>
/// <remarks>
/// <para>
/// This is the list the spec's rule is enforced against: "there is no second
/// store, no in-memory-only setting and no setting that exists in one place but
/// not the other. If it can be configured, it is in the file and it is in the
/// app." <see cref="AutomationSetting.Key"/> is the entry name in the file, so
/// the two halves of that promise are checkably the same setting rather than
/// two things with similar titles.
/// </para>
/// <para>
/// The labels are the drawing's, verbatim. Each "Automated" answer says what it
/// automates, because the two price blocks cannot be told apart by their
/// headings — both of us confused them — and the second line of explanation that
/// would otherwise be needed beside each heading is exactly what the owner
/// deleted.
/// </para>
/// <para>
/// The handover's grade answer is spelled as its "yes": the key is on when a
/// delivery may round the grade up, so the off state — the default — is
/// <i>exactly the grade that was ordered</i>, which is what a fresh install does
/// and what the block draws first.
/// </para>
/// <para>
/// Every entry <em>this catalogue</em> describes is on the page. The acceptance
/// threshold was the last one that was not, and it is drawn now as the chance
/// floor under the negotiation block — <see cref="ChanceFloor"/> for what it
/// means and why it is a floor rather than a target.
/// </para>
/// <para>
/// That is not the same sentence as "every key the preferences file holds is on
/// the page", and it was read as it for a while. The file holds one key no
/// catalogue describes, the handover ledger, and
/// <see cref="AutomationForm.FileOnly"/> is where that is written down.
/// </para>
/// </remarks>
public static class AutomationCatalog
{
    public static IReadOnlyList<AutomationSetting> Describe(AdvisorSettings settings) =>
        new List<AutomationSetting>
        {
            new(
                "AutoCounterOffer",
                "Automated handling of customer requests",
                AutomationSetting.OnOff(settings.AutoCounterOffer),
                summary: string.Empty,
                hostOnly: true,
                change: AutomationChoice.Switch(settings.AutoCounterOffer)),
            new(
                "AutoHandover",
                "Automated handover when I talk to them",
                AutomationSetting.OnOff(settings.AutoHandover),

                // No line under it. "when I talk to them" is what closes the
                // courier reading — the player is standing there with the goods,
                // and he pressed E — and the drawing carries no footnote here.
                summary: string.Empty,

                // Not host-only, and it is the one switch that is not. A
                // handover is this player's goods leaving this player's pockets,
                // so whoever installed Dealcraft gets it for their own
                // deliveries; see HandoverGate.
                hostOnly: false,
                change: AutomationChoice.Switch(settings.AutoHandover)),
            new(
                "HandoverFromAnywhere",
                "Automated handover from anywhere",
                AutomationSetting.OnOff(settings.HandoverFromAnywhere),

                // No line under it either. What this answer costs is one row of
                // its own under it, because it is true whether or not the answer
                // is the one ticked and a player has to read it before choosing
                // rather than after.
                summary: string.Empty,

                // Follows the handover, for the handover's reason: it is this
                // player's goods leaving this player's pockets, so where they
                // may leave from is this player's answer. The multiplayer cost
                // of the answer is not that it belongs to the host — it is that
                // the host's copy reaches the shared contract list first.
                hostOnly: false,
                change: AutomationChoice.Switch(settings.HandoverFromAnywhere)),
            new(
                "MayUseAHigherGrade",
                "May use a higher grade",
                AutomationSetting.OnOff(settings.MayUseAHigherGrade),

                // No line under this answer. The drawing's footnote about
                // package sizes sits under the pair rather than beside one of
                // them — it is true whichever answer is ticked — so the block
                // draws it as its own row and this stays empty.
                summary: string.Empty,

                // The handover's own sub-option, so it follows the handover: a
                // guest's goods, on a guest's machine, chosen by the guest.
                hostOnly: false,
                change: AutomationChoice.Switch(settings.MayUseAHigherGrade)),
            new(
                ChanceFloor.Key,
                ChanceFloor.Title,

                // Whole percent, which is not how the file spells it. Every
                // other row is the file's own text, and this one cannot be: the
                // game shows the player a percentage, the drawing asks for one,
                // and the file holds the chance itself because that is the unit
                // every reader of it works in. The preference's description says
                // so where somebody editing by hand will see it.
                ChanceFloor.Spelled(settings.AcceptanceProbabilityThreshold),

                // No line under it. The footnote the drawing once carried here
                // was read out of the binary by ticket 37 and is false on both
                // halves; see ChanceFloor.
                summary: string.Empty,

                // The counter it holds back is the host's to send, so it is the
                // host's to set. A guest's copy would refuse nothing, because a
                // guest never reaches CounterofferGate.Send at all.
                hostOnly: true,
                change: AutomationChoice.Range(
                    settings.AcceptanceProbabilityThreshold,
                    ChanceFloor.Lowest,
                    ChanceFloor.Highest,
                    ChanceFloor.Step)),
        };
}
