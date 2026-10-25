using System.Globalization;

namespace BetterWinUI.Navigation.Generator;

internal static class NameUtilities
{
    /// <summary>Turns a name into an identifier suffix that is stable across builds and unique per name.</summary>
    /// <remarks>
    /// Replacing invalid characters can map different names to the same identifier, so a hash of the original
    /// name is appended. FNV-1a is used because <see cref="string.GetHashCode()" /> is randomized per process.
    /// </remarks>
    internal static string CreateSuffix(string value)
    {
        var identifier = new string(value.Select(static character => char.IsLetterOrDigit(character) ? character : '_').ToArray());
        var hash = value.Aggregate(2166136261u, static (current, character) => unchecked((current ^ character) * 16777619u));
        return identifier + "_" + hash.ToString("X8", CultureInfo.InvariantCulture);
    }
}
