using Microsoft.CodeAnalysis;

namespace BetterWinUI.DependencyInjection.PageActivation.Generator;

/// <summary>
/// Defines stable diagnostics produced by the page activation generator.
/// </summary>
internal static class DiagnosticDescriptors
{
    private const string Category = "BetterWinUI.PageActivation";

    private const string HelpRoot =
        "https://github.com/BetterWinUI/BetterWinUI.DependencyInjection.PageActivation";

    /// <summary>The application must be partial.</summary>
    public static readonly DiagnosticDescriptor AppMustBePartial = Create(
        "BWPA0001",
        "Page activation application must be partial",
        "Type '{0}' must be declared partial so page activation can extend it",
        DiagnosticSeverity.Error);

    /// <summary>The application target is invalid.</summary>
    public static readonly DiagnosticDescriptor InvalidApp = Create(
        "BWPA0002",
        "Unsupported page activation application",
        "Type '{0}' must be a concrete, non-generic, top-level WinUI Application subclass",
        DiagnosticSeverity.Error);

    /// <summary>Multiple applications are marked.</summary>
    public static readonly DiagnosticDescriptor MultipleApps = Create(
        "BWPA0003",
        "Multiple page activation applications",
        "Only one type per assembly can be marked with PageActivationAttribute",
        DiagnosticSeverity.Error);

    /// <summary>The native XAML metadata provider is unavailable.</summary>
    public static readonly DiagnosticDescriptor MissingXamlProvider = Create(
        "BWPA0004",
        "WinUI XAML metadata provider was not found",
        "The XAML compiler output is present, but a unique native IXamlMetadataProvider property was not found on '{0}'",
        DiagnosticSeverity.Error);

    /// <summary>The initialization member conflicts with user code.</summary>
    public static readonly DiagnosticDescriptor InitializationMemberConflict = Create(
        "BWPA0005",
        "InitializeBetterPageActivation member conflict",
        "Type '{0}' already declares InitializeBetterPageActivation; rename that member so the generator can emit its initialization method",
        DiagnosticSeverity.Error);

    /// <summary>The view is unsupported.</summary>
    public static readonly DiagnosticDescriptor InvalidView = Create(
        "BWPA0006",
        "Unsupported view declaration",
        "Type '{0}' must be an accessible, concrete, non-generic WinUI Page subclass",
        DiagnosticSeverity.Error);

    /// <summary>The view model is unsupported.</summary>
    public static readonly DiagnosticDescriptor InvalidViewModel = Create(
        "BWPA0007",
        "Unsupported view model declaration",
        "View model type '{0}' must be an accessible, concrete, non-abstract, closed class",
        DiagnosticSeverity.Error);

    /// <summary>Scoped generated registrations are not implemented.</summary>
    public static readonly DiagnosticDescriptor ScopedRegistration = Create(
        "BWPA0009",
        "Scoped registrations require navigation scopes",
        "Type '{0}' cannot use ServiceLifetime.Scoped until navigation scope ownership and disposal are implemented",
        DiagnosticSeverity.Error);

    /// <summary>A referenced generated module is incompatible.</summary>
    public static readonly DiagnosticDescriptor IncompatibleModule = Create(
        "BWPA0010",
        "Incompatible page activation view module",
        "Referenced view module '{0}' uses an incompatible generated contract version",
        DiagnosticSeverity.Error);

    /// <summary>The view model lacks a public constructor.</summary>
    public static readonly DiagnosticDescriptor MissingPublicConstructor = Create(
        "BWPA0011",
        "View model has no public constructor",
        "View model type '{0}' has no public constructor; provide an explicit DI factory before AddBetterPageActivation",
        DiagnosticSeverity.Warning);

    /// <summary>No initialization call was found.</summary>
    public static readonly DiagnosticDescriptor MissingInitializationCall = Create(
        "BWPA0012",
        "Page activation initialization call was not found",
        "No InitializeBetterPageActivation call for '{0}' was found in this assembly; activation will fail unless initialization is performed by generated or external code",
        DiagnosticSeverity.Warning);

    /// <summary>The referenced WASDK XAML contract cannot be intercepted safely.</summary>
    public static readonly DiagnosticDescriptor UnsupportedXamlContract = Create(
        "BWPA0013",
        "Unsupported WASDK XAML metadata contract",
        "The referenced WASDK XAML interfaces do not expose a unique supported GetXamlType(Type), GetXamlType(string), UnderlyingType, and ActivateInstance() interception contract",
        DiagnosticSeverity.Error);

    /// <summary>The Page lacks a public constructor.</summary>
    public static readonly DiagnosticDescriptor MissingPagePublicConstructor = Create(
        "BWPA0014",
        "Page has no public constructor",
        "Page type '{0}' has no public constructor; provide an explicit DI factory before AddBetterPageActivation",
        DiagnosticSeverity.Warning);

    /// <summary>The generated registration lifetime value is unsupported.</summary>
    public static readonly DiagnosticDescriptor UnsupportedLifetime = Create(
        "BWPA0015",
        "Unsupported generated registration lifetime",
        "Type '{0}' must use ServiceLifetime.Transient or ServiceLifetime.Singleton",
        DiagnosticSeverity.Error);

    /// <summary>
    /// Gets the descriptor for a diagnostic kind.
    /// </summary>
    /// <param name="kind">The diagnostic kind.</param>
    /// <returns>The matching descriptor.</returns>
    public static DiagnosticDescriptor Get(DiagnosticKind kind)
    {
        return kind switch
        {
            DiagnosticKind.AppMustBePartial => AppMustBePartial,
            DiagnosticKind.InvalidApp => InvalidApp,
            DiagnosticKind.MultipleApps => MultipleApps,
            DiagnosticKind.MissingXamlProvider => MissingXamlProvider,
            DiagnosticKind.InitializationMemberConflict => InitializationMemberConflict,
            DiagnosticKind.InvalidView => InvalidView,
            DiagnosticKind.InvalidViewModel => InvalidViewModel,
            DiagnosticKind.ScopedRegistration => ScopedRegistration,
            DiagnosticKind.IncompatibleModule => IncompatibleModule,
            DiagnosticKind.MissingPublicConstructor => MissingPublicConstructor,
            DiagnosticKind.MissingPagePublicConstructor => MissingPagePublicConstructor,
            DiagnosticKind.MissingInitializationCall => MissingInitializationCall,
            DiagnosticKind.UnsupportedXamlContract => UnsupportedXamlContract,
            DiagnosticKind.UnsupportedLifetime => UnsupportedLifetime,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    private static DiagnosticDescriptor Create(
        string id,
        string title,
        string message,
        DiagnosticSeverity severity)
    {
        return new DiagnosticDescriptor(
            id,
            title,
            message,
            Category,
            severity,
            true,
            helpLinkUri: $"{HelpRoot}#{id.ToLowerInvariant()}");
    }
}