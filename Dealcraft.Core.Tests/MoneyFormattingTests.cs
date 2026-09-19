using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// One helper per job, checked rather than agreed.
///
/// The game's own <c>MoneyManager.FormatAmount</c> is what keeps every figure
/// the mod shows in the game's currency rather than one the mod picked. It has
/// exactly one caller — <c>GameMoney</c> — and that is the whole of the rule:
/// a second caller is a second answer about decimals, colour markup and what to
/// do when no save is loaded, and those drift. <c>AutomaticHandover</c> grew one
/// and it went unnoticed through six waves of work, which is why this is a test
/// now and not a note.
///
/// Checked as text for the reason <see cref="AdapterSource"/> gives: the call is
/// on the far side of the seam, where no test here may follow it.
/// </summary>
public class MoneyFormattingTests
{
    [Fact]
    public void Only_one_file_spells_money_the_way_the_game_does()
    {
        var callers = new List<string>();

        foreach (string file in AdapterSource.Files())
        {
            int line = 0;

            foreach (string text in File.ReadLines(file))
            {
                line++;

                if (IsComment(text) || !text.Contains("FormatAmount", StringComparison.Ordinal))
                {
                    continue;
                }

                callers.Add($"{Path.GetFileName(file)}:{line}");
            }
        }

        string only = Assert.Single(callers);
        Assert.StartsWith("GameMoney.cs:", only, StringComparison.Ordinal);
    }

    /// <summary>
    /// Doc comments are where the method's name belongs — the remarks on
    /// <c>GameMoney</c> say why it is the only caller — so they do not count.
    /// </summary>
    private static bool IsComment(string text)
    {
        string trimmed = text.TrimStart();

        return trimmed.StartsWith("//", StringComparison.Ordinal)
            || trimmed.StartsWith("/*", StringComparison.Ordinal)
            || trimmed.StartsWith("*", StringComparison.Ordinal);
    }
}
