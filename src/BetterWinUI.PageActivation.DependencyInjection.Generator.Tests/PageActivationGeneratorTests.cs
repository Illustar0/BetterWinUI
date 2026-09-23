using BetterWinUI.PageActivation.DependencyInjection;
using BetterWinUI.PageActivation;
using BetterWinUI.PageActivation.DependencyInjection.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BetterWinUI.PageActivation.DependencyInjection.Generator.Tests;

/// <summary>
/// Verifies generator output, diagnostics, and compile-time composition.
/// </summary>
public sealed class PageActivationGeneratorTests
{
    /// <summary>Both factory and provider overloads compile with an application-defined factory.</summary>
    [Fact]
    public void ExplicitFactoryCanReplaceDependencyInjectionPolicy()
    {
        var source = GeneratorTestHost.ValidApplicationSource.Replace(
            "this.UsePageActivation(services);",
            "this.UsePageActivation(pageType => new ManualPage(new ManualViewModel()));",
            StringComparison.Ordinal);
        var result = GeneratorTestHost.Run(
            "ExplicitFactory",
            (GeneratorTestHost.WinUiStubs, "WinUI.cs"),
            (source, "App.cs"),
            (GeneratorTestHost.NativeProviderSource, "XamlTypeInfo.g.cs"));
        result.AssertNoErrors();
    }

    /// <summary>The generated factory requires a Page rather than an arbitrary object.</summary>
    [Fact]
    public void FactoryReturnTypeIsCheckedAtCompileTime()
    {
        var source = GeneratorTestHost.ValidApplicationSource.Replace(
            "this.UsePageActivation(services);",
            "this.UsePageActivation(pageType => new object());",
            StringComparison.Ordinal);
        var result = GeneratorTestHost.Run(
            "InvalidFactory",
            (GeneratorTestHost.WinUiStubs, "WinUI.cs"),
            (source, "App.cs"));
        Assert.Contains(result.OutputCompilation.GetDiagnostics(TestContext.Current.CancellationToken),
            static diagnostic => string.Equals(diagnostic.Id, "CS0266", StringComparison.Ordinal));
    }

    /// <summary>A user-defined method cannot silently replace the generated activation entry point.</summary>
    [Fact]
    public void ConflictingActivationMethodIsDiagnosed()
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
        result.AssertGeneratorDiagnostic("BWPH0005");
    }

    /// <summary>
    /// Verifies the generated source contract.
    /// </summary>
    [Fact]
    public void GeneratedAdapterAndRegistrationsCompile()
    {
        var result = GeneratorTestHost.GenerateValidApplication();
        result.AssertNoErrors();

    }

    /// <summary>
    /// Verifies an interface-typed native XAML provider is accepted.
    /// </summary>
    [Fact]
    public void InterfaceTypedNativeProviderIsAccepted()
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

        result.AssertNoErrors();
        Assert.DoesNotContain(
            result.RunResult.Diagnostics,
            static diagnostic => string.Equals(
                diagnostic.Id,
                "BWPH0004",
                StringComparison.Ordinal));

    }

    /// <summary>An unrelated private provider property must not hide WinUI's generated provider.</summary>
    [Fact]
    public void UserProviderPropertyDoesNotHideNativeProvider()
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
        result.AssertNoErrors();

        using var stream = new MemoryStream(GeneratorTestHost.Emit(result.OutputCompilation));
        var assembly = System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromStream(stream);
        var app = Activator.CreateInstance(assembly.GetType("Fixture.App", true)!)!;
        var providerType = assembly.GetType("Microsoft.UI.Xaml.Markup.IXamlMetadataProvider", true)!;
        var pageType = assembly.GetType("Fixture.MainPage", true)!;
        var metadata = providerType.GetMethod("GetXamlType", [typeof(Type)])!.Invoke(app, [pageType]);
        Assert.NotNull(metadata);
    }

    /// <summary>
    /// Verifies DI composition is available during WinUI's first markup compilation pass.
    /// </summary>
    [Fact]
    public void EarlyGenerationIncludesCompositionRoot()
    {
        var result = GeneratorTestHost.Run(
            "EarlyGeneration",
            (GeneratorTestHost.WinUiStubs, "WinUI.cs"),
            (GeneratorTestHost.ValidApplicationSource, "App.cs"));

        result.AssertNoErrors();

    }

    /// <summary>Scoped attributes generate standard DI descriptors for Pages and ViewModels.</summary>
    [Fact]
    public void ScopedRegistrationsAreGenerated()
    {
        var source = GeneratorTestHost.ValidApplicationSource
            .Replace("ServiceLifetime.Singleton", "ServiceLifetime.Scoped", StringComparison.Ordinal);
        var result = GeneratorTestHost.Run(
            "ScopedRegistrations",
            (GeneratorTestHost.WinUiStubs, "WinUI.cs"),
            (source, "App.cs"),
            (GeneratorTestHost.NativeProviderSource, "XamlTypeInfo.g.cs"));
        result.AssertNoErrors();
        var services = result.ConfigureServices("Fixture.App");
        Assert.Contains(services, static descriptor =>
            string.Equals(descriptor.ServiceType.Name, "SingletonPage", StringComparison.Ordinal) && descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, static descriptor =>
            string.Equals(descriptor.ServiceType.Name, "SingletonViewModel", StringComparison.Ordinal) && descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    /// <summary>
    /// Verifies invalid page constructors and service lifetimes are diagnosed.
    /// </summary>
    [Fact]
    public void InvalidRegistrationDiagnostics()
    {
        var missingPageConstructor = GeneratorTestHost.Run(
            "MissingPageConstructorDiagnostic",
            (GeneratorTestHost.WinUiStubs, "WinUI.cs"),
            (
                """
                using BetterWinUI.PageActivation.DependencyInjection;
                using BetterWinUI.PageActivation;
                using Microsoft.UI.Xaml.Controls;
                namespace Fixture;
                [View]
                public sealed class View : Page
                {
                    private View() { }
                }
                """,
                "MissingPageConstructor.cs"));
        missingPageConstructor.AssertGeneratorDiagnostic("BWPA0006");

        var unsupportedLifetime = GeneratorTestHost.Run(
            "UnsupportedLifetimeDiagnostic",
            (GeneratorTestHost.WinUiStubs, "WinUI.cs"),
            (
                """
                using BetterWinUI.PageActivation.DependencyInjection;
                using BetterWinUI.PageActivation;
                using Microsoft.Extensions.DependencyInjection;
                using Microsoft.UI.Xaml.Controls;
                namespace Fixture;
                [View((ServiceLifetime)42)]
                public sealed class View : Page;
                [ViewModel((ServiceLifetime)42)]
                public sealed class ViewModel;
                """,
                "UnsupportedLifetime.cs"));
        unsupportedLifetime.AssertGeneratorDiagnostic("BWPA0007");
    }

    /// <summary>
    /// Verifies missing generated application infrastructure is diagnosed.
    /// </summary>
    [Fact]
    public void MissingApplicationInfrastructureDiagnostics()
    {
        const string applicationSource =
            """
            using BetterWinUI.PageActivation.DependencyInjection;
            using BetterWinUI.PageActivation;
            using Microsoft.UI.Xaml;
            namespace Fixture;
            [GeneratePageActivationHook] public sealed partial class App : Application;
            """;
        var missingProvider = GeneratorTestHost.Run(
            "MissingProviderDiagnostic",
            (GeneratorTestHost.WinUiStubs, "WinUI.cs"),
            (applicationSource, "App.cs"),
            ("namespace Fixture; internal sealed class XamlMarker;", "XamlTypeInfo.g.cs"));
        missingProvider.AssertGeneratorDiagnostic("BWPH0004");

    }

    /// <summary>
    /// Verifies unsupported XAML contracts are diagnosed.
    /// </summary>
    [Fact]
    public void UnsupportedXamlContractDiagnostic()
    {
        var unsupportedContract = GeneratorTestHost.Run(
            "UnsupportedContractDiagnostic",
            (
                GeneratorTestHost.WinUiStubs.Replace(
                    "object ActivateInstance();",
                    "object CreateInstance();",
                    StringComparison.Ordinal),
                "WinUI.cs"),
            (
                """
                using BetterWinUI.PageActivation.DependencyInjection;
                using BetterWinUI.PageActivation;
                using Microsoft.UI.Xaml;
                namespace Fixture;
                [GeneratePageActivationHook]
                public sealed partial class App : Application
                {
                    public void Initialize(System.IServiceProvider services) =>
                        this.UsePageActivation(services);
                }
                """,
                "App.cs"),
            (GeneratorTestHost.NativeProviderSource, "XamlTypeInfo.g.cs"));
        unsupportedContract.AssertGeneratorDiagnostic("BWPH0006");
    }

    /// <summary>Other overloads and deferred installation through a delegate compile without warnings.</summary>
    [Fact]
    public void ApplicationCanChooseItsInstallationEntryPoint()
    {
        var source = GeneratorTestHost.ValidApplicationSource.Replace(
            "this.UsePageActivation(services);",
            "((System.Action<System.IServiceProvider>)this.UsePageActivation)(services);",
            StringComparison.Ordinal).Replace(
            "public void Initialize(System.IServiceProvider services)",
            "public void UsePageActivation() { }\n    public void Initialize(System.IServiceProvider services)",
            StringComparison.Ordinal);
        var result = GeneratorTestHost.Run(
            "DeferredInstallation",
            (GeneratorTestHost.WinUiStubs, "WinUI.cs"),
            (source, "App.cs"),
            (GeneratorTestHost.NativeProviderSource, "XamlTypeInfo.g.cs"));
        result.AssertNoErrors();
        Assert.Empty(result.RunResult.Diagnostics);
    }

    /// <summary>The DI generator reports only conflicts with its provider overload.</summary>
    [Fact]
    public void ConflictingProviderOverloadIsDiagnosed()
    {
        var source = GeneratorTestHost.ValidApplicationSource.Replace(
            "public void Initialize(System.IServiceProvider services)",
            "public void UsePageActivation(System.IServiceProvider services) { }\n    public void Initialize(System.IServiceProvider services)",
            StringComparison.Ordinal);
        var result = GeneratorTestHost.Run(
            "ProviderConflict",
            (GeneratorTestHost.WinUiStubs, "WinUI.cs"),
            (source, "App.cs"));
        result.AssertGeneratorDiagnostic("BWPA0001");
        Assert.DoesNotContain(result.RunResult.Diagnostics,
            static diagnostic => string.Equals(diagnostic.Id, "BWPH0005", StringComparison.Ordinal));
    }

    /// <summary>
    /// Verifies types that a namespace-level generated module cannot name are rejected.
    /// </summary>
    [Fact]
    public void InaccessibleAndOuterGenericRegistrationsAreRejected()
    {
        var result = GeneratorTestHost.Run(
            "InvalidRegistrationTypes",
            (GeneratorTestHost.WinUiStubs, "WinUI.cs"),
            (
                """
                using BetterWinUI.PageActivation.DependencyInjection;
                using BetterWinUI.PageActivation;
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

        result.AssertGeneratorDiagnostic("BWPA0002");
        result.AssertGeneratorDiagnostic("BWPA0003");
        Assert.DoesNotContain(
            result.OutputCompilation.GetDiagnostics(TestContext.Current.CancellationToken),
            static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    /// <summary>
    /// Verifies compile-time composition through a referenced generated module.
    /// </summary>
    [Fact]
    public void CrossAssemblyComposition()
    {
        var winUiReference = CreateWinUiReference();
        var viewsReference = CreateFeatureViewsReference(winUiReference);
        var application = GenerateComposedApplication(winUiReference, viewsReference);

        application.AssertNoErrors();
        var services = application.ConfigureServices("Composition.App");
        Assert.Contains(services, static descriptor =>
            string.Equals(descriptor.ServiceType.FullName, "Feature.FeaturePage", StringComparison.Ordinal) &&
            descriptor.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, static descriptor =>
            string.Equals(descriptor.ServiceType.FullName, "Feature.FeatureViewModel", StringComparison.Ordinal) &&
            descriptor.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, static descriptor =>
            string.Equals(descriptor.ServiceType.FullName, "Composition.LocalPage", StringComparison.Ordinal) &&
            descriptor.Lifetime == ServiceLifetime.Transient);
    }

    /// <summary>Referenced modules are accepted according to their callable API, without a version marker.</summary>
    [Theory]
    [InlineData("public class Module { public static void Register(IServiceCollection services) { } }", true)]
    [InlineData("public class Module { public static void Register(IServiceCollection services) { } public static void Register(object services) { } }", true)]
    [InlineData("public class Module { }", false)]
    [InlineData("public class Module { public void Register(IServiceCollection services) { } }", false)]
    [InlineData("public class Module { private static void Register(IServiceCollection services) { } }", false)]
    [InlineData("public class Module { public static int Register(IServiceCollection services) => 0; }", false)]
    [InlineData("public class Module { public static void Register<T>(IServiceCollection services) { } }", false)]
    [InlineData("public class Module { public static void Register(object services) { } }", false)]
    [InlineData("public class Module { public static void Register(ref IServiceCollection services) { } }", false)]
    [InlineData("public class Module { public static void Register(IServiceCollection services, bool option = false) { } }", false)]
    [InlineData("internal class Module { public static void Register(IServiceCollection services) { } }", false)]
    public void ReferencedModuleMustExposeRegistrationContract(string declaration, bool valid)
    {
        var moduleSource = """
            using BetterWinUI.PageActivation.DependencyInjection;
            using Microsoft.Extensions.DependencyInjection;
            [assembly: PageActivationViewModule(typeof(Module))]
            """ + Environment.NewLine + declaration;
        var module = GeneratorTestHost.CreateCompilation(
            "Module_" + Guid.NewGuid().ToString("N"), [(moduleSource, "Module.cs")]);
        var reference = MetadataReference.CreateFromImage(GeneratorTestHost.Emit(module));
        var result = GeneratorTestHost.Run(GeneratorTestHost.CreateCompilation(
            "ModuleConsumer",
            [(GeneratorTestHost.WinUiStubs, "WinUI.cs"),
             (GeneratorTestHost.ValidApplicationSource, "App.cs"),
             (GeneratorTestHost.NativeProviderSource, "XamlTypeInfo.g.cs")],
            [reference]));

        if (valid)
            result.AssertNoErrors();
        else
        {
            result.AssertGeneratorDiagnostic("BWPA0004");
            Assert.DoesNotContain(result.OutputCompilation.GetDiagnostics(TestContext.Current.CancellationToken),
                static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        }
    }

    /// <summary>
    /// Creates the WinUI contract reference used by cross-assembly generator tests.
    /// </summary>
    private static MetadataReference CreateWinUiReference()
    {
        var image = GeneratorTestHost.Emit(
            GeneratorTestHost.CreateCompilation(
                "WinUI.Abstractions",
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
                "Feature.Views",
                [
                    (
                        """
                        using BetterWinUI.PageActivation.DependencyInjection;
                        using BetterWinUI.PageActivation;
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
        views.AssertNoErrors();
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
                        using BetterWinUI.PageActivation;
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
