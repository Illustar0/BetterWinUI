using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace BetterWinUI.DependencyInjection.PageActivation.Generator;

/// <summary>
/// Represents a value-equatable attributed view model registration.
/// </summary>
internal readonly struct ViewModelInfo : IEquatable<ViewModelInfo>
{
    /// <summary>
    /// Initializes a view model registration.
    /// </summary>
    public ViewModelInfo(
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

    /// <summary>Gets the fully qualified view model type name.</summary>
    public string TypeName { get; }

    /// <summary>Gets the numeric Microsoft DI lifetime.</summary>
    public int Lifetime { get; }

    /// <summary>Gets the attributed view model location.</summary>
    public DiagnosticLocation Location { get; }

    /// <summary>Gets a value indicating whether the registration is valid.</summary>
    public bool IsValid { get; }

    /// <summary>Gets diagnostics discovered while creating the registration.</summary>
    public ImmutableArray<DiagnosticInfo> Diagnostics { get; }

    /// <summary>
    /// Creates a view model registration from an attributed type.
    /// </summary>
    public static ViewModelInfo Create(
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

        var validType =
            symbol.TypeKind == TypeKind.Class &&
            !symbol.IsAbstract &&
            !symbol.IsUnboundGenericType &&
            symbol.Arity == 0;

        if (!validType)
            diagnostics.Add(new DiagnosticInfo(
                DiagnosticKind.InvalidViewModel,
                location,
                symbol.ToDisplayString()));

        var lifetime = attribute.ConstructorArguments.Length == 1 &&
                       attribute.ConstructorArguments[0].Value is int value
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
                DiagnosticKind.MissingPublicConstructor,
                location,
                symbol.ToDisplayString()));

        var validLifetime =
            lifetime is ServiceLifetimeValues.Singleton or ServiceLifetimeValues.Transient;
        return new ViewModelInfo(
            symbol.ToGlobalDisplayString(),
            lifetime,
            location,
            validType && validLifetime,
            diagnostics.ToImmutable());
    }

    /// <inheritdoc />
    public bool Equals(ViewModelInfo other)
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
        return obj is ViewModelInfo other && Equals(other);
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