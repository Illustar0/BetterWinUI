using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace BetterWinUI.DependencyInjection.PageActivation.Generator;

/// <summary>
/// Stores a diagnostic location without retaining a syntax tree in the incremental pipeline.
/// </summary>
internal readonly struct DiagnosticLocation : IEquatable<DiagnosticLocation>
{
    /// <summary>
    /// Initializes a source location.
    /// </summary>
    /// <param name="path">The source file path.</param>
    /// <param name="span">The source text span.</param>
    /// <param name="lineSpan">The source line span.</param>
    public DiagnosticLocation(string path, TextSpan span, LinePositionSpan lineSpan)
    {
        Path = path;
        Span = span;
        LineSpan = lineSpan;
    }

    /// <summary>
    /// Gets the source file path.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the source text span.
    /// </summary>
    public TextSpan Span { get; }

    /// <summary>
    /// Gets the source line span.
    /// </summary>
    public LinePositionSpan LineSpan { get; }

    /// <summary>
    /// Creates a value location from a Roslyn location.
    /// </summary>
    /// <param name="location">The Roslyn location.</param>
    /// <returns>The value location.</returns>
    public static DiagnosticLocation Create(Location location)
    {
        var lineSpan = location.GetLineSpan();
        return new DiagnosticLocation(
            lineSpan.Path ?? string.Empty,
            location.SourceSpan,
            lineSpan.Span);
    }

    /// <summary>
    /// Recreates a Roslyn location for diagnostic reporting.
    /// </summary>
    /// <returns>The recreated Roslyn location.</returns>
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