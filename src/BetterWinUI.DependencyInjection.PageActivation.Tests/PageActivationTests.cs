using System.Collections.Immutable;
using System.Reflection;
using Basic.Reference.Assemblies;
using BetterWinUI.DependencyInjection.PageActivation;
using BetterWinUI.DependencyInjection.PageActivation.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BetterWinUI.DependencyInjection.PageActivation.Tests;

/// <summary>
/// Runs generator, composition, and activation contract tests.
/// </summary>
public sealed class PageActivationTests
{
    /// <summary>
    /// Verifies the generated source contract.
    /// </summary>
    [Fact]
    public void GenerationSnapshot()
    {
        var result = GeneratorTestHost.GenerateValidApplication();
        result.AssertNoErrors();
        var generated = result.GetGeneratedSource("App.PageActivation.g.cs");
        var module = result.GetGeneratedSource("PageActivation.ViewModule");

        Assert.Contains("AddBetterPageActivation(", generated, StringComparison.Ordinal);
        Assert.Contains("ConcurrentDictionary<", generated, StringComparison.Ordinal);
        Assert.Contains(
            "internal sealed partial class PageActivationXamlType_",
            generated,
            StringComparison.Ordinal);
        Assert.Contains("WinRTExposedType", generated, StringComparison.Ordinal);
        Assert.Contains("IWinRTExposedTypeDetails", generated, StringComparison.Ordinal);
        Assert.Contains("ServiceLifetime.Transient", module, StringComparison.Ordinal);
        Assert.Contains("ServiceLifetime.Singleton", module, StringComparison.Ordinal);
        Assert.DoesNotContain("global::Fixture.ManualPage", module, StringComparison.Ordinal);
        Assert.DoesNotContain("global::Fixture.ManualViewModel", module, StringComparison.Ordinal);
        Assert.DoesNotContain("DataContext", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("IViewRegistry", generated, StringComparison.Ordinal);
        Assert.Contains(
            "PageActivationViewModuleAttribute(",
            module,
            StringComparison.Ordinal);
        Assert.Contains(
            $"    {PageActivationViewModuleAttribute.CurrentContractVersion},",
            module,
            StringComparison.Ordinal);
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
        var generated = result.GetGeneratedSource("App.PageActivation.g.cs");
        Assert.Contains("AddBetterPageActivation(", generated, StringComparison.Ordinal);
        Assert.Contains("InitializeBetterPageActivation(", generated, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies generated and manual registrations, lifetimes, and keyed dependencies.
    /// </summary>
    [Fact]
    public void Registrations()
    {
        var loaded = GeneratorTestHost.LoadValidApplication();
        var services = new ServiceCollection();
        var clockContract = loaded.Assembly.GetType("Fixture.IClock", true)!;
        var clockImplementation = loaded.Assembly.GetType("Fixture.Clock", true)!;
        var manualViewModelType = loaded.GetType("Fixture.ManualViewModel");
        var manualPageType = loaded.GetType("Fixture.ManualPage");
        services.AddKeyedSingleton(clockContract, "clock", clockImplementation);
        services.AddTransient(manualViewModelType);
        services.AddTransient(manualPageType);
        loaded.AddBetterPageActivation(services, null);
        var sharedViewModelType = loaded.GetType("Fixture.SharedViewModel");
        Assert.Single(
            services,
            descriptor => descriptor.ServiceType == sharedViewModelType);

        using var provider = services.BuildServiceProvider();
        var mainPageType = loaded.GetType("Fixture.MainPage");
        var firstPage = provider.GetRequiredService(mainPageType);
        var secondPage = provider.GetRequiredService(mainPageType);
        var firstViewModel = mainPageType.GetProperty("ViewModel")!.GetValue(firstPage)!;
        var secondViewModel = mainPageType.GetProperty("ViewModel")!.GetValue(secondPage)!;
        var clock = mainPageType.GetProperty("Clock")!.GetValue(firstPage)!;

        Assert.NotSame(firstPage, secondPage);
        Assert.NotSame(firstViewModel, secondViewModel);
        Assert.Equal(clockImplementation, clock.GetType());
        Assert.Null(mainPageType.GetProperty("DataContext")!.GetValue(firstPage));

        var singletonPageType = loaded.GetType("Fixture.SingletonPage");
        var singletonPage1 = provider.GetRequiredService(singletonPageType);
        var singletonPage2 = provider.GetRequiredService(singletonPageType);
        Assert.Same(singletonPage1, singletonPage2);
        var singletonViewModel1 =
            singletonPageType.GetProperty("ViewModel")!.GetValue(singletonPage1)!;
        var singletonViewModel2 =
            singletonPageType.GetProperty("ViewModel")!.GetValue(singletonPage2)!;
        Assert.Same(singletonViewModel1, singletonViewModel2);

        var manualPage = provider.GetRequiredService(manualPageType);
        Assert.Equal(
            manualViewModelType,
            manualPageType.GetProperty("ViewModel")!.GetValue(manualPage)!.GetType());

        var explicitViewModel = Activator.CreateInstance(
            loaded.GetType("Fixture.MainViewModel"))!;
        var overrideServices = new ServiceCollection();
        overrideServices.AddKeyedSingleton(clockContract, "clock", clockImplementation);
        overrideServices.AddSingleton(explicitViewModel.GetType(), explicitViewModel);
        overrideServices.AddSingleton(
            mainPageType,
            serviceProvider => ActivatorUtilities.CreateInstance(serviceProvider, mainPageType));
        loaded.AddBetterPageActivation(overrideServices, null);
        using var overrideProvider = overrideServices.BuildServiceProvider();
        var overriddenPage = overrideProvider.GetRequiredService(mainPageType);
        Assert.Same(
            overriddenPage,
            overrideProvider.GetRequiredService(mainPageType));
        Assert.Same(
            explicitViewModel,
            mainPageType.GetProperty("ViewModel")!.GetValue(overriddenPage));
    }

    /// <summary>
    /// Verifies strict, fallback, cached, and one-time activation behavior.
    /// </summary>
    [Fact]
    public void ActivationBehavior()
    {
        var loaded = GeneratorTestHost.LoadValidApplication();
        var nativeProvider = loaded.CreateNativeProvider();
        var generatedProvider = loaded.CreateGeneratedProvider(nativeProvider);
        var mainPageType = loaded.GetType("Fixture.MainPage");
        var wrappedType = loaded.GetXamlType(generatedProvider, mainPageType);

        Assert.Throws<InvalidOperationException>(() => loaded.Activate(wrappedType));
        Assert.Same(wrappedType, loaded.GetXamlType(generatedProvider, mainPageType));

        var registeredServices = new ServiceCollection();
        var clockContract = loaded.GetType("Fixture.IClock");
        var clockImplementation = loaded.GetType("Fixture.Clock");
        registeredServices.AddKeyedSingleton(clockContract, "clock", clockImplementation);
        loaded.AddBetterPageActivation(registeredServices, null);
        using var registeredProvider = registeredServices.BuildServiceProvider();
        loaded.Initialize(generatedProvider, registeredProvider);
        var activated = loaded.Activate(wrappedType);
        Assert.Equal(mainPageType, activated.GetType());
        Assert.Throws<InvalidOperationException>(() => loaded.Initialize(generatedProvider, registeredProvider));

        var unregisteredPage = loaded.GetType("Fixture.UnregisteredPage");
        var strictServices = new ServiceCollection();
        strictServices.AddOptions<PageActivationOptions>();
        using var strictProvider = strictServices.BuildServiceProvider();
        var strictGeneratedProvider =
            loaded.CreateGeneratedProvider(loaded.CreateNativeProvider());
        loaded.Initialize(strictGeneratedProvider, strictProvider);
        var strictXamlType = loaded.GetXamlType(strictGeneratedProvider, unregisteredPage);
        Assert.Throws<InvalidOperationException>(() => loaded.Activate(strictXamlType));

        var fallbackServices = new ServiceCollection();
        fallbackServices
            .AddOptions<PageActivationOptions>()
            .Configure(static options =>
                options.UnregisteredPageBehavior =
                    UnregisteredPageBehavior.FallbackToXamlActivator);
        using var fallbackProvider = fallbackServices.BuildServiceProvider();
        var fallbackGeneratedProvider =
            loaded.CreateGeneratedProvider(loaded.CreateNativeProvider());
        loaded.Initialize(fallbackGeneratedProvider, fallbackProvider);
        var fallbackXamlType =
            loaded.GetXamlType(fallbackGeneratedProvider, unregisteredPage);
        Assert.Equal(
            $"native:{unregisteredPage.Name}",
            loaded.Activate(fallbackXamlType));
    }

    /// <summary>
    /// Verifies representative generator errors and warnings.
    /// </summary>
    [Fact]
    public void GeneratorDiagnostics()
    {
        var scoped = GeneratorTestHost.Run(
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
        scoped.AssertGeneratorDiagnostic("BWPA0009");
        Assert.Equal(
            "https://github.com/Illustar0/BetterWinUI/blob/main/src/" +
            "BetterWinUI.DependencyInjection.PageActivation/README.md#bwpa0009",
            scoped.RunResult.Diagnostics.First(static diagnostic =>
                diagnostic.Id == "BWPA0009").Descriptor.HelpLinkUri);

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

        var missingProvider = GeneratorTestHost.Run(
            "MissingProviderDiagnostic",
            (GeneratorTestHost.WinUiStubs, "WinUI.cs"),
            (
                """
                using BetterWinUI.DependencyInjection.PageActivation;
                using Microsoft.UI.Xaml;
                namespace Fixture;
                [PageActivation] public sealed partial class App : Application;
                """,
                "App.cs"),
            ("namespace Fixture; internal sealed class XamlMarker;", "XamlTypeInfo.g.cs"));
        missingProvider.AssertGeneratorDiagnostic("BWPA0004");

        var missingInitialization = GeneratorTestHost.Run(
            "MissingInitializationDiagnostic",
            (GeneratorTestHost.WinUiStubs, "WinUI.cs"),
            (
                """
                using BetterWinUI.DependencyInjection.PageActivation;
                using Microsoft.UI.Xaml;
                namespace Fixture;
                [PageActivation] public sealed partial class App : Application;
                """,
                "App.cs"),
            (GeneratorTestHost.NativeProviderSource, "XamlTypeInfo.g.cs"));
        missingInitialization.AssertGeneratorDiagnostic("BWPA0012");

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
            static diagnostic => diagnostic.Id == "BWPA0012");
    }

    /// <summary>
    /// Verifies record classes participate in generated ViewModel registration.
    /// </summary>
    [Fact]
    public void RecordClassViewModelIsRegistered()
    {
        var result = GeneratorTestHost.Run(
            "RecordViewModel",
            (GeneratorTestHost.WinUiStubs, "WinUI.cs"),
            (
                """
                using BetterWinUI.DependencyInjection.PageActivation;
                using Microsoft.Extensions.DependencyInjection;
                namespace Fixture;

                [ViewModel(ServiceLifetime.Transient)]
                public sealed record MainViewModel;
                """,
                "RecordViewModel.cs"));

        result.AssertNoErrors();
        Assert.Contains(
            "typeof(global::Fixture.MainViewModel)",
            result.GetGeneratedSource("PageActivation.ViewModule"),
            StringComparison.Ordinal);
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
    /// Verifies structural forwarding for future interface members.
    /// </summary>
    [Fact]
    public void FutureXamlInterfaceMembersAreForwarded()
    {
        var futureContract = GeneratorTestHost.WinUiStubs
            .Replace(
                "XmlnsDefinition[] GetXmlnsDefinitions();",
                """
                XmlnsDefinition[] GetXmlnsDefinitions();
                int ContractVersion { get => 1; set { } }
                string this[int index] { get => string.Empty; set { } }
                event System.Action Changed { add { } remove { } }
                T Echo<T>(T value) where T : class, new() => value;
                void Probe(ref int value) => value++;
                """,
                StringComparison.Ordinal)
            .Replace(
                "void RunInitializer();",
                """
                void RunInitializer();
                int ContractVersion { get => 1; set { } }
                string this[int index] { get => string.Empty; set { } }
                event System.Action Changed { add { } remove { } }
                T Echo<T>(T value) where T : class, new() => value;
                T EchoRefLike<T>(T value) where T : allows ref struct => value;
                void AddMany(params System.ReadOnlySpan<object> items) { }
                void Probe(ref int value) => value++;
                """,
                StringComparison.Ordinal);

        var result = GeneratorTestHost.Run(
            "FutureContract",
            (futureContract, "WinUI.cs"),
            (GeneratorTestHost.ValidApplicationSource, "App.cs"),
            (GeneratorTestHost.NativeProviderSource, "XamlTypeInfo.g.cs"));
        result.AssertNoErrors();
        var generated = result.GetGeneratedSource("App.PageActivation.g.cs");

        Assert.Contains("Echo<T>", generated, StringComparison.Ordinal);
        Assert.Contains(
            "where T : allows ref struct",
            generated,
            StringComparison.Ordinal);
        Assert.Contains(
            "params global::System.ReadOnlySpan<global::System.Object> items",
            generated,
            StringComparison.Ordinal);
        Assert.Contains("Probe(", generated, StringComparison.Ordinal);
        Assert.Contains("ref global::System.Int32 value", generated, StringComparison.Ordinal);
        Assert.Contains("this[global::System.Int32 index]", generated, StringComparison.Ordinal);
        Assert.Contains("event global::System.Action Changed", generated, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies compile-time composition through a referenced generated module.
    /// </summary>
    [Fact]
    public void CrossAssemblyComposition()
    {
        var winUiImage = GeneratorTestHost.Emit(
            GeneratorTestHost.CreateCompilation(
                "WinUI.Abstractions",
                [(GeneratorTestHost.WinUiStubs, "WinUI.cs")]));
        MetadataReference winUiReference = MetadataReference.CreateFromImage(winUiImage);

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
        var viewsImage = GeneratorTestHost.Emit(views.OutputCompilation);

        var application = GeneratorTestHost.Run(
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
                [winUiReference, MetadataReference.CreateFromImage(viewsImage)]));
        application.AssertNoErrors();
        var source = application.GetGeneratedSource("App.PageActivation.g.cs");
        Assert.Contains(
            "global::BetterWinUI.DependencyInjection.PageActivation.Generated.PageActivationViewModule_",
            source,
            StringComparison.Ordinal);
        Assert.Contains(".Register(services);", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AddMappings", source, StringComparison.Ordinal);
    }
}

/// <summary>
/// Hosts Roslyn generator execution and dynamic compilation.
/// </summary>
internal static class GeneratorTestHost
{
    internal const string WinUiStubs =
        """
        namespace WinRT
        {
            [System.AttributeUsage(System.AttributeTargets.Class)]
            public sealed class WinRTRuntimeClassNameAttribute(string name) : System.Attribute;
            [System.AttributeUsage(System.AttributeTargets.Class)]
            public sealed class WinRTExposedTypeAttribute(System.Type type) : System.Attribute;
            public interface IWinRTExposedTypeDetails
            {
                System.Runtime.InteropServices.ComWrappers.ComInterfaceEntry[]
                    GetExposedInterfaces();
            }
        }
        namespace ABI.Microsoft.UI.Xaml.Markup
        {
            public static class IXamlTypeMethods
            {
                public static System.Guid IID => default;
                public static nint AbiToProjectionVftablePtr => default;
            }
        }
        namespace Microsoft.UI.Xaml
        {
            public class Application { }
        }
        namespace Microsoft.UI.Xaml.Controls
        {
            public class Page
            {
                public object? DataContext { get; set; }
            }
        }
        namespace Microsoft.UI.Xaml.Markup
        {
            public sealed class XmlnsDefinition { }
            public interface IXamlMember { }
            public interface IXamlMetadataProvider
            {
                IXamlType GetXamlType(System.Type type);
                IXamlType GetXamlType(string fullName);
                XmlnsDefinition[] GetXmlnsDefinitions();
            }
            public interface IXamlType
            {
                IXamlType BaseType { get; }
                IXamlType BoxedType { get; }
                IXamlMember ContentProperty { get; }
                string FullName { get; }
                bool IsArray { get; }
                bool IsBindable { get; }
                bool IsCollection { get; }
                bool IsConstructible { get; }
                bool IsDictionary { get; }
                bool IsMarkupExtension { get; }
                IXamlType ItemType { get; }
                IXamlType KeyType { get; }
                System.Type UnderlyingType { get; }
                object ActivateInstance();
                void AddToMap(object instance, object key, object item);
                void AddToVector(object instance, object item);
                object CreateFromString(string value);
                IXamlMember GetMember(string name);
                void RunInitializer();
            }
        }
        """;

    internal const string NativeProviderSource =
        """
        using Microsoft.UI.Xaml.Markup;
        namespace Fixture;
        public sealed partial class App
        {
            private NativeProvider _AppProvider { get; } = new();
        }
        public sealed class NativeProvider : IXamlMetadataProvider
        {
            public IXamlType GetXamlType(System.Type type) => new NativeXamlType(type);
            public IXamlType GetXamlType(string fullName) =>
                new NativeXamlType(typeof(MainPage));
            public XmlnsDefinition[] GetXmlnsDefinitions() => [];
        }
        public sealed class NativeXamlType(System.Type type) : IXamlType
        {
            public IXamlType BaseType => null!;
            public IXamlType BoxedType => null!;
            public IXamlMember ContentProperty => null!;
            public string FullName => type.FullName!;
            public bool IsArray => false;
            public bool IsBindable => false;
            public bool IsCollection => false;
            public bool IsConstructible => true;
            public bool IsDictionary => false;
            public bool IsMarkupExtension => false;
            public IXamlType ItemType => null!;
            public IXamlType KeyType => null!;
            public System.Type UnderlyingType => type;
            public object ActivateInstance() => $"native:{type.Name}";
            public void AddToMap(object instance, object key, object item) { }
            public void AddToVector(object instance, object item) { }
            public object CreateFromString(string value) => value;
            public IXamlMember GetMember(string name) => null!;
            public void RunInitializer() { }
        }
        """;

    internal const string ValidApplicationSource =
        """
        using BetterWinUI.DependencyInjection.PageActivation;
        using Microsoft.Extensions.DependencyInjection;
        using Microsoft.UI.Xaml;
        using Microsoft.UI.Xaml.Controls;
        namespace Fixture;

        [PageActivation]
        public sealed partial class App : Application
        {
            public static IServiceCollection Configure(IServiceCollection services) =>
                services.AddBetterPageActivation();

            public void Initialize(System.IServiceProvider services) =>
                this.InitializeBetterPageActivation(services);
        }

        public interface IClock { }
        public sealed class Clock : IClock { }

        [View]
        public sealed class MainPage : Page
        {
            public MainPage(
                MainViewModel viewModel,
                [FromKeyedServices("clock")] IClock clock)
            {
                ViewModel = viewModel;
                Clock = clock;
            }

            public MainViewModel ViewModel { get; }
            public IClock Clock { get; }
        }

        [ViewModel(ServiceLifetime.Transient)]
        public sealed class MainViewModel { }

        [View(ServiceLifetime.Singleton)]
        public sealed class SingletonPage : Page
        {
            public SingletonPage(SingletonViewModel viewModel) => ViewModel = viewModel;
            public SingletonViewModel ViewModel { get; }
        }

        [ViewModel(ServiceLifetime.Singleton)]
        public sealed class SingletonViewModel { }

        [View]
        public sealed class FirstSharedPage(SharedViewModel viewModel) : Page
        {
            public SharedViewModel ViewModel { get; } = viewModel;
        }

        [View]
        public sealed class SecondSharedPage(SharedViewModel viewModel) : Page
        {
            public SharedViewModel ViewModel { get; } = viewModel;
        }

        [ViewModel(ServiceLifetime.Transient)]
        public sealed class SharedViewModel { }

        public sealed class ManualPage : Page
        {
            public ManualPage(ManualViewModel viewModel) => ViewModel = viewModel;
            public ManualViewModel ViewModel { get; }
        }

        public sealed class ManualViewModel { }

        public sealed class UnregisteredPage : Page { }
        """;

    private static readonly CSharpParseOptions ParseOptions =
        new(LanguageVersion.Preview, DocumentationMode.Diagnose);

    private static readonly ImmutableArray<MetadataReference> PlatformReferences =
        CreatePlatformReferences();

    /// <summary>
    /// Generates and compiles the complete valid application fixture.
    /// </summary>
    public static GenerationResult GenerateValidApplication()
    {
        return Run(
            "ValidApplication_" + Guid.NewGuid().ToString("N"),
            (WinUiStubs, "WinUI.cs"),
            (ValidApplicationSource, "App.cs"),
            (NativeProviderSource, "XamlTypeInfo.g.cs"));
    }

    /// <summary>
    /// Generates, emits, and loads the complete application fixture.
    /// </summary>
    public static LoadedApplication LoadValidApplication()
    {
        var result = GenerateValidApplication();
        result.AssertNoErrors();
        return new LoadedApplication(Assembly.Load(Emit(result.OutputCompilation)));
    }

    /// <summary>
    /// Creates a compilation from source tuples and optional additional references.
    /// </summary>
    public static CSharpCompilation CreateCompilation(
        string assemblyName,
        IEnumerable<(string Source, string Path)> sources,
        IEnumerable<MetadataReference>? additionalReferences = null)
    {
        var references = additionalReferences is null
            ? PlatformReferences
            : PlatformReferences.Concat(additionalReferences);
        return CSharpCompilation.Create(
            assemblyName,
            sources.Select(static source =>
                CSharpSyntaxTree.ParseText(
                    source.Source,
                    ParseOptions,
                    source.Path)),
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release,
                nullableContextOptions: NullableContextOptions.Enable));
    }

    /// <summary>
    /// Runs the generator for source tuples.
    /// </summary>
    public static GenerationResult Run(
        string assemblyName,
        params (string Source, string Path)[] sources)
    {
        return Run(CreateCompilation(assemblyName, sources));
    }

    /// <summary>
    /// Runs the generator and updates a compilation with generated sources.
    /// </summary>
    public static GenerationResult Run(CSharpCompilation compilation)
    {
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new PageActivationGenerator().AsSourceGenerator()],
            parseOptions: ParseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var output,
            out _);
        return new GenerationResult(
            (CSharpCompilation)output,
            driver.GetRunResult());
    }

    /// <summary>
    /// Emits a compilation to an in-memory assembly image.
    /// </summary>
    public static byte[] Emit(Compilation compilation)
    {
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success)
            throw new InvalidOperationException(
                string.Join(
                    Environment.NewLine,
                    result.Diagnostics.Select(static diagnostic => diagnostic.ToString())));

        return stream.ToArray();
    }

    private static ImmutableArray<MetadataReference> CreatePlatformReferences()
    {
        return Net100.References.All
            .Cast<MetadataReference>()
            .Concat(
            [
                MetadataReference.CreateFromFile(
                    typeof(PageActivationAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IServiceCollection).Assembly.Location),
                MetadataReference.CreateFromFile(
                    typeof(Microsoft.Extensions.Options.IOptions<>).Assembly.Location)
            ])
            .ToImmutableArray<MetadataReference>();
    }
}

/// <summary>
/// Represents one completed generator run.
/// </summary>
internal sealed class GenerationResult(
    CSharpCompilation outputCompilation,
    GeneratorDriverRunResult runResult)
{
    /// <summary>Gets the compilation containing generated sources.</summary>
    public CSharpCompilation OutputCompilation { get; } = outputCompilation;

    /// <summary>Gets generator diagnostics and generated source results.</summary>
    public GeneratorDriverRunResult RunResult { get; } = runResult;

    /// <summary>
    /// Asserts that neither generator nor output compilation contains errors.
    /// </summary>
    public void AssertNoErrors()
    {
        var diagnostics = RunResult.Diagnostics
            .Concat(OutputCompilation.GetDiagnostics())
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();
        if (!diagnostics.IsDefaultOrEmpty)
            throw new InvalidOperationException(
                string.Join(
                    Environment.NewLine,
                    diagnostics.Select(static diagnostic => diagnostic.ToString())));
    }

    /// <summary>
    /// Asserts that the generator reported a diagnostic identifier.
    /// </summary>
    public void AssertGeneratorDiagnostic(string id)
    {
        if (!RunResult.Diagnostics.Any(diagnostic => string.Equals(diagnostic.Id, id, StringComparison.Ordinal)))
            throw new InvalidOperationException(
                $"Expected generator diagnostic {id}, got: " +
                string.Join(", ", RunResult.Diagnostics.Select(static diagnostic => diagnostic.Id)));
    }

    /// <summary>
    /// Gets one generated source whose hint name contains the requested fragment.
    /// </summary>
    public string GetGeneratedSource(string hintNameFragment)
    {
        var generated = RunResult.Results
            .SelectMany(static result => result.GeneratedSources)
            .Single(source => source.HintName.Contains(
                hintNameFragment,
                StringComparison.Ordinal));
        return generated.SourceText.ToString();
    }
}

/// <summary>
/// Provides reflection access to a dynamically generated application assembly.
/// </summary>
internal sealed class LoadedApplication(Assembly assembly)
{
    /// <summary>Gets the dynamically loaded assembly.</summary>
    public Assembly Assembly { get; } = assembly;

    /// <summary>
    /// Gets a required fixture type.
    /// </summary>
    public Type GetType(string fullName)
    {
        return Assembly.GetType(fullName, true)!;
    }

    /// <summary>
    /// Invokes the generated service collection extension.
    /// </summary>
    public void AddBetterPageActivation(
        IServiceCollection services,
        Action<PageActivationOptions>? configure)
    {
        var extension = Assembly.GetTypes().Single(static type =>
            type.Name.StartsWith(
                "PageActivationServiceCollectionExtensions_",
                StringComparison.Ordinal));
        extension.GetMethod("AddBetterPageActivation")!.Invoke(null, [services, configure]);
    }

    /// <summary>
    /// Creates the fixture's native XAML metadata provider.
    /// </summary>
    public object CreateNativeProvider()
    {
        return Activator.CreateInstance(GetType("Fixture.NativeProvider"))!;
    }

    /// <summary>
    /// Creates the generated XAML metadata provider around a native provider.
    /// </summary>
    public object CreateGeneratedProvider(object nativeProvider)
    {
        var providerType = Assembly.GetTypes().Single(static type =>
            type.Name.StartsWith(
                "PageActivationXamlMetadataProvider_",
                StringComparison.Ordinal));
        return Activator.CreateInstance(
            providerType,
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            [nativeProvider],
            null)!;
    }

    /// <summary>
    /// Initializes a generated provider.
    /// </summary>
    public void Initialize(object generatedProvider, IServiceProvider services)
    {
        Invoke(generatedProvider, "Initialize", services);
    }

    /// <summary>
    /// Resolves an IXamlType through a generated provider.
    /// </summary>
    public object GetXamlType(object generatedProvider, Type type)
    {
        return Invoke(generatedProvider, "GetXamlType", type)!;
    }

    /// <summary>
    /// Activates a resolved IXamlType.
    /// </summary>
    public object Activate(object xamlType)
    {
        return Invoke(xamlType, "ActivateInstance")!;
    }

    private static object? Invoke(object target, string methodName, params object[] arguments)
    {
        try
        {
            var method = target.GetType()
                .GetMethods(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Single(candidate =>
                {
                    if (candidate.Name != methodName) return false;

                    var parameters = candidate.GetParameters();
                    return parameters.Length == arguments.Length &&
                           parameters
                               .Select(static parameter => parameter.ParameterType)
                               .Zip(
                                   arguments,
                                   static (parameter, argument) =>
                                       argument is null ||
                                       parameter.IsInstanceOfType(argument))
                               .All(static matches => matches);
                });
            return method.Invoke(target, arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }
    }
}
