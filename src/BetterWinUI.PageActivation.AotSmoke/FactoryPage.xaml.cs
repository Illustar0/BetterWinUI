using Microsoft.UI.Xaml.Controls;

namespace BetterWinUI.PageActivation.AotSmoke;

/// <summary>Requires the application factory because it has no default constructor.</summary>
public sealed partial class FactoryPage : Page
{
    /// <summary>Gets the dependency supplied by the application factory.</summary>
    public object Dependency { get; }

    /// <summary>Initializes the Page with an application-owned dependency.</summary>
    public FactoryPage(object dependency)
    {
        InitializeComponent();
        Dependency = dependency;
    }
}
