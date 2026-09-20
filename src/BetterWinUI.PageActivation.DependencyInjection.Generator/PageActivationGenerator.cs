using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace BetterWinUI.PageActivation.DependencyInjection.Generator;

/// <summary>
/// Generates WinUI page activation adapters and compile-time registration modules.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class PageActivationGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var apps = CreateApps(context);
        var views = CreateViews(context);
        var viewModels = CreateViewModels(context);

        var assemblyName = context.CompilationProvider
            .Select(static (compilation, _) => compilation.AssemblyName ?? "Assembly");

        var referencedModules = context.CompilationProvider.Select(
            static (compilation, cancellationToken) =>
                ReferencedViewModuleReader.Read(compilation, cancellationToken));

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
            apps.Collect()
                .Combine(hasLocalModule)
                .Combine(referencedModules),
            static (productionContext, input) =>
            {
                var appsViewsAndModules = input;
                var appsAndViews = appsViewsAndModules.Left;
                SourceEmitter.EmitApplication(
                    productionContext,
                    appsAndViews.Left,
                    appsAndViews.Right,
                    appsViewsAndModules.Right);
            });
    }

    private static IncrementalValuesProvider<AppModel> CreateApps(
        IncrementalGeneratorInitializationContext context)
    {
        return context.SyntaxProvider.ForAttributeWithMetadataName(
            MetadataNames.GeneratePageActivationHookAttribute,
            static (node, _) => node is ClassDeclarationSyntax,
            static (syntaxContext, cancellationToken) =>
                AppModel.Create(syntaxContext, cancellationToken));
    }

    private static IncrementalValuesProvider<ViewInfo> CreateViews(
        IncrementalGeneratorInitializationContext context)
    {
        return context.SyntaxProvider.ForAttributeWithMetadataName(
            MetadataNames.ViewAttribute,
            static (node, _) => node is ClassDeclarationSyntax,
            static (syntaxContext, cancellationToken) =>
                ViewInfo.Create(syntaxContext, cancellationToken));
    }

    private static IncrementalValuesProvider<ViewModelInfo> CreateViewModels(
        IncrementalGeneratorInitializationContext context)
    {
        return context.SyntaxProvider.ForAttributeWithMetadataName(
            MetadataNames.ViewModelAttribute,
            static (node, _) => node is TypeDeclarationSyntax,
            static (syntaxContext, cancellationToken) =>
                ViewModelInfo.Create(syntaxContext, cancellationToken));
    }

}
