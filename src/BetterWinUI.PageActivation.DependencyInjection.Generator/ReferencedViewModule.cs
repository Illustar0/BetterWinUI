using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace BetterWinUI.PageActivation.DependencyInjection.Generator;

/// <summary>Describes a view module that a referenced assembly exports through PageActivationViewModuleAttribute.</summary>
internal readonly struct ReferencedViewModule : IEquatable<ReferencedViewModule>
{
    public ReferencedViewModule(
        bool isValid,
        string assemblyName,
        string moduleTypeName)
    {
        IsValid = isValid;
        AssemblyName = assemblyName;
        ModuleTypeName = moduleTypeName;
    }

    /// <summary>
    /// Gets a value that indicates whether this compilation can call the module's
    /// <c>public static void Register(IServiceCollection)</c> method.
    /// </summary>
    public bool IsValid { get; }

    public string AssemblyName { get; }

    public string ModuleTypeName { get; }

    /// <inheritdoc />
    public bool Equals(ReferencedViewModule other)
    {
        return IsValid == other.IsValid &&
               string.Equals(AssemblyName, other.AssemblyName, StringComparison.Ordinal) &&
               string.Equals(ModuleTypeName, other.ModuleTypeName, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is ReferencedViewModule other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = IsValid.GetHashCode();
            hashCode = (hashCode * HashCodeValues.Multiplier) ^
                       StringComparer.Ordinal.GetHashCode(AssemblyName);
            return (hashCode * HashCodeValues.Multiplier) ^
                   StringComparer.Ordinal.GetHashCode(ModuleTypeName);
        }
    }
}

/// <summary>Compares module lists by value, which ImmutableArray does not, so unchanged references stay cached.</summary>
internal sealed class ReferencedViewModuleSequenceComparer : IEqualityComparer<ImmutableArray<ReferencedViewModule>>
{
    public static ReferencedViewModuleSequenceComparer Instance { get; } = new();

    /// <inheritdoc />
    public bool Equals(ImmutableArray<ReferencedViewModule> left, ImmutableArray<ReferencedViewModule> right) =>
        left.SequenceEqual(right);

    /// <inheritdoc />
    public int GetHashCode(ImmutableArray<ReferencedViewModule> modules)
    {
        unchecked
        {
            var hashCode = 0;
            foreach (var module in modules)
                hashCode = (hashCode * HashCodeValues.Multiplier) ^ module.GetHashCode();
            return hashCode;
        }
    }
}

internal static class ReferencedViewModuleReader
{
    /// <summary>Reads the view modules exported by referenced assemblies, in a stable order.</summary>
    /// <remarks>
    /// The Register method is validated here rather than trusted, because a reference may come from a different
    /// generator version or declare the attribute by hand.
    /// </remarks>
    public static ImmutableArray<ReferencedViewModule> Read(
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        var attributeType =
            compilation.GetTypeByMetadataName(MetadataNames.ViewModuleAttribute);
        var servicesType = compilation.GetTypeByMetadataName(
            "Microsoft.Extensions.DependencyInjection.IServiceCollection");
        var modules = ImmutableArray.CreateBuilder<ReferencedViewModule>();
        foreach (var reference in compilation.References)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assembly) continue;

            foreach (var attribute in assembly.GetAttributes())
                if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType) &&
                    TryRead(attribute, compilation, servicesType, out var module))
                    modules.Add(module);
        }

        return modules
            .OrderBy(static module => module.AssemblyName, StringComparer.Ordinal)
            .ThenBy(static module => module.ModuleTypeName, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static bool TryRead(
        AttributeData attribute,
        Compilation compilation,
        INamedTypeSymbol? servicesType,
        out ReferencedViewModule module)
    {
        module = default;
        if (attribute.ConstructorArguments is not [{ Value: INamedTypeSymbol moduleType }])
            return false;

        module = new ReferencedViewModule(
            HasRegistrationMethod(moduleType, compilation, servicesType),
            moduleType.ContainingAssembly.Name,
            moduleType.ToGlobalDisplayString());
        return true;
    }

    private static bool HasRegistrationMethod(
        INamedTypeSymbol moduleType,
        Compilation compilation,
        INamedTypeSymbol? servicesType)
    {
        if (!moduleType.CanBeReferencedFromGeneratedModule() ||
            !compilation.IsSymbolAccessibleWithin(moduleType, compilation.Assembly))
            return false;

        return moduleType.GetMembers("Register")
            .OfType<IMethodSymbol>()
            .Any(method => method.MethodKind == MethodKind.Ordinary &&
                method.IsStatic && !method.IsAbstract && method.ReturnsVoid && method.Arity == 0 &&
                method.DeclaredAccessibility == Accessibility.Public &&
                method.Parameters.Length == 1 && method.Parameters[0].RefKind == RefKind.None &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, servicesType));
    }
}
