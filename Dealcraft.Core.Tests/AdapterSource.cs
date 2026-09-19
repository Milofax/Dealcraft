using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Dealcraft.Core.Tests;

/// <summary>
/// Reads the Il2Cpp adapter's own source.
///
/// Two rules the project holds cannot be checked any other way. "The probe is
/// GetOfferSuccessChance, never EvaluateCounteroffer" and "every setting is in
/// the file and in the app" are both about code that calls into the running
/// game, and no test here may reference an Il2Cpp type — if a test needs one,
/// the spec says, the seam is in the wrong place. The seam is in the right
/// place; the rule is simply about the far side of it. So the far side is read
/// as text.
/// </summary>
internal static class AdapterSource
{
    /// <summary>The <c>Dealcraft.Mod</c> project directory.</summary>
    public static string Directory => Find();

    public static string Read(string fileName) =>
        File.ReadAllText(Path.Combine(Find(), fileName));

    /// <summary>
    /// Every source file of the adapter, in a fixed order, without the generated
    /// copies under <c>bin</c> and <c>obj</c> — read twice, those would double
    /// whatever a guard counts.
    /// </summary>
    /// <remarks>
    /// The guards read the adapter whole rather than one named file because the
    /// rules they check are about the adapter everywhere: a feature declares its
    /// own settings in its own file, and any file at all could reach for the
    /// probe that rolls dice.
    /// </remarks>
    public static IEnumerable<string> Files() => System.IO.Directory
        .EnumerateFiles(Find(), "*.cs", SearchOption.AllDirectories)
        .Where(path => !IsBuildOutput(path))
        .OrderBy(path => path, StringComparer.Ordinal);

    /// <summary>Every source file of the adapter, joined.</summary>
    public static string ReadAll()
    {
        var joined = new StringBuilder();

        foreach (string file in Files())
        {
            joined.AppendLine(File.ReadAllText(file));
        }

        return joined.ToString();
    }

    private static bool IsBuildOutput(string path) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(part => part is "bin" or "obj");

    /// <summary>
    /// Found by walking up from the test assembly rather than by a path written
    /// down, which would go stale the first time either project moved.
    /// </summary>
    private static string Find()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "Dealcraft.Mod");
            if (System.IO.Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Dealcraft.Mod was not found above {AppContext.BaseDirectory}. The guards that read it only "
            + "mean anything when they are run against the checkout.");
    }
}
