using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace BetterWinUI.Navigation.Frame;

/// <summary>
/// Exposes a dynamically attached WinUI <see cref="Microsoft.UI.Xaml.Controls.Frame"/>
/// and its navigation capabilities.
/// </summary>
public interface IFrameNavigationHost
{
    /// <summary>Gets the attached Frame, or <see langword="null"/> when detached.</summary>
    Microsoft.UI.Xaml.Controls.Frame? Frame { get; }

    /// <summary>Gets whether a Frame is currently attached.</summary>
    bool IsAttached { get; }

    /// <summary>Gets whether the attached Frame can navigate backward.</summary>
    bool CanGoBack { get; }

    /// <summary>Gets whether the attached Frame can navigate forward.</summary>
    bool CanGoForward { get; }

    /// <summary>Attaches a Frame until the returned lease is disposed.</summary>
    /// <param name="frame">The Frame to attach.</param>
    /// <returns>A lease that detaches this exact Frame when disposed.</returns>
    IDisposable Attach(Microsoft.UI.Xaml.Controls.Frame frame);

    /// <summary>Navigates to a Page type.</summary>
    /// <param name="pageType">The destination Page type.</param>
    /// <returns>The underlying Frame navigation result.</returns>
    bool Navigate(Type pageType);

    /// <summary>Navigates to a Page type with a parameter.</summary>
    /// <param name="pageType">The destination Page type.</param>
    /// <param name="parameter">The navigation parameter.</param>
    /// <returns>The underlying Frame navigation result.</returns>
    bool Navigate(Type pageType, object? parameter);

    /// <summary>Navigates to a Page type with a parameter and transition.</summary>
    /// <param name="pageType">The destination Page type.</param>
    /// <param name="parameter">The navigation parameter.</param>
    /// <param name="transitionInfo">The transition to apply.</param>
    /// <returns>The underlying Frame navigation result.</returns>
    bool Navigate(
        Type pageType,
        object? parameter,
        NavigationTransitionInfo transitionInfo);

    /// <summary>Navigates backward using the default transition.</summary>
    void GoBack();

    /// <summary>Navigates backward using a specific transition.</summary>
    /// <param name="transitionInfo">The transition to apply.</param>
    void GoBack(NavigationTransitionInfo transitionInfo);

    /// <summary>Navigates forward.</summary>
    void GoForward();
}