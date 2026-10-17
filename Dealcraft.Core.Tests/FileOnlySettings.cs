using System.Collections.Generic;
using Dealcraft.Core;

namespace Dealcraft.Core.Tests;

/// <summary>
/// The settings that live in <c>MelonPreferences.cfg</c> and deliberately
/// nowhere else.
/// </summary>
/// <remarks>
/// <para>
/// The mod's standing rule is that every setting is in the file <em>and</em> in
/// the app — "there is no second store, no in-memory-only setting and no setting
/// that exists in one place but not the other". Several tests check it. This is
/// the list of entries that are exempt, and it exists so that the exemption is a
/// decision somebody wrote down rather than a guard somebody quietly stopped
/// running.
/// </para>
/// <para>
/// There is one, and it was asked for. The handover ledger is an instrument, not
/// a feature: the owner asked for the bonuses to be tracked so they could be
/// read afterwards, explicitly not surfaced again. A row in the app's Automation
/// section would make it a feature and put it in front of everyone who opens the
/// phone. So it is in the file, it is off there, and the app does not know it
/// exists.
/// </para>
/// <para>
/// The exemption is all-or-nothing on purpose, and
/// <see cref="AutomationCatalogTests.A_file_only_setting_is_absent_from_the_app_in_both_directions"/>
/// holds it to that: a setting the app could show but not change, or change but
/// not show, would be the half-wired state the original rule exists to prevent.
/// </para>
/// </remarks>
internal static class FileOnlySettings
{
    /// <summary>
    /// The preferences entry names that no catalogue describes.
    /// </summary>
    /// <remarks>
    /// Read off <see cref="AutomationForm.FileOnly"/> rather than written here,
    /// because that property's documented job is to be this list and it used to
    /// be empty while this one was not. A reader of the product code was told
    /// there was nothing to look for; two lists is how that happened, so there
    /// is one.
    /// </remarks>
    public static IReadOnlyList<string> Keys => AutomationForm.FileOnly;

    /// <summary>
    /// The adapter files that own them. Named because several guards are about
    /// what a file does rather than about a key, and each needs to know which
    /// file is allowed to be different.
    /// </summary>
    public static readonly string[] Files = { "HandoverLedgerFeature.cs" };
}
