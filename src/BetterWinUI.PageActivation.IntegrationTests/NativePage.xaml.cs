using Microsoft.UI.Xaml.Controls;

namespace BetterWinUI.PageActivation.IntegrationTests;

/// <summary>Provides a native default constructor to detect unintended fallback.</summary>
public sealed partial class NativePage : Page
{
    /// <summary>Gets the number of native constructions.</summary>
    public static int Constructions { get; private set; }

    /// <summary>Initializes a real XAML Page.</summary>
    public NativePage()
    {
        InitializeComponent();
        Constructions++;
    }
}
