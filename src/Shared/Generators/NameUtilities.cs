using System.Globalization;

namespace BetterWinUI.PageActivation.Generators.Internal;

internal static class NameUtilities
{
    private const uint Fnv1A32OffsetBasis = 2166136261u;

    private const uint Fnv1A32Prime = 16777619u;

    /// <summary>Creates a hexadecimal identifier suffix that is stable across builds and unique per name.</summary>
    /// <remarks>FNV-1a is used because <see cref="string.GetHashCode()" /> is randomized per process.</remarks>
    public static string CreateSuffix(string value)
    {
        unchecked
        {
            var hash = Fnv1A32OffsetBasis;
            for (var index = 0; index < value.Length; index++) hash = (hash ^ value[index]) * Fnv1A32Prime;

            return hash.ToString("X8", CultureInfo.InvariantCulture);
        }
    }
}
