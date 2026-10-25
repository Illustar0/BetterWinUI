using Microsoft.CodeAnalysis;

namespace BetterWinUI.PageActivation.Generators.Internal;

/// <summary>Stores a diagnostic in the incremental pipeline without retaining syntax trees or symbols.</summary>
/// <remarks><see cref="DiagnosticDescriptor" /> has value equality, so the record stays cacheable.</remarks>
internal readonly record struct DiagnosticInfo(
    DiagnosticDescriptor Descriptor,
    DiagnosticLocation Location,
    string? Argument = null)
{
    public Diagnostic ToDiagnostic() => Diagnostic.Create(Descriptor, Location.ToLocation(), Argument);
}
