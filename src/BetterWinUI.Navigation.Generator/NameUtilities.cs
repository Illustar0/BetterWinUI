using System.Text;

namespace BetterWinUI.Navigation.Generator;

/// <summary>
/// Produces deterministic source identifiers from assembly names.
/// </summary>
internal static class NameUtilities
{
    internal static string CreateSuffix(string value)
    {
        var identifier = new StringBuilder(value.Length + 9);
        foreach (var character in value) identifier.Append(char.IsLetterOrDigit(character) ? character : '_');

        var hash = 2166136261;
        foreach (var character in value) hash = (hash ^ character) * 16777619;

        return identifier.Append('_').Append(hash.ToString("X8")).ToString();
    }
}