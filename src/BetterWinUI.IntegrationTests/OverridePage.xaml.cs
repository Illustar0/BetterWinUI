using BetterWinUI.DependencyInjection.PageActivation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace BetterWinUI.IntegrationTests;

/// <summary>Supplies a real XAML Page for activation and navigation tests.</summary>
[View]
public sealed partial class OverridePage : Page
{
    /// <summary>Receives the explicitly registered ViewModel.</summary>
    public OverridePage(OverrideViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
    }
    /// <summary>Gets the explicit registration.</summary>
    public OverrideViewModel ViewModel { get; }
}
