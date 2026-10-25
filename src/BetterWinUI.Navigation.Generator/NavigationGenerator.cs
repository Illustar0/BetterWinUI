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
            static (input, token) => PageInfo.Create(input, token)).Collect();
        var modules = context.SyntaxProvider.ForAttributeWithMetadataName(Prefix + "PageModuleAttribute",
            static (node, _) => node is TypeDeclarationSyntax,
            static (input, token) => ModuleInfo.Create((INamedTypeSymbol)input.TargetSymbol, token)).Collect();
        var compilationInfo = context.CompilationProvider.Select(static (compilation, _) =>
            new CompilationInfo(compilation.AssemblyName ?? "Assembly",
                compilation.GetTypeByMetadataName(Prefix + "PageMap") is not null));
        context.RegisterSourceOutput(pages.Combine(modules).Combine(compilationInfo),
            static (output, input) => Generate(output, input.Right, input.Left.Left, input.Left.Right));
    }

    /// <summary>Validates and emits the groups declared in this compilation.</summary>
    private static void Generate(SourceProductionContext output, CompilationInfo compilation,
        ImmutableArray<PageInfo> pages, ImmutableArray<ModuleInfo> modules)
    {
        if (!compilation.HasPageMap) return;
        var validModules = GetValidModules(output, modules);
        var groups = GroupMappings(output, pages, validModules);
        var suffix = NameUtilities.CreateSuffix(compilation.AssemblyName);
        foreach (var group in groups.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            var registrations = GetNonConflictingMappings(output, group.Value);
            var source = group.Key.Length == 0 ? EmitDefault(suffix, registrations) : EmitModule(validModules[group.Key], registrations);
            output.AddSource("Pages." + NameUtilities.CreateSuffix(group.Key + suffix) + ".g.cs", SourceText.From(source, Encoding.UTF8));
        }
    }

    /// <summary>Reports unsupported module declarations and indexes valid local modules.</summary>
    private static Dictionary<string, ModuleInfo> GetValidModules(
        SourceProductionContext output, ImmutableArray<ModuleInfo> modules)
    {
        var declarations = modules.GroupBy(static module => module.FullName, StringComparer.Ordinal)
            .Select(static group => group.First()).ToArray();
        foreach (var module in declarations.Where(static module => !module.IsValid))
            output.ReportDiagnostic(Diagnostic.Create(InvalidModule, module.Location.ToLocation(), module.FullName));
        return declarations.Where(static module => module.IsValid)
            .ToDictionary(static module => module.FullName, static module => module, StringComparer.Ordinal);
    }

    /// <summary>Collects Page mappings into the default group and explicitly named modules.</summary>
    private static Dictionary<string, List<Mapping>> GroupMappings(SourceProductionContext output,
        ImmutableArray<PageInfo> pages, Dictionary<string, ModuleInfo> validModules)
    {
        var groups = validModules.Keys.Append(string.Empty)
            .ToDictionary(static name => name, static _ => new List<Mapping>(), StringComparer.Ordinal);
        foreach (var mapping in pages.Distinct().SelectMany(static page => page.Mappings))
            AddMapping(output, mapping, validModules, groups);
        return groups;
    }

    /// <summary>Reports duplicate targets within one group and orders the remaining mappings.</summary>
    private static Mapping[] GetNonConflictingMappings(SourceProductionContext output, List<Mapping> mappings)
    {
        var conflicts = mappings.GroupBy(static mapping => mapping.ViewModel, StringComparer.Ordinal)
            .Where(static group => group.Count() > 1).ToArray();
        foreach (var mapping in conflicts.SelectMany(static group => group))
            output.ReportDiagnostic(Diagnostic.Create(Conflict, mapping.Location.ToLocation(), mapping.ViewModel));
        var conflictingTargets = new HashSet<string>(conflicts.Select(static group => group.Key), StringComparer.Ordinal);
        return mappings.Where(mapping => !conflictingTargets.Contains(mapping.ViewModel))
            .OrderBy(static mapping => mapping.ViewModel, StringComparer.Ordinal).ToArray();
    }

    /// <summary>Adds one valid attribute to its local mapping group.</summary>
    private static void AddMapping(SourceProductionContext output, Mapping mapping,
        Dictionary<string, ModuleInfo> modules, Dictionary<string, List<Mapping>> groups)
    {
        var location = mapping.Location.ToLocation();
        if (!mapping.IsValidPage)
        {
            output.ReportDiagnostic(Diagnostic.Create(InvalidPage, location, mapping.Page));
            return;
        }
        if (!mapping.IsValidModuleType)
        {
            output.ReportDiagnostic(Diagnostic.Create(InvalidModule, location, mapping.ModuleName));
            return;
        }
        var group = mapping.ModuleName;
        if (group.Length > 0 && (!modules.TryGetValue(group, out var localModule) ||
                                 !string.Equals(mapping.ModuleAssemblyIdentity, localModule.AssemblyIdentity,
                                     StringComparison.Ordinal)))
        {
            output.ReportDiagnostic(Diagnostic.Create(InvalidModule, location, group));
            return;
        }
        groups[group].Add(mapping);
    }

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

    /// <summary>Formats a source-safe type name.</summary>
    private static string Name(ITypeSymbol type) => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    /// <summary>Emits the public module contract in the declaring assembly.</summary>
    private static string EmitModule(ModuleInfo module, Mapping[] mappings)
    {
        return TemplateRenderer.Render("PageModule.scriban", new
        {
            module.Namespace,
            ModuleName = "@" + module.SimpleName,
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

    /// <summary>Stores only stable compilation facts needed by emission.</summary>
    private readonly record struct CompilationInfo(string AssemblyName, bool HasPageMap);

    /// <summary>Stores a module declaration without retaining a Roslyn symbol.</summary>
    private readonly record struct ModuleInfo(
        string FullName,
        string Namespace,
        string SimpleName,
        string AssemblyIdentity,
        bool IsValid,
        SourceLocation Location)
    {
        internal static ModuleInfo Create(INamedTypeSymbol module, CancellationToken token) => new(
            NavigationGenerator.Name(module),
            module.ContainingNamespace.IsGlobalNamespace ? string.Empty : module.ContainingNamespace.ToDisplayString(),
            module.Name,
            module.ContainingAssembly.Identity.ToString(),
            IsValidModule(module, token),
            SourceLocation.Create(module.Locations[0]));

        /// <summary>Checks declarations that receive a generated module implementation.</summary>
        private static bool IsValidModule(INamedTypeSymbol module, CancellationToken token) =>
            module.TypeKind == TypeKind.Class && !module.IsRecord && !module.IsAbstract && !module.IsStatic &&
            module.Arity == 0 && module.ContainingType is null && Accessible(module) && module.GetMembers("Register").Length == 0 &&
            module.DeclaringSyntaxReferences.All(reference => reference.GetSyntax(token) is ClassDeclarationSyntax declaration &&
                declaration.Modifiers.Any(SyntaxKind.PartialKeyword));
    }

    /// <summary>Stores one mapping candidate and its diagnostics as values.</summary>
    private readonly record struct Mapping(
        string ViewModel,
        string Page,
        string ModuleName,
        string ModuleAssemblyIdentity,
        bool IsValidPage,
        bool IsValidModuleType,
        SourceLocation Location);

    /// <summary>Collects all mapping attributes on a Page into an equatable model.</summary>
    private sealed class PageInfo(ImmutableArray<Mapping> mappings) : IEquatable<PageInfo>
    {
        internal ImmutableArray<Mapping> Mappings { get; } = mappings;

        internal static PageInfo Create(GeneratorAttributeSyntaxContext context, CancellationToken token)
        {
            var page = (INamedTypeSymbol)context.TargetSymbol;
            var compilation = context.SemanticModel.Compilation;
            var pageForAttribute = compilation.GetTypeByMetadataName(Prefix + "PageForAttribute`1");
            var pageName = Name(page);
            var pageIsValid = !page.IsAbstract && Accessible(page) &&
                              DerivesFrom(page, compilation.GetTypeByMetadataName("Microsoft.UI.Xaml.Controls.Page"));
            var mappings = ImmutableArray.CreateBuilder<Mapping>();
            foreach (var attribute in page.GetAttributes().Where(attribute =>
                         SymbolEqualityComparer.Default.Equals(attribute.AttributeClass?.OriginalDefinition, pageForAttribute)))
            {
                var location = attribute.ApplicationSyntaxReference!.GetSyntax(token).GetLocation();
                var viewModel = attribute.AttributeClass!.TypeArguments[0];
                var moduleValue = attribute.NamedArguments.FirstOrDefault(static pair =>
                    string.Equals(pair.Key, "Module", StringComparison.Ordinal)).Value.Value;
                var module = moduleValue as INamedTypeSymbol;
                mappings.Add(new Mapping(
                    Name(viewModel),
                    pageName,
                    moduleValue is ITypeSymbol moduleType ? Name(moduleType) : string.Empty,
                    module?.ContainingAssembly.Identity.ToString() ?? string.Empty,
                    pageIsValid && Accessible(viewModel),
                    moduleValue is null or INamedTypeSymbol,
                    SourceLocation.Create(location)));
            }

            return new PageInfo(mappings.ToImmutable());
        }

        /// <summary>Checks inheritance from the compilation's WinUI Page type.</summary>
        private static bool DerivesFrom(INamedTypeSymbol type, INamedTypeSymbol? baseType)
        {
            for (var current = type.BaseType; current is not null; current = current.BaseType)
                if (SymbolEqualityComparer.Default.Equals(current, baseType)) return true;
            return false;
        }

        public bool Equals(PageInfo? other) => other is not null && Mappings.SequenceEqual(other.Mappings);

        public override bool Equals(object? obj) => obj is PageInfo other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = 0;
                foreach (var mapping in Mappings) hashCode = (hashCode * 397) ^ mapping.GetHashCode();
                return hashCode;
            }
        }
    }

    /// <summary>Recreates diagnostic locations without keeping syntax trees in the pipeline.</summary>
    private readonly record struct SourceLocation(string Path, TextSpan Span, LinePositionSpan LineSpan)
    {
        internal static SourceLocation Create(Location location)
        {
            var lineSpan = location.GetLineSpan();
            return new SourceLocation(lineSpan.Path, location.SourceSpan, lineSpan.Span);
        }

        internal Location ToLocation() => Location.Create(Path, Span, LineSpan);
    }
}
