using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace BetterWinUI.PageActivation.IntegrationTests;

/// <summary>Hosts activation tests with no DI package or service container.</summary>
[GeneratePageActivationHook]
public sealed partial class App : Application
{
    /// <summary>Gets the application-owned constructor dependency.</summary>
    public object Dependency { get; } = new();

    /// <summary>Gets the exact exception thrown by the application factory.</summary>
    public InvalidOperationException FactoryFailure { get; } = new("Factory failed.");

    /// <summary>Gets the metadata acquired before installing the hook.</summary>
    public IXamlType CachedMetadata { get; private set; } = null!;

    /// <summary>Gets the Page created by native navigation before installing the hook.</summary>
    public object NativeContent { get; private set; } = null!;

    /// <summary>Gets the last type received by the factory.</summary>
    public Type? LastRequestedType { get; private set; }

    /// <summary>Gets the number of factory calls.</summary>
    public int FactoryCalls { get; private set; }

    /// <summary>Loads real XAML metadata without installing a factory yet.</summary>
    public App() => InitializeComponent();

    /// <summary>Captures unhooked behavior, installs one factory, and runs the tests on the XAML thread.</summary>
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        UITestMethodAttribute.DispatcherQueue = DispatcherQueue.GetForCurrentThread();
        var frame = new Frame();
        frame.Navigate(typeof(NativePage));
        NativeContent = frame.Content;
        CachedMetadata = ((IXamlMetadataProvider)Application.Current).GetXamlType(typeof(FactoryPage));
        this.UsePageActivation(CreatePage);
        try
        {
            Environment.ExitCode = await MicrosoftTestingPlatformApplication.RunAsync(Environment.GetCommandLineArgs()[1..]).ConfigureAwait(true);
        }
        finally
        {
            Exit();
        }
    }

    /// <summary>Selects explicit factories, including invalid results used to exercise the public contract.</summary>
    private Page CreatePage(Type pageType)
    {
        FactoryCalls++;
        LastRequestedType = pageType;
        if (pageType == typeof(FactoryPage)) return new FactoryPage(Dependency);
        if (pageType == typeof(NativePage)) return new NativePage();
        if (pageType == typeof(NullPage)) return null!;
        if (pageType == typeof(WrongPage)) return new NativePage();
        throw FactoryFailure;
    }
}
