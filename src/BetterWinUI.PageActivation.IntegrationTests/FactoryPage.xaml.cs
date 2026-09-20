using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace BetterWinUI.PageActivation.IntegrationTests;

/// <summary>Requires an application-owned dependency and participates in native Page caching.</summary>
public sealed partial class FactoryPage : Page
{
    /// <summary>Gets the dependency passed by the application factory.</summary>
    public object Dependency { get; }

    /// <summary>Creates a real Page without a default constructor.</summary>
    public FactoryPage(object dependency)
    {
        InitializeComponent();
        Dependency = dependency;
        NavigationCacheMode = NavigationCacheMode.Required;
    }
}
