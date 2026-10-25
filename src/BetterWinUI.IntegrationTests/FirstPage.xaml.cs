using BetterWinUI.PageActivation.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace BetterWinUI.IntegrationTests;

/// <summary>Requires a generated ViewModel registration and a keyed service through its constructor.</summary>
[View]
public sealed partial class FirstPage : Page
{
    public FirstPage(FirstViewModel viewModel, [FromKeyedServices("clock")] TestClock clock)
    {
        InitializeComponent();
        ViewModel = viewModel;
        Clock = clock;
    }

    public FirstViewModel ViewModel { get; }

    public TestClock Clock { get; }
}
