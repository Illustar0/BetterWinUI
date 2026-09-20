using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace BetterWinUI.PageActivation.Generator;

/// <summary>Connects application Page factories to WinUI's native XAML activation contract.</summary>
[Generator(LanguageNames.CSharp)]
public sealed class PageActivationGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var apps = context.SyntaxProvider.ForAttributeWithMetadataName(
            MetadataNames.GeneratePageActivationHookAttribute,
            static (node, _) => node is ClassDeclarationSyntax,
            static (input, token) => AppModel.Create(input, token));
        var contract = context.CompilationProvider.Select(
            static (compilation, token) => XamlContractModel.Create(compilation, token));
        context.RegisterSourceOutput(apps.Collect().Combine(contract),
            static (output, input) => Generate(output, input.Left, input.Right));
    }

    /// <summary>Validates the application and emits either the markup-pass stub or the native adapter.</summary>
    private static void Generate(SourceProductionContext output, ImmutableArray<AppModel> apps, XamlContractModel contract)
    {
        foreach (var diagnostic in apps.SelectMany(static app => app.Diagnostics))
            Report(output, diagnostic);
        if (apps.Length > 1)
        {
            foreach (var app in apps)
                Report(output, new DiagnosticInfo(DiagnosticKind.MultipleApps, app.Location));
            return;
        }
        if (apps.IsDefaultOrEmpty || !apps[0].IsValid) return;
        var application = apps[0];
        if (application.ProviderPropertyName is not null &&
            (!contract.IsAvailable || !contract.HasRequiredInterceptors))
        {
            Report(output, new DiagnosticInfo(DiagnosticKind.UnsupportedXamlContract, application.Location));
            return;
        }
        var source = TemplateRenderer.Render(
            application.ProviderPropertyName is null ? "ApplicationStub.scriban" : "Application.scriban",
            new
            {
                application.Namespace,
                AppName = application.Name,
                application.Suffix,
                application.ProviderPropertyName,
                contract.MetadataProviderInterfaceName,
                contract.XamlTypeInterfaceName,
                contract.MetadataProviderMembers,
                contract.XamlTypeMembers
            });
        output.AddSource($"{application.Name}.PageActivation.g.cs", SourceText.From(source, Encoding.UTF8));
    }

    /// <summary>Reports a value-only diagnostic at its original source location.</summary>
    private static void Report(SourceProductionContext output, DiagnosticInfo diagnostic) =>
        output.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.Get(diagnostic.Kind),
            diagnostic.Location.ToLocation(), diagnostic.Argument));
}
