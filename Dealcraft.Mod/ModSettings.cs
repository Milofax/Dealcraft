using System;
using System.Collections.Generic;
using Dealcraft.Core;
using MelonLoader;

namespace Dealcraft;

/// <summary>
/// The MelonPreferences surface. Every switch defaults to off so a fresh install
/// cannot surprise a running save.
/// </summary>
/// <remarks>
/// <para>
/// The one owner of the mod's one category. What is declared here is what the
/// whole mod shares — the two automation switches, the handover's where and
/// grade answers, and the acceptance threshold. A
/// setting that belongs to a single feature is declared by that feature, in its
/// own file, into this same category: see <see cref="Feature.Declare"/>. So
/// there is still one category and one owner, and adding a feature's setting no
/// longer means editing this class.
/// </para>
/// <para>
/// <see cref="Describe"/> is the other half of that: the app's Automation
/// section is the shared settings plus each feature's own, assembled here so
/// that "the file and the app hold the same settings" has one place to be true
/// in.
/// </para>
/// </remarks>
internal sealed class ModSettings
{
    private const string Category = "Dealcraft";

    /// <summary>
    /// Where every default comes from. The core owns them and the preferences
    /// file is seeded from it, so the two cannot drift into disagreeing about
    /// what a fresh install does.
    /// </summary>
    private static readonly AdvisorSettings Defaults = new();

    private MelonPreferences_Entry<bool> _autoCounterOffer;
    private MelonPreferences_Entry<bool> _autoHandover;
    private MelonPreferences_Entry<bool> _handoverFromAnywhere;
    private MelonPreferences_Entry<bool> _mayUseAHigherGrade;
    private MelonPreferences_Entry<float> _acceptanceProbabilityThreshold;

    /// <summary>
    /// The mod's one category, kept from <see cref="Load"/> so a change can be
    /// flushed to disk the moment it is made. One category, one owner, one
    /// place that writes the file.
    /// </summary>
    private MelonPreferences_Category _category;

    // The switches have no readers of their own: everything that wants one
    // reads it off ToAdvisorSettings, so there is one projection rather than a
    // projection and a set of shortcuts that could answer differently.

    /// <param name="features">
    /// Every registered feature, each given the chance to declare its own
    /// entries into this same category, after the shared ones and before the
    /// single flush. The order is the registration order, so the file reads in
    /// the order the app shows.
    /// </param>
    public void Load(IReadOnlyList<Feature> features, Action<string> warn)
    {
        MelonPreferences_Category category = MelonPreferences.CreateCategory(Category);
        _category = category;

        _autoCounterOffer = category.CreateEntry("AutoCounterOffer", Defaults.AutoCounterOffer,
            description: "Host only: send counter-offers automatically.");
        _autoHandover = category.CreateEntry("AutoHandover", Defaults.AutoHandover,
            description: "Complete a handover when the game reports it valid. Works for whoever "
                + "installs Dealcraft, host or guest: it is your own goods leaving your own "
                + "pockets, and the game's own readiness check already asks about the player "
                + "standing there. Negotiating and scheduling stay host-only, because those act "
                + "on one shared conversation and one shared contract list.");
        _handoverFromAnywhere = category.CreateEntry(
            "HandoverFromAnywhere", Defaults.HandoverFromAnywhere,
            description: "Complete an automated handover wherever you are standing, rather than "
                + "only when you are close enough that the game would let you talk to the "
                + "customer. Off by default, and that default is the game's own condition rather "
                + "than a distance Dealcraft picked: InteractionManager only ever offers an "
                + "interaction on the object it is hovering, and it stops hovering one beyond "
                + "4 metres. In multiplayer the contract list is shared and a handover takes from "
                + "your own inventory, so a host with this on completes every contract on that "
                + "list out of his own pockets and a guest walking to the customer finds the deal "
                + "gone. Read with AutoHandover: this says where, that says whether.");
        _mayUseAHigherGrade = category.CreateEntry("MayUseAHigherGrade", Defaults.MayUseAHigherGrade,
            description: "Let an automated handover go out in a better grade than the contract asked "
                + "for, when nothing of the grade ordered covers the order. Off by default: a jar of "
                + "Heavenly going out on a contract that asked for Poor is the owner's best product "
                + "spent at a Poor price. It says nothing about how much leaves the bag — packages "
                + "cannot be split, so a delivery may still overshoot in units either way.");

        // The entry name is written here as a literal rather than as
        // ChanceFloor.Key, because it is the preferences file's contract and the
        // test that holds the file against the catalogues reads it out of this
        // source.
        _acceptanceProbabilityThreshold = category.CreateEntry(
            "AcceptanceProbabilityThreshold", Defaults.AcceptanceProbabilityThreshold,
            description: "Never offer below this chance. A floor and not a target: the search "
                + "draws the curve at every confidence and keeps whichever is worth most - the "
                + "best confidence is different for every customer - and this only refuses that "
                + "winner when it is a bigger gamble than you wanted. Held here as the chance "
                + "itself, 0..1, because that is what the game's own figure is; the app shows "
                + "and takes the same number as whole percent, 50 to 100.");
        // Each feature's own, into the same category. A feature that has none
        // does nothing here.
        //
        // Not guarded, unlike starting and ticking a feature: a feature that
        // cannot declare its settings would go on to describe entries it does
        // not hold, and a mod that half-read its configuration is worse than one
        // that says it could not load.
        foreach (Feature feature in features)
        {
            feature.Declare(category);
        }

        // Flush so the file exists on disk and can actually be edited.
        category.SaveToFile(false);

        // Keys an older install left behind are not read, not rewritten and not
        // warned about. That is now most of what an old file holds — the two
        // tabs' settings, the backlog's two, the save guard, the deal-scheduling
        // switch, the price ceiling, the counter-gain floor, the probe budget,
        // the exclusion list and the overlay key — and a file carrying every one
        // of them loads exactly as a fresh one does.
    }

    /// <summary>
    /// Every setting the app's Automation section shows: the shared ones first,
    /// then each feature's own, in registration order.
    /// </summary>
    /// <remarks>
    /// This is where the spec's rule is kept whole — "there is no second store,
    /// no in-memory-only setting and no setting that exists in one place but not
    /// the other" — now that a feature may declare an entry without touching
    /// this class. Whatever <see cref="Load"/> created, this describes, because
    /// both walk the same list of features.
    /// </remarks>
    public IReadOnlyList<AutomationSetting> Describe(IReadOnlyList<Feature> features)
    {
        var rows = new List<AutomationSetting>(AutomationCatalog.Describe(ToAdvisorSettings()));

        foreach (Feature feature in features)
        {
            rows.AddRange(feature.Describe());
        }

        return rows;
    }

    /// <summary>
    /// Write one setting the app asked to change, and flush the category so the
    /// file on disk says it before the next frame.
    /// </summary>
    /// <param name="features">
    /// Every registered feature, in registration order, so a feature's own entry
    /// is written by the feature that declared it. The same list
    /// <see cref="Load"/> and <see cref="Describe"/> walk, which is what makes
    /// "one owner per setting" true rather than intended.
    /// </param>
    /// <returns>
    /// Whether anybody owned the key. False is a setting the app offered and
    /// nothing holds, which is a bug in the catalogues rather than something a
    /// player did — the caller says so in the log.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Flushed immediately, and that is the point rather than an optimisation
    /// left undone: the spec says a change "writes straight back and flushes to
    /// <c>UserData/MelonPreferences.cfg</c> immediately, so a crash cannot lose
    /// it". A game that dies an hour later must still come back up with the
    /// switch the player set.
    /// </para>
    /// <para>
    /// The other direction needs nothing from us. MelonLoader puts a
    /// <c>FileSystemWatcher</c> on the preferences file and reloads the
    /// categories when it changes — read out of <c>MelonLoader.dll</c>,
    /// <c>Preferences.IO.Watcher</c> — and everything here reads
    /// <c>entry.Value</c> afresh on every call, so a value edited by hand is
    /// picked up without a restart.
    /// </para>
    /// </remarks>
    public bool Change(IReadOnlyList<Feature> features, SettingChange change)
    {
        bool written = Preference.Set(
            change,
            _autoCounterOffer,
            _autoHandover,
            _handoverFromAnywhere,
            _mayUseAHigherGrade,
            _acceptanceProbabilityThreshold);

        if (!written)
        {
            foreach (Feature feature in features)
            {
                if (feature.Change(change))
                {
                    written = true;
                    break;
                }
            }
        }

        if (!written)
        {
            return false;
        }

        _category?.SaveToFile(false);
        return true;
    }

    /// <summary>
    /// Project the preferences onto the pure core's settings type. The one
    /// projection there is: the counter-offer rule decides on this object and the phone
    /// app's Automation section describes the same object, so the file stays the
    /// only store and the two cannot show different answers.
    /// </summary>
    public AdvisorSettings ToAdvisorSettings() => new()
    {
        AutoCounterOffer = _autoCounterOffer.Value,
        AutoHandover = _autoHandover.Value,
        HandoverFromAnywhere = _handoverFromAnywhere.Value,
        MayUseAHigherGrade = _mayUseAHigherGrade.Value,
        AcceptanceProbabilityThreshold = _acceptanceProbabilityThreshold.Value,
    };
}
