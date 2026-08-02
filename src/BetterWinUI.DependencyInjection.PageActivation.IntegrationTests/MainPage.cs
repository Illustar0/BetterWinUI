using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace BetterWinUI.DependencyInjection.PageActivation.IntegrationTests;

/// <summary>
/// A generated-registration integration fixture.
/// </summary>
[View]
public sealed partial class MainPage : Page
{
    /// <summary>
    /// Initializes the fixture through constructor injection.
    /// </summary>
    public MainPage(MainViewModel viewModel)
    {
        ViewModel = viewModel;
    }

    /// <summary>
    /// Gets the injected view model without assigning DataContext.
    /// </summary>
    public MainViewModel ViewModel { get; }
}

/// <summary>
/// A concrete transient view model fixture.
/// </summary>
[ViewModel(ServiceLifetime.Transient)]
public sealed class MainViewModel;