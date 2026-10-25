using BetterWinUI.PageActivation.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace BetterWinUI.IntegrationTests;

/// <summary>Is registered explicitly by the test application instead of with the View attribute.</summary>

public sealed partial class ManualPage : Page
{
    public ManualPage() => InitializeComponent();
}
