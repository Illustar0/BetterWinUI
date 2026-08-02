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
            ContainsTypeParameter(viewType))
            return Invalid(
                viewTypeName,
                location,
                Diagnostic.Create(
                    DiagnosticDescriptors.InvalidView,
                    location,
                    viewTypeName));

        var route = attribute.ConstructorArguments.Length == 1
            ? attribute.ConstructorArguments[0].Value as string ?? string.Empty
            : string.Empty;
        if (route.Length == 0 ||
            char.IsWhiteSpace(route[0]) ||
            char.IsWhiteSpace(route[route.Length - 1]))
            return Invalid(
                viewTypeName,
                location,
                Diagnostic.Create(
                    DiagnosticDescriptors.InvalidRoute,
                    location,
                    route));

        var attributeType = attribute.AttributeClass!;
        var viewModelType = attributeType.TypeArguments[0];
        var viewModelTypeName = viewModelType.ToDisplayString(FullyQualifiedFormat);
        var parameterType = isParameterized
            ? attributeType.TypeArguments[1]
            : null;

        if (parameterType is not null &&
            HasMismatchedNavigationParameter(parameterType, viewModelType))
        {
            var parameterTypeName = parameterType.ToDisplayString(FullyQualifiedFormat);
            return new RegistrationInfo(
                viewTypeName,
                viewModelTypeName,
                parameterTypeName,
                route,
                location,
                Diagnostic.Create(
                    DiagnosticDescriptors.MismatchedParameterMarker,
                    location,
                    parameterTypeName,
                    viewModelTypeName));
        }

        return new RegistrationInfo(
            viewTypeName,
            viewModelTypeName,
            parameterType?.ToDisplayString(FullyQualifiedFormat),
            route,
            location,
            null);
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

    private static bool ContainsTypeParameter(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.ContainingType)
            if (current.Arity != 0)
                return true;

        return false;
    }

    private static bool HasMismatchedNavigationParameter(
        ITypeSymbol parameterType,
        ITypeSymbol viewModelType)
    {
        var hasMarker = false;
        foreach (var implementedInterface in parameterType.AllInterfaces)
        {
            if (implementedInterface.OriginalDefinition.ToDisplayString() !=
                "BetterWinUI.Navigation.INavigationParameter<TViewModel>")
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