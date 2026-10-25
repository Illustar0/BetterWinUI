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
            static (input, token) => HookApplication.Analyze(input, token)).Collect();
        var contract = apps.Combine(context.CompilationProvider).Select(
            static (input, token) => input.Left.IsEmpty
                ? XamlContractModel.Unavailable
                : XamlContractModel.Create(input.Right, token));
        context.RegisterSourceOutput(apps.Combine(contract),
            static (output, input) => Generate(output, input.Left, input.Right));
    }

    /// <summary>Validates the application and emits either the markup-pass stub or the native adapter.</summary>
    private static void Generate(SourceProductionContext output, ImmutableArray<HookApplication> apps, XamlContractModel contract)
    {
        foreach (var diagnostic in apps.SelectMany(static app => app.GetDiagnostics()))
            output.ReportDiagnostic(diagnostic.ToDiagnostic());
        if (apps.Length > 1)
        {
            foreach (var app in apps)
                output.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.MultipleApps, app.Location.ToLocation()));
            return;
        }
        if (apps.IsEmpty || !apps[0].IsValid) return;
        var application = apps[0];
        if (application.ProviderPropertyName is not null &&
            (!contract.IsAvailable || !contract.HasRequiredInterceptors))
        {
            output.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.UnsupportedXamlContract, application.Location.ToLocation()));
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
}
