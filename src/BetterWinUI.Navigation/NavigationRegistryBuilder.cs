using System.Collections.Frozen;

namespace BetterWinUI.Navigation;

/// <summary>
/// Builds an immutable navigation destination registry.
/// </summary>
public sealed class NavigationRegistryBuilder
{
    private readonly Dictionary<Type, NavigationDestination> _destinationsByViewModel = [];
    private readonly Dictionary<Type, NavigationDestination> _destinationsByView = [];

    private readonly Dictionary<string, NavigationDestination> _destinationsByRoute =
        new(StringComparer.Ordinal);

    private bool _isBuilt;

    /// <summary>Registers a destination that accepts no navigation parameter.</summary>
    /// <typeparam name="TViewModel">The ViewModel-first destination key.</typeparam>
    /// <typeparam name="TView">The View type interpreted by a navigation adapter.</typeparam>
    /// <param name="route">The exact, case-sensitive route identifier.</param>
    /// <returns>This builder.</returns>
    public NavigationRegistryBuilder Register<TViewModel, TView>(string route)
        where TViewModel : class
        where TView : class
    {
        return Register(typeof(TViewModel), typeof(TView), route, null);
    }

    /// <summary>Registers a destination that requires a navigation parameter.</summary>
    /// <typeparam name="TViewModel">The ViewModel-first destination key.</typeparam>
    /// <typeparam name="TView">The View type interpreted by a navigation adapter.</typeparam>
    /// <typeparam name="TParameter">The required navigation parameter type.</typeparam>
    /// <param name="route">The exact, case-sensitive route identifier.</param>
    /// <returns>This builder.</returns>
    public NavigationRegistryBuilder Register<TViewModel, TView, TParameter>(string route)
        where TViewModel : class
        where TView : class
        where TParameter : notnull
    {
        return Register(typeof(TViewModel), typeof(TView), route, typeof(TParameter));
    }

    /// <summary>
    /// Creates the immutable registry and prevents further changes to this builder.
    /// </summary>
    /// <returns>The completed navigation registry.</returns>
    public NavigationRegistry Build()
    {
        ThrowIfBuilt();
        _isBuilt = true;

        return new NavigationRegistry(
            _destinationsByViewModel.ToFrozenDictionary(),
            _destinationsByView.ToFrozenDictionary(),
            _destinationsByRoute.ToFrozenDictionary(StringComparer.Ordinal));
    }

    private NavigationRegistryBuilder Register(
        Type viewModelType,
        Type viewType,
        string route,
        Type? parameterType)
    {
        ThrowIfBuilt();
        ValidateRoute(route);

        var destination = new NavigationDestination(
            viewModelType,
            viewType,
            route,
            parameterType);

        EnsureUnique(_destinationsByViewModel, viewModelType, "ViewModel");
        EnsureUnique(_destinationsByView, viewType, "View");
        EnsureUnique(_destinationsByRoute, route, "route");

        _destinationsByViewModel.Add(viewModelType, destination);
        _destinationsByView.Add(viewType, destination);
        _destinationsByRoute.Add(route, destination);
        return this;
    }

    private static void ValidateRoute(string route)
    {
        ArgumentNullException.ThrowIfNull(route);
        if (route.Length == 0 ||
            char.IsWhiteSpace(route[0]) ||
            char.IsWhiteSpace(route[^1]))
            throw new ArgumentException(
                "A route must be non-empty and cannot have leading or trailing whitespace.",
                nameof(route));
    }

    private static void EnsureUnique<TKey>(
        Dictionary<TKey, NavigationDestination> destinations,
        TKey key,
        string keyKind)
        where TKey : notnull
    {
        if (destinations.TryGetValue(key, out var existing))
            throw new InvalidOperationException(
                $"The {keyKind} key '{key}' is already registered for route " +
                $"'{existing.Route}'. Navigation keys must be globally unique.");
    }

    private void ThrowIfBuilt()
    {
        if (_isBuilt)
            throw new InvalidOperationException(
                "This navigation registry builder has already been built.");
    }
}