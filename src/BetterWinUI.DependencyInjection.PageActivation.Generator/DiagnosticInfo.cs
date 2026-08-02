namespace BetterWinUI.DependencyInjection.PageActivation.Generator;

/// <summary>
/// Identifies generator diagnostics without retaining Roslyn symbols.
/// </summary>
internal enum DiagnosticKind : byte
{
    /// <summary>The page activation application must be partial.</summary>
    AppMustBePartial,

    /// <summary>The page activation target must be a supported WinUI application.</summary>
    InvalidApp,

    /// <summary>Only one page activation application is allowed per assembly.</summary>
    MultipleApps,

    /// <summary>The native XAML metadata provider could not be resolved.</summary>
    MissingXamlProvider,

    /// <summary>The generated initialization member conflicts with user code.</summary>
    InitializationMemberConflict,

    /// <summary>A view target is not a supported WinUI page.</summary>
    InvalidView,

    /// <summary>A view model target is unsupported.</summary>
    InvalidViewModel,

    /// <summary>Scoped generated registrations are reserved for future navigation scopes.</summary>
    ScopedRegistration,

    /// <summary>A referenced generated module uses an incompatible contract.</summary>
    IncompatibleModule,

    /// <summary>A view model has no public constructor.</summary>
    MissingPublicConstructor,

    /// <summary>A Page has no public constructor.</summary>
    MissingPagePublicConstructor,

    /// <summary>No page activation initialization call was found.</summary>
    MissingInitializationCall,

    /// <summary>The referenced WASDK XAML interfaces cannot be intercepted safely.</summary>
    UnsupportedXamlContract,

    /// <summary>A generated registration lifetime value is unsupported.</summary>
    UnsupportedLifetime
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