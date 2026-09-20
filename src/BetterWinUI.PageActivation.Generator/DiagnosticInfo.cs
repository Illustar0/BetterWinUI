namespace BetterWinUI.PageActivation.Generator;

/// <summary>Identifies diagnostics without retaining Roslyn symbols.</summary>
internal enum DiagnosticKind : byte
{
    /// <summary>Identifies the AppMustBePartial diagnostic.</summary>
    AppMustBePartial,
    /// <summary>Identifies the InvalidApp diagnostic.</summary>
    InvalidApp,
    /// <summary>Identifies the MultipleApps diagnostic.</summary>
    MultipleApps,
    /// <summary>Identifies the MissingXamlProvider diagnostic.</summary>
    MissingXamlProvider,
    /// <summary>Identifies the InitializationMemberConflict diagnostic.</summary>
    InitializationMemberConflict,
    /// <summary>Identifies the UnsupportedXamlContract diagnostic.</summary>
    UnsupportedXamlContract,
}

/// <summary>
/// Represents an incremental, value-equatable diagnostic payload.
/// </summary>
internal readonly struct DiagnosticInfo : IEquatable<DiagnosticInfo>
{
    /// <summary>
    /// Initializes a diagnostic payload.
    /// </summary>
    /// <param name="kind">The diagnostic kind.</param>
    /// <param name="location">The diagnostic source location.</param>
    /// <param name="argument">The optional message argument.</param>
    public DiagnosticInfo(DiagnosticKind kind, DiagnosticLocation location, string? argument = null)
    {
        Kind = kind;
        Location = location;
        Argument = argument;
    }

    /// <summary>
    /// Gets the diagnostic kind.
    /// </summary>
    public DiagnosticKind Kind { get; }

    /// <summary>
    /// Gets the diagnostic source location.
    /// </summary>
    public DiagnosticLocation Location { get; }

    /// <summary>
    /// Gets the optional message argument.
    /// </summary>
    public string? Argument { get; }

    /// <inheritdoc />
    public bool Equals(DiagnosticInfo other)
    {
        return Kind == other.Kind &&
               Location.Equals(other.Location) &&
               string.Equals(Argument, other.Argument, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is DiagnosticInfo other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = ((int)Kind * HashCodeValues.Multiplier) ^ Location.GetHashCode();
            return (hashCode * HashCodeValues.Multiplier) ^
                   (Argument is null ? 0 : StringComparer.Ordinal.GetHashCode(Argument));
        }
    }
}