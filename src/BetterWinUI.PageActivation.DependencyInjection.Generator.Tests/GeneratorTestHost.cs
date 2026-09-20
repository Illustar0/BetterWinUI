using System.Collections.Immutable;
using Basic.Reference.Assemblies;
using BetterWinUI.PageActivation.DependencyInjection;
using BetterWinUI.PageActivation;
using BetterWinUI.PageActivation.DependencyInjection.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BetterWinUI.PageActivation.DependencyInjection.Generator.Tests;

/// <summary>
/// Hosts compiler-only fixtures; these synthetic contracts never stand in for runtime WinUI tests.
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
        using BetterWinUI.PageActivation.DependencyInjection;
        using BetterWinUI.PageActivation;
        using Microsoft.Extensions.DependencyInjection;
        using Microsoft.UI.Xaml;
        using Microsoft.UI.Xaml.Controls;
        namespace Fixture;

        [GeneratePageActivationHook]
        public sealed partial class App : Application
        {
            public static IServiceCollection Configure(IServiceCollection services) =>
                services.AddGeneratedPageServices();

            public void Initialize(System.IServiceProvider services) =>
                this.UsePageActivation(services);
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
        new(LanguageVersion.CSharp12, DocumentationMode.Diagnose);

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
            [new PageActivationGenerator().AsSourceGenerator(), new BetterWinUI.PageActivation.Generator.PageActivationGenerator().AsSourceGenerator()],
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
        return Net80.References.All
            .Cast<MetadataReference>()
            .Concat(
            [
                MetadataReference.CreateFromFile(
                    typeof(GeneratePageActivationHookAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IServiceCollection).Assembly.Location),
                MetadataReference.CreateFromFile(
                    typeof(ViewAttribute).Assembly.Location)
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

    /// <summary>Calls the fixture application's public generated-registration composition root.</summary>
    public IServiceCollection ConfigureServices(string applicationType)
    {
        using var stream = new MemoryStream(GeneratorTestHost.Emit(OutputCompilation));
        var assembly = System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromStream(stream);
        var services = new ServiceCollection();
        assembly.GetType(applicationType, true)!.GetMethod("Configure")!.Invoke(null, [services]);
        return services;
    }
}
