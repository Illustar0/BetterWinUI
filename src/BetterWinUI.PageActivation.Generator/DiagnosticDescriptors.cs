using Microsoft.CodeAnalysis;
namespace BetterWinUI.PageActivation.Generator;

/// <summary>Defines diagnostics owned by this generator.</summary>
internal static class DiagnosticDescriptors
{
    private const string Category = "BetterWinUI.PageActivation";
    private const string HelpRoot = "https://github.com/Illustar0/BetterWinUI/blob/main/src/BetterWinUI.PageActivation/README.md";
    /// <summary>The application must be partial.</summary>
    public static readonly DiagnosticDescriptor AppMustBePartial = Create(
        "BWPH0001",
        "Page activation application must be partial",
        "Type '{0}' must be declared partial so page activation can extend it",
        DiagnosticSeverity.Error);

    /// <summary>The application target is invalid.</summary>
    public static readonly DiagnosticDescriptor InvalidApp = Create(
        "BWPH0002",
        "Unsupported page activation application",
        "Type '{0}' must be a concrete, non-generic, top-level WinUI Application subclass",
        DiagnosticSeverity.Error);

    /// <summary>Multiple applications are marked.</summary>
    public static readonly DiagnosticDescriptor MultipleApps = Create(
        "BWPH0003",
        "Multiple page activation applications",
        "Only one type per assembly can be marked with GeneratePageActivationHookAttribute",
        DiagnosticSeverity.Error);

    /// <summary>The native XAML metadata provider is unavailable.</summary>
    public static readonly DiagnosticDescriptor MissingXamlProvider = Create(
        "BWPH0004",
        "WinUI XAML metadata provider was not found",
        "The XAML compiler output is present, but a unique native IXamlMetadataProvider property was not found on '{0}'",
        DiagnosticSeverity.Error);

    /// <summary>The initialization member conflicts with user code.</summary>
    public static readonly DiagnosticDescriptor InitializationMemberConflict = Create(
        "BWPH0005",
        "UsePageActivation member conflict",
        "Type '{0}' declares a member that conflicts with UsePageActivation(Func<Type, Page>)",
        DiagnosticSeverity.Error);

    /// <summary>The referenced WASDK XAML contract cannot be intercepted safely.</summary>
    public static readonly DiagnosticDescriptor UnsupportedXamlContract = Create(
        "BWPH0006",
        "Unsupported WASDK XAML metadata contract",
        "The referenced WASDK XAML interfaces do not expose a unique supported GetXamlType(Type), GetXamlType(string), UnderlyingType, and ActivateInstance() interception contract",
        DiagnosticSeverity.Error);

    /// <summary>Gets the descriptor for a diagnostic kind.</summary>
    public static DiagnosticDescriptor Get(DiagnosticKind kind) => kind switch
    {
        DiagnosticKind.AppMustBePartial => AppMustBePartial,
        DiagnosticKind.InvalidApp => InvalidApp,
        DiagnosticKind.MultipleApps => MultipleApps,
        DiagnosticKind.MissingXamlProvider => MissingXamlProvider,
        DiagnosticKind.InitializationMemberConflict => InitializationMemberConflict,
        DiagnosticKind.UnsupportedXamlContract => UnsupportedXamlContract,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

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
