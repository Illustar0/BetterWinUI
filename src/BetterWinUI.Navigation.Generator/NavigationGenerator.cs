using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace BetterWinUI.Navigation.Generator;

/// <summary>Generates explicitly selected Page modules and an assembly-local default group.</summary>
[Generator(LanguageNames.CSharp)]
public sealed class NavigationGenerator : IIncrementalGenerator
{
    private const string NavigationNamespace = "BetterWinUI.Navigation";
    private const string Prefix = NavigationNamespace + ".";
    private static readonly DiagnosticDescriptor InvalidPage = new(
        "BWNAV001", "Invalid Page mapping", "Mapping '{0}' must use accessible, closed types and a concrete Page subclass",
        NavigationNamespace, DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor InvalidModule = new(
        "BWNAV002", "Invalid Page module", "Module '{0}' must be a local, accessible, non-generic, non-abstract, non-static top-level partial class marked PageModule, without a Register member",
        NavigationNamespace, DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor Conflict = new(
        "BWNAV003", "Conflicting Page mappings", "ViewModel '{0}' has multiple mappings in the same Page module",
        NavigationNamespace, DiagnosticSeverity.Error, true);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var pages = context.SyntaxProvider.ForAttributeWithMetadataName(Prefix + "PageForAttribute`1",
            static (node, _) => node is TypeDeclarationSyntax,
            static (input, _) => (INamedTypeSymbol)input.TargetSymbol).Collect();
        var modules = context.SyntaxProvider.ForAttributeWithMetadataName(Prefix + "PageModuleAttribute",
            static (node, _) => node is TypeDeclarationSyntax,
            static (input, _) => (INamedTypeSymbol)input.TargetSymbol).Collect();
        context.RegisterSourceOutput(pages.Combine(modules).Combine(context.CompilationProvider),
            static (output, input) => Generate(output, input.Right, input.Left.Left, input.Left.Right));
    }

    /// <summary>Validates and emits the groups declared in this compilation.</summary>
    private static void Generate(SourceProductionContext output, Compilation compilation,
        ImmutableArray<INamedTypeSymbol> pages, ImmutableArray<INamedTypeSymbol> modules)
    {
        if (compilation.GetTypeByMetadataName(Prefix + "PageMap") is null) return;
        var validModules = GetValidModules(output, modules);
        var groups = GroupMappings(output, pages, validModules);
        var suffix = NameUtilities.CreateSuffix(compilation.AssemblyName ?? "Assembly");
        foreach (var group in groups.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            var registrations = GetNonConflictingMappings(output, group.Value);
            var source = group.Key.Length == 0 ? EmitDefault(suffix, registrations) : EmitModule(validModules[group.Key], registrations);
            output.AddSource("Pages." + NameUtilities.CreateSuffix(group.Key + suffix) + ".g.cs", SourceText.From(source, Encoding.UTF8));
        }
    }

    /// <summary>Reports unsupported module declarations and indexes valid local modules.</summary>
    private static Dictionary<string, INamedTypeSymbol> GetValidModules(
        SourceProductionContext output, ImmutableArray<INamedTypeSymbol> modules)
    {
        var declarations = modules.Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default)
            .Select(static module => (Module: module, IsValid: IsValidModule(module))).ToArray();
        foreach (var module in declarations.Where(static declaration => !declaration.IsValid)
                     .Select(static declaration => declaration.Module))
            output.ReportDiagnostic(Diagnostic.Create(InvalidModule, module.Locations[0], Name(module)));
        return declarations.Where(static declaration => declaration.IsValid)
            .ToDictionary(static declaration => Name(declaration.Module), static declaration => declaration.Module, StringComparer.Ordinal);
    }

    /// <summary>Collects Page mappings into the default group and explicitly named modules.</summary>
    private static Dictionary<string, List<Mapping>> GroupMappings(SourceProductionContext output,
        ImmutableArray<INamedTypeSymbol> pages, Dictionary<string, INamedTypeSymbol> validModules)
    {
        var groups = validModules.Keys.Append(string.Empty)
            .ToDictionary(static name => name, static _ => new List<Mapping>(), StringComparer.Ordinal);
        var declarations = pages.Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default)
            .SelectMany(static page => page.GetAttributes().Where(IsPageForAttribute),
                static (page, attribute) => (Page: page, Attribute: attribute));
        foreach (var declaration in declarations)
            AddMapping(output, declaration.Page, declaration.Attribute, validModules, groups);
        return groups;
    }

    /// <summary>Recognizes the Page mapping attribute without matching unrelated generic attributes.</summary>
    private static bool IsPageForAttribute(AttributeData attribute) =>
        attribute.AttributeClass is { MetadataName: "PageForAttribute`1" } attributeClass &&
        string.Equals(attributeClass.ContainingNamespace.ToDisplayString(), NavigationNamespace, StringComparison.Ordinal);

    /// <summary>Reports duplicate targets within one group and orders the remaining mappings.</summary>
    private static Mapping[] GetNonConflictingMappings(SourceProductionContext output, List<Mapping> mappings)
    {
        var conflicts = mappings.GroupBy(static mapping => mapping.ViewModel, StringComparer.Ordinal)
            .Where(static group => group.Count() > 1).ToArray();
        foreach (var mapping in conflicts.SelectMany(static group => group))
            output.ReportDiagnostic(Diagnostic.Create(Conflict, mapping.Location, mapping.ViewModel));
        var conflictingTargets = new HashSet<string>(conflicts.Select(static group => group.Key), StringComparer.Ordinal);
        return mappings.Where(mapping => !conflictingTargets.Contains(mapping.ViewModel))
            .OrderBy(static mapping => mapping.ViewModel, StringComparer.Ordinal).ToArray();
    }

    /// <summary>Adds one valid attribute to its local mapping group.</summary>
    private static void AddMapping(SourceProductionContext output, INamedTypeSymbol page, AttributeData attribute,
        Dictionary<string, INamedTypeSymbol> modules, Dictionary<string, List<Mapping>> groups)
    {
        var location = attribute.ApplicationSyntaxReference?.GetSyntax(output.CancellationToken).GetLocation() ?? page.Locations[0];
        var viewModel = attribute.AttributeClass!.TypeArguments[0];
        if (page.IsAbstract || !Accessible(page) || !DerivesFromPage(page) || !Accessible(viewModel))
        {
            output.ReportDiagnostic(Diagnostic.Create(InvalidPage, location, Name(page)));
            return;
        }
        var module = attribute.NamedArguments.FirstOrDefault(static pair => string.Equals(pair.Key, "Module", StringComparison.Ordinal)).Value.Value as INamedTypeSymbol;
        var group = module is null ? string.Empty : Name(module);
        if (module is not null && (!modules.TryGetValue(group, out var localModule) ||
                                  !SymbolEqualityComparer.Default.Equals(module, localModule)))
        {
            output.ReportDiagnostic(Diagnostic.Create(InvalidModule, location, group));
            return;
        }
        groups[group].Add(new Mapping(Name(viewModel), Name(page), location));
    }

    /// <summary>Checks declarations that receive a generated module implementation.</summary>
    private static bool IsValidModule(INamedTypeSymbol module) =>
        module.TypeKind == TypeKind.Class && !module.IsRecord && !module.IsAbstract && !module.IsStatic &&
        module.Arity == 0 && module.ContainingType is null && Accessible(module) && module.GetMembers("Register").Length == 0 &&
        module.DeclaringSyntaxReferences.All(static reference => reference.GetSyntax() is ClassDeclarationSyntax declaration &&
            declaration.Modifiers.Any(SyntaxKind.PartialKeyword));

    /// <summary>Checks that namespace-level generated code can name a closed type.</summary>
    private static bool Accessible(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol array) return Accessible(array.ElementType);
        if (type is not INamedTypeSymbol named) return false;
        for (var current = named; current is not null; current = current.ContainingType)
            if (current.IsFileLocal || current.IsUnboundGenericType ||
                current.DeclaredAccessibility is not Accessibility.Public and not Accessibility.Internal and not Accessibility.ProtectedOrInternal ||
                current.TypeArguments.Any(static argument => !Accessible(argument))) return false;
        return true;
    }

    /// <summary>Checks WinUI Page inheritance without loading the assembly.</summary>
    private static bool DerivesFromPage(INamedTypeSymbol type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
            if (string.Equals(current.ToDisplayString(), "Microsoft.UI.Xaml.Controls.Page", StringComparison.Ordinal)) return true;
        return false;
    }

    /// <summary>Formats a source-safe type name.</summary>
    private static string Name(ITypeSymbol type) => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    /// <summary>Emits the public module contract in the declaring assembly.</summary>
    private static string EmitModule(INamedTypeSymbol module, Mapping[] mappings)
    {
        return TemplateRenderer.Render("PageModule.scriban", new
        {
            Namespace = module.ContainingNamespace.IsGlobalNamespace ? string.Empty : module.ContainingNamespace.ToDisplayString(),
            ModuleName = "@" + module.Name,
            Mappings = mappings.Select(static mapping => new { mapping.ViewModel, mapping.Page }).ToArray()
        });
    }

    /// <summary>Emits an assembly-private default group and its local extension method.</summary>
    private static string EmitDefault(string suffix, Mapping[] mappings)
    {
        return TemplateRenderer.Render("DefaultPages.scriban", new
        {
            Suffix = suffix,
            Mappings = mappings.Select(static mapping => new { mapping.ViewModel, mapping.Page }).ToArray()
        });
    }

    /// <summary>Stores one validated mapping for emission.</summary>
    private sealed class Mapping(string viewModel, string page, Location location)
    {
        internal string ViewModel { get; } = viewModel;
        internal string Page { get; } = page;
        internal Location Location { get; } = location;
    }
}
