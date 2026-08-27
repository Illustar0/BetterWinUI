using Microsoft.CodeAnalysis;

namespace BetterWinUI.Navigation.Generator;

/// <summary>
/// Captures one value-only generated navigation registration.
/// </summary>
internal sealed class RegistrationInfo
{
    private static readonly SymbolDisplayFormat FullyQualifiedFormat =
        SymbolDisplayFormat.FullyQualifiedFormat;

    private RegistrationInfo(
        string viewTypeName,
        string viewModelTypeName,
        string? parameterTypeName,
        string route,
        Location location,
        Diagnostic? diagnostic)
    {
        ViewTypeName = viewTypeName;
        ViewModelTypeName = viewModelTypeName;
        ParameterTypeName = parameterTypeName;
        Route = route;
        Location = location;
        Diagnostic = diagnostic;
    }

    internal string ViewTypeName { get; }

    internal string ViewModelTypeName { get; }

    internal string? ParameterTypeName { get; }

    internal string Route { get; }

    internal Location Location { get; }

    internal Diagnostic? Diagnostic { get; }

    internal bool IsValid => Diagnostic is null;

    internal static RegistrationInfo Create(
        GeneratorAttributeSyntaxContext context,
        bool isParameterized,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var viewType = (INamedTypeSymbol)context.TargetSymbol;
        var attribute = context.Attributes[0];
        var location = context.TargetNode.GetLocation();
        var viewTypeName = viewType.ToDisplayString(FullyQualifiedFormat);

        if (viewType.TypeKind != TypeKind.Class ||
            viewType.IsAbstract ||
            !CanBeReferencedFromGeneratedModule(viewType))
            return Invalid(
                viewTypeName,
                location,
                Diagnostic.Create(
                    DiagnosticDescriptors.InvalidView,
                    location,
                    viewTypeName));

        var route = GetRoute(attribute);
        if (!IsValidRoute(route))
            return Invalid(
                viewTypeName,
                location,
                Diagnostic.Create(
                    DiagnosticDescriptors.InvalidRoute,
                    location,
                    route));

        var attributeType = attribute.AttributeClass!;
        var viewModelType = attributeType.TypeArguments[0];
        var parameterType = isParameterized ? attributeType.TypeArguments[1] : null;
        var unsupportedType = GetUnsupportedType(viewModelType, parameterType);
        if (unsupportedType is not null)
            return Invalid(
                viewTypeName,
                location,
                Diagnostic.Create(
                    DiagnosticDescriptors.InvalidView,
                    location,
                    unsupportedType.ToDisplayString(FullyQualifiedFormat)));

        return CreateRegistration(
            viewTypeName,
            viewModelType,
            parameterType,
            route,
            location);
    }

    private static string GetRoute(AttributeData attribute)
    {
        return attribute.ConstructorArguments.Length == 1
            ? attribute.ConstructorArguments[0].Value as string ?? string.Empty
            : string.Empty;
    }

    private static bool IsValidRoute(string route)
    {
        return route.Length > 0 &&
               !char.IsWhiteSpace(route[0]) &&
               !char.IsWhiteSpace(route[route.Length - 1]);
    }

    private static ITypeSymbol? GetUnsupportedType(
        ITypeSymbol viewModelType,
        ITypeSymbol? parameterType)
    {
        if (!CanBeReferencedFromGeneratedModule(viewModelType)) return viewModelType;

        return parameterType is not null && !CanBeReferencedFromGeneratedModule(parameterType)
            ? parameterType
            : null;
    }

    private static RegistrationInfo CreateRegistration(
        string viewTypeName,
        ITypeSymbol viewModelType,
        ITypeSymbol? parameterType,
        string route,
        Location location)
    {
        var viewModelTypeName = viewModelType.ToDisplayString(FullyQualifiedFormat);
        var parameterTypeName = parameterType?.ToDisplayString(FullyQualifiedFormat);
        var diagnostic = parameterType is not null &&
                         HasMismatchedNavigationParameter(parameterType, viewModelType)
            ? Diagnostic.Create(
                DiagnosticDescriptors.MismatchedParameterMarker,
                location,
                parameterTypeName,
                viewModelTypeName)
            : null;
        return new RegistrationInfo(
            viewTypeName,
            viewModelTypeName,
            parameterTypeName,
            route,
            location,
            diagnostic);
    }

    private static RegistrationInfo Invalid(
        string viewTypeName,
        Location location,
        Diagnostic diagnostic)
    {
        return new RegistrationInfo(
            viewTypeName,
            string.Empty,
            null,
            string.Empty,
            location,
            diagnostic);
    }

    private static bool CanBeReferencedFromGeneratedModule(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol arrayType)
            return CanBeReferencedFromGeneratedModule(arrayType.ElementType);

        if (type is IPointerTypeSymbol pointerType)
            return CanBeReferencedFromGeneratedModule(pointerType.PointedAtType);

        if (type is not INamedTypeSymbol namedType) return type.TypeKind != TypeKind.TypeParameter;

        for (var current = namedType; current is not null; current = current.ContainingType)
        {
            if (current.IsUnboundGenericType ||
                current.IsFileLocal ||
                current.DeclaredAccessibility is not Accessibility.Public and
                    not Accessibility.Internal and
                    not Accessibility.ProtectedOrInternal ||
                current.TypeArguments.Any(static argument =>
                    !CanBeReferencedFromGeneratedModule(argument)))
                return false;
        }

        return true;
    }

    private static bool HasMismatchedNavigationParameter(
        ITypeSymbol parameterType,
        ITypeSymbol viewModelType)
    {
        var hasMarker = false;
        foreach (var implementedInterface in parameterType.AllInterfaces)
        {
            if (!string.Equals(
                    implementedInterface.OriginalDefinition.ToDisplayString(),
                    "BetterWinUI.Navigation.INavigationParameter<TViewModel>",
                    StringComparison.Ordinal))
                continue;

            hasMarker = true;
            if (SymbolEqualityComparer.Default.Equals(
                    implementedInterface.TypeArguments[0],
                    viewModelType))
                return false;
        }

        return hasMarker;
    }
}