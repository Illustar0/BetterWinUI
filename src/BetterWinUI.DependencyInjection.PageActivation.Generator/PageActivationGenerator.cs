using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace BetterWinUI.DependencyInjection.PageActivation.Generator;

/// <summary>
/// Generates WinUI page activation adapters and compile-time registration modules.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class PageActivationGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var apps = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                MetadataNames.PageActivationAttribute,
                static (node, _) => node is ClassDeclarationSyntax,
                static (syntaxContext, cancellationToken) =>
                    AppModel.Create(syntaxContext, cancellationToken));

        var views = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                MetadataNames.ViewAttribute,
                static (node, _) => node is ClassDeclarationSyntax,
                static (syntaxContext, cancellationToken) =>
                    ViewInfo.Create(syntaxContext, cancellationToken));

        var viewModels = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                MetadataNames.ViewModelAttribute,
                static (node, _) => node is TypeDeclarationSyntax,
                static (syntaxContext, cancellationToken) =>
                    ViewModelInfo.Create(syntaxContext, cancellationToken));

        var initializationCalls = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => IsInitializeBetterPageActivationInvocation(node),
                static (syntaxContext, cancellationToken) =>
                    GetInitializationTargetTypeName(syntaxContext, cancellationToken))
            .Where(static typeName => typeName is not null)
            .Select(static (typeName, _) => typeName!);

        var assemblyName = context.CompilationProvider
            .Select(static (compilation, _) => compilation.AssemblyName ?? "Assembly");

        var
            referencedModules = context.CompilationProvider.Select(static (compilation, cancellationToken) =>
                ReferencedViewModuleReader.Read(compilation, cancellationToken));

        var xamlContract =
            context.CompilationProvider.Select(static (compilation, cancellationToken) =>
                XamlContractModel.Create(compilation, cancellationToken));

        var
            collectedViews = views.Collect();
        var
            collectedViewModels = viewModels.Collect();
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
                .Combine(referencedModules)
                .Combine(initializationCalls.Collect())
                .Combine(xamlContract),
            static (productionContext, input) =>
            {
                var appsViewsModulesAndCalls = input.Left;
                var appsViewsAndModules = appsViewsModulesAndCalls.Left;
                var appsAndViews = appsViewsAndModules.Left;
                SourceEmitter.EmitApplication(
                    productionContext,
                    appsAndViews.Left,
                    appsAndViews.Right,
                    appsViewsAndModules.Right,
                    appsViewsModulesAndCalls.Right,
                    input.Right);
            });
    }

    private static bool IsInitializeBetterPageActivationInvocation(SyntaxNode node)
    {
        if (node is not InvocationExpressionSyntax invocation) return false;

        return invocation.Expression switch
        {
            IdentifierNameSyntax identifier =>
                identifier.Identifier.ValueText == "InitializeBetterPageActivation",
            MemberAccessExpressionSyntax member =>
                member.Name.Identifier.ValueText == "InitializeBetterPageActivation",
            _ => false
        };
    }

    private static string? GetInitializationTargetTypeName(
        GeneratorSyntaxContext context,
        CancellationToken cancellationToken)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is MemberAccessExpressionSyntax member)
            return context.SemanticModel
                .GetTypeInfo(member.Expression, cancellationToken)
                .Type?
                .ToGlobalDisplayString();

        var enclosingSymbol = context.SemanticModel.GetEnclosingSymbol(
            invocation.SpanStart,
            cancellationToken);
        return enclosingSymbol?.ContainingType?.ToGlobalDisplayString();
    }
}