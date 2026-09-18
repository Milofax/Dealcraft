using System;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.UI.Phone.ProductManagerApp;
using UnityEngine;

namespace Dealcraft.Phone;

/// <summary>
/// Makes the app type's singleton point at Dealcraft's copy for exactly the
/// length of that copy's own <c>Start</c>, and at the vanilla Product Manager
/// everywhere else.
/// </summary>
/// <remarks>
/// <para>
/// <b>What this is for.</b> The copy's <c>PlayerSingleton.Awake</c> claims the
/// singleton and <see cref="PhoneApp"/> puts the vanilla app back on the next
/// line, so by the time the copy's <c>Start</c> runs — a frame later — the
/// pointer is the vanilla app's again. Anything in that <c>Start</c> that
/// reached for the singleton instead of <c>this</c> would therefore write into
/// the vanilla Product Manager, and this makes the two the same object for the
/// length of that one call.
/// </para>
/// <para>
/// <b>It was built against a duplication that turned out not to be one.</b> The
/// warning that prompted it fired on the vanilla app's rows having grown, and
/// the vanilla app grows on its own: its own <c>Start</c> subscribes
/// <c>CreateEntry</c> to <c>ProductManager.onProductDiscovered</c>
/// (<c>0x1809ec339</c>–<c>0x1809ec3df</c>, the field at <c>+0x120</c>), and the
/// save's loader fires that event once per product as it loads. See
/// <see cref="Dealcraft.Core.ProductListCheck"/>, which counts the rows against
/// the game's own list rather than against what the list used to hold, and
/// ticket 50.
/// </para>
/// <para>
/// <b>So the row count is the belt and this is the braces, and both stay.</b> Two careful
/// readings of <c>ProductManagerApp::Start</c> (RVA <c>0x9EC220</c>) — see
/// <c>docs/native-truth.md</c> §3, and a third this session — all found it
/// keeping <c>this</c> in <c>%rsi</c> and writing nothing through a singleton,
/// and nothing has now been seen to contradict them. Aiming the pointer at the
/// copy costs one write and makes the two readings agree whichever is right: a
/// write through <c>this</c> and a write through the singleton land in the same
/// place, and that place is the copy, whose vanilla rows the app clears anyway.
/// What it cannot do is cover the coroutines and events that <c>Start</c> sets
/// in motion, which is why the row count is the guard and this is not.
/// </para>
/// <para>
/// <b>Wrapped, not skipped.</b> <c>ProductManagerApp.Start</c> is
/// <c>Public, Virtual, HideBySig</c> with no <c>NewSlot</c> — an override of
/// <c>App&lt;T&gt;.Start</c>, read out of the interop metadata. A prefix that
/// returned false would therefore also skip whatever the base does for the copy,
/// and nothing in this project knows what that is. Nothing is skipped here: the
/// same code runs, and only the pointer it may reach for is different.
/// </para>
/// <para>
/// <b>If the original throws</b> the postfix does not run, and the pointer would
/// be left at the copy. <see cref="PhoneApp"/> writes the vanilla app back into
/// it two frames after the copy is made, unconditionally, which is the net under
/// that case — it was already there, said twice on purpose, for the frame
/// boundary.
/// </para>
/// </remarks>
internal static class ClonedAppStart
{
    /// <summary>A frame that never happened.</summary>
    private const int NoFrame = -1;

    /// <summary>
    /// The copy whose <c>Start</c> has not run yet, and the app the pointer
    /// belongs to. Static because a Harmony patch method must be static; only
    /// ever written by <see cref="Expect"/> and <see cref="Forget"/>, which the
    /// one <see cref="PhoneApp"/> in the process calls from the game's own
    /// thread.
    /// </summary>
    private static ProductManagerApp _clone;

    private static ProductManagerApp _vanilla;

    private static Action<string> _log = _ => { };

    private static Action<string> _warn = _ => { };

    /// <summary>
    /// Whether the guard is actually in place. False means the patch could not
    /// be installed, which the caller reports: the copy is still made, and the
    /// duplication check in <see cref="PhoneApp"/> is then the only thing
    /// standing between that and the owner's screen.
    /// </summary>
    public static bool Installed { get; private set; }

    /// <summary>
    /// The frame the pointer was aimed at the copy on, or -1 if that never
    /// happened for the copy now being watched.
    /// </summary>
    /// <remarks>
    /// Kept so the duplication check can say which stretch of the copy's
    /// start-up was covered and which was not. Ticket 23 guarded the window it
    /// could measure; this is the wrap saying, in a running game, exactly which
    /// window that was.
    /// </remarks>
    public static int AimedOnFrame { get; private set; } = NoFrame;

    /// <summary>
    /// The frame the vanilla app was put back on, or -1 if the wrap is still in
    /// place or never took hold.
    /// </summary>
    public static int LiftedOnFrame { get; private set; } = NoFrame;

    /// <summary>
    /// Install the wrap. Called once, when the phone feature starts, so it is in
    /// place long before any scene holds an app to copy.
    /// </summary>
    /// <returns>Null when it went on; otherwise what went wrong.</returns>
    public static string Install(HarmonyLib.Harmony harmony, Action<string> log, Action<string> warn)
    {
        if (harmony is null)
        {
            return "this mod has no Harmony instance, so the copy's Start cannot be wrapped";
        }

        try
        {
            MethodInfo start = AccessTools.Method(typeof(ProductManagerApp), "Start");
            if (start is null)
            {
                return "ProductManagerApp.Start was not found in the interop assemblies";
            }

            harmony.Patch(
                start,
                prefix: new HarmonyMethod(typeof(ClonedAppStart), nameof(Before)),
                postfix: new HarmonyMethod(typeof(ClonedAppStart), nameof(After)));

            _log = log ?? (_ => { });
            _warn = warn ?? (_ => { });
            Installed = true;
            return null;
        }
        catch (Exception error)
        {
            return error.Message;
        }
    }

    /// <summary>
    /// A copy has just been made and its <c>Start</c> is still to come. Called
    /// with the copy and with the app the singleton pointer belongs to.
    /// </summary>
    public static void Expect(ProductManagerApp clone, ProductManagerApp vanilla)
    {
        _clone = clone;
        _vanilla = vanilla;
        AimedOnFrame = NoFrame;
        LiftedOnFrame = NoFrame;
    }

    /// <summary>
    /// Stop watching. Called once the copy's first frames are behind it, so the
    /// patch is a no-op on every instance from then on, and on a new scene, so a
    /// copy that died with its scene is not still being waited for.
    /// </summary>
    /// <remarks>
    /// The two frame numbers are left where they are. They are a record of what
    /// happened to this copy rather than state to watch, and the check that
    /// reports them runs after this.
    /// </remarks>
    public static void Forget()
    {
        _clone = null;
        _vanilla = null;
    }

    /// <summary>
    /// What this wrap covered, in the words the duplication check needs: when
    /// the pointer was aimed at the copy, when it was put back, and when the
    /// rows were counted against all that.
    /// </summary>
    /// <param name="countedOnFrame">
    /// The frame the counts were taken on, or -1 if that is not known.
    /// </param>
    public static string Window(int countedOnFrame)
    {
        string counted = countedOnFrame >= 0
            ? $", and the rows were counted on frame {Frame(countedOnFrame)}"
            : string.Empty;

        if (!Installed)
        {
            return "the wrap around the copy's own Start was never installed, which is the first "
                + $"thing to fix{counted}";
        }

        if (AimedOnFrame < 0)
        {
            return "the wrap was installed but never took hold: the copy's own Start was not seen, "
                + $"so nothing it did was covered{counted}";
        }

        if (LiftedOnFrame < 0)
        {
            return $"the app singleton was aimed at the copy on frame {Frame(AimedOnFrame)} and has "
                + $"not been put back, which means the copy's Start did not return{counted}";
        }

        return $"the copy's own Start ran with the app singleton aimed at itself from frame "
            + $"{Frame(AimedOnFrame)} to frame {Frame(LiftedOnFrame)}{counted}, so whatever did "
            + "this ran outside that";
    }

    private static string Frame(int frame) => frame.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Which frame the game is on, or -1 where that cannot be read. Only ever
    /// called from the game's own thread, which is the only place
    /// <c>Time.frameCount</c> answers.
    /// </summary>
    public static int ThisFrame()
    {
        try
        {
            return Time.frameCount;
        }
        catch (Exception)
        {
            return NoFrame;
        }
    }

    /// <summary>
    /// The copy is about to start: aim the pointer at it.
    /// </summary>
    /// <remarks>
    /// Every other instance — the vanilla app's own <c>Start</c> at the moment
    /// the phone comes up, and any app in a scene this mod never touched — goes
    /// past untouched, which is what makes a patch on a shared method safe to
    /// leave installed.
    /// </remarks>
    public static void Before(ProductManagerApp __instance, out bool __state)
    {
        __state = false;

        try
        {
            if (_clone == null || __instance == null || __instance.Pointer != _clone.Pointer)
            {
                return;
            }

            // Only where there is something to put back. Aiming the pointer at
            // the copy with nothing to restore afterwards would leave the game
            // without a Product Manager, which is worse than the duplication
            // this guards against.
            if (_vanilla == null)
            {
                _warn("the copy is about to start but the app the singleton belongs to is gone, so "
                    + "the pointer is left where it is");
                return;
            }

            PlayerSingleton<ProductManagerApp>.instance = _clone;
            AimedOnFrame = ThisFrame();
            __state = true;
        }
        catch (Exception error)
        {
            // Into the game's own start-up, so never thrown on. A guard that
            // could not be applied leaves the duplication check to report it.
            __state = false;
            _warn($"could not aim the app singleton at Dealcraft's copy for its own start-up "
                + $"({error.Message}); anything that copy writes through the singleton will reach "
                + "the vanilla Product Manager");
        }
    }

    /// <summary>
    /// The copy has started: put the vanilla app back, and stop watching. One
    /// copy starts once, so the wrap has nothing left to do afterwards.
    /// </summary>
    public static void After(bool __state)
    {
        if (!__state)
        {
            return;
        }

        try
        {
            ProductManagerApp vanilla = _vanilla;
            _clone = null;
            _vanilla = null;

            PlayerSingleton<ProductManagerApp>.instance = vanilla;
            LiftedOnFrame = ThisFrame();
            _log("Dealcraft's copy ran its own Start with the app singleton aimed at itself, so "
                + $"nothing it wrote could reach the vanilla Product Manager; that covered frame "
                + $"{Frame(AimedOnFrame)} to frame {Frame(LiftedOnFrame)} and nothing after it");
        }
        catch (Exception error)
        {
            _warn($"could not put the vanilla Product Manager back in the app singleton after the "
                + $"copy started: {error.Message}");
        }
    }
}
