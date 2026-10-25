using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace BetterWinUI.PageActivation.DependencyInjection.Generator;

internal static class SourceEmitter
{
    public static void EmitViewModule(
        SourceProductionContext context,
        ImmutableArray<ViewInfo> views,
        ImmutableArray<ViewModelInfo> viewModels,
        string assemblyName)
    {
        ReportDiagnostics(context, views.SelectMany(static view => view.Diagnostics));
        ReportDiagnostics(
            context,
            viewModels.SelectMany(static viewModel => viewModel.Diagnostics));

        var validViews = views
            .Where(static view => view.IsValid)
            .OrderBy(static view => view.TypeName, StringComparer.Ordinal)
            .ToImmutableArray();
        var validViewModels = viewModels
            .Where(static viewModel => viewModel.IsValid)
            .OrderBy(static viewModel => viewModel.TypeName, StringComparer.Ordinal)
            .ToImmutableArray();
        if (validViews.IsEmpty && validViewModels.IsEmpty) return;

        var registrations = validViewModels
            .Select(static viewModel => new ViewModelRegistrationTemplateModel(viewModel))
            .ToImmutableArray();
        var suffix = NameUtilities.CreateSuffix(assemblyName);
        var source = TemplateRenderer.Render(
            "ViewModule.scriban",
            new ViewModuleTemplateModel(
                EscapeXml(assemblyName),
                GetModuleName(assemblyName),
                validViews.Select(static view => new ViewTemplateModel(view)).ToImmutableArray(),
                registrations));

        context.AddSource(
            $"PageActivation.ViewModule.{suffix}.g.cs",
            SourceText.From(source, Encoding.UTF8));
    }

    /// <summary>
    /// Emits the composition root of the assembly that declares the hook application.
    /// </summary>
    public static void EmitCompositionRoot(
        SourceProductionContext context,
        DiagnosticLocation hookApplication,
        bool hasLocalModule,
        ImmutableArray<ReferencedViewModule> referencedModules,
        string assemblyName)
    {
        var compatibleModules =
            ValidateReferencedModules(context, hookApplication, referencedModules);
        var modules = GetModules(assemblyName, hasLocalModule, compatibleModules);
        var suffix = NameUtilities.CreateSuffix(assemblyName);

        var source = TemplateRenderer.Render("CompositionRoot.scriban", new
        {
            AssemblyName = EscapeXml(assemblyName),
            Suffix = suffix,
            Modules = modules
        });

        context.AddSource(
            $"PageActivation.CompositionRoot.{suffix}.g.cs",
            SourceText.From(source, Encoding.UTF8));
    }

    private static string GetModuleName(string assemblyName) =>
        $"PageActivationViewModule_{NameUtilities.CreateSuffix(assemblyName)}";

    private static ImmutableArray<string> GetModules(
        string assemblyName,
        bool hasLocalModule,
        ImmutableArray<ReferencedViewModule> referencedModules)
    {
        var modules = ImmutableArray.CreateBuilder<string>();

        if (hasLocalModule)
            modules.Add(
                "global::" + MetadataNames.GeneratedNamespace + "." + GetModuleName(assemblyName));

        foreach (var module in referencedModules) modules.Add(module.ModuleTypeName);
        return modules.ToImmutable();
    }

    private static ImmutableArray<ReferencedViewModule> ValidateReferencedModules(
        SourceProductionContext context,
        DiagnosticLocation hookApplication,
        ImmutableArray<ReferencedViewModule> modules)
    {
        var compatible = ImmutableArray.CreateBuilder<ReferencedViewModule>();
        foreach (var module in modules)
        {
            if (!module.IsValid)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.IncompatibleModule,
                    hookApplication.ToLocation(),
                    module.ModuleTypeName));
                continue;
            }

            compatible.Add(module);
        }

        return compatible.ToImmutable();
    }

    private static void ReportDiagnostics(
        SourceProductionContext context,
        IEnumerable<DiagnosticInfo> diagnostics)
    {
        foreach (var diagnostic in diagnostics) context.ReportDiagnostic(diagnostic.ToDiagnostic());
    }

    /// <summary>Escapes an assembly name for the XML documentation comments in the generated source.</summary>
    private static string EscapeXml(string value)
    {
        return value.Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }
}

// Template models are classes with public properties because Scriban reads public members by reflection.
internal sealed class ViewModuleTemplateModel
{
    public ViewModuleTemplateModel(
        string assemblyName,
        string moduleName,
        ImmutableArray<ViewTemplateModel> views,
        ImmutableArray<ViewModelRegistrationTemplateModel> viewModels)
    {
        AssemblyName = assemblyName;
        ModuleName = moduleName;
        Views = views;
        ViewModels = viewModels;
    }

    public string ModuleNamespace { get; } = MetadataNames.GeneratedNamespace;

    public string AssemblyName { get; }

    public string ModuleName { get; }

    public ImmutableArray<ViewTemplateModel> Views { get; }

    public ImmutableArray<ViewModelRegistrationTemplateModel> ViewModels { get; }
}

internal sealed class ViewTemplateModel
{
    public ViewTemplateModel(ViewInfo view)
    {
        ViewTypeName = view.TypeName;
        LifetimeName = ServiceLifetimeValues.GetName(view.Lifetime);
    }

    public string ViewTypeName { get; }

    public string LifetimeName { get; }
}

internal sealed class ViewModelRegistrationTemplateModel
{
    public ViewModelRegistrationTemplateModel(ViewModelInfo viewModel)
    {
        TypeName = viewModel.TypeName;
        LifetimeName = ServiceLifetimeValues.GetName(viewModel.Lifetime);
    }

    public string TypeName { get; }

    public string LifetimeName { get; }
}
