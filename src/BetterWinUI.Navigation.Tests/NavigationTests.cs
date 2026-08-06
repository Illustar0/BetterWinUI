using System.Collections.Immutable;
using BetterWinUI.Navigation.Frame;
using BetterWinUI.Navigation.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Xunit;

namespace BetterWinUI.Navigation.Tests;

/// <summary>
/// Verifies registry, Frame navigation, and generated registration behavior.
/// </summary>
public sealed class NavigationTests
{
    /// <summary>
    /// Verifies exact lookup, parameter metadata, builder state, and conflicts.
    /// </summary>
    [Fact]
    public void RegistryEnforcesRegistrationContracts()
    {
        var builder = new NavigationRegistryBuilder();
        builder.Register<HomeViewModel, HomePage>("home");
        builder.Register<DetailViewModel, DetailPage, DetailArgs>("detail");

        var registry = builder.Build();

        Assert.Equal("home", registry.GetByViewModel<HomeViewModel>().Route);
        Assert.True(registry.TryGetByRoute("detail", out var detail));
        Assert.Equal(typeof(DetailArgs), detail.ParameterType);
        Assert.False(registry.TryGetByRoute("HOME", out _));
        Assert.Throws<InvalidOperationException>(() => builder.Build());

        var duplicate = new NavigationRegistryBuilder();
        duplicate.Register<HomeViewModel, HomePage>("home");
        Assert.Throws<InvalidOperationException>(() => duplicate.Register<HomeViewModel, AlternatePage>("alternate"));
    }

    /// <summary>
    /// Verifies strongly typed navigation, route failures, and history delegation.
    /// </summary>
    [Fact]
    public void FrameNavigationDelegatesResolvedDestinationsToHost()
    {
        var builder = new NavigationRegistryBuilder();
        builder.Register<HomeViewModel, HomePage>("home");
        builder.Register<DetailViewModel, DetailPage, DetailArgs>("detail");
        var host = new TestFrameNavigationHost();
        var navigation = new FrameNavigationService<TestFrameNavigationHost>(
            builder.Build(),
            host);

        Assert.True(navigation.NavigateToViewModel<HomeViewModel>());
        Assert.Equal(typeof(HomePage), host.LastPageType);
        Assert.Null(host.LastParameter);

        var args = new DetailArgs(42);
        Assert.True(navigation.NavigateToViewModel<DetailViewModel, DetailArgs>(args));
        Assert.Same(args, host.LastParameter);
        Assert.False(navigation.TryNavigateToRoute("missing"));
        Assert.False(navigation.TryNavigateToRoute("detail"));
        Assert.False(navigation.TryNavigateToRoute("detail", "wrong"));
        Assert.True(navigation.TryNavigateToRoute("detail", args));
        Assert.Throws<InvalidOperationException>(() => navigation.NavigateToViewModel<DetailViewModel>("wrong"));

        host.CanGoBackValue = true;
        host.CanGoForwardValue = true;
        Assert.True(navigation.CanGoBack);
        Assert.True(navigation.CanGoForward);
        navigation.GoBack();
        navigation.GoForward();
        Assert.Equal(1, host.GoBackCount);
        Assert.Equal(1, host.GoForwardCount);
    }

    /// <summary>
    /// Verifies generated, manual, and singleton DI registration.
    /// </summary>
    [Fact]
    public void GeneratedRegistrationComposesAllSources()
    {
        var services = new ServiceCollection();
        services.AddSingleton<TestFrameNavigationHost>();
        services.AddBetterFrameNavigation(builder =>
            builder.Register<ManualViewModel, ManualPage>("manual"));

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<NavigationRegistry>();
        Assert.Equal(
            typeof(GeneratedPage),
            registry.GetByViewModel<GeneratedViewModel>().ViewType);
        Assert.Equal(
            typeof(ManualPage),
            registry.GetByViewModel<ManualViewModel>().ViewType);

        var first =
            provider.GetRequiredService<FrameNavigationService<TestFrameNavigationHost>>();
        var second =
            provider.GetRequiredService<FrameNavigationService<TestFrameNavigationHost>>();
        Assert.Same(first, second);
        Assert.Same(first.Host, provider.GetRequiredService<TestFrameNavigationHost>());
    }

    /// <summary>
    /// Verifies generated modules reject types they cannot reference.
    /// </summary>
    [Fact]
    public void GeneratorRejectsInaccessibleNavigationTypes()
    {
        const string source =
            """
            using BetterWinUI.Navigation;
            namespace Fixture;

            public sealed class PublicViewModel;

            public sealed class PrivateContainer
            {
                private sealed class PrivateViewModel;
                private sealed class PrivateParameter;

                [ViewFor<PrivateViewModel>("private-view-model")]
                public sealed class PublicPage;

                [ViewFor<PublicViewModel>("private-page")]
                private sealed class PrivatePage;

                [ViewFor<PublicViewModel, PrivateParameter>("private-parameter")]
                public sealed class ParameterizedPage;
            }

            file sealed class FileViewModel;

            [ViewFor<FileViewModel>("file-view-model")]
            public sealed class FileViewModelPage;
            """;
        var cancellationToken = TestContext.Current.CancellationToken;
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var compilation = CSharpCompilation.Create(
            "InvalidNavigationTypes",
            [CSharpSyntaxTree.ParseText(
                source,
                parseOptions,
                cancellationToken: cancellationToken)],
            CreateGeneratorReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new NavigationGenerator().AsSourceGenerator()],
            parseOptions: parseOptions);

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var output,
            out _,
            cancellationToken);

        Assert.Contains(
            driver.GetRunResult().Diagnostics,
            static diagnostic => diagnostic.Id == "BWNAV002");
        Assert.DoesNotContain(
            output.GetDiagnostics(cancellationToken),
            static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    /// <summary>
    /// Creates runtime and BetterWinUI metadata references for generator tests.
    /// </summary>
    private static ImmutableArray<MetadataReference> CreateGeneratorReferences()
    {
        var platformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ??
                                 throw new InvalidOperationException(
                                     "Trusted platform assemblies are unavailable.");
        return platformAssemblies
            .Split(Path.PathSeparator)
            .Select(static path => MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(ViewForAttribute<>).Assembly.Location))
            .ToImmutableArray<MetadataReference>();
    }
}

/// <summary>Supplies a parameterless test destination.</summary>
public sealed class HomeViewModel;

/// <summary>Supplies a parameterized test destination.</summary>
public sealed class DetailViewModel;

/// <summary>Supplies a generated test destination.</summary>
public sealed class GeneratedViewModel;

/// <summary>Supplies a manually registered test destination.</summary>
public sealed class ManualViewModel;

/// <summary>Supplies a compile-time associated navigation parameter.</summary>
public sealed record DetailArgs(int Id) : INavigationParameter<DetailViewModel>;

/// <summary>Supplies a parameterless test Page.</summary>
public sealed partial class HomePage : Page;

/// <summary>Supplies a parameterized test Page.</summary>
public sealed partial class DetailPage : Page;

/// <summary>Supplies a duplicate-key test Page.</summary>
public sealed partial class AlternatePage : Page;

/// <summary>Supplies a generated local test Page.</summary>
[ViewFor<GeneratedViewModel>("generated")]
public sealed partial class GeneratedPage : Page;

/// <summary>Supplies a manually registered test Page.</summary>
public sealed partial class ManualPage : Page;

/// <summary>
/// Captures Frame navigation calls without constructing a WinUI visual tree.
/// </summary>
public sealed class TestFrameNavigationHost : IFrameNavigationHost
{
    /// <inheritdoc />
    public Microsoft.UI.Xaml.Controls.Frame? Frame => null;

    /// <inheritdoc />
    public bool IsAttached => true;

    /// <inheritdoc />
    public bool CanGoBack => CanGoBackValue;

    /// <inheritdoc />
    public bool CanGoForward => CanGoForwardValue;

    /// <summary>Gets or sets the simulated back-history state.</summary>
    public bool CanGoBackValue { get; set; }

    /// <summary>Gets or sets the simulated forward-history state.</summary>
    public bool CanGoForwardValue { get; set; }

    /// <summary>Gets the last requested Page type.</summary>
    public Type? LastPageType { get; private set; }

    /// <summary>Gets the last requested parameter.</summary>
    public object? LastParameter { get; private set; }

    /// <summary>Gets the number of back requests.</summary>
    public int GoBackCount { get; private set; }

    /// <summary>Gets the number of forward requests.</summary>
    public int GoForwardCount { get; private set; }

    /// <inheritdoc />
    public IDisposable Attach(Microsoft.UI.Xaml.Controls.Frame frame)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public bool Navigate(Type pageType)
    {
        LastPageType = pageType;
        LastParameter = null;
        return true;
    }

    /// <inheritdoc />
    public bool Navigate(Type pageType, object? parameter)
    {
        LastPageType = pageType;
        LastParameter = parameter;
        return true;
    }

    /// <inheritdoc />
    public bool Navigate(
        Type pageType,
        object? parameter,
        NavigationTransitionInfo transitionInfo)
    {
        return Navigate(pageType, parameter);
    }

    /// <inheritdoc />
    public void GoBack()
    {
        GoBackCount++;
    }

    /// <inheritdoc />
    public void GoBack(NavigationTransitionInfo transitionInfo)
    {
        GoBack();
    }

    /// <inheritdoc />
    public void GoForward()
    {
        GoForwardCount++;
    }
}