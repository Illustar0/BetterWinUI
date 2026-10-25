using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace BetterWinUI.PageActivation.Generators.Internal;

internal static class SymbolExtensions
{
    private static readonly SymbolDisplayFormat FullyQualifiedFormat =
        SymbolDisplayFormat.FullyQualifiedFormat
            .WithGenericsOptions(SymbolDisplayGenericsOptions.IncludeTypeParameters)
            .WithMiscellaneousOptions(
                SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <summary>Gets a <c>global::</c>-qualified type name that generated code can use in any namespace.</summary>
    public static string ToGlobalDisplayString(this ITypeSymbol symbol)
    {
        return symbol.ToDisplayString(FullyQualifiedFormat);
    }

    public static bool DerivesFrom(this INamedTypeSymbol symbol, INamedTypeSymbol? baseType)
    {
        for (var current = symbol.BaseType; current is not null; current = current.BaseType)
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
                return true;

        return false;
    }

    /// <summary>Determines whether namespace-level generated code in the same assembly can name a type.</summary>
    /// <returns>
    /// <see langword="true" /> if the type and its containing types are non-generic, not file-local, and at least
    /// internal; otherwise, <see langword="false" />.
    /// </returns>
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

    /// <summary>Determines whether every source declaration of a type is partial.</summary>
    public static bool IsPartial(
        this INamedTypeSymbol symbol,
        CancellationToken cancellationToken)
    {
        return symbol.DeclaringSyntaxReferences.All(reference =>
            ((TypeDeclarationSyntax)reference.GetSyntax(cancellationToken)).Modifiers.Any(SyntaxKind.PartialKeyword));
    }
}
