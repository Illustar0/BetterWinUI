using BetterWinUI.PageActivation.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace BetterWinUI.IntegrationTests;

/// <summary>Receives a ViewModel whose explicit registration replaces the generated one.</summary>
[View]
public sealed partial class OverridePage : Page
{
    public OverridePage(OverrideViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
    }

    public OverrideViewModel ViewModel { get; }
}
