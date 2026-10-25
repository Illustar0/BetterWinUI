using Microsoft.UI.Xaml.Controls;

namespace BetterWinUI.PageActivation.IntegrationTests;

/// <summary>Provides a native default constructor to detect unintended fallback.</summary>
public sealed partial class NullPage : Page
{
    public static int Constructions { get; private set; }

    public NullPage()
    {
        InitializeComponent();
        Constructions++;
    }
}
