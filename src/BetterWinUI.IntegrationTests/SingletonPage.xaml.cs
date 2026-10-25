using BetterWinUI.PageActivation.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace BetterWinUI.IntegrationTests;

/// <summary>Is registered as a singleton together with its ViewModel.</summary>
[View(ServiceLifetime.Singleton)]
public sealed partial class SingletonPage : Page
{
    public SingletonPage(SingletonViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
    }

    public SingletonViewModel ViewModel { get; }
}
