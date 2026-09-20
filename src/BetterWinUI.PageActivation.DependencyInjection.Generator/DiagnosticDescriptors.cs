using Microsoft.CodeAnalysis;
namespace BetterWinUI.PageActivation.DependencyInjection.Generator;

/// <summary>Defines diagnostics owned by this generator.</summary>
internal static class DiagnosticDescriptors
{
    private const string Category = "BetterWinUI.PageActivation.DependencyInjection";
    private const string HelpRoot = "https://github.com/Illustar0/BetterWinUI/blob/main/src/BetterWinUI.PageActivation.DependencyInjection/README.md";
    /// <summary>The initialization member conflicts with user code.</summary>
    public static readonly DiagnosticDescriptor InitializationMemberConflict = Create(
        "BWPA0001",
        "UsePageActivation member conflict",
        "Type '{0}' declares a member that conflicts with UsePageActivation(IServiceProvider)",
        DiagnosticSeverity.Error);

    /// <summary>The view is unsupported.</summary>
    public static readonly DiagnosticDescriptor InvalidView = Create(
        "BWPA0002",
        "Unsupported view declaration",
        "Type '{0}' must be an accessible, concrete, non-generic WinUI Page subclass",
        DiagnosticSeverity.Error);

    /// <summary>The view model is unsupported.</summary>
    public static readonly DiagnosticDescriptor InvalidViewModel = Create(
        "BWPA0003",
        "Unsupported view model declaration",
        "View model type '{0}' must be an accessible, concrete, non-abstract, closed class",
        DiagnosticSeverity.Error);

    /// <summary>A referenced generated module is incompatible.</summary>
    public static readonly DiagnosticDescriptor IncompatibleModule = Create(
        "BWPA0004",
        "Incompatible page activation view module",
        "Referenced view module '{0}' must be accessible, non-generic, and declare public static void Register(IServiceCollection services)",
        DiagnosticSeverity.Error);

    /// <summary>The view model lacks a public constructor.</summary>
    public static readonly DiagnosticDescriptor MissingPublicConstructor = Create(
        "BWPA0005",
        "View model has no public constructor",
        "View model type '{0}' has no public constructor; provide an explicit DI factory before AddGeneratedPageServices",
        DiagnosticSeverity.Warning);

    /// <summary>The Page lacks a public constructor.</summary>
    public static readonly DiagnosticDescriptor MissingPagePublicConstructor = Create(
        "BWPA0006",
        "Page has no public constructor",
        "Page type '{0}' has no public constructor; provide an explicit DI factory before AddGeneratedPageServices",
        DiagnosticSeverity.Warning);

    /// <summary>The generated registration lifetime value is unsupported.</summary>
    public static readonly DiagnosticDescriptor UnsupportedLifetime = Create(
        "BWPA0007",
        "Unsupported generated registration lifetime",
        "Type '{0}' must use ServiceLifetime.Transient, ServiceLifetime.Scoped, or ServiceLifetime.Singleton",
        DiagnosticSeverity.Error);

    /// <summary>Gets the descriptor for a diagnostic kind.</summary>
    public static DiagnosticDescriptor Get(DiagnosticKind kind) => kind switch
    {
        DiagnosticKind.InitializationMemberConflict => InitializationMemberConflict,
        DiagnosticKind.InvalidView => InvalidView,
        DiagnosticKind.InvalidViewModel => InvalidViewModel,
        DiagnosticKind.IncompatibleModule => IncompatibleModule,
        DiagnosticKind.MissingPublicConstructor => MissingPublicConstructor,
        DiagnosticKind.MissingPagePublicConstructor => MissingPagePublicConstructor,
        DiagnosticKind.UnsupportedLifetime => UnsupportedLifetime,
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
