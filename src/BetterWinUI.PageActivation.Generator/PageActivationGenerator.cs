using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace BetterWinUI.PageActivation.Generator;

/// <summary>Generates the Page activation hook for the application marked with GeneratePageActivationHookAttribute.</summary>
/// <remarks>
/// The XAML compiler compiles the application twice. The markup pass, which runs before XamlTypeInfo.g.cs exists,
/// receives a stub; the final compilation receives an adapter that intercepts the native XAML metadata provider.
/// </remarks>
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
        // Reading the referenced XAML interfaces walks every member, so assemblies without a hook application skip it.
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
        // The markup-pass stub needs no XAML contract. The adapter must find exactly one member to intercept for
        // each lookup and activation call, or it cannot route activation safely.
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
