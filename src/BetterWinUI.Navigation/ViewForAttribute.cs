namespace BetterWinUI.Navigation;

/// <summary>
/// Generates a parameterless navigation mapping from a ViewModel to the annotated View.
/// </summary>
/// <typeparam name="TViewModel">The ViewModel-first destination key.</typeparam>
/// <param name="route">The exact, case-sensitive route identifier.</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ViewForAttribute<TViewModel>(string route) : Attribute
    where TViewModel : class
{
    /// <summary>Gets the exact, case-sensitive route identifier.</summary>
    public string Route { get; } = route;
}

/// <summary>
/// Generates a parameterized navigation mapping from a ViewModel to the annotated View.
/// </summary>
/// <typeparam name="TViewModel">The ViewModel-first destination key.</typeparam>
/// <typeparam name="TParameter">The required navigation parameter type.</typeparam>
/// <param name="route">The exact, case-sensitive route identifier.</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ViewForAttribute<TViewModel, TParameter>(string route) : Attribute
    where TViewModel : class
    where TParameter : notnull
{
    /// <summary>Gets the exact, case-sensitive route identifier.</summary>
    public string Route { get; } = route;
}