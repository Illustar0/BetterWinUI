using Microsoft.CodeAnalysis;

namespace BetterWinUI.PageActivation.Generator;

/// <summary>
/// Describes the application marked for page activation and the facts that decide whether it can be extended.
/// </summary>
internal readonly record struct HookApplication(
    string Namespace,
    string Name,
    string DisplayName,
    string Suffix,
    DiagnosticLocation Location,
    bool IsSupportedShape,
    bool IsPartial,
    bool HasActivationConflict,
    string? ProviderPropertyName,
    bool IsXamlOutputReady)
{
    /// <summary>Gets a value indicating whether generated partial declarations can extend the application.</summary>
    public bool IsValid => IsSupportedShape && IsPartial && !HasActivationConflict;

    /// <summary>Gets the diagnostics that explain why the application cannot be extended or activated.</summary>
    public IEnumerable<DiagnosticInfo> GetDiagnostics()
    {
        if (!IsSupportedShape) yield return new DiagnosticInfo(DiagnosticKind.InvalidApp, Location, DisplayName);
        if (!IsPartial) yield return new DiagnosticInfo(DiagnosticKind.AppMustBePartial, Location, DisplayName);
        if (HasActivationConflict) yield return new DiagnosticInfo(DiagnosticKind.InitializationMemberConflict, Location, DisplayName);
        if (IsXamlOutputReady && ProviderPropertyName is null)
            yield return new DiagnosticInfo(DiagnosticKind.MissingXamlProvider, Location, DisplayName);
    }

    /// <summary>Analyzes an attributed application without retaining Roslyn symbols.</summary>
    public static HookApplication Analyze(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        var symbol = (INamedTypeSymbol)context.TargetSymbol;
        var compilation = context.SemanticModel.Compilation;
        var fullName = symbol.ToGlobalDisplayString();
        return new HookApplication(
            symbol.ContainingNamespace.ToDisplayString(),
            symbol.Name,
            symbol.ToDisplayString(),
            NameUtilities.CreateSuffix(fullName),
            DiagnosticLocation.Create(symbol.Locations[0]),
            IsSupported(symbol, compilation.GetTypeByMetadataName(MetadataNames.Application)),
            symbol.IsPartial(cancellationToken),
            ConflictsWithActivationMethod(symbol, GetFactoryType(compilation)),
            GetNativeProviderPropertyName(symbol, compilation.GetTypeByMetadataName(MetadataNames.XamlMetadataProvider)),
            compilation.SyntaxTrees.Any(static tree => IsXamlTypeInfoTree(tree) && tree.Length > 0));
    }

    /// <summary>Checks the application shape supported by generated partial declarations.</summary>
    private static bool IsSupported(INamedTypeSymbol symbol, INamedTypeSymbol? applicationType)
    {
        if (symbol.TypeKind != TypeKind.Class || symbol.IsAbstract || symbol.Arity != 0 ||
            symbol.ContainingType is not null || symbol.ContainingNamespace.IsGlobalNamespace)
            return false;

        return symbol.DerivesFrom(applicationType);
    }

    /// <summary>Checks whether user code declares the member the generated factory method would occupy.</summary>
    private static bool ConflictsWithActivationMethod(INamedTypeSymbol symbol, ITypeSymbol? factoryType)
    {
        return symbol.GetMembers("UsePageActivation").Any(member =>
            !member.Locations.Any(static location => location.SourceTree?.FilePath.EndsWith(
                ".PageActivation.g.cs", StringComparison.OrdinalIgnoreCase) == true) &&
            (member is not IMethodSymbol method ||
             (method is { Arity: 0, Parameters: [{ RefKind: RefKind.None }] } &&
              SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, factoryType))));
    }

    private static INamedTypeSymbol? GetFactoryType(Compilation compilation)
    {
        var factoryType = compilation.GetTypeByMetadataName("System.Func`2");
        var typeType = compilation.GetTypeByMetadataName("System.Type");
        var pageType = compilation.GetTypeByMetadataName("Microsoft.UI.Xaml.Controls.Page");
        return factoryType is not null && typeType is not null && pageType is not null
            ? factoryType.Construct(typeType, pageType)
            : null;
    }

    private static bool IsXamlTypeInfoTree(SyntaxTree? tree) =>
        tree?.FilePath.EndsWith("XamlTypeInfo.g.cs", StringComparison.OrdinalIgnoreCase) == true;

    private static string? GetNativeProviderPropertyName(
        INamedTypeSymbol symbol,
        INamedTypeSymbol? xamlProviderInterface)
    {
        var providerProperties = symbol
            .GetMembers()
            .OfType<IPropertySymbol>()
            .Where(property => IsNativeProviderProperty(property, xamlProviderInterface))
            .ToArray();
        return providerProperties.Length == 1 ? providerProperties[0].Name : null;
    }

    private static bool IsNativeProviderProperty(
        IPropertySymbol property,
        INamedTypeSymbol? xamlProviderInterface)
    {
        if (xamlProviderInterface is null ||
            !property.Locations.Any(static location => IsXamlTypeInfoTree(location.SourceTree)) ||
            property.IsStatic ||
            property.GetMethod is null ||
            property.SetMethod is not null ||
            property.DeclaredAccessibility != Accessibility.Private ||
            property.Type is not INamedTypeSymbol propertyType)
            return false;

        return SymbolEqualityComparer.Default.Equals(propertyType, xamlProviderInterface) ||
               propertyType.AllInterfaces.Any(implemented => SymbolEqualityComparer.Default.Equals(
                   implemented,
                   xamlProviderInterface));
    }
}
