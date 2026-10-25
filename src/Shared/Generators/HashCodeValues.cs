namespace BetterWinUI.PageActivation.Generators.Internal;

internal static class HashCodeValues
{
    // System.HashCode is unavailable in netstandard2.0, so value models combine fields with this odd prime.
    public const int Multiplier = 397;
}
