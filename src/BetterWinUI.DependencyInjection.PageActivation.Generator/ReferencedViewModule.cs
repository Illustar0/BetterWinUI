using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace BetterWinUI.DependencyInjection.PageActivation.Generator;

/// <summary>
/// Represents a value-equatable generated module exported by a referenced assembly.
/// </summary>
internal readonly struct ReferencedViewModule : IEquatable<ReferencedViewModule>
{
    /// <summary>
    /// Initializes a referenced generated registration module.
    /// </summary>
    public ReferencedViewModule(
        int contractVersion,
        string assemblyName,
        string moduleTypeName)
    {
        ContractVersion = contractVersion;
        AssemblyName = assemblyName;
        ModuleTypeName = moduleTypeName;
    }

    /// <summary>Gets the module contract version.</summary>
    public int ContractVersion { get; }

    /// <summary>Gets the declaring assembly name.</summary>
    public string AssemblyName { get; }

    /// <summary>Gets the fully qualified generated module type name.</summary>
    public string ModuleTypeName { get; }

    /// <inheritdoc />
    public bool Equals(ReferencedViewModule other)
    {
        return ContractVersion == other.ContractVersion &&
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
            var hashCode = ContractVersion;
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
                    TryRead(attribute, assembly.Name, out var module))
                    modules.Add(module);
        }

        return modules
            .OrderBy(static module => module.AssemblyName, StringComparer.Ordinal)
            .ThenBy(static module => module.ModuleTypeName, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static bool TryRead(
        AttributeData attribute,
        string assemblyName,
        out ReferencedViewModule module)
    {
        module = default;
        if (attribute.ConstructorArguments.Length != 2 ||
            attribute.ConstructorArguments[0].Value is not int version ||
            attribute.ConstructorArguments[1].Value is not INamedTypeSymbol moduleType)
            return false;

        module = new ReferencedViewModule(
            version,
            assemblyName,
            moduleType.ToGlobalDisplayString());
        return true;
    }
}