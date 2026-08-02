using System.Globalization;

namespace BetterWinUI.DependencyInjection.PageActivation.Generator;

/// <summary>
/// Creates deterministic, source-safe names for generated declarations.
/// </summary>
internal static class NameUtilities
{
    /// <summary>The 32-bit FNV-1a offset basis.</summary>
    private const uint Fnv1A32OffsetBasis = 2166136261u;

    /// <summary>The 32-bit FNV-1a prime.</summary>
    private const uint Fnv1A32Prime = 16777619u;

    /// <summary>
    /// Creates a deterministic hexadecimal suffix for a fully qualified name.
    /// </summary>
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