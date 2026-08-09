using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace BetterWinUI.Navigation.Generator;

/// <summary>
/// Generates navigation registration modules and composition helpers.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class NavigationGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var parameterless =
            context.SyntaxProvider.ForAttributeWithMetadataName(
                MetadataNames.ViewForAttribute,
                static (node, _) => node is TypeDeclarationSyntax,
                static (syntaxContext, cancellationToken) =>
                    RegistrationInfo.Create(
                        syntaxContext,
                        false,
                        cancellationToken));

        var parameterized =
            context.SyntaxProvider.ForAttributeWithMetadataName(
                MetadataNames.ParameterizedViewForAttribute,
                static (node, _) => node is TypeDeclarationSyntax,
                static (syntaxContext, cancellationToken) =>
                    RegistrationInfo.Create(
                        syntaxContext,
                        true,
                        cancellationToken));

        var assemblyName = context.CompilationProvider
            .Select(static (compilation, _) => compilation.AssemblyName ?? "Assembly");
        var referencedModules =
            context.CompilationProvider.Select(static (compilation, cancellationToken) =>
                ReferencedModuleReader.Read(compilation, cancellationToken));
        var supportsFrameDependencyInjection =
            context.CompilationProvider.Select(static (compilation, _) =>
                compilation.GetTypeByMetadataName(MetadataNames.ServiceCollection) is not null &&
                compilation.GetTypeByMetadataName(MetadataNames.FrameNavigationService) is not null);

        var local = parameterless
            .Collect()
            .Combine(parameterized.Collect())
            .Combine(assemblyName)
            .Select(static (input, _) => GenerationModel.Create(
                input.Left.Left,
                input.Left.Right,
                input.Right));

        context.RegisterSourceOutput(
            local,
            static (productionContext, model) =>
                SourceEmitter.EmitModule(productionContext, model));

        context.RegisterSourceOutput(
            local.Combine(referencedModules).Combine(supportsFrameDependencyInjection),
            static (productionContext, input) =>
                SourceEmitter.EmitComposition(
                    productionContext,
                    input.Left.Left,
                    input.Left.Right,
                    input.Right));
    }
}