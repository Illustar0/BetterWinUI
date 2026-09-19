using BetterWinUI.DependencyInjection.PageActivation;
using BetterWinUI.DependencyInjection.PageActivation.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BetterWinUI.DependencyInjection.PageActivation.Generator.Tests;

/// <summary>
/// Verifies generator output, diagnostics, and compile-time composition.
/// </summary>
public sealed class PageActivationGeneratorTests
{
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
                "BWPA0004",
                StringComparison.Ordinal));

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

    /// <summary>
    /// Verifies scoped registration diagnostics link to their documentation.
    /// </summary>
    [Fact]
    public void ScopedRegistrationDiagnosticIncludesDocumentation()
    {
        var result = GeneratorTestHost.Run(
            "ScopedDiagnostic",
            (GeneratorTestHost.WinUiStubs, "WinUI.cs"),
            (
                """
                using BetterWinUI.DependencyInjection.PageActivation;
                using Microsoft.Extensions.DependencyInjection;
                using Microsoft.UI.Xaml.Controls;
                namespace Fixture;
                [View(ServiceLifetime.Scoped)]
                public sealed class View : Page;
                [ViewModel(ServiceLifetime.Scoped)]
                public sealed class ViewModel;
                """,
                "Scoped.cs"));

        result.AssertGeneratorDiagnostic("BWPA0009");
        Assert.Equal(
            "https://github.com/Illustar0/BetterWinUI/blob/main/src/" +
            "BetterWinUI.DependencyInjection.PageActivation/README.md#bwpa0009",
            result.RunResult.Diagnostics.First(static diagnostic => string.Equals(
                diagnostic.Id,
                "BWPA0009",
                StringComparison.Ordinal)).Descriptor.HelpLinkUri);
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
                using BetterWinUI.DependencyInjection.PageActivation;
                using Microsoft.UI.Xaml.Controls;
                namespace Fixture;
                [View]
                public sealed class View : Page
                {
                    private View() { }
                }
                """,
                "MissingPageConstructor.cs"));
        missingPageConstructor.AssertGeneratorDiagnostic("BWPA0014");

        var unsupportedLifetime = GeneratorTestHost.Run(
            "UnsupportedLifetimeDiagnostic",
            (GeneratorTestHost.WinUiStubs, "WinUI.cs"),
            (
                """
                using BetterWinUI.DependencyInjection.PageActivation;
                using Microsoft.Extensions.DependencyInjection;
                using Microsoft.UI.Xaml.Controls;
                namespace Fixture;
                [View((ServiceLifetime)42)]
                public sealed class View : Page;
                [ViewModel((ServiceLifetime)42)]
                public sealed class ViewModel;
                """,
                "UnsupportedLifetime.cs"));
        unsupportedLifetime.AssertGeneratorDiagnostic("BWPA0015");
    }

    /// <summary>
    /// Verifies missing generated application infrastructure is diagnosed.
    /// </summary>
    [Fact]
    public void MissingApplicationInfrastructureDiagnostics()
    {
        const string applicationSource =
            """
            using BetterWinUI.DependencyInjection.PageActivation;
            using Microsoft.UI.Xaml;
            namespace Fixture;
            [PageActivation] public sealed partial class App : Application;
            """;
        var missingProvider = GeneratorTestHost.Run(
            "MissingProviderDiagnostic",
            (GeneratorTestHost.WinUiStubs, "WinUI.cs"),
            (applicationSource, "App.cs"),
            ("namespace Fixture; internal sealed class XamlMarker;", "XamlTypeInfo.g.cs"));
        missingProvider.AssertGeneratorDiagnostic("BWPA0004");

        var missingInitialization = GeneratorTestHost.Run(
            "MissingInitializationDiagnostic",
            (GeneratorTestHost.WinUiStubs, "WinUI.cs"),
            (applicationSource, "App.cs"),
            (GeneratorTestHost.NativeProviderSource, "XamlTypeInfo.g.cs"));
        missingInitialization.AssertGeneratorDiagnostic("BWPA0012");
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
                using BetterWinUI.DependencyInjection.PageActivation;
                using Microsoft.UI.Xaml;
                namespace Fixture;
                [PageActivation]
                public sealed partial class App : Application
                {
                    public void Initialize(System.IServiceProvider services) =>
                        this.InitializeBetterPageActivation(services);
                }
                """,
                "App.cs"),
            (GeneratorTestHost.NativeProviderSource, "XamlTypeInfo.g.cs"));
        unsupportedContract.AssertGeneratorDiagnostic("BWPA0013");
    }

    /// <summary>
    /// Verifies initialization detection requires the generated application receiver.
    /// </summary>
    [Fact]
    public void InitializationDetectionRequiresApplicationReceiver()
    {
        var unrelatedReceiver = GeneratorTestHost.Run(
            "UnrelatedInitializationReceiver",
            (GeneratorTestHost.WinUiStubs, "WinUI.cs"),
            (
                """
                using BetterWinUI.DependencyInjection.PageActivation;
                using Microsoft.UI.Xaml;
                using Microsoft.UI.Xaml.Controls;
                namespace Fixture;

                public sealed class MainPage : Page;

                [PageActivation]
                public sealed partial class App : Application
                {
                    private readonly OtherComponent other = new();

                    public void Initialize(System.IServiceProvider services) =>
                        other.InitializeBetterPageActivation(services);
                }

                public sealed class OtherComponent
                {
                    public void InitializeBetterPageActivation(
                        System.IServiceProvider services) { }
                }
                """,
                "App.cs"),
            (GeneratorTestHost.NativeProviderSource, "XamlTypeInfo.g.cs"));

        unrelatedReceiver.AssertNoErrors();
        unrelatedReceiver.AssertGeneratorDiagnostic("BWPA0012");

        var applicationReceiver = GeneratorTestHost.GenerateValidApplication();
        applicationReceiver.AssertNoErrors();
        Assert.DoesNotContain(
            applicationReceiver.RunResult.Diagnostics,
            static diagnostic => string.Equals(
                diagnostic.Id,
                "BWPA0012",
                StringComparison.Ordinal));
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
                using BetterWinUI.DependencyInjection.PageActivation;
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

        result.AssertGeneratorDiagnostic("BWPA0006");
        result.AssertGeneratorDiagnostic("BWPA0007");
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
                        using BetterWinUI.DependencyInjection.PageActivation;
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
                        using BetterWinUI.DependencyInjection.PageActivation;
                        using Microsoft.UI.Xaml;
                        namespace Composition;
                        [PageActivation]
                        public sealed partial class App : Application
                        {
                            public static Microsoft.Extensions.DependencyInjection.IServiceCollection Configure(
                                Microsoft.Extensions.DependencyInjection.IServiceCollection services) =>
                                services.AddBetterPageActivation();
                            public void Initialize(System.IServiceProvider services) =>
                                this.InitializeBetterPageActivation(services);
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
                        using BetterWinUI.DependencyInjection.PageActivation;
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
