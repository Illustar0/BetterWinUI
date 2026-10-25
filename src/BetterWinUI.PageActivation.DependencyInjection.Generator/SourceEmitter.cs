using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace BetterWinUI.PageActivation.DependencyInjection.Generator;

/// <summary>
/// Validates value-only incremental models and renders generated source templates.
/// </summary>
internal static class SourceEmitter
{
    /// <summary>
    /// Emits the registration module exported by an assembly containing attributed types.
    /// </summary>
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
    /// <param name="context">The source production context.</param>
    /// <param name="hookApplication">The location of the only hook application in the assembly.</param>
    /// <param name="hasLocalModule">Whether this assembly exports a generated registration module.</param>
    /// <param name="referencedModules">The generated registration modules exported by references.</param>
    /// <param name="assemblyName">The name of the composing assembly.</param>
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

    private static string EscapeXml(string value)
    {
        return value.Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }
}

/// <summary>
/// Supplies values used by the generated view module template.
/// </summary>
internal sealed class ViewModuleTemplateModel
{
    /// <summary>Initializes a view module template model.</summary>
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

    /// <summary>Gets the namespace containing generated modules.</summary>
    public string ModuleNamespace { get; } = MetadataNames.GeneratedNamespace;

    /// <summary>Gets the XML-escaped assembly name.</summary>
    public string AssemblyName { get; }

    /// <summary>Gets the generated module name.</summary>
    public string ModuleName { get; }

    /// <summary>Gets the valid generated view registrations.</summary>
    public ImmutableArray<ViewTemplateModel> Views { get; }

    /// <summary>Gets the deduplicated generated view model registrations.</summary>
    public ImmutableArray<ViewModelRegistrationTemplateModel> ViewModels { get; }
}

/// <summary>
/// Supplies one view registration to a Scriban template.
/// </summary>
internal sealed class ViewTemplateModel
{
    /// <summary>Initializes a view template model.</summary>
    public ViewTemplateModel(ViewInfo view)
    {
        ViewTypeName = view.TypeName;
        LifetimeName = ServiceLifetimeValues.GetName(view.Lifetime);
    }

    /// <summary>Gets the fully qualified Page type name.</summary>
    public string ViewTypeName { get; }

    /// <summary>Gets the Microsoft DI lifetime member name.</summary>
    public string LifetimeName { get; }
}

/// <summary>
/// Supplies one deduplicated view model registration to a Scriban template.
/// </summary>
internal sealed class ViewModelRegistrationTemplateModel
{
    /// <summary>Initializes a view model registration template model.</summary>
    public ViewModelRegistrationTemplateModel(ViewModelInfo viewModel)
    {
        TypeName = viewModel.TypeName;
        LifetimeName = ServiceLifetimeValues.GetName(viewModel.Lifetime);
    }

    /// <summary>Gets the fully qualified view model type name.</summary>
    public string TypeName { get; }

    /// <summary>Gets the Microsoft DI lifetime member name.</summary>
    public string LifetimeName { get; }
}
