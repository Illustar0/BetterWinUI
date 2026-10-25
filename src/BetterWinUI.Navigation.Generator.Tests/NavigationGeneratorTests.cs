using System.Reflection;
using System.Runtime.Loader;
using BetterWinUI.Navigation.Frame;
using BetterWinUI.Navigation.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.UI.Xaml.Controls;
using Shouldly;
using Xunit;

namespace BetterWinUI.Navigation.Generator.Tests;

/// <summary>Compiles generated modules and verifies selection and diagnostics across assemblies.</summary>
public sealed class NavigationGeneratorTests
{
    private const string InvalidPageDiagnostic = "BWNAV001";
    private const string InvalidModuleDiagnostic = "BWNAV002";
    private const string ConflictingMappingDiagnostic = "BWNAV003";
    private const string UnsatisfiedConstraintDiagnostic = "CS0311";

    private const string ConflictingModulesSource = """
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
        """;

    private const string NavigateCallsSource = """
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

    /// <summary>An unrelated source edit reuses every cached generator output.</summary>
    [Fact]
    public void Generator_UnrelatedEdit_ReusesCachedOutputs()
    {
        var compilation = CreateCompilation("IncrementalPages", """
            using BetterWinUI.Navigation;
            using Microsoft.UI.Xaml.Controls;
            namespace Fixture;
            public sealed class Target;
            [PageFor<Target>] public sealed class MainPage : Page;
            """);
        var parseOptions = (CSharpParseOptions)compilation.SyntaxTrees.First().Options;
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new NavigationGenerator().AsSourceGenerator()],
            parseOptions: parseOptions,
            driverOptions: new GeneratorDriverOptions(default, trackIncrementalGeneratorSteps: true));
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _, TestContext.Current.CancellationToken);
        var edited = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(
            "namespace Fixture; internal sealed class Unrelated;",
            parseOptions,
            cancellationToken: TestContext.Current.CancellationToken));

        driver = driver.RunGeneratorsAndUpdateCompilation(edited, out var output, out _, TestContext.Current.CancellationToken);

        ShouldHaveNoErrors(((CSharpCompilation)output, driver.GetRunResult()));
        var outputs = driver.GetRunResult().Results[0].TrackedOutputSteps.Values
            .SelectMany(static steps => steps)
            .SelectMany(static step => step.Outputs)
            .ToArray();
        outputs.ShouldNotBeEmpty();
        outputs.ShouldAllBe(static result => result.Reason == IncrementalStepRunReason.Cached);
    }

    /// <summary>A consumer adds its local defaults and explicitly selected referenced modules only.</summary>
    [Fact]
    public void AddGeneratedPages_ReferencedAssembly_AddsOnlySelectedNamedModule()
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
        ShouldHaveNoErrors(feature);
        var featureImage = Emit(feature.Compilation);
        var featureAssembly = Load(featureImage);
        var consumer = Generate("Consumer_" + Guid.NewGuid().ToString("N"), """
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
        ShouldHaveNoErrors(consumer);
        var consumerAssembly = Load(Emit(consumer.Compilation));

        var map = CreatePageMap(consumerAssembly, "Consumer.Composition");

        map.ShouldSatisfyAllConditions(
            () => map.Count.ShouldBe(2),
            () => map.Resolve(consumerAssembly.GetType("Consumer.LocalViewModel", true)!)
                .ShouldBe(consumerAssembly.GetType("Consumer.LocalPage", true)),
            () => map.Resolve(featureAssembly.GetType("Feature.NamedViewModel", true)!)
                .ShouldBe(featureAssembly.GetType("Feature.NamedPage", true)),
            () => map.ContainsKey(featureAssembly.GetType("Feature.DefaultViewModel", true)!).ShouldBeFalse());
    }

    /// <summary>Two mappings for one ViewModel in the same module are both reported.</summary>
    [Fact]
    public void PageFor_DuplicateTargetInOneModule_ReportsEachConflict()
    {
        var result = Generate("ConflictingModules", ConflictingModulesSource);

        result.Run.Diagnostics.Count(static diagnostic => HasId(diagnostic, ConflictingMappingDiagnostic)).ShouldBe(2);
    }

    /// <summary>A conflict in one module does not affect the same ViewModel in another module.</summary>
    [Fact]
    public void AddGeneratedPages_ModuleWithoutConflict_MapsItsPage()
    {
        var result = Generate("IndependentModules", ConflictingModulesSource);
        var assembly = Load(Emit(result.Compilation));

        var map = CreatePageMap(assembly, "Fixture.Composition");

        map.Resolve(assembly.GetType("Fixture.Target", true)!).ShouldBe(assembly.GetType("Fixture.C", true));
    }

    /// <summary>Invalid Page and module declarations report a generator diagnostic and still compile.</summary>
    [Theory]
    [InlineData("[PageFor<Target>] public sealed class NotPage;", InvalidPageDiagnostic)]
    [InlineData("[PageFor<Target>] public abstract class AbstractPage : Page;", InvalidPageDiagnostic)]
    [InlineData("[PageFor<Target>] public sealed class GenericPage<T> : Page;", InvalidPageDiagnostic)]
    [InlineData("[PageModule] public sealed class MissingPartial;", InvalidModuleDiagnostic)]
    [InlineData("[PageModule] public static partial class StaticModule;", InvalidModuleDiagnostic)]
    [InlineData("public sealed class Unmarked; [PageFor<Target>(Module = typeof(Unmarked))] public sealed class A : Page;", InvalidModuleDiagnostic)]
    [InlineData("[PageFor<Target>(Module = typeof(int[]))] public sealed class A : Page;", InvalidModuleDiagnostic)]
    public void Generator_InvalidDeclaration_ReportsDiagnosticWithoutCompilationErrors(string declaration, string diagnosticId)
    {
        var source = $"using BetterWinUI.Navigation; using Microsoft.UI.Xaml.Controls; namespace Fixture; public sealed class Target; {declaration}";

        var result = Generate("InvalidDeclaration", source);

        result.ShouldSatisfyAllConditions(
            () => result.Run.Diagnostics.ShouldContain(diagnostic => HasId(diagnostic, diagnosticId)),
            () => result.Compilation.GetDiagnostics(TestContext.Current.CancellationToken)
                .ShouldNotContain(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
    }

    /// <summary>Generated code compiles for escaped identifiers in the global namespace.</summary>
    [Fact]
    public void PageModule_EscapedNamesInGlobalNamespace_Compiles()
    {
        var result = Generate("EscapedModules", """
            using BetterWinUI.Navigation;
            using Microsoft.UI.Xaml.Controls;
            public sealed class Target;
            [PageModule] public sealed partial class @event;
            [PageFor<Target>(Module = typeof(@event))] public sealed class @class : Page;
            """);

        ShouldHaveNoErrors(result);
    }

    /// <summary>Open navigation accepts any parameter and the strict overload accepts an associated one.</summary>
    [Fact]
    public void Navigate_OpenAndAssociatedParameters_Compile()
    {
        var result = Generate("ValidCalls", NavigateCallsSource);

        ShouldHaveNoErrors(result);
    }

    /// <summary>The strict overload rejects a parameter associated with another ViewModel.</summary>
    [Fact]
    public void NavigateWithAssociatedParameter_OtherTarget_ReportsUnsatisfiedConstraint()
    {
        var source = NavigateCallsSource.Replace("Navigate<Target, Args>", "Navigate<Other, Args>", StringComparison.Ordinal);

        var result = Generate("InvalidCalls", source);

        result.Compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .ShouldContain(static diagnostic => HasId(diagnostic, UnsatisfiedConstraintDiagnostic));
    }

    /// <summary>Runs the production generator against a standalone compilation.</summary>
    private static (CSharpCompilation Compilation, GeneratorDriverRunResult Run) Generate(
        string name, string source, params MetadataReference[] extraReferences)
    {
        var compilation = CreateCompilation(name, source, extraReferences);
        var parseOptions = (CSharpParseOptions)compilation.SyntaxTrees.First().Options;
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new NavigationGenerator().AsSourceGenerator()], parseOptions: parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _, TestContext.Current.CancellationToken);
        return ((CSharpCompilation)output, driver.GetRunResult());
    }

    /// <summary>Creates an independent consumer compilation for generator tests.</summary>
    private static CSharpCompilation CreateCompilation(string name, string source, params MetadataReference[] extraReferences)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp12);
        var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(PageMap).Assembly.Location).Append(typeof(Page).Assembly.Location)
            .Append(typeof(FrameNavigator).Assembly.Location).Distinct(StringComparer.OrdinalIgnoreCase);
        var references = paths.Select(static path => MetadataReference.CreateFromFile(path)).Cast<MetadataReference>().Concat(extraReferences);
        return CSharpCompilation.Create(name, [CSharpSyntaxTree.ParseText(source, parseOptions, cancellationToken: TestContext.Current.CancellationToken)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    }

    /// <summary>Asserts that generated and handwritten sources both compile.</summary>
    private static void ShouldHaveNoErrors((CSharpCompilation Compilation, GeneratorDriverRunResult Run) result) =>
        result.Run.Diagnostics.Concat(result.Compilation.GetDiagnostics(TestContext.Current.CancellationToken))
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ShouldBeEmpty();

    /// <summary>Matches a diagnostic identifier exactly.</summary>
    private static bool HasId(Diagnostic diagnostic, string id) =>
        string.Equals(diagnostic.Id, id, StringComparison.Ordinal);

    /// <summary>Emits an assembly used as a real cross-assembly module reference.</summary>
    private static byte[] Emit(Compilation compilation)
    {
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream, cancellationToken: TestContext.Current.CancellationToken);
        result.Success.ShouldBeTrue(string.Join(Environment.NewLine, result.Diagnostics));
        return stream.ToArray();
    }

    /// <summary>Loads an emitted fixture assembly into the default context.</summary>
    private static Assembly Load(byte[] image)
    {
        using var stream = new MemoryStream(image);
        return AssemblyLoadContext.Default.LoadFromStream(stream);
    }

    /// <summary>Calls a fixture composition root that returns a Page map.</summary>
    private static PageMap CreatePageMap(Assembly assembly, string compositionType) =>
        (PageMap)assembly.GetType(compositionType, true)!.GetMethod("Create")!.Invoke(null, null)!;
}
