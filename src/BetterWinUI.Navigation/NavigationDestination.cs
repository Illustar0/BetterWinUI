namespace BetterWinUI.Navigation;

/// <summary>
/// Describes one immutable ViewModel, route, View, and parameter mapping.
/// </summary>
public sealed class NavigationDestination
{
    internal NavigationDestination(
        Type viewModelType,
        Type viewType,
        string route,
        Type? parameterType)
    {
        ViewModelType = viewModelType;
        ViewType = viewType;
        Route = route;
        ParameterType = parameterType;
    }

    /// <summary>Gets the ViewModel type that identifies this destination.</summary>
    public Type ViewModelType { get; }

    /// <summary>Gets the View type interpreted by the selected navigation adapter.</summary>
    public Type ViewType { get; }

    /// <summary>Gets the exact, case-sensitive route identifier.</summary>
    public string Route { get; }

    /// <summary>
    /// Gets the required parameter type, or <see langword="null"/> when the
    /// destination accepts no parameter.
    /// </summary>
    public Type? ParameterType { get; }

    /// <summary>Gets whether this destination requires a parameter.</summary>
    public bool RequiresParameter => ParameterType is not null;
}