using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace BetterWinUI.PageActivation.DependencyInjection.Generator;

/// <summary>Describes a ViewModel marked for a generated registration, as a cacheable value.</summary>
internal readonly struct ViewModelInfo : IEquatable<ViewModelInfo>
{
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

    public string TypeName { get; }

    public int Lifetime { get; }

    public DiagnosticLocation Location { get; }

    /// <summary>Gets a value that indicates whether the registration is emitted; a missing public constructor only warns.</summary>
    public bool IsValid { get; }

    public ImmutableArray<DiagnosticInfo> Diagnostics { get; }

    public static ViewModelInfo Create(GeneratorAttributeSyntaxContext context)
    {
        var symbol = (INamedTypeSymbol)context.TargetSymbol;
        var attribute = context.Attributes[0];
        var location = DiagnosticLocation.Create(symbol.Locations[0]);
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();

        var validType =
            symbol.TypeKind == TypeKind.Class &&
            !symbol.IsAbstract &&
            symbol.CanBeReferencedFromGeneratedModule();

        if (!validType)
            diagnostics.Add(new DiagnosticInfo(
                DiagnosticDescriptors.InvalidViewModel,
                location,
                symbol.ToDisplayString()));

        var lifetime = attribute.ConstructorArguments.Length == 1 &&
                       attribute.ConstructorArguments[0].Value is int value
            ? value
            : int.MinValue;

        if (lifetime is not ServiceLifetimeValues.Singleton and
            not ServiceLifetimeValues.Scoped and not ServiceLifetimeValues.Transient)
            diagnostics.Add(new DiagnosticInfo(
                DiagnosticDescriptors.UnsupportedLifetime,
                location,
                symbol.ToDisplayString()));

        if (!symbol.InstanceConstructors.Any(static constructor =>
                !constructor.IsStatic &&
                constructor.DeclaredAccessibility == Accessibility.Public))
            diagnostics.Add(new DiagnosticInfo(
                DiagnosticDescriptors.MissingPublicConstructor,
                location,
                symbol.ToDisplayString()));

        var validLifetime =
            lifetime is ServiceLifetimeValues.Singleton or ServiceLifetimeValues.Scoped or ServiceLifetimeValues.Transient;
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
