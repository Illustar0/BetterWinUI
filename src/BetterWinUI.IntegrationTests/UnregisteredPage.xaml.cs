using BetterWinUI.PageActivation.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace BetterWinUI.IntegrationTests;

/// <summary>Has no service registration, so DI activation must fail instead of falling back to native activation.</summary>

public sealed partial class UnregisteredPage : Page
{
    public UnregisteredPage() => InitializeComponent();
}
