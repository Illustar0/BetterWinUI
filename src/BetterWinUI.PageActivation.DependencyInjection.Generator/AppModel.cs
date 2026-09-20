using System.Collections.Immutable;
using BetterWinUI.PageActivation.Generators.Internal;
using Microsoft.CodeAnalysis;

namespace BetterWinUI.PageActivation.DependencyInjection.Generator;

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
        DiagnosticLocation location,
        bool isValid,
        ImmutableArray<DiagnosticInfo> diagnostics)
    {
        Namespace = @namespace;
        Name = name;
        FullName = fullName;
        AssemblyName = assemblyName;
        Suffix = suffix;
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
        var location = GetLocation(symbol, context.Attributes[0], cancellationToken);
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();

        var applicationType =
            context.SemanticModel.Compilation.GetTypeByMetadataName(MetadataNames.Application);

        var supportedShape = PageActivationApplication.IsSupported(symbol, applicationType);

        var partial = symbol.IsPartial(cancellationToken);

        var parameterType = context.SemanticModel.Compilation.GetTypeByMetadataName("System.IServiceProvider");
        var hasUserInitializationMember = PageActivationApplication.HasActivationConflict(symbol, parameterType);

        if (hasUserInitializationMember)
            diagnostics.Add(new DiagnosticInfo(
                DiagnosticKind.InitializationMemberConflict,
                location,
                symbol.ToDisplayString()));

        var fullName = symbol.ToGlobalDisplayString();
        return new AppModel(
            symbol.ContainingNamespace.ToDisplayString(),
            symbol.Name,
            fullName,
            symbol.ContainingAssembly.Name,
            NameUtilities.CreateSuffix(fullName),
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
            hashCode = (hashCode * HashCodeValues.Multiplier) ^ IsValid.GetHashCode();
            return (hashCode * HashCodeValues.Multiplier) ^ Diagnostics.Length;
        }
    }

    private static DiagnosticLocation GetLocation(
        INamedTypeSymbol symbol,
        AttributeData attribute,
        CancellationToken cancellationToken)
    {
        var attributeLocation =
            attribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken).GetLocation() ??
            symbol.Locations[0];
        return symbol.GetSourceLocation(attributeLocation);
    }

}
