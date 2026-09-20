using BetterWinUI.PageActivation.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace BetterWinUI.IntegrationTests;

/// <summary>Supplies a real XAML Page for activation and navigation tests.</summary>
[View]
public sealed partial class FirstPage : Page
{
    /// <summary>Receives real constructor and keyed dependencies.</summary>
    public FirstPage(FirstViewModel viewModel, [FromKeyedServices("clock")] TestClock clock)
    {
        InitializeComponent();
        ViewModel = viewModel;
        Clock = clock;
    }
    /// <summary>Gets the injected ViewModel.</summary>
    public FirstViewModel ViewModel { get; }
    /// <summary>Gets the injected keyed dependency.</summary>
    public TestClock Clock { get; }
}
