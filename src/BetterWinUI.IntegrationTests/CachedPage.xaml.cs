using BetterWinUI.PageActivation.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace BetterWinUI.IntegrationTests;

/// <summary>Requests native Page caching during construction.</summary>
[View]
public sealed partial class CachedPage : Page
{
    /// <summary>Enables the native cache before the Page enters the Frame.</summary>
    public CachedPage()
    {
        InitializeComponent();
        NavigationCacheMode = NavigationCacheMode.Required;
    }
}
