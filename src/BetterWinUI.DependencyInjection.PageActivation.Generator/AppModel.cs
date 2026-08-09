using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace BetterWinUI.DependencyInjection.PageActivation.Generator;

/// <summary>
/// Represents the value-equatable information required to generate an application adapter.
/// </summary>
internal readonly struct AppModel : IEquatable<AppModel>
{
    /// <summary>
    /// Initializes an application model.
    /// </summary>
    public AppModel(
        string @namespace,
        string name,
        string fullName,
        string assemblyName,
        string suffix,
        string? providerPropertyName,
        DiagnosticLocation location,
        bool isValid,
        ImmutableArray<DiagnosticInfo> diagnostics)
    {
        Namespace = @namespace;
        Name = name;
        FullName = fullName;
        AssemblyName = assemblyName;
        Suffix = suffix;
        ProviderPropertyName = providerPropertyName;
        Location = location;
        IsValid = isValid;
        Diagnostics = diagnostics;
    }

    /// <summary>Gets the application namespace.</summary>
    public string Namespace { get; }

    /// <summary>Gets the application type name.</summary>
    public string Name { get; }

    /// <summary>Gets the fully qualified application type name.</summary>
    public string FullName { get; }

    /// <summary>Gets the containing assembly name.</summary>
    public string AssemblyName { get; }

    /// <summary>Gets the deterministic generated name suffix.</summary>
    public string Suffix { get; }

    /// <summary>Gets the native XAML provider property name when available.</summary>
    public string? ProviderPropertyName { get; }

    /// <summary>Gets the application declaration location.</summary>
    public DiagnosticLocation Location { get; }

    /// <summary>Gets a value indicating whether the application shape is supported.</summary>
    public bool IsValid { get; }

    /// <summary>Gets the diagnostics discovered while creating the model.</summary>
    public ImmutableArray<DiagnosticInfo> Diagnostics { get; }

    /// <summary>
    /// Creates an application model from an attributed type.
    /// </summary>
    public static AppModel Create(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        var symbol = (INamedTypeSymbol)context.TargetSymbol;
        var attribute = context.Attributes[0];
        var attributeLocation =
            attribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken).GetLocation() ??
            symbol.Locations[0];
        var location = symbol.GetSourceLocation(attributeLocation);
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();

        var applicationType =
            context.SemanticModel.Compilation.GetTypeByMetadataName(MetadataNames.Application);

        var supportedShape =
            symbol.TypeKind == TypeKind.Class &&
            !symbol.IsAbstract &&
            symbol.Arity == 0 &&
            symbol.ContainingType is null &&
            !symbol.ContainingNamespace.IsGlobalNamespace &&
            symbol.DerivesFrom(applicationType);

        if (!supportedShape)
            diagnostics.Add(new DiagnosticInfo(
                DiagnosticKind.InvalidApp,
                location,
                symbol.ToDisplayString()));

        var partial = symbol.IsPartial(cancellationToken);
        if (!partial)
            diagnostics.Add(new DiagnosticInfo(
                DiagnosticKind.AppMustBePartial,
                location,
                symbol.ToDisplayString()));

        var hasUserInitializationMember = symbol
            .GetMembers("InitializeBetterPageActivation")
            .Any(static member => !IsGeneratedMember(member));

        if (hasUserInitializationMember)
            diagnostics.Add(new DiagnosticInfo(
                DiagnosticKind.InitializationMemberConflict,
                location,
                symbol.ToDisplayString()));

        var xamlProviderInterface =
            context.SemanticModel.Compilation.GetTypeByMetadataName(
                MetadataNames.XamlMetadataProvider);

        var providerProperties = symbol
            .GetMembers()
            .OfType<IPropertySymbol>()
            .Where(property => IsNativeProviderProperty(property, xamlProviderInterface))
            .ToImmutableArray();

        var xamlOutputReady = context.SemanticModel.Compilation.SyntaxTrees.Any(static tree =>
            tree.FilePath.EndsWith("XamlTypeInfo.g.cs", StringComparison.OrdinalIgnoreCase) &&
            tree.Length > 0);

        var providerPropertyName =
            providerProperties.Length == 1 ? providerProperties[0].Name : null;

        if (xamlOutputReady && providerPropertyName is null)
            diagnostics.Add(new DiagnosticInfo(
                DiagnosticKind.MissingXamlProvider,
                location,
                symbol.ToDisplayString()));

        var fullName = symbol.ToGlobalDisplayString();
        return new AppModel(
            symbol.ContainingNamespace.ToDisplayString(),
            symbol.Name,
            fullName,
            symbol.ContainingAssembly.Name,
            NameUtilities.CreateSuffix(fullName),
            providerPropertyName,
            location,
            supportedShape && partial && !hasUserInitializationMember,
            diagnostics.ToImmutable());
    }

    /// <inheritdoc />
    public bool Equals(AppModel other)
    {
        return string.Equals(Namespace, other.Namespace, StringComparison.Ordinal) &&
               string.Equals(Name, other.Name, StringComparison.Ordinal) &&
               string.Equals(FullName, other.FullName, StringComparison.Ordinal) &&
               string.Equals(AssemblyName, other.AssemblyName, StringComparison.Ordinal) &&
               string.Equals(Suffix, other.Suffix, StringComparison.Ordinal) &&
               string.Equals(ProviderPropertyName, other.ProviderPropertyName, StringComparison.Ordinal) &&
               Location.Equals(other.Location) &&
               IsValid == other.IsValid &&
               Diagnostics.SequenceEqual(other.Diagnostics);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is AppModel other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = StringComparer.Ordinal.GetHashCode(FullName);
            hashCode = (hashCode * HashCodeValues.Multiplier) ^ (ProviderPropertyName is null
                ? 0
                : StringComparer.Ordinal.GetHashCode(ProviderPropertyName));
            hashCode = (hashCode * HashCodeValues.Multiplier) ^ IsValid.GetHashCode();
            return (hashCode * HashCodeValues.Multiplier) ^ Diagnostics.Length;
        }
    }

    private static bool IsNativeProviderProperty(
        IPropertySymbol property,
        INamedTypeSymbol? xamlProviderInterface)
    {
        if (xamlProviderInterface is null ||
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

    private static bool IsGeneratedMember(ISymbol member)
    {
        return member.Locations.Any(static location =>
            location.SourceTree?.FilePath.EndsWith(
                ".PageActivation.g.cs",
                StringComparison.OrdinalIgnoreCase) == true);
    }
}