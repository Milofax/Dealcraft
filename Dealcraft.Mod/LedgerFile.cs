using System;
using System.IO;
using System.Text;
using MelonLoader.Utils;

namespace Dealcraft;

/// <summary>
/// A file of Dealcraft's own: where it is, and appending a line to it without
/// ever being able to break a handover.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two files, one path.</b> The handover ledger and the debug record differ
/// in their name and in the shape of their rows, and in nothing else — so they
/// are two instances of this rather than two writers. That is not tidiness: the
/// mod is held to <em>one</em> file that knows where anything is written, by
/// <c>SaveSafetyTests.No_adapter_can_write_into_the_games_save</c> and
/// <c>Only_that_file_knows_where_anything_is_written</c>, and a second writer
/// would be a second answer to "where does Dealcraft write" in a codebase whose
/// nearest neighbour is the save folder.
/// </para>
/// <para>
/// <b>Where.</b> <c>MelonEnvironment.UserDataDirectory</c> is MelonLoader's own
/// answer to "where does a mod put its files", so the path is the game's rather
/// than this machine's and nothing user-specific is written down. The ledger
/// lives in a <c>Dealcraft</c> folder beside the other mods' data, and it is
/// nowhere near the save folder: a save is the one thing in this game that
/// cannot be replaced, and a recorder that writes next to it is one bug away
/// from being the reason a save is gone.
/// </para>
/// <para>
/// <b>Never fatal.</b> Every call here is wrapped and returns a bool instead of
/// throwing. The caller is inside a Harmony patch on the game's handover path,
/// so an exception escaping this class would be an exception thrown into the
/// middle of a deal. A disk that is full is a reason to stop recording, never a
/// reason for the player to lose a handover.
/// </para>
/// <para>
/// <b>Once, then quiet.</b> After the first failure the file is closed for the
/// session: <see cref="Failed"/> goes true, and the caller says so once and
/// stops asking. Retrying on every handover would turn one broken thing into a
/// log full of the same line.
/// </para>
/// </remarks>
internal sealed class LedgerFile
{
    /// <summary>The folder inside <c>UserData</c>, so the mod's files sit together.</summary>
    public const string FolderName = "Dealcraft";

    public const string FileName = "handover-ledger.jsonl";

    /// <summary>
    /// The debug record, beside the ledger. Named for what is in it rather than
    /// for what it is for: the owner never opens it, and whoever does is looking
    /// for a decision.
    /// </summary>
    public const string DecisionsFileName = "decisions.jsonl";

    private readonly Func<string> _userDataDirectory;

    private readonly string _fileName;

    private string _path = string.Empty;

    public LedgerFile()
        : this(() => MelonEnvironment.UserDataDirectory, FileName)
    {
    }

    /// <param name="fileName">
    /// Which of the mod's files this is. The folder, the encoding and the
    /// never-fatal appending are the same for both.
    /// </param>
    public LedgerFile(string fileName)
        : this(() => MelonEnvironment.UserDataDirectory, fileName)
    {
    }

    /// <param name="userDataDirectory">
    /// Where MelonLoader keeps mod data. Injected so the path logic is not
    /// welded to a static, though in the game there is only ever one answer.
    /// </param>
    /// <param name="fileName">See the other constructor.</param>
    public LedgerFile(Func<string> userDataDirectory, string fileName = FileName)
    {
        _userDataDirectory = userDataDirectory;
        _fileName = fileName;
    }

    /// <summary>Whether writing has been given up on for this session.</summary>
    public bool Failed { get; private set; }

    /// <summary>Why it was given up on. Empty until it is.</summary>
    public string Failure { get; private set; } = string.Empty;

    /// <summary>The file being appended to, once one has been opened.</summary>
    public string Path => _path;

    /// <summary>
    /// Append one line. Returns false when the line did not get written, which
    /// is either because recording has already been given up on or because this
    /// is the attempt that gives up.
    /// </summary>
    public bool Append(string line)
    {
        if (Failed)
        {
            return false;
        }

        try
        {
            if (_path.Length == 0)
            {
                string folder = System.IO.Path.Combine(_userDataDirectory(), FolderName);
                Directory.CreateDirectory(folder);
                _path = System.IO.Path.Combine(folder, _fileName);
            }

            // Opened and closed per row rather than held open. A handover is a
            // rare event by any file's standards, and a decision that has
            // already been recorded is not written again at all, so neither
            // caller arrives often enough for a handle to be worth keeping —
            // and a handle kept across a session is a handle that survives the
            // crash these files are most likely to be recording around.
            File.AppendAllText(_path, line + "\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return true;
        }
        catch (Exception error)
        {
            Failed = true;
            Failure = error.Message;
            return false;
        }
    }
}
