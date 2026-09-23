using BetterWinUI.PageActivation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BetterWinUI.PageActivation.AotSmoke;

/// <summary>Checks early Page factory registration in a published Native AOT application.</summary>
[GeneratePageActivationHook]
public sealed partial class App : Application
{
    private readonly object dependency = new();

    /// <summary>Installs the factory before WinUI loads application resources.</summary>
    public App()
    {
        Environment.ExitCode = 1;
        UsePageActivation(type => type == typeof(FactoryPage)
            ? new FactoryPage(dependency)
            : throw new InvalidOperationException($"Unexpected Page: {type}"));
        InitializeComponent();
    }

    /// <summary>Exits successfully only after Frame uses the registered factory.</summary>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var frame = new Frame();
        if (!frame.Navigate(typeof(FactoryPage)) ||
            frame.Content is not FactoryPage page ||
            !ReferenceEquals(page.Dependency, dependency))
            throw new InvalidOperationException("Frame did not use the Page factory.");

        Environment.ExitCode = 0;
        Exit();
    }
}
