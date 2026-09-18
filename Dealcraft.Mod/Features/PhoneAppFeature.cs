using System.Collections.Generic;
using Dealcraft.Core;
using Dealcraft.Phone;

namespace Dealcraft;

/// <summary>
/// Dealcraft's own entry on the phone: one page carrying every setting the mod
/// holds.
/// </summary>
internal sealed class PhoneAppFeature : Feature
{
    private PhoneApp _phone;
    private FeatureContext _context;

    public override string Name => "phone";

    public override void Start(FeatureContext context)
    {
        _context = context;

        // Before any scene holds an app to copy. The copy's own Start would
        // otherwise run with the app singleton pointing at the vanilla Product
        // Manager, which is how the owner's first live run ended up with the
        // vanilla product list drawn twice.
        string guard = ClonedAppStart.Install(
            context.Harmony, context.Log(Name), context.Warn(Name));

        if (guard is not null)
        {
            context.Warn(Name)(
                $"the copy's own Start cannot be wrapped ({guard}), so anything it writes through "
                + "the app singleton would reach the vanilla Product Manager; the app still reports "
                + "it if that happens");
        }

        _phone = new PhoneApp(
            context.Automation,

            // The reader the counteroffer, the scheduler and the lifecycle watch
            // already ask before they act. The page is given the same answer
            // rather than one worked out for it: three of its four blocks act on
            // state the session shares and are the host's, and a guest setting
            // them changed nothing on his machine.
            ServerAuthorityReader.Read,
            Write,
            context.Log(Name),
            context.Warn(Name));
    }

    /// <summary>
    /// The phone belongs to the scene, so the app is added once per scene and
    /// dies with it. A scene without a phone — the main menu — is left alone.
    /// </summary>
    public override void SceneChanged() => _phone?.InstallIntoCurrentScene();

    public override void Tick() => _phone?.Tick();

    /// <summary>
    /// A player changed a setting in the app. It goes to the feature that owns
    /// the entry and the category is flushed there and then; the app has no
    /// copy of its own to update, because it re-reads the file afterwards.
    /// </summary>
    /// <returns>
    /// Whether it was written. False means the app offered a setting nothing
    /// holds, which the app reports rather than swallowing: the alternative is a
    /// row that looks as though it changed and did not.
    /// </returns>
    private bool Write(SettingChange change) => _context.Change(change);
}
