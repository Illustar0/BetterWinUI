using BetterWinUI.PageActivation.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace BetterWinUI.IntegrationTests;

/// <summary>Supplies a real XAML Page for activation and navigation tests.</summary>
[View(ServiceLifetime.Singleton)]
public sealed partial class SingletonPage : Page
{
    /// <summary>Receives the singleton ViewModel.</summary>
    public SingletonPage(SingletonViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
    }
    /// <summary>Gets the injected singleton.</summary>
    public SingletonViewModel ViewModel { get; }
}
