using System.Globalization;

namespace BetterWinUI.Navigation.Generator;

/// <summary>
/// Produces deterministic source identifiers from assembly names.
/// </summary>
internal static class NameUtilities
{
    /// <summary>Combines a readable identifier with a deterministic FNV-1a suffix.</summary>
    internal static string CreateSuffix(string value)
    {
        var identifier = new string(value.Select(static character => char.IsLetterOrDigit(character) ? character : '_').ToArray());
        var hash = value.Aggregate(2166136261u, static (current, character) => unchecked((current ^ character) * 16777619u));
        return identifier + "_" + hash.ToString("X8", CultureInfo.InvariantCulture);
    }
}
