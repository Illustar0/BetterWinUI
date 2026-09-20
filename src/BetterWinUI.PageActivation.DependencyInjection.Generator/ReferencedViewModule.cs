using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace BetterWinUI.PageActivation.DependencyInjection.Generator;

/// <summary>
/// Represents a value-equatable generated module exported by a referenced assembly.
/// </summary>
internal readonly struct ReferencedViewModule : IEquatable<ReferencedViewModule>
{
    /// <summary>
    /// Initializes a referenced generated registration module.
    /// </summary>
    public ReferencedViewModule(
        bool isValid,
        string assemblyName,
        string moduleTypeName)
    {
        IsValid = isValid;
        AssemblyName = assemblyName;
        ModuleTypeName = moduleTypeName;
    }

    /// <summary>Gets whether the module exposes the required callable registration method.</summary>
    public bool IsValid { get; }

    /// <summary>Gets the declaring assembly name.</summary>
    public string AssemblyName { get; }

    /// <summary>Gets the fully qualified generated module type name.</summary>
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

/// <summary>
/// Extracts value-equatable generated registration modules from compilation references.
/// </summary>
internal static class ReferencedViewModuleReader
{
    /// <summary>
    /// Reads generated registration modules from referenced assemblies.
    /// </summary>
    public static ImmutableArray<ReferencedViewModule> Read(
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        var attributeType =
            compilation.GetTypeByMetadataName(MetadataNames.ViewModuleAttribute);
        if (attributeType is null) return ImmutableArray<ReferencedViewModule>.Empty;

        var modules = ImmutableArray.CreateBuilder<ReferencedViewModule>();
        foreach (var reference in compilation.References)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assembly) continue;

            foreach (var attribute in assembly.GetAttributes())
                if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType) &&
                    TryRead(attribute, compilation, out var module))
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
        out ReferencedViewModule module)
    {
        module = default;
        if (attribute.ConstructorArguments.Length != 1 ||
            attribute.ConstructorArguments[0].Value is not INamedTypeSymbol moduleType)
            return false;

        module = new ReferencedViewModule(
            HasRegistrationMethod(moduleType, compilation),
            moduleType.ContainingAssembly.Name,
            moduleType.ToGlobalDisplayString());
        return true;
    }

    private static bool HasRegistrationMethod(INamedTypeSymbol moduleType, Compilation compilation)
    {
        if (!moduleType.CanBeReferencedFromGeneratedModule() ||
            !compilation.IsSymbolAccessibleWithin(moduleType, compilation.Assembly))
            return false;

        var servicesType = compilation.GetTypeByMetadataName(
            "Microsoft.Extensions.DependencyInjection.IServiceCollection");
        return servicesType is not null && moduleType.GetMembers("Register")
            .OfType<IMethodSymbol>()
            .Any(method => method.MethodKind == MethodKind.Ordinary &&
                method.IsStatic && !method.IsAbstract && method.ReturnsVoid && method.Arity == 0 &&
                method.DeclaredAccessibility == Accessibility.Public &&
                method.Parameters.Length == 1 && method.Parameters[0].RefKind == RefKind.None &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, servicesType));
    }
}
