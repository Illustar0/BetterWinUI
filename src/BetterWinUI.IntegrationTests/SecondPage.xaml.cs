using BetterWinUI.PageActivation.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace BetterWinUI.IntegrationTests;

/// <summary>Serves as a second navigation target for history and remapping tests.</summary>
[View]
public sealed partial class SecondPage : Page
{
    public SecondPage() => InitializeComponent();
}
