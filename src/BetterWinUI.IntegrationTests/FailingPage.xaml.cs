using BetterWinUI.DependencyInjection.PageActivation;
using Microsoft.UI.Xaml.Controls;

namespace BetterWinUI.IntegrationTests;

/// <summary>Produces a constructor failure through actual native Page activation.</summary>
[View]
public sealed partial class FailingPage : Page
{
    /// <summary>Throws after initializing the real XAML Page.</summary>
    public FailingPage()
    {
        InitializeComponent();
        throw new InvalidOperationException("Page construction failed.");
    }
}
