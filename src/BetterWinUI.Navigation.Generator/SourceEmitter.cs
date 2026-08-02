using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace BetterWinUI.Navigation.Generator;

/// <summary>
/// Emits local registration modules and per-compilation composition helpers.
/// </summary>
internal static class SourceEmitter
{
    internal const int ContractVersion = 1;

    internal static void EmitModule(SourceProductionContext context, GenerationModel model)
    {
        foreach (var diagnostic in model.Diagnostics) context.ReportDiagnostic(diagnostic);

        if (!model.HasModule) return;

        var templateModel = new
        {
            ContractVersion,
            ModuleTypeName = model.ModuleTypeName,
            model.Suffix,
            Registrations = model.Registrations.Select(static registration => new
            {
                registration.ViewModelTypeName,
                registration.ViewTypeName,
                registration.ParameterTypeName,
                RouteLiteral = SymbolDisplay.FormatLiteral(registration.Route, true)
            }).ToArray()
        };
        var source = TemplateRenderer.RenderNavigationViewModule(templateModel);

        context.AddSource(
            $"Navigation.ViewModule.{model.Suffix}.g.cs",
            SourceText.From(source, Encoding.UTF8));
    }

    internal static void EmitComposition(
        SourceProductionContext context,
        GenerationModel local,
        ImmutableArray<ReferencedModule> referencedModules,
        bool supportsFrameDependencyInjection)
    {
        var compatible = ValidateModules(
            context,
            referencedModules);
        var modules = ImmutableArray.CreateBuilder<string>();
        if (local.HasModule) modules.Add(local.ModuleTypeName);

        modules.AddRange(compatible.Select(static module => module.TypeName));

        var templateModel = new
        {
            local.Suffix,
            SupportsFrameDependencyInjection = supportsFrameDependencyInjection,
            Modules = modules.ToImmutable()
        };
        var source = TemplateRenderer.RenderNavigationComposition(templateModel);

        context.AddSource(
            $"Navigation.Composition.{local.Suffix}.g.cs",
            SourceText.From(source, Encoding.UTF8));
    }

    private static ImmutableArray<ReferencedModule> ValidateModules(
        SourceProductionContext context,
        ImmutableArray<ReferencedModule> modules)
    {
        var compatible = ImmutableArray.CreateBuilder<ReferencedModule>();
        foreach (var module in modules)
        {
            if (module.ContractVersion != ContractVersion)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.IncompatibleModule,
                    Location.None,
                    module.TypeName));
                continue;
            }

            compatible.Add(module);
        }

        return compatible.ToImmutable();
    }
}