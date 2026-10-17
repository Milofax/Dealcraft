using System;
using System.Collections;
using Dealcraft.Core;
using UnityEngine;

namespace Dealcraft.Phone;

/// <summary>
/// Waiting for a piece of the phone to be there and be usable. A scene comes up
/// before its UI does, and the objects of a scene come up before the pointers
/// between them, so a look that finds nothing — or finds something half-built —
/// is looked again. A scene without a phone at all, the main menu, never
/// produces one, so the wait is bounded and says so when it runs out.
/// </summary>
/// <remarks>
/// The rule itself is <see cref="InstallWait"/> in Dealcraft.Core, where it can
/// be tested. This is only the frames: how often to look, and how to stop.
/// </remarks>
internal static class ScenePoll
{
    /// <summary>
    /// How often to look. Searching a whole scene is not something to do every
    /// frame while a menu sits there having no phone at all.
    /// </summary>
    private const float ProbeSeconds = 0.25f;

    /// <summary>
    /// Keep calling <paramref name="look"/> until it settles the matter or the
    /// wait runs out. <paramref name="stillWanted"/> is checked each time round
    /// so a later scene supersedes a wait that will never end, and
    /// <paramref name="gaveUp"/> is called at most once, with which of the two
    /// ways the wait ran out.
    /// </summary>
    public static IEnumerator Until(
        Func<SceneLook> look,
        Func<bool> stillWanted,
        Action<InstallVerdict> gaveUp)
    {
        var wait = new InstallWait(InstallWait.DefaultPatienceSeconds);

        float nextProbe = 0f;

        while (stillWanted())
        {
            if (Time.realtimeSinceStartup >= nextProbe)
            {
                nextProbe = Time.realtimeSinceStartup + ProbeSeconds;

                InstallVerdict verdict = wait.Look(look(), Time.realtimeSinceStartup);
                if (verdict == InstallVerdict.Done)
                {
                    yield break;
                }

                if (verdict != InstallVerdict.LookAgain)
                {
                    gaveUp(verdict);
                    yield break;
                }
            }

            yield return null;
        }
    }
}
