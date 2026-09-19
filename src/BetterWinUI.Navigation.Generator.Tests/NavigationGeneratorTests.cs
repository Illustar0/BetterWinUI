using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;
using BetterWinUI.Navigation.Frame;
using BetterWinUI.Navigation.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.UI.Xaml.Controls;
using Xunit;

namespace BetterWinUI.Navigation.Generator.Tests;

/// <summary>Compiles generated modules and verifies selection and diagnostics across assemblies.</summary>
public sealed class NavigationGeneratorTests
{
    /// <summary>Verifies referenced defaults remain excluded while public named modules can be selected.</summary>
    [Fact]
    public void ReferencedModulesRequireExplicitSelection()
    {
        var feature = Generate("Feature_" + Guid.NewGuid().ToString("N"), """
            using BetterWinUI.Navigation;
            using Microsoft.UI.Xaml.Controls;
            namespace Feature;
            public sealed class DefaultViewModel;
            public sealed class NamedViewModel;
            [PageFor<DefaultViewModel>]
            public sealed class DefaultPage : Page;
            [PageModule]
            public sealed partial class FeaturePages;
            [PageFor<NamedViewModel>(Module = typeof(FeaturePages))]
            public sealed class NamedPage : Page;
            """);
        AssertNoErrors(feature);
        var featureImage = Emit(feature.Compilation);
        using var featureStream = new MemoryStream(featureImage);
        var featureAssembly = AssemblyLoadContext.Default.LoadFromStream(featureStream);
        var app = Generate("Consumer_" + Guid.NewGuid().ToString("N"), """
            using BetterWinUI.Navigation;
            using Microsoft.UI.Xaml.Controls;
            namespace Consumer;
            public sealed class LocalViewModel;
            [PageFor<LocalViewModel>]
            public sealed class LocalPage : Page;
            public static class Composition
            {
                public static PageMap Create()
                {
                    var pages = new PageMap();
                    pages.AddGeneratedPages();
                    pages.AddGeneratedPages<Feature.FeaturePages>();
                    return pages;
                }
            }
            """, MetadataReference.CreateFromImage(featureImage));
        AssertNoErrors(app);
        using var appStream = new MemoryStream(Emit(app.Compilation));
        var assembly = AssemblyLoadContext.Default.LoadFromStream(appStream);
        var map = (PageMap)assembly.GetType("Consumer.Composition", true)!.GetMethod("Create")!.Invoke(null, null)!;
        Assert.Equal(2, map.Count);
        Assert.Equal(assembly.GetType("Consumer.LocalPage", true),
            map.Resolve(assembly.GetType("Consumer.LocalViewModel", true)!));
        Assert.Equal(featureAssembly.GetType("Feature.NamedPage", true),
            map.Resolve(featureAssembly.GetType("Feature.NamedViewModel", true)!));
        Assert.False(map.ContainsKey(featureAssembly.GetType("Feature.DefaultViewModel", true)!));
    }

    /// <summary>Verifies identical ViewModel targets are legal in different modules but not in one group.</summary>
    [Fact]
    public void ConflictsAreScopedToOneModule()
    {
        var result = Generate("ConflictingModules", """
            using BetterWinUI.Navigation;
            using Microsoft.UI.Xaml.Controls;
            namespace Fixture;
            public sealed class Target;
            [PageModule] public sealed partial class Desktop;
            [PageModule] public sealed partial class Compact;
            [PageFor<Target>(Module = typeof(Desktop))] public sealed class A : Page;
            [PageFor<Target>(Module = typeof(Desktop))] public sealed class B : Page;
            [PageFor<Target>(Module = typeof(Compact))] public sealed class C : Page;
            public static class Composition
            {
                public static PageMap Create()
                {
                    var pages = new PageMap();
                    pages.AddGeneratedPages<Compact>();
                    return pages;
                }
            }
            """);
        Assert.Equal(2, result.Run.Diagnostics.Count(static diagnostic => string.Equals(diagnostic.Id, "BWNAV003", StringComparison.Ordinal)));
        Assert.DoesNotContain(result.Compilation.GetDiagnostics(TestContext.Current.CancellationToken), static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        using var stream = new MemoryStream(Emit(result.Compilation));
        var assembly = AssemblyLoadContext.Default.LoadFromStream(stream);
        var map = (PageMap)assembly.GetType("Fixture.Composition", true)!.GetMethod("Create")!.Invoke(null, null)!;
        Assert.Equal(assembly.GetType("Fixture.C", true), map.Resolve(assembly.GetType("Fixture.Target", true)!));
    }

    /// <summary>Verifies invalid Page and module declarations produce actionable generator diagnostics.</summary>
    [Theory]
    [InlineData("[PageFor<Target>] public sealed class NotPage;", "BWNAV001")]
    [InlineData("[PageFor<Target>] public abstract class AbstractPage : Page;", "BWNAV001")]
    [InlineData("[PageFor<Target>] public sealed class GenericPage<T> : Page;", "BWNAV001")]
    [InlineData("[PageModule] public sealed class MissingPartial;", "BWNAV002")]
    [InlineData("[PageModule] public static partial class StaticModule;", "BWNAV002")]
    [InlineData("public sealed class Unmarked; [PageFor<Target>(Module = typeof(Unmarked))] public sealed class A : Page;", "BWNAV002")]
    public void InvalidDeclarationsAreDiagnosed(string declaration, string diagnosticId)
    {
        var result = Generate("InvalidDeclaration", "using BetterWinUI.Navigation; using Microsoft.UI.Xaml.Controls; namespace Fixture; public sealed class Target; " + declaration);
        Assert.Contains(result.Run.Diagnostics, diagnostic => string.Equals(diagnostic.Id, diagnosticId, StringComparison.Ordinal));
        Assert.DoesNotContain(result.Compilation.GetDiagnostics(TestContext.Current.CancellationToken), static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    /// <summary>Verifies generated code supports escaped identifiers and global-namespace modules.</summary>
    [Fact]
    public void EscapedAndGlobalNamesCompile()
    {
        var result = Generate("EscapedModules", """
            using BetterWinUI.Navigation;
            using Microsoft.UI.Xaml.Controls;
            public sealed class Target;
            [PageModule] public sealed partial class @event;
            [PageFor<Target>(Module = typeof(@event))] public sealed class @class : Page;
            """);
        AssertNoErrors(result);
    }

    /// <summary>Verifies open calls accept any parameter while explicit strict calls enforce the marker.</summary>
    [Fact]
    public void ExplicitGenericOverloadEnforcesParameterAssociation()
    {
        const string source = """
            using BetterWinUI.Navigation;
            using BetterWinUI.Navigation.Frame;
            public sealed class Target;
            public sealed class Other;
            public sealed record Args : INavigationParameter<Target>;
            public static class Calls
            {
                public static void Navigate(FrameNavigator navigator)
                {
                    navigator.Navigate<Other>(new Args());
                    navigator.Navigate<Target, Args>(new Args());
                    navigator.Navigate<Target>(transitionInfo: null);
                }
            }
            """;
        AssertNoErrors(Generate("ValidCalls", source));
        var invalid = Generate("InvalidCalls", source.Replace("Navigate<Target, Args>", "Navigate<Other, Args>", StringComparison.Ordinal));
        Assert.Contains(invalid.Compilation.GetDiagnostics(TestContext.Current.CancellationToken), static diagnostic => string.Equals(diagnostic.Id, "CS0311", StringComparison.Ordinal));
    }

    /// <summary>Runs the production generator against a standalone compilation.</summary>
    private static (CSharpCompilation Compilation, GeneratorDriverRunResult Run) Generate(
        string name, string source, params MetadataReference[] extraReferences)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp12);
        var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(PageMap).Assembly.Location).Append(typeof(Page).Assembly.Location)
            .Append(typeof(FrameNavigator).Assembly.Location).Distinct(StringComparer.OrdinalIgnoreCase);
        var references = paths.Select(static path => MetadataReference.CreateFromFile(path)).Cast<MetadataReference>().Concat(extraReferences);
        var compilation = CSharpCompilation.Create(name, [CSharpSyntaxTree.ParseText(source, parseOptions)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new NavigationGenerator().AsSourceGenerator()], parseOptions: parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        return ((CSharpCompilation)output, driver.GetRunResult());
    }

    /// <summary>Asserts generated and handwritten sources both compile.</summary>
    private static void AssertNoErrors((CSharpCompilation Compilation, GeneratorDriverRunResult Run) result)
    {
        var errors = result.Run.Diagnostics.Concat(result.Compilation.GetDiagnostics())
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToImmutableArray();
        Assert.True(errors.IsEmpty, string.Join(Environment.NewLine, errors));
    }

    /// <summary>Emits an assembly used as a real cross-assembly module reference.</summary>
    private static byte[] Emit(Compilation compilation)
    {
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        return stream.ToArray();
    }
}
