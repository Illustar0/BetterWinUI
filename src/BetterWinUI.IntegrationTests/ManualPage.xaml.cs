using BetterWinUI.DependencyInjection.PageActivation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace BetterWinUI.IntegrationTests;

/// <summary>Supplies a real XAML Page for activation and navigation tests.</summary>

public sealed partial class ManualPage : Page
{
    /// <summary>Initializes a manually registered Page.</summary>
    public ManualPage() => InitializeComponent();
}
