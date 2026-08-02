using Microsoft.CodeAnalysis;

namespace BetterWinUI.Navigation.Generator;

/// <summary>
/// Defines diagnostics reported by navigation registration generation.
/// </summary>
internal static class DiagnosticDescriptors
{
    internal static readonly DiagnosticDescriptor InvalidRoute = new(
        "BWNAV001",
        "Invalid navigation route",
        "Route '{0}' must be non-empty and cannot have leading or trailing whitespace",
        "BetterWinUI.Navigation",
        DiagnosticSeverity.Error,
        true);

    internal static readonly DiagnosticDescriptor InvalidView = new(
        "BWNAV002",
        "Invalid navigation View",
        "View '{0}' must be a concrete, non-generic class",
        "BetterWinUI.Navigation",
        DiagnosticSeverity.Error,
        true);

    internal static readonly DiagnosticDescriptor ConflictingMapping = new(
        "BWNAV003",
        "Conflicting navigation mapping",
        "Generated navigation mapping for '{0}' conflicts with another mapping",
        "BetterWinUI.Navigation",
        DiagnosticSeverity.Error,
        true);

    internal static readonly DiagnosticDescriptor IncompatibleModule = new(
        "BWNAV004",
        "Incompatible navigation module",
        "Referenced navigation module '{0}' uses an incompatible contract version",
        "BetterWinUI.Navigation",
        DiagnosticSeverity.Error,
        true);

    internal static readonly DiagnosticDescriptor MismatchedParameterMarker = new(
        "BWNAV005",
        "Navigation parameter targets another ViewModel",
        "Parameter '{0}' is registered for ViewModel '{1}' but declares a different INavigationParameter target",
        "BetterWinUI.Navigation",
        DiagnosticSeverity.Error,
        true);
}