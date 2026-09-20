using BetterWinUI.PageActivation.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace BetterWinUI.IntegrationTests;

/// <summary>Supplies a real XAML Page for activation and navigation tests.</summary>

public sealed partial class UnregisteredPage : Page
{
    /// <summary>Initializes a Page through the native XAML activator.</summary>
    public UnregisteredPage() => InitializeComponent();
}
