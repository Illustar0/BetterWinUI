using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace BetterWinUI.PageActivation.Generators.Internal;

/// <summary>Stores a source location as plain values so it can be cached in the incremental pipeline.</summary>
/// <remarks>
/// A Roslyn <see cref="Location" /> holds its syntax tree, which would keep old compilations alive and defeat
/// incremental caching.
/// </remarks>
internal readonly struct DiagnosticLocation : IEquatable<DiagnosticLocation>
{
    public DiagnosticLocation(string path, TextSpan span, LinePositionSpan lineSpan)
    {
        Path = path;
        Span = span;
        LineSpan = lineSpan;
    }

    public string Path { get; }

    public TextSpan Span { get; }

    public LinePositionSpan LineSpan { get; }

    public static DiagnosticLocation Create(Location location)
    {
        var lineSpan = location.GetLineSpan();
        return new DiagnosticLocation(
            lineSpan.Path,
            location.SourceSpan,
            lineSpan.Span);
    }

    public Location ToLocation()
    {
        return Location.Create(Path, Span, LineSpan);
    }

    /// <inheritdoc />
    public bool Equals(DiagnosticLocation other)
    {
        return string.Equals(Path, other.Path, StringComparison.Ordinal) &&
               Span.Equals(other.Span) &&
               LineSpan.Equals(other.LineSpan);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is DiagnosticLocation other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = StringComparer.Ordinal.GetHashCode(Path);
            hashCode = (hashCode * HashCodeValues.Multiplier) ^ Span.GetHashCode();
            return (hashCode * HashCodeValues.Multiplier) ^ LineSpan.GetHashCode();
        }
    }
}
