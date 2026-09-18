using System;
using System.Collections.Generic;
using Dealcraft.Core;
using MelonLoader;

namespace Dealcraft;

/// <summary>
/// One piece of Dealcraft that the mod entry point starts, ticks and asks about
/// its settings.
/// </summary>
/// <remarks>
/// <para>
/// This exists because of what integration kept costing. Every feature used to
/// register itself by editing the same three places — one constructor, one
/// <c>OnUpdate</c> and one settings class — so three waves running in parallel
/// produced three conflicts in the same two files, every time, whatever the
/// features actually did. A feature now says what it is in a file of its own and
/// appears in <see cref="Features.All"/>, which is a list rather than a
/// procedure: two features adding a line to a list is a conflict git resolves.
/// </para>
/// <para>
/// It is deliberately not a plugin system. There is no discovery, no manifest
/// and no load order beyond the order of that list. Everything here is either
/// something <see cref="DealcraftMod"/> used to do inline or something the spec
/// requires of every feature.
/// </para>
/// </remarks>
internal abstract class Feature
{
    /// <summary>
    /// What this feature's log lines are tagged with, and how it is named when
    /// something about its registration is reported. Lower case, one word.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Create this feature's own MelonPreferences entries.
    /// </summary>
    /// <param name="category">
    /// The mod's one category. A feature declares into it and never makes a
    /// category of its own: <see cref="ModSettings"/> owns the category, creates
    /// it, and flushes it once after every feature has declared.
    /// </param>
    /// <remarks>
    /// Whatever is created here must also be described by
    /// <see cref="Describe"/>. That is the spec's rule — "if it can be
    /// configured, it is in the file and it is in the app" — and
    /// <c>AutomationCatalogTests</c> checks it by reading this project's source,
    /// so an entry declared here and not described is a failing test rather than
    /// a setting a player cannot find.
    /// </remarks>
    public virtual void Declare(MelonPreferences_Category category)
    {
    }

    /// <summary>
    /// The Automation rows for exactly the entries <see cref="Declare"/>
    /// created, each keyed on its entry name, each reading the value the file
    /// currently holds.
    /// </summary>
    public virtual IReadOnlyList<AutomationSetting> Describe() =>
        Array.Empty<AutomationSetting>();

    /// <summary>
    /// Write a value the app asked for into one of this feature's own entries,
    /// if this feature is the one that owns it.
    /// </summary>
    /// <returns>
    /// Whether this feature owns the key and took the value. False means it
    /// belongs to somebody else, which is the ordinary answer for all but one
    /// feature.
    /// </returns>
    /// <remarks>
    /// The third side of the same triangle as <see cref="Declare"/> and
    /// <see cref="Describe"/>: a feature declares its entries, says what they
    /// currently hold, and takes them back when a player changes one. One owner
    /// per setting, so a feature never writes an entry another feature declared
    /// and there is no second store to disagree with the file.
    /// </remarks>
    public virtual bool Change(SettingChange change) => false;

    /// <summary>
    /// One line about this feature's own state, written when the mod loads, or
    /// nothing when the shared settings already say everything there is to say.
    /// Read after the preferences file, so it may report what the file holds.
    /// </summary>
    public virtual string Summary() => null;

    /// <summary>
    /// Build whatever this feature needs. Called once, after the preferences
    /// file has been read, and with every feature already registered — so a
    /// feature may look another one up here.
    /// </summary>
    public abstract void Start(FeatureContext context);

    /// <summary>
    /// A new scene has come up, so anything cached in the old one is gone.
    /// Features that hold nothing from the scene do not override this.
    /// </summary>
    public virtual void SceneChanged()
    {
    }

    /// <summary>
    /// One pass, on the game's update. Must not throw into it: the game's loop
    /// is not ours to break.
    /// </summary>
    public abstract void Tick();
}
