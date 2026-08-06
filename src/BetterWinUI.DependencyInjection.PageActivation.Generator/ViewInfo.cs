using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace BetterWinUI.DependencyInjection.PageActivation.Generator;

/// <summary>
/// Represents a value-equatable attributed Page registration.
/// </summary>
internal readonly struct ViewInfo : IEquatable<ViewInfo>
{
    /// <summary>
    /// Initializes a Page registration.
    /// </summary>
    public ViewInfo(
        string typeName,
        int lifetime,
        DiagnosticLocation location,
        bool isValid,
        ImmutableArray<DiagnosticInfo> diagnostics)
    {
        TypeName = typeName;
        Lifetime = lifetime;
        Location = location;
        IsValid = isValid;
        Diagnostics = diagnostics;
    }

    /// <summary>Gets the fully qualified Page type name.</summary>
    public string TypeName { get; }

    /// <summary>Gets the numeric Microsoft DI lifetime.</summary>
    public int Lifetime { get; }

    /// <summary>Gets the attributed Page location.</summary>
    public DiagnosticLocation Location { get; }

    /// <summary>Gets a value indicating whether the registration is valid.</summary>
    public bool IsValid { get; }

    /// <summary>Gets diagnostics discovered while creating the registration.</summary>
    public ImmutableArray<DiagnosticInfo> Diagnostics { get; }

    /// <summary>
    /// Creates a Page registration from an attributed type.
    /// </summary>
    public static ViewInfo Create(
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

        var pageType =
            context.SemanticModel.Compilation.GetTypeByMetadataName(MetadataNames.Page);
        var validType =
            symbol.TypeKind == TypeKind.Class &&
            !symbol.IsAbstract &&
            symbol.CanBeReferencedFromGeneratedModule() &&
            symbol.DerivesFrom(pageType);

        if (!validType)
            diagnostics.Add(new DiagnosticInfo(
                DiagnosticKind.InvalidView,
                location,
                symbol.ToDisplayString()));

        var lifetime = attribute.ConstructorArguments.Length == 0
            ? ServiceLifetimeValues.Transient
            : attribute.ConstructorArguments[0].Value is int value
                ? value
                : int.MinValue;

        if (lifetime == ServiceLifetimeValues.Scoped)
            // TODO: Support Scoped only after a navigation scope owns the lifetime and
            // disposes it in sync with Frame cache, back-stack eviction, and teardown.
            diagnostics.Add(new DiagnosticInfo(
                DiagnosticKind.ScopedRegistration,
                location,
                symbol.ToDisplayString()));
        else if (lifetime is not ServiceLifetimeValues.Singleton and
                 not ServiceLifetimeValues.Transient)
            diagnostics.Add(new DiagnosticInfo(
                DiagnosticKind.UnsupportedLifetime,
                location,
                symbol.ToDisplayString()));

        if (!symbol.InstanceConstructors.Any(static constructor =>
                !constructor.IsStatic &&
                constructor.DeclaredAccessibility == Accessibility.Public))
            diagnostics.Add(new DiagnosticInfo(
                DiagnosticKind.MissingPagePublicConstructor,
                location,
                symbol.ToDisplayString()));

        var validLifetime =
            lifetime is ServiceLifetimeValues.Singleton or ServiceLifetimeValues.Transient;
        return new ViewInfo(
            symbol.ToGlobalDisplayString(),
            lifetime,
            location,
            validType && validLifetime,
            diagnostics.ToImmutable());
    }

    /// <inheritdoc />
    public bool Equals(ViewInfo other)
    {
        return string.Equals(TypeName, other.TypeName, StringComparison.Ordinal) &&
               Lifetime == other.Lifetime &&
               Location.Equals(other.Location) &&
               IsValid == other.IsValid &&
               Diagnostics.SequenceEqual(other.Diagnostics);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is ViewInfo other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = StringComparer.Ordinal.GetHashCode(TypeName);
            hashCode = (hashCode * HashCodeValues.Multiplier) ^ Lifetime;
            return (hashCode * HashCodeValues.Multiplier) ^ IsValid.GetHashCode();
        }
    }
}