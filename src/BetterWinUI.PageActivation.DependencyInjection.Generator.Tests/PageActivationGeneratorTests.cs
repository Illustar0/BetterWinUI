using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace BetterWinUI.PageActivation.DependencyInjection.Generator.Tests;

/// <summary>
/// Verifies generator output, diagnostics, and compile-time composition.
/// </summary>
public sealed class PageActivationGeneratorTests
{
    private const string ApplicationMustBePartialDiagnostic = "BWPH0001";
    private const string InvalidApplicationDiagnostic = "BWPH0002";
    private const string MultipleApplicationsDiagnostic = "BWPH0003";
    private const string MissingXamlProviderDiagnostic = "BWPH0004";
    private const string ActivationConflictDiagnostic = "BWPH0005";
    private const string UnsupportedXamlContractDiagnostic = "BWPH0006";
    private const string InvalidViewDiagnostic = "BWPA0002";
    private const string InvalidViewModelDiagnostic = "BWPA0003";
    private const string IncompatibleModuleDiagnostic = "BWPA0004";
    private const string MissingPagePublicConstructorDiagnostic = "BWPA0006";
    private const string UnsupportedLifetimeDiagnostic = "BWPA0007";
    private const string MissingConversionDiagnostic = "CS0266";
    private const string ProviderInstallation = "this.UsePageActivation(services);";
    private const string InitializeDeclaration = "public void Initialize(System.IServiceProvider services)";

    [Fact]
    public void Generator_UnrelatedEditWithReferencedModule_ReusesCachedOutputs()
    {
        var winUiReference = CreateWinUiReference();
        var viewsReference = CreateFeatureViewsReference(winUiReference);
        var compilation = GeneratorTestHost.CreateCompilation(
            "IncrementalApp",
            [(GeneratorTestHost.ValidApplicationSource, "App.cs")],
            [winUiReference, viewsReference]);
        var driver = GeneratorTestHost.CreateDriver(trackIncrementalSteps: true)
            .RunGeneratorsAndUpdateCompilation(compilation, out _, out _, TestContext.Current.CancellationToken);

        driver = driver.RunGeneratorsAndUpdateCompilation(
            GeneratorTestHost.AddUnrelatedSource(compilation), out var output, out _, TestContext.Current.CancellationToken);

        new GenerationResult((CSharpCompilation)output, driver.GetRunResult()).ShouldHaveNoErrors();
        var outputs = driver.GetRunResult().Results[0].TrackedOutputSteps.Values
            .SelectMany(static steps => steps)
            .SelectMany(static step => step.Outputs)
            .ToArray();
        outputs.ShouldNotBeEmpty();
        outputs.ShouldAllBe(static step => step.Reason == IncrementalStepRunReason.Cached);
    }

    [Fact]
    public void Generator_LibraryWithoutWinUiAfterEdit_ReportsNoErrors()
    {
        var compilation = GeneratorTestHost.CreateCompilation(
            "ViewModelLibrary",
            [(
                """
                using BetterWinUI.PageActivation.DependencyInjection;
                using Microsoft.Extensions.DependencyInjection;
                namespace Fixture;
                [ViewModel(ServiceLifetime.Transient)] public sealed class MainViewModel;
                """,
                "MainViewModel.cs")]);
        var driver = GeneratorTestHost.CreateDriver()
            .RunGeneratorsAndUpdateCompilation(compilation, out _, out _, TestContext.Current.CancellationToken);

        driver = driver.RunGeneratorsAndUpdateCompilation(
            GeneratorTestHost.AddUnrelatedSource(compilation), out var output, out _, TestContext.Current.CancellationToken);

        new GenerationResult((CSharpCompilation)output, driver.GetRunResult()).ShouldHaveNoErrors();
    }

    [Fact]
    public void GeneratePageActivationHook_EscapedNameInMarkupPass_Compiles()
    {
        var appSource = GeneratorTestHost.ValidApplicationSource.Replace("class App", "class @event", StringComparison.Ordinal);

        var result = GeneratorTestHost.Run(
            "EscapedApplicationMarkupPass",
            (GeneratorTestHost.WinUiStubs, "WinUI.cs"),
            (appSource, "App.cs"));

        result.ShouldHaveNoErrors();
    }

    [Fact]
    public void GeneratePageActivationHook_EscapedNameInFullPass_Compiles()
    {
        var appSource = GeneratorTestHost.ValidApplicationSource.Replace("class App", "class @event", StringComparison.Ordinal);
        var providerSource = GeneratorTestHost.NativeProviderSource.Replace("class App", "class @event", StringComparison.Ordinal);

        var result = GeneratorTestHost.Run(
            "EscapedApplicationFullPass",
            (GeneratorTestHost.WinUiStubs, "WinUI.cs"),
            (appSource, "App.cs"),
            (providerSource, "XamlTypeInfo.g.cs"));

        result.ShouldHaveNoErrors();
    }

    [Fact]
    public void GeneratePageActivationHook_ValidApplication_Compiles()
    {
        var result = GeneratorTestHost.GenerateValidApplication();

        result.ShouldHaveNoErrors();
    }

    [Fact]
    public void UsePageActivation_ExplicitFactory_Compiles()
    {
        var source = GeneratorTestHost.ValidApplicationSource.Replace(
            ProviderInstallation,
            "this.UsePageActivation(pageType => new ManualPage(new ManualViewModel()));",
            StringComparison.Ordinal);

        var result = GeneratorTestHost.Run(
            "ExplicitFactory",
            (GeneratorTestHost.WinUiStubs, "WinUI.cs"),
            (source, "App.cs"),
            (GeneratorTestHost.NativeProviderSource, "XamlTypeInfo.g.cs"));

        result.ShouldHaveNoErrors();
    }

    [Fact]
    public void UsePageActivation_FactoryReturningObject_ReportsMissingConversion()
    {
        var source = GeneratorTestHost.ValidApplicationSource.Replace(
            ProviderInstallation,
            "this.UsePageActivation(pageType => new object());",
            StringComparison.Ordinal);

        var result = GeneratorTestHost.Run(
            "InvalidFactory",
            (GeneratorTestHost.WinUiStubs, "WinUI.cs"),
            (source, "App.cs"));

        result.OutputCompilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .ShouldContain(static diagnostic => string.Equals(diagnostic.Id, MissingConversionDiagnostic, StringComparison.Ordinal));
    }

    [Fact]
    public void GeneratePageActivationHook_UserFactoryOverload_ReportsActivationConflict()
    {
        var result = GeneratorTestHost.Run(
            "ConflictingActivation",
            (GeneratorTestHost.WinUiStubs, "WinUI.cs"),
            ("""
             using BetterWinUI.PageActivation;
             using Microsoft.UI.Xaml;
             namespace Fixture;
             [GeneratePageActivationHook]
             public sealed partial class App : Application
             {
                 public void UsePageActivation(System.Func<System.Type, Microsoft.UI.Xaml.Controls.Page> factory) { }
             }
             """, "App.cs"));

        result.ShouldReportDiagnostic(ActivationConflictDiagnostic);
    }

    [Theory]
    [InlineData("[GeneratePageActivationHook] public sealed class App : Application;", ApplicationMustBePartialDiagnostic)]
    [InlineData("[GeneratePageActivationHook] public sealed partial class App;", InvalidApplicationDiagnostic)]
    [InlineData("[GeneratePageActivationHook] public abstract partial class App : Application;", InvalidApplicationDiagnostic)]
    [InlineData("[GeneratePageActivationHook] public sealed partial class App<T> : Application;", InvalidApplicationDiagnostic)]
    [InlineData("public sealed class Outer { [GeneratePageActivationHook] public sealed partial class App : Application; }", InvalidApplicationDiagnostic)]
    [InlineData("[GeneratePageActivationHook] public sealed partial class First : Application; [GeneratePageActivationHook] public sealed partial class Second : Application;", MultipleApplicationsDiagnostic)]
    public void GeneratePageActivationHook_UnsupportedDeclaration_ReportsDiagnostic(string declaration, string diagnosticId)
    {
        var source = $"""
            using BetterWinUI.PageActivation;
            using Microsoft.UI.Xaml;
            namespace Fixture;
            {declaration}
            """;

        var result = GeneratorTestHost.Run(
            "UnsupportedApplication",
            (GeneratorTestHost.WinUiStubs, "WinUI.cs"),
            (source, "App.cs"));

        result.ShouldReportDiagnostic(diagnosticId);
    }

    [Fact]
    public void GeneratePageActivationHook_InterfaceTypedNativeProvider_Compiles()
    {
        var providerSource = GeneratorTestHost.NativeProviderSource.Replace(
            "private NativeProvider _AppProvider { get; } = new();",
            "private IXamlMetadataProvider _AppProvider { get; } = new NativeProvider();",
            StringComparison.Ordinal);

        var result = GeneratorTestHost.Run(
            "InterfaceTypedNativeProvider",
            (GeneratorTestHost.WinUiStubs, "WinUI.cs"),
            (GeneratorTestHost.ValidApplicationSource, "App.cs"),
            (providerSource, "XamlTypeInfo.g.cs"));

        result.ShouldHaveNoErrors();
    }

    [Fact]
    public void GetXamlType_AdditionalPrivateProviderProperty_UsesNativeProvider()
    {
        var result = GeneratorTestHost.Run(
            "AdditionalProvider",
            (GeneratorTestHost.WinUiStubs, "WinUI.cs"),
            (GeneratorTestHost.ValidApplicationSource, "App.cs"),
            ("""
             namespace Fixture;
             public sealed partial class App
             {
                 private Microsoft.UI.Xaml.Markup.IXamlMetadataProvider OtherProvider { get; } = null!;
             }
             """, "UserProvider.cs"),
            (GeneratorTestHost.NativeProviderSource, "XamlTypeInfo.g.cs"));
        using var stream = new MemoryStream(GeneratorTestHost.Emit(result.OutputCompilation));
        var assembly = System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromStream(stream);
        var app = Activator.CreateInstance(assembly.GetType("Fixture.App", true)!)!;
        var getXamlType = assembly.GetType("Microsoft.UI.Xaml.Markup.IXamlMetadataProvider", true)!
            .GetMethod("GetXamlType", [typeof(Type)])!;

        var metadata = getXamlType.Invoke(app, [assembly.GetType("Fixture.MainPage", true)]);

        metadata.ShouldNotBeNull();
    }

    [Fact]
    public void AddGeneratedPageServices_MarkupPass_Compiles()
    {
        var result = GeneratorTestHost.Run(
            "EarlyGeneration",
            (GeneratorTestHost.WinUiStubs, "WinUI.cs"),
            (GeneratorTestHost.ValidApplicationSource, "App.cs"));

        result.ShouldHaveNoErrors();
    }

    [Fact]
    public void AddGeneratedPageServices_ScopedAttributes_RegistersScopedDescriptors()
    {
        var source = GeneratorTestHost.ValidApplicationSource
            .Replace("ServiceLifetime.Singleton", "ServiceLifetime.Scoped", StringComparison.Ordinal);
        var result = GeneratorTestHost.Run(
            "ScopedRegistrations",
            (GeneratorTestHost.WinUiStubs, "WinUI.cs"),
            (source, "App.cs"),
            (GeneratorTestHost.NativeProviderSource, "XamlTypeInfo.g.cs"));

        var services = result.ConfigureServices("Fixture.App");

        services.ShouldSatisfyAllConditions(
            () => services.ShouldContain(descriptor => IsRegistration(descriptor, "Fixture.SingletonPage", ServiceLifetime.Scoped)),
            () => services.ShouldContain(descriptor => IsRegistration(descriptor, "Fixture.SingletonViewModel", ServiceLifetime.Scoped)));
    }

    [Fact]
    public void View_PrivateConstructor_ReportsMissingPublicConstructor()
    {
        var result = GeneratorTestHost.Run(
            "MissingPageConstructorDiagnostic",
            (GeneratorTestHost.WinUiStubs, "WinUI.cs"),
            (
                """
                using BetterWinUI.PageActivation.DependencyInjection;
                using Microsoft.UI.Xaml.Controls;
                namespace Fixture;
                [View]
                public sealed class View : Page
                {
                    private View() { }
                }
                """,
                "MissingPageConstructor.cs"));

        result.ShouldReportDiagnostic(MissingPagePublicConstructorDiagnostic);
    }

    [Theory]
    [InlineData("[View((ServiceLifetime)42)] public sealed class View : Page;")]
    [InlineData("[ViewModel((ServiceLifetime)42)] public sealed class ViewModel;")]
    public void Registration_UndefinedLifetime_ReportsUnsupportedLifetime(string declaration)
    {
        var source = $"""
            using BetterWinUI.PageActivation.DependencyInjection;
            using Microsoft.Extensions.DependencyInjection;
            using Microsoft.UI.Xaml.Controls;
            namespace Fixture;
            {declaration}
            """;

        var result = GeneratorTestHost.Run(
            "UnsupportedLifetimeDiagnostic",
            (GeneratorTestHost.WinUiStubs, "WinUI.cs"),
            (source, "UnsupportedLifetime.cs"));

        result.ShouldReportDiagnostic(UnsupportedLifetimeDiagnostic);
    }

    [Fact]
    public void GeneratePageActivationHook_XamlOutputWithoutProvider_ReportsMissingXamlProvider()
    {
        var result = GeneratorTestHost.Run(
            "MissingProviderDiagnostic",
            (GeneratorTestHost.WinUiStubs, "WinUI.cs"),
            (
                """
                using BetterWinUI.PageActivation;
                using Microsoft.UI.Xaml;
                namespace Fixture;
                [GeneratePageActivationHook] public sealed partial class App : Application;
                """,
                "App.cs"),
            ("namespace Fixture; internal sealed class XamlMarker;", "XamlTypeInfo.g.cs"));

        result.ShouldReportDiagnostic(MissingXamlProviderDiagnostic);
    }

    [Fact]
    public void GeneratePageActivationHook_XamlTypeWithoutActivateInstance_ReportsUnsupportedContract()
    {
        var winUiStubs = GeneratorTestHost.WinUiStubs.Replace(
            "object ActivateInstance();",
            "object CreateInstance();",
            StringComparison.Ordinal);

        var result = GeneratorTestHost.Run(
            "UnsupportedContractDiagnostic",
            (winUiStubs, "WinUI.cs"),
            (GeneratorTestHost.ValidApplicationSource, "App.cs"),
            (GeneratorTestHost.NativeProviderSource, "XamlTypeInfo.g.cs"));

        result.ShouldReportDiagnostic(UnsupportedXamlContractDiagnostic);
    }

    [Fact]
    public void UsePageActivation_UnrelatedOverloadAndDelegateInstallation_ReportsNoDiagnostics()
    {
        var source = GeneratorTestHost.ValidApplicationSource
            .Replace(ProviderInstallation, "((System.Action<System.IServiceProvider>)this.UsePageActivation)(services);", StringComparison.Ordinal)
            .Replace(InitializeDeclaration, $"public void UsePageActivation() {{ }}\n    {InitializeDeclaration}", StringComparison.Ordinal);

        var result = GeneratorTestHost.Run(
            "DeferredInstallation",
            (GeneratorTestHost.WinUiStubs, "WinUI.cs"),
            (source, "App.cs"),
            (GeneratorTestHost.NativeProviderSource, "XamlTypeInfo.g.cs"));

        result.ShouldSatisfyAllConditions(
            result.ShouldCompile,
            () => result.RunResult.Diagnostics.ShouldBeEmpty());
    }

    [Fact]
    public void UsePageActivation_UserProviderOverload_CompilesWithoutDiagnostics()
    {
        var source = GeneratorTestHost.ValidApplicationSource.Replace(
            InitializeDeclaration,
            $"public void UsePageActivation(System.IServiceProvider services) {{ }}\n    {InitializeDeclaration}",
            StringComparison.Ordinal);

        var result = GeneratorTestHost.Run(
            "UserProviderOverload",
            (GeneratorTestHost.WinUiStubs, "WinUI.cs"),
            (source, "App.cs"));

        result.ShouldSatisfyAllConditions(
            result.ShouldCompile,
            () => result.RunResult.Diagnostics.ShouldBeEmpty());
    }

    [Fact]
    public void ViewAndViewModel_UnnameableTypes_ReportDiagnosticsAndCompile()
    {
        var result = GeneratorTestHost.Run(
            "InvalidRegistrationTypes",
            (GeneratorTestHost.WinUiStubs, "WinUI.cs"),
            (
                """
                using BetterWinUI.PageActivation.DependencyInjection;
                using Microsoft.Extensions.DependencyInjection;
                using Microsoft.UI.Xaml.Controls;
                namespace Fixture;

                public sealed class GenericContainer<T>
                {
                    [View]
                    public sealed class NestedPage : Page;

                    [ViewModel(ServiceLifetime.Transient)]
                    public sealed class NestedViewModel;
                }

                public sealed class PrivateContainer
                {
                    [View]
                    private sealed class NestedPage : Page;

                    [ViewModel(ServiceLifetime.Transient)]
                    private sealed class NestedViewModel;
                }

                [View]
                file sealed class FilePage : Page;

                [ViewModel(ServiceLifetime.Transient)]
                file sealed class FileViewModel;
                """,
                "InvalidRegistrationTypes.cs"));

        result.ShouldSatisfyAllConditions(
            () => result.ShouldReportDiagnostic(InvalidViewDiagnostic),
            () => result.ShouldReportDiagnostic(InvalidViewModelDiagnostic),
            result.ShouldCompile);
    }

    [Fact]
    public void AddGeneratedPageServices_ReferencedModule_RegistersLocalAndReferencedServices()
    {
        var winUiReference = CreateWinUiReference();
        var viewsReference = CreateFeatureViewsReference(winUiReference);
        var application = GenerateComposedApplication(winUiReference, viewsReference);

        var services = application.ConfigureServices("Composition.App");

        services.ShouldSatisfyAllConditions(
            () => services.ShouldContain(descriptor => IsRegistration(descriptor, "Feature.FeaturePage", ServiceLifetime.Singleton)),
            () => services.ShouldContain(descriptor => IsRegistration(descriptor, "Feature.FeatureViewModel", ServiceLifetime.Singleton)),
            () => services.ShouldContain(descriptor => IsRegistration(descriptor, "Composition.LocalPage", ServiceLifetime.Transient)));
    }

    [Theory]
    [InlineData("public class Module { public static void Register(IServiceCollection services) { } }")]
    [InlineData("public class Module { public static void Register(IServiceCollection services) { } public static void Register(object services) { } }")]
    public void AddGeneratedPageServices_ModuleWithRegistrationContract_Compiles(string declaration)
    {
        var result = GenerateModuleConsumer(declaration);

        result.ShouldHaveNoErrors();
    }

    [Theory]
    [InlineData("public class Module { }")]
    [InlineData("public class Module { public void Register(IServiceCollection services) { } }")]
    [InlineData("public class Module { private static void Register(IServiceCollection services) { } }")]
    [InlineData("public class Module { public static int Register(IServiceCollection services) => 0; }")]
    [InlineData("public class Module { public static void Register<T>(IServiceCollection services) { } }")]
    [InlineData("public class Module { public static void Register(object services) { } }")]
    [InlineData("public class Module { public static void Register(ref IServiceCollection services) { } }")]
    [InlineData("public class Module { public static void Register(IServiceCollection services, bool option = false) { } }")]
    [InlineData("internal class Module { public static void Register(IServiceCollection services) { } }")]
    public void AddGeneratedPageServices_ModuleWithoutRegistrationContract_ReportsIncompatibleModule(string declaration)
    {
        var result = GenerateModuleConsumer(declaration);

        result.ShouldSatisfyAllConditions(
            () => result.ShouldReportDiagnostic(IncompatibleModuleDiagnostic),
            result.ShouldCompile);
    }

    /// <summary>Matches a generated service registration by implementation name and lifetime.</summary>
    private static bool IsRegistration(ServiceDescriptor descriptor, string serviceTypeName, ServiceLifetime lifetime) =>
        string.Equals(descriptor.ServiceType.FullName, serviceTypeName, StringComparison.Ordinal) &&
        descriptor.Lifetime == lifetime;

    /// <summary>Generates the valid application against a referenced assembly declaring one module.</summary>
    private static GenerationResult GenerateModuleConsumer(string moduleDeclaration)
    {
        var moduleSource = $$"""
            using BetterWinUI.PageActivation.DependencyInjection;
            using Microsoft.Extensions.DependencyInjection;
            [assembly: PageActivationViewModule(typeof(Module))]
            {{moduleDeclaration}}
            """;
        var module = GeneratorTestHost.CreateCompilation(
            "Module_" + Guid.NewGuid().ToString("N"), [(moduleSource, "Module.cs")]);
        var reference = MetadataReference.CreateFromImage(GeneratorTestHost.Emit(module));
        return GeneratorTestHost.Run(GeneratorTestHost.CreateCompilation(
            "ModuleConsumer",
            [(GeneratorTestHost.WinUiStubs, "WinUI.cs"),
             (GeneratorTestHost.ValidApplicationSource, "App.cs"),
             (GeneratorTestHost.NativeProviderSource, "XamlTypeInfo.g.cs")],
            [reference]));
    }

    /// <summary>
    /// Creates the WinUI contract reference used by cross-assembly generator tests.
    /// </summary>
    private static MetadataReference CreateWinUiReference()
    {
        var image = GeneratorTestHost.Emit(
            GeneratorTestHost.CreateCompilation(
                "WinUI.Abstractions_" + Guid.NewGuid().ToString("N"),
                [(GeneratorTestHost.WinUiStubs, "WinUI.cs")]));
        using var stream = new MemoryStream(image);
        _ = System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromStream(stream);
        return MetadataReference.CreateFromImage(image);
    }

    /// <summary>
    /// Creates a referenced assembly containing generated page registrations.
    /// </summary>
    private static MetadataReference CreateFeatureViewsReference(
        MetadataReference winUiReference)
    {
        var views = GeneratorTestHost.Run(
            GeneratorTestHost.CreateCompilation(
                "Feature.Views_" + Guid.NewGuid().ToString("N"),
                [
                    (
                        """
                        using BetterWinUI.PageActivation.DependencyInjection;
                        using Microsoft.Extensions.DependencyInjection;
                        using Microsoft.UI.Xaml.Controls;
                        namespace Feature;
                        [View(ServiceLifetime.Singleton)]
                        public sealed class FeaturePage : Page;
                        [ViewModel(ServiceLifetime.Singleton)]
                        public sealed class FeatureViewModel;
                        """,
                        "Feature.cs")
                ],
                [winUiReference]));
        views.ShouldHaveNoErrors();
        var image = GeneratorTestHost.Emit(views.OutputCompilation);
        using var stream = new MemoryStream(image);
        _ = System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromStream(stream);
        return MetadataReference.CreateFromImage(image);
    }

    /// <summary>
    /// Generates an application that composes local and referenced registrations.
    /// </summary>
    private static GenerationResult GenerateComposedApplication(
        MetadataReference winUiReference,
        MetadataReference viewsReference)
    {
        return GeneratorTestHost.Run(
            GeneratorTestHost.CreateCompilation(
                "Composition.App",
                [
                    (
                        """
                        using BetterWinUI.PageActivation.DependencyInjection;
                        using BetterWinUI.PageActivation;
                        using Microsoft.UI.Xaml;
                        namespace Composition;
                        [GeneratePageActivationHook]
                        public sealed partial class App : Application
                        {
                            public static Microsoft.Extensions.DependencyInjection.IServiceCollection Configure(
                                Microsoft.Extensions.DependencyInjection.IServiceCollection services) =>
                                services.AddGeneratedPageServices();
                            public void Initialize(System.IServiceProvider services) =>
                                this.UsePageActivation(services);
                        }
                        """,
                        "App.cs"),
                    (
                        """
                        using Microsoft.UI.Xaml.Markup;
                        namespace Composition;
                        public sealed partial class App
                        {
                            private NativeProvider _AppProvider { get; } = new();
                        }
                        internal sealed class NativeProvider : IXamlMetadataProvider
                        {
                            public IXamlType GetXamlType(System.Type type) => null!;
                            public IXamlType GetXamlType(string fullName) => null!;
                            public XmlnsDefinition[] GetXmlnsDefinitions() => [];
                        }
                        """,
                        "XamlTypeInfo.g.cs"),
                    (
                        """
                        using BetterWinUI.PageActivation.DependencyInjection;
                        using Microsoft.UI.Xaml.Controls;
                        namespace Composition;
                        [View]
                        public sealed class LocalPage : Page;
                        """,
                        "LocalPage.cs")
                ],
                [winUiReference, viewsReference]));
    }
}
