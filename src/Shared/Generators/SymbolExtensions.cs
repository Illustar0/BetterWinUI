using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace BetterWinUI.PageActivation.Generators.Internal;

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
        for (var current = symbol.BaseType; current is not null; current = current.BaseType)
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
                return true;

        return false;
    }

    /// <summary>
    /// Determines whether a generated namespace-level module can reference a type.
    /// </summary>
    /// <param name="symbol">The candidate type.</param>
    /// <returns><see langword="true"/> when the type is closed and assembly-accessible.</returns>
    public static bool CanBeReferencedFromGeneratedModule(this INamedTypeSymbol symbol)
    {
        for (var current = symbol; current is not null; current = current.ContainingType)
        {
            if (current.Arity != 0 ||
                current.IsFileLocal ||
                current.DeclaredAccessibility is not Accessibility.Public and
                    not Accessibility.Internal and
                    not Accessibility.ProtectedOrInternal)
                return false;
        }

        return true;
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
        return symbol.DeclaringSyntaxReferences.All(reference =>
            ((TypeDeclarationSyntax)reference.GetSyntax(cancellationToken)).Modifiers.Any(SyntaxKind.PartialKeyword));
    }
}