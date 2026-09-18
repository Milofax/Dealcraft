namespace System.Runtime.CompilerServices;

/// <summary>
/// The marker the compiler needs for <c>init</c> accessors and records.
/// .NET ships it from .NET 5 on; this assembly targets netstandard2.1, which
/// does not, so it is declared here. Nothing references it by hand.
/// </summary>
internal static class IsExternalInit
{
}
