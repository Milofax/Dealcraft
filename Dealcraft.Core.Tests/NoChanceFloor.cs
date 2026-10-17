namespace Dealcraft.Core.Tests;

/// <summary>
/// The chance floor a test uses when it is not about the chance floor.
/// </summary>
/// <remarks>
/// Zero, which admits every rung, so a test written before the floor entered
/// the search still asks the question it was written to ask. It is a named
/// constant rather than a bare <c>0f</c> at forty call sites so that the ones
/// which are genuinely indifferent to the floor can be told apart from any that
/// should have been given a real one.
/// </remarks>
public static class Floors
{
    public const float NoChanceFloor = 0f;
}
