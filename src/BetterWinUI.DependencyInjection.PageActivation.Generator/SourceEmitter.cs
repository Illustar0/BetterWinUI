using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace BetterWinUI.DependencyInjection.PageActivation.Generator;

/// <summary>
/// Validates value-only incremental models and renders generated source templates.
/// </summary>
internal static class SourceEmitter
{
    private const int ContractVersion = 1;

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
        if (validViews.IsDefaultOrEmpty && validViewModels.IsDefaultOrEmpty) return;

        var registrations = validViewModels
            .Select(static viewModel => new ViewModelRegistrationTemplateModel(viewModel))
            .ToImmutableArray();
        var suffix = NameUtilities.CreateSuffix(assemblyName);
        var source = TemplateRenderer.Render(
            "ViewModule.scriban",
            new ViewModuleTemplateModel(
                ContractVersion,
                EscapeXml(assemblyName),
                $"PageActivationViewModule_{suffix}",
                validViews.Select(static view => new ViewTemplateModel(view)).ToImmutableArray(),
                registrations));

        context.AddSource(
            $"PageActivation.ViewModule.{suffix}.g.cs",
            SourceText.From(source, Encoding.UTF8));
    }

    /// <summary>
    /// Emits the application adapter and its composition root.
    /// </summary>
    public static void EmitApplication(
        SourceProductionContext context,
        ImmutableArray<AppModel> apps,
        bool hasLocalModule,
        ImmutableArray<ReferencedViewModule> referencedModules,
        ImmutableArray<string> initializationCalls,
        XamlContractModel xamlContract)
    {
        ReportDiagnostics(context, apps.SelectMany(static app => app.Diagnostics));

        if (apps.Length > 1)
        {
            foreach (var candidate in apps)
                Report(
                    context,
                    new DiagnosticInfo(DiagnosticKind.MultipleApps, candidate.Location));

            return;
        }

        if (apps.IsDefaultOrEmpty || !apps[0].IsValid) return;

        var app = apps[0];
        var compatibleModules =
            ValidateReferencedModules(context, app, referencedModules);
        var modules = GetModules(app, hasLocalModule, compatibleModules);

        if (!initializationCalls.Contains(app.FullName, StringComparer.Ordinal))
            Report(
                context,
                new DiagnosticInfo(
                    DiagnosticKind.MissingInitializationCall,
                    app.Location,
                    app.FullName));

        if (app.ProviderPropertyName is not null &&
            (!xamlContract.IsAvailable || !xamlContract.HasRequiredInterceptors))
        {
            Report(
                context,
                new DiagnosticInfo(
                    DiagnosticKind.UnsupportedXamlContract,
                    app.Location));
            return;
        }

        var source = app.ProviderPropertyName is null
            ? RenderApplicationStub(app, modules)
            : RenderApplication(app, modules, xamlContract);

        context.AddSource(
            $"{app.Name}.PageActivation.g.cs",
            SourceText.From(source, Encoding.UTF8));
    }

    private static string RenderApplicationStub(
        AppModel app,
        ImmutableArray<string> modules)
    {
        return TemplateRenderer.Render(
            "ApplicationStub.scriban",
            new ApplicationStubTemplateModel(
                app.Namespace,
                app.Name,
                app.FullName,
                app.Suffix,
                modules));
    }

    private static string RenderApplication(
        AppModel app,
        ImmutableArray<string> modules,
        XamlContractModel xamlContract)
    {
        return TemplateRenderer.Render(
            "Application.scriban",
            new ApplicationTemplateModel(
                app.Namespace,
                app.Name,
                EscapeXml(app.FullName),
                app.Suffix,
                app.ProviderPropertyName!,
                modules,
                xamlContract.MetadataProviderInterfaceName,
                xamlContract.XamlTypeInterfaceName,
                xamlContract.MetadataProviderMembers,
                xamlContract.XamlTypeMembers));
    }

    private static ImmutableArray<string> GetModules(
        AppModel app,
        bool hasLocalModule,
        ImmutableArray<ReferencedViewModule> referencedModules)
    {
        var modules = ImmutableArray.CreateBuilder<string>();

        if (hasLocalModule)
            modules.Add(
                "global::BetterWinUI.DependencyInjection.PageActivation.Generated." +
                $"PageActivationViewModule_{NameUtilities.CreateSuffix(app.AssemblyName)}");

        foreach (var module in referencedModules) modules.Add(module.ModuleTypeName);
        return modules.ToImmutable();
    }

    private static ImmutableArray<ReferencedViewModule> ValidateReferencedModules(
        SourceProductionContext context,
        AppModel app,
        ImmutableArray<ReferencedViewModule> modules)
    {
        var compatible = ImmutableArray.CreateBuilder<ReferencedViewModule>();
        foreach (var module in modules)
        {
            if (module.ContractVersion != ContractVersion)
            {
                Report(
                    context,
                    new DiagnosticInfo(
                        DiagnosticKind.IncompatibleModule,
                        app.Location,
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
        foreach (var diagnostic in diagnostics) Report(context, diagnostic);
    }

    private static void Report(
        SourceProductionContext context,
        DiagnosticInfo diagnostic)
    {
        var descriptor = DiagnosticDescriptors.Get(diagnostic.Kind);
        var created = diagnostic.Argument is null
            ? Diagnostic.Create(descriptor, diagnostic.Location.ToLocation())
            : Diagnostic.Create(
                descriptor,
                diagnostic.Location.ToLocation(),
                diagnostic.Argument);
        context.ReportDiagnostic(created);
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
        int contractVersion,
        string assemblyName,
        string moduleName,
        ImmutableArray<ViewTemplateModel> views,
        ImmutableArray<ViewModelRegistrationTemplateModel> viewModels)
    {
        ContractVersion = contractVersion;
        AssemblyName = assemblyName;
        ModuleName = moduleName;
        Views = views;
        ViewModels = viewModels;
    }

    /// <summary>Gets the generated contract version.</summary>
    public int ContractVersion { get; }

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

/// <summary>
/// Supplies values used by the early application stub template.
/// </summary>
internal sealed class ApplicationStubTemplateModel
{
    /// <summary>Initializes an application stub template model.</summary>
    public ApplicationStubTemplateModel(
        string @namespace,
        string appName,
        string appFullName,
        string suffix,
        ImmutableArray<string> modules)
    {
        Namespace = @namespace;
        AppName = appName;
        AppFullName = appFullName;
        Suffix = suffix;
        Modules = modules;
    }

    /// <summary>Gets the application namespace.</summary>
    public string Namespace { get; }

    /// <summary>Gets the application type name.</summary>
    public string AppName { get; }

    /// <summary>Gets the fully qualified application type name.</summary>
    public string AppFullName { get; }

    /// <summary>Gets the deterministic generated name suffix.</summary>
    public string Suffix { get; }

    /// <summary>Gets the registration modules composed by the application.</summary>
    public ImmutableArray<string> Modules { get; }
}

/// <summary>
/// Supplies values used by the complete application adapter template.
/// </summary>
internal sealed class ApplicationTemplateModel
{
    /// <summary>Initializes an application adapter template model.</summary>
    public ApplicationTemplateModel(
        string @namespace,
        string appName,
        string appFullName,
        string suffix,
        string providerPropertyName,
        ImmutableArray<string> modules,
        string metadataProviderInterfaceName,
        string xamlTypeInterfaceName,
        ImmutableArray<InterfaceMemberModel> metadataProviderMembers,
        ImmutableArray<InterfaceMemberModel> xamlTypeMembers)
    {
        Namespace = @namespace;
        AppName = appName;
        AppFullName = appFullName;
        Suffix = suffix;
        ProviderPropertyName = providerPropertyName;
        Modules = modules;
        MetadataProviderInterfaceName = metadataProviderInterfaceName;
        XamlTypeInterfaceName = xamlTypeInterfaceName;
        MetadataProviderMembers = metadataProviderMembers;
        XamlTypeMembers = xamlTypeMembers;
    }

    /// <summary>Gets the application namespace.</summary>
    public string Namespace { get; }

    /// <summary>Gets the application type name.</summary>
    public string AppName { get; }

    /// <summary>Gets the XML-escaped fully qualified application type name.</summary>
    public string AppFullName { get; }

    /// <summary>Gets the deterministic generated name suffix.</summary>
    public string Suffix { get; }

    /// <summary>Gets the native XAML provider property name.</summary>
    public string ProviderPropertyName { get; }

    /// <summary>Gets the generated registration module type names.</summary>
    public ImmutableArray<string> Modules { get; }

    /// <summary>Gets the referenced metadata provider interface name.</summary>
    public string MetadataProviderInterfaceName { get; }

    /// <summary>Gets the referenced XAML type interface name.</summary>
    public string XamlTypeInterfaceName { get; }

    /// <summary>Gets the metadata provider members to implement.</summary>
    public ImmutableArray<InterfaceMemberModel> MetadataProviderMembers { get; }

    /// <summary>Gets the XAML type members to implement.</summary>
    public ImmutableArray<InterfaceMemberModel> XamlTypeMembers { get; }
}