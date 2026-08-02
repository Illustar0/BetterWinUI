using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace BetterWinUI.DependencyInjection.PageActivation.Generator;

/// <summary>
/// Provides focused Roslyn symbol operations used by generator models.
/// </summary>
internal static class SymbolExtensions
{
    private static readonly SymbolDisplayFormat FullyQualifiedFormat =
        SymbolDisplayFormat.FullyQualifiedFormat
            .WithGenericsOptions(SymbolDisplayGenericsOptions.IncludeTypeParameters)
            .WithMiscellaneousOptions(
                SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <summary>
    /// Gets a source-safe fully qualified type name.
    /// </summary>
    /// <param name="symbol">The type symbol.</param>
    /// <returns>The fully qualified type name.</returns>
    public static string ToGlobalDisplayString(this ITypeSymbol symbol)
    {
        return symbol.ToDisplayString(FullyQualifiedFormat);
    }

    /// <summary>
    /// Determines whether a type derives from a base type.
    /// </summary>
    /// <param name="symbol">The candidate type.</param>
    /// <param name="baseType">The expected base type.</param>
    /// <returns><see langword="true"/> when the candidate derives from the base type.</returns>
    public static bool DerivesFrom(this INamedTypeSymbol symbol, INamedTypeSymbol? baseType)
    {
        if (baseType is null) return false;

        for (var current = symbol.BaseType; current is not null; current = current.BaseType)
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
                return true;

        return false;
    }

    /// <summary>
    /// Determines whether every declaration of a type is partial.
    /// </summary>
    /// <param name="symbol">The type symbol.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when every source declaration is partial.</returns>
    public static bool IsPartial(
        this INamedTypeSymbol symbol,
        CancellationToken cancellationToken)
    {
        foreach (var reference in symbol.DeclaringSyntaxReferences)
            if (reference.GetSyntax(cancellationToken) is not TypeDeclarationSyntax declaration ||
                !declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
                return false;

        return symbol.DeclaringSyntaxReferences.Length > 0;
    }

    /// <summary>
    /// Gets the preferred source location for a generated target.
    /// </summary>
    /// <param name="symbol">The target symbol.</param>
    /// <param name="fallback">The fallback attribute location.</param>
    /// <returns>The value source location.</returns>
    public static DiagnosticLocation GetSourceLocation(
        this ISymbol symbol,
        Location fallback)
    {
        var location = symbol.Locations.FirstOrDefault(static item => item.IsInSource) ?? fallback;
        return DiagnosticLocation.Create(location);
    }
}