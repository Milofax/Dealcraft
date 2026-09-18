using System.Collections.Generic;

namespace Dealcraft.Core;

/// <summary>
/// What a guest's page says where the host's three blocks would be.
/// </summary>
/// <remarks>
/// <para>
/// <b>The second sentence is the one that matters.</b> Without it a guest reads
/// a page with one block on it and concludes he was given a quarter of the mod,
/// when the other three are working for his deals on somebody else's machine.
/// </para>
/// <para>
/// <b>The host's own settings are not in it, and that was decided against rather
/// than forgotten.</b> Showing them means the host sending them, which means a
/// network message a vanilla client does not have — <c>CLAUDE.md</c>'s first
/// rule, and the one that keeps an unmodified friend able to join at all.
/// Inferring them from what a guest can observe is guessing, and a guessed
/// setting on screen is worse than an absent one. Three options were put to the
/// owner with that cost stated; he chose the note.
/// </para>
/// <para>
/// <b>A list of lines rather than one string</b>, because every row of this page
/// lives inside a column the game reports as 63 characters and a test holds each
/// of them to it. The sentence is longer than that, so it arrives already broken
/// where it reads best rather than wherever a wrap happens to fall.
/// </para>
/// </remarks>
public static class HostOnlyNote
{
    /// <summary>What the note's rows are keyed on.</summary>
    public const string Key = "host-only";

    /// <summary>
    /// The note, one row per line. Read <see cref="Rows"/> for what the page
    /// makes of them.
    /// </summary>
    public static IReadOnlyList<string> Lines { get; } = new[]
    {
        "Listed price, price negotiation and accepted schedule can",
        "only be set on the host. They apply to your deals too.",
    };

    /// <summary>The whole sentence, for a reader rather than for a row.</summary>
    public static string Sentence => string.Join(" ", Lines);

    /// <summary>The note as the page carries it: one notice per line, unindented.</summary>
    public static IReadOnlyList<FormRow> Rows()
    {
        var rows = new List<FormRow>(Lines.Count);

        for (int line = 0; line < Lines.Count; line++)
        {
            rows.Add(new FormRow($"{Key}:{line}", FormRowKind.Notice, Lines[line]));
        }

        return rows;
    }
}
