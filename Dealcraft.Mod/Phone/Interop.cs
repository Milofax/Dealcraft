using UnityEngine;

namespace Dealcraft.Phone;

/// <summary>
/// Stand-ins for interop calls that compile against the Il2Cpp assemblies and
/// then cannot run.
/// </summary>
/// <remarks>
/// <para>
/// Il2CppInterop generates a managed body for every game method. Some of those
/// bodies are unverifiable: they forward the caller's own type parameter into a
/// helper that constrains it, which the caller does not. The C# compiler never
/// sees that — it only sees the generated signature, whose <c>T</c> is
/// unconstrained — so the call compiles, and the CLR throws
/// <c>System.Security.VerificationException</c> the first time the method is
/// JITted, for every <c>T</c>.
/// </para>
/// <para>
/// <c>UnityEngine.Component.GetComponentInParent&lt;T&gt;(bool)</c> is one of
/// them: its <c>T</c> is declared unconstrained and its body calls
/// <c>Il2CppObjectBase.Cast&lt;T&gt;()</c>, which requires
/// <c>T : Il2CppObjectBase</c>. <c>tools/interop-verify</c> finds these by
/// reading the assemblies rather than by running into them; run it after
/// building, because a compile is not evidence.
/// </para>
/// </remarks>
internal static class Interop
{
    /// <summary>
    /// The nearest <typeparamref name="T"/> on <paramref name="start"/> or above
    /// it, inactive objects included.
    /// </summary>
    /// <remarks>
    /// What <c>GetComponentInParent&lt;T&gt;(true)</c> means, written out of
    /// parts that run: <c>Transform.parent</c> is an ordinary property and
    /// <c>GetComponent&lt;T&gt;()</c> is generated over
    /// <c>IL2CPP.PointerToValueGeneric&lt;T&gt;</c>, which constrains nothing.
    /// Walking the parent chain by hand also makes the inactive case moot —
    /// there is no <c>includeInactive</c> to pass, because a parent is visited
    /// whether it is switched on or not.
    /// </remarks>
    public static T FindInParents<T>(Transform start)
        where T : Component
    {
        for (Transform node = start; node != null; node = node.parent)
        {
            T found = node.GetComponent<T>();
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    /// <inheritdoc cref="FindInParents{T}(Transform)"/>
    public static T FindInParents<T>(GameObject start)
        where T : Component =>
        start != null ? FindInParents<T>(start.transform) : null;

    /// <summary>
    /// The first <typeparamref name="T"/> on <paramref name="root"/> or under
    /// it, inactive objects included, or null.
    /// </summary>
    /// <remarks>
    /// The donor panel hangs its stepper's buttons in a wrapper on some builds
    /// and on the object itself on others, so a copy has to be asked rather than
    /// assumed. <c>GetComponentsInChildren&lt;T&gt;</c> is generated over
    /// <c>IL2CPP.PointerToValueGeneric&lt;T&gt;</c> and constrains nothing, so
    /// unlike <c>GetComponentInParent&lt;T&gt;</c> it runs — see the class
    /// remarks, and <c>tools/interop-verify</c>, which checks rather than trusts.
    /// </remarks>
    public static T FindHere<T>(GameObject root)
        where T : Component
    {
        if (root == null)
        {
            return null;
        }

        foreach (T found in root.GetComponentsInChildren<T>(true))
        {
            return found;
        }

        return null;
    }
}
