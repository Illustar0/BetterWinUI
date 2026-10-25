using BetterWinUI.PageActivation.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace BetterWinUI.IntegrationTests;

/// <summary>Produces a constructor failure through actual native Page activation.</summary>
[View]
public sealed partial class FailingPage : Page
{
    /// <summary>The message of the exception thrown by the constructor.</summary>
    public const string FailureMessage = "Page construction failed.";

    /// <summary>Throws after initializing the real XAML Page.</summary>
    public FailingPage()
    {
        InitializeComponent();
        throw new InvalidOperationException(FailureMessage);
    }
}
