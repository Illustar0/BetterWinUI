using BetterWinUI.PageActivation.DependencyInjection;
using BetterWinUI.PageActivation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace BetterWinUI.IntegrationTests;

/// <summary>Hosts tests on the real XAML thread and installs generated Page activation.</summary>
[GeneratePageActivationHook]
public sealed partial class App : Application
{
    /// <summary>Gets the provider owned by the test application.</summary>
    public ServiceProvider Services { get; }

    /// <summary>Creates real XAML metadata and DI registrations before any navigation.</summary>
    public App()
    {
        InitializeComponent();
        var services = new ServiceCollection();
        services.AddKeyedSingleton("clock", new TestClock());
        services.AddSingleton(new OverrideViewModel());
        services.AddTransient<ManualPage>();
        services.AddGeneratedPageServices();
        Services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        this.UsePageActivation(Services);
    }

    /// <summary>Runs MSTest inside the existing Application and shuts down its owned services.</summary>
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        UITestMethodAttribute.DispatcherQueue = DispatcherQueue.GetForCurrentThread();
        try
        {
            Environment.ExitCode = await MicrosoftTestingPlatformApplication.RunAsync(Environment.GetCommandLineArgs()[1..]).ConfigureAwait(true);
        }
        finally
        {
            await Services.DisposeAsync().ConfigureAwait(true);
            Exit();
        }
    }
}
