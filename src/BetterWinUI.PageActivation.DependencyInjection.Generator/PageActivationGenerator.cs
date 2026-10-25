using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace BetterWinUI.PageActivation.DependencyInjection.Generator;

/// <summary>
/// Generates compile-time registration modules and the hook application's composition root.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class PageActivationGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var hookApplications = CreateHookApplications(context);
        var views = CreateViews(context);
        var viewModels = CreateViewModels(context);

        var assemblyName = context.CompilationProvider
            .Select(static (compilation, _) => compilation.AssemblyName ?? "Assembly");

        var referencedModules = context.CompilationProvider.Select(
            static (compilation, cancellationToken) =>
                ReferencedViewModuleReader.Read(compilation, cancellationToken))
            .WithComparer(ReferencedViewModuleSequenceComparer.Instance);

        var collectedViews = views.Collect();
        var collectedViewModels = viewModels.Collect();
        var
            localRegistrations = collectedViews.Combine(collectedViewModels);
        var hasLocalModule = localRegistrations.Select(static (registrations, _) =>
            registrations.Left.Any(static view => view.IsValid) ||
            registrations.Right.Any(static viewModel => viewModel.IsValid));

        context.RegisterSourceOutput(
            localRegistrations.Combine(assemblyName),
            static (productionContext, input) =>
                SourceEmitter.EmitViewModule(
                    productionContext,
                    input.Left.Left,
                    input.Left.Right,
                    input.Right));

        context.RegisterSourceOutput(
            hookApplications.Collect()
                .Combine(hasLocalModule)
                .Combine(referencedModules)
                .Combine(assemblyName),
            static (productionContext, input) =>
            {
                var (((applications, hasLocal), modules), name) = input;
                if (applications.Length != 1) return;
                SourceEmitter.EmitCompositionRoot(productionContext, applications[0], hasLocal, modules, name);
            });
    }

    /// <summary>Locates hook applications; the activation generator owns their validation.</summary>
    private static IncrementalValuesProvider<DiagnosticLocation> CreateHookApplications(
        IncrementalGeneratorInitializationContext context)
    {
        return context.SyntaxProvider.ForAttributeWithMetadataName(
            MetadataNames.GeneratePageActivationHookAttribute,
            static (node, _) => node is ClassDeclarationSyntax,
            static (syntaxContext, _) => DiagnosticLocation.Create(syntaxContext.TargetSymbol.Locations[0]));
    }

    private static IncrementalValuesProvider<ViewInfo> CreateViews(
        IncrementalGeneratorInitializationContext context)
    {
        return context.SyntaxProvider.ForAttributeWithMetadataName(
            MetadataNames.ViewAttribute,
            static (node, _) => node is ClassDeclarationSyntax,
            static (syntaxContext, _) => ViewInfo.Create(syntaxContext));
    }

    private static IncrementalValuesProvider<ViewModelInfo> CreateViewModels(
        IncrementalGeneratorInitializationContext context)
    {
        return context.SyntaxProvider.ForAttributeWithMetadataName(
            MetadataNames.ViewModelAttribute,
            static (node, _) => node is TypeDeclarationSyntax,
            static (syntaxContext, _) => ViewModelInfo.Create(syntaxContext));
    }

}
