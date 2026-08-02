using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace BetterWinUI.Navigation;

/// <summary>
/// Resolves immutable navigation destinations by ViewModel, route, or View type.
/// </summary>
public sealed class NavigationRegistry
{
    private readonly FrozenDictionary<Type, NavigationDestination> _destinationsByViewModel;
    private readonly FrozenDictionary<Type, NavigationDestination> _destinationsByView;
    private readonly FrozenDictionary<string, NavigationDestination> _destinationsByRoute;

    internal NavigationRegistry(
        FrozenDictionary<Type, NavigationDestination> destinationsByViewModel,
        FrozenDictionary<Type, NavigationDestination> destinationsByView,
        FrozenDictionary<string, NavigationDestination> destinationsByRoute)
    {
        _destinationsByViewModel = destinationsByViewModel;
        _destinationsByView = destinationsByView;
        _destinationsByRoute = destinationsByRoute;
    }

    /// <summary>Gets every registered destination.</summary>
    public IReadOnlyCollection<NavigationDestination> Destinations =>
        _destinationsByRoute.Values;

    /// <summary>Gets the destination associated with a ViewModel type.</summary>
    /// <typeparam name="TViewModel">The registered ViewModel type.</typeparam>
    /// <returns>The registered destination.</returns>
    public NavigationDestination GetByViewModel<TViewModel>()
        where TViewModel : class
    {
        return GetByViewModel(typeof(TViewModel));
    }

    /// <summary>Gets the destination associated with a ViewModel type.</summary>
    /// <param name="viewModelType">The registered ViewModel type.</param>
    /// <returns>The registered destination.</returns>
    public NavigationDestination GetByViewModel(Type viewModelType)
    {
        ArgumentNullException.ThrowIfNull(viewModelType);
        return _destinationsByViewModel.TryGetValue(viewModelType, out var destination)
            ? destination
            : throw new InvalidOperationException(
                $"No navigation destination is registered for ViewModel '{viewModelType}'.");
    }

    /// <summary>Attempts to resolve an exact, case-sensitive route identifier.</summary>
    /// <param name="route">The route identifier.</param>
    /// <param name="destination">The resolved destination when successful.</param>
    /// <returns><see langword="true"/> when the route is registered.</returns>
    public bool TryGetByRoute(
        string route,
        [NotNullWhen(true)] out NavigationDestination? destination)
    {
        ArgumentNullException.ThrowIfNull(route);
        return _destinationsByRoute.TryGetValue(route, out destination);
    }

    /// <summary>Attempts to resolve a destination by its View type.</summary>
    /// <param name="viewType">The registered View type.</param>
    /// <param name="destination">The resolved destination when successful.</param>
    /// <returns><see langword="true"/> when the View is registered.</returns>
    public bool TryGetByView(
        Type viewType,
        [NotNullWhen(true)] out NavigationDestination? destination)
    {
        ArgumentNullException.ThrowIfNull(viewType);
        return _destinationsByView.TryGetValue(viewType, out destination);
    }
}