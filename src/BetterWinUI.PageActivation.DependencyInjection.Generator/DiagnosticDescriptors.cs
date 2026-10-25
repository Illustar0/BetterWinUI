using Microsoft.CodeAnalysis;
namespace BetterWinUI.PageActivation.DependencyInjection.Generator;

/// <summary>Defines diagnostics owned by this generator.</summary>
internal static class DiagnosticDescriptors
{
    private const string Category = "BetterWinUI.PageActivation.DependencyInjection";
    private const string HelpRoot = "https://github.com/Illustar0/BetterWinUI/blob/main/src/BetterWinUI.PageActivation.DependencyInjection/README.md";
    public static readonly DiagnosticDescriptor InvalidView = Create(
        "BWPA0002",
        "Unsupported view declaration",
        "Type '{0}' must be an accessible, concrete, non-generic WinUI Page subclass",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor InvalidViewModel = Create(
        "BWPA0003",
        "Unsupported view model declaration",
        "View model type '{0}' must be an accessible, concrete, non-abstract, closed class",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor IncompatibleModule = Create(
        "BWPA0004",
        "Incompatible page activation view module",
        "Referenced view module '{0}' must be accessible, non-generic, and declare public static void Register(IServiceCollection services)",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor MissingPublicConstructor = Create(
        "BWPA0005",
        "View model has no public constructor",
        "View model type '{0}' has no public constructor; provide an explicit DI factory before AddGeneratedPageServices",
        DiagnosticSeverity.Warning);

    public static readonly DiagnosticDescriptor MissingPagePublicConstructor = Create(
        "BWPA0006",
        "Page has no public constructor",
        "Page type '{0}' has no public constructor; provide an explicit DI factory before AddGeneratedPageServices",
        DiagnosticSeverity.Warning);

    public static readonly DiagnosticDescriptor UnsupportedLifetime = Create(
        "BWPA0007",
        "Unsupported generated registration lifetime",
        "Type '{0}' must use ServiceLifetime.Transient, ServiceLifetime.Scoped, or ServiceLifetime.Singleton",
        DiagnosticSeverity.Error);

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
