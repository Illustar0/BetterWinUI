using BetterWinUI.DependencyInjection.PageActivation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace BetterWinUI.IntegrationTests;

/// <summary>Supplies a real XAML Page for activation and navigation tests.</summary>
[View]
public sealed partial class SecondPage : Page
{
    /// <summary>Initializes a second native navigation target.</summary>
    public SecondPage() => InitializeComponent();
}
