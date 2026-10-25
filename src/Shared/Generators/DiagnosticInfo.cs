using Microsoft.CodeAnalysis;

namespace BetterWinUI.PageActivation.Generators.Internal;

/// <summary>
/// Stores a diagnostic in the incremental pipeline without retaining syntax trees or symbols.
/// </summary>
/// <param name="Descriptor">The descriptor of the reported diagnostic.</param>
/// <param name="Location">The diagnostic source location.</param>
/// <param name="Argument">The optional message argument.</param>
internal readonly record struct DiagnosticInfo(
    DiagnosticDescriptor Descriptor,
    DiagnosticLocation Location,
    string? Argument = null)
{
    /// <summary>Recreates the Roslyn diagnostic at its original source location.</summary>
    public Diagnostic ToDiagnostic() => Diagnostic.Create(Descriptor, Location.ToLocation(), Argument);
}
