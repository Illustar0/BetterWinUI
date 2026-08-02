using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace BetterWinUI.Navigation.Frame;

/// <summary>
/// Resolves ViewModel-first destinations and executes them through one Frame host.
/// </summary>
/// <typeparam name="THost">The application-defined Frame host type.</typeparam>
public sealed class FrameNavigationService<THost>
    where THost : class, IFrameNavigationHost
{
    private readonly NavigationRegistry _registry;

    /// <summary>Initializes a host-bound Frame navigation service.</summary>
    /// <param name="registry">The immutable destination registry.</param>
    /// <param name="host">The Frame host used by this service.</param>
    public FrameNavigationService(NavigationRegistry registry, THost host)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        Host = host ?? throw new ArgumentNullException(nameof(host));
    }

    /// <summary>Gets the application-defined host bound to this service.</summary>
    public THost Host { get; }

    /// <summary>Gets whether the host can navigate backward.</summary>
    public bool CanGoBack => Host.CanGoBack;

    /// <summary>Gets whether the host can navigate forward.</summary>
    public bool CanGoForward => Host.CanGoForward;

    /// <summary>Navigates to a parameterless ViewModel destination.</summary>
    /// <typeparam name="TViewModel">The registered destination ViewModel.</typeparam>
    /// <param name="transitionInfo">An optional Frame transition.</param>
    /// <returns>The underlying Frame navigation result.</returns>
    public bool NavigateToViewModel<TViewModel>(
        NavigationTransitionInfo? transitionInfo = null)
        where TViewModel : class
    {
        var destination = _registry.GetByViewModel<TViewModel>();
        EnsureParameterless(destination);
        return Navigate(destination, null, transitionInfo);
    }

    /// <summary>Navigates to a ViewModel destination with runtime parameter validation.</summary>
    /// <typeparam name="TViewModel">The registered destination ViewModel.</typeparam>
    /// <param name="parameter">The required navigation parameter.</param>
    /// <param name="transitionInfo">An optional Frame transition.</param>
    /// <returns>The underlying Frame navigation result.</returns>
    public bool NavigateToViewModel<TViewModel>(
        object parameter,
        NavigationTransitionInfo? transitionInfo = null)
        where TViewModel : class
    {
        ArgumentNullException.ThrowIfNull(parameter);
        var destination = _registry.GetByViewModel<TViewModel>();
        EnsureParameter(destination, parameter.GetType());
        return Navigate(destination, parameter, transitionInfo);
    }

    /// <summary>Navigates with an optional compile-time ViewModel/parameter association.</summary>
    /// <typeparam name="TViewModel">The registered destination ViewModel.</typeparam>
    /// <typeparam name="TParameter">The associated navigation parameter type.</typeparam>
    /// <param name="parameter">The required navigation parameter.</param>
    /// <param name="transitionInfo">An optional Frame transition.</param>
    /// <returns>The underlying Frame navigation result.</returns>
    public bool NavigateToViewModel<TViewModel, TParameter>(
        TParameter parameter,
        NavigationTransitionInfo? transitionInfo = null)
        where TViewModel : class
        where TParameter : INavigationParameter<TViewModel>
    {
        ArgumentNullException.ThrowIfNull(parameter);
        var destination = _registry.GetByViewModel<TViewModel>();
        EnsureParameter(destination, typeof(TParameter));
        return Navigate(destination, parameter, transitionInfo);
    }

    /// <summary>Attempts to navigate to a parameterless route.</summary>
    /// <param name="route">The exact, case-sensitive route identifier.</param>
    /// <param name="transitionInfo">An optional Frame transition.</param>
    /// <returns><see langword="false"/> when the route is unknown, expects a parameter, or Frame navigation fails.</returns>
    public bool TryNavigateToRoute(
        string route,
        NavigationTransitionInfo? transitionInfo = null)
    {
        return _registry.TryGetByRoute(route, out var destination) &&
               !destination.RequiresParameter &&
               Navigate(destination, null, transitionInfo);
    }

    /// <summary>Attempts to navigate to a parameterized route.</summary>
    /// <typeparam name="TParameter">The supplied parameter type.</typeparam>
    /// <param name="route">The exact, case-sensitive route identifier.</param>
    /// <param name="parameter">The required navigation parameter.</param>
    /// <param name="transitionInfo">An optional Frame transition.</param>
    /// <returns><see langword="false"/> when the route or parameter type does not match, or Frame navigation fails.</returns>
    public bool TryNavigateToRoute<TParameter>(
        string route,
        TParameter parameter,
        NavigationTransitionInfo? transitionInfo = null)
        where TParameter : notnull
    {
        ArgumentNullException.ThrowIfNull(parameter);
        return _registry.TryGetByRoute(route, out var destination) &&
               destination.ParameterType == typeof(TParameter) &&
               Navigate(destination, parameter, transitionInfo);
    }

    /// <summary>Navigates backward using the default transition.</summary>
    public void GoBack()
    {
        Host.GoBack();
    }

    /// <summary>Navigates backward using a specific transition.</summary>
    /// <param name="transitionInfo">The transition to apply.</param>
    public void GoBack(NavigationTransitionInfo transitionInfo)
    {
        Host.GoBack(transitionInfo);
    }

    /// <summary>Navigates forward.</summary>
    public void GoForward()
    {
        Host.GoForward();
    }

    private static void EnsureParameterless(NavigationDestination destination)
    {
        if (destination.RequiresParameter)
            throw new InvalidOperationException(
                $"Navigation destination '{destination.Route}' requires parameter type " +
                $"'{destination.ParameterType}'.");
    }

    private static void EnsureParameter(
        NavigationDestination destination,
        Type suppliedParameterType)
    {
        if (destination.ParameterType != suppliedParameterType)
            throw new InvalidOperationException(
                $"Navigation destination '{destination.Route}' requires parameter type " +
                $"'{destination.ParameterType}', but '{suppliedParameterType}' was supplied.");
    }

    private bool Navigate(
        NavigationDestination destination,
        object? parameter,
        NavigationTransitionInfo? transitionInfo)
    {
        if (!typeof(Page).IsAssignableFrom(destination.ViewType))
            throw new InvalidOperationException(
                $"View type '{destination.ViewType}' registered for route " +
                $"'{destination.Route}' must derive from '{typeof(Page)}' for Frame navigation.");

        return transitionInfo is null
            ? Host.Navigate(destination.ViewType, parameter)
            : Host.Navigate(destination.ViewType, parameter, transitionInfo);
    }
}