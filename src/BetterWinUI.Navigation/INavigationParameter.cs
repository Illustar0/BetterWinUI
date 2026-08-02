namespace BetterWinUI.Navigation;

/// <summary>
/// Associates a navigation parameter with the view model that consumes it.
/// </summary>
/// <typeparam name="TViewModel">The destination view model type.</typeparam>
/// <remarks>
/// Implementing this interface is optional. It enables constrained navigation
/// overloads supplied by an adapter. Registrations remain the authoritative
/// runtime mapping.
/// </remarks>
public interface INavigationParameter<TViewModel>
    where TViewModel : class;