using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace BetterWinUI.Navigation.Generator;

/// <summary>
/// Describes a generated navigation module exported by a referenced assembly.
/// </summary>
internal sealed class ReferencedModule
{
    internal ReferencedModule(int contractVersion, string assemblyName, string typeName)
    {
        ContractVersion = contractVersion;
        AssemblyName = assemblyName;
        TypeName = typeName;
    }

    internal int ContractVersion { get; }

    internal string AssemblyName { get; }

    internal string TypeName { get; }
}

/// <summary>
/// Reads navigation module metadata without loading referenced assemblies.
/// </summary>
internal static class ReferencedModuleReader
{
    internal static ImmutableArray<ReferencedModule> Read(
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        var attributeType =
            compilation.GetTypeByMetadataName(MetadataNames.ViewModuleAttribute);
        if (attributeType is null) return ImmutableArray<ReferencedModule>.Empty;

        var modules = ImmutableArray.CreateBuilder<ReferencedModule>();
        foreach (var reference in compilation.References)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assembly) continue;

            foreach (var attribute in assembly.GetAttributes())
            {
                if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType) ||
                    attribute.ConstructorArguments.Length != 2 ||
                    attribute.ConstructorArguments[0].Value is not int version ||
                    attribute.ConstructorArguments[1].Value is not INamedTypeSymbol moduleType)
                    continue;

                modules.Add(new ReferencedModule(
                    version,
                    assembly.Name,
                    moduleType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
            }
        }

        return modules
            .OrderBy(static module => module.AssemblyName, StringComparer.Ordinal)
            .ThenBy(static module => module.TypeName, StringComparer.Ordinal)
            .ToImmutableArray();
    }
}