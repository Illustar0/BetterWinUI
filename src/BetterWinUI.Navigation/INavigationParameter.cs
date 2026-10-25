namespace BetterWinUI.Navigation;

#pragma warning disable S2326 // The generic argument is the marker's compile-time association.

/// <summary>Defines a navigation parameter type that belongs to one ViewModel navigation target.</summary>
/// <typeparam name="TViewModel">The ViewModel type that consumes the parameter.</typeparam>
/// <remarks>
/// Implementing this marker is optional. It enables navigator overloads that accept only parameters associated
/// with the target. It neither restricts the open-parameter overloads nor registers a parameter type for the target.
/// </remarks>
public interface INavigationParameter<TViewModel>
    where TViewModel : class;

#pragma warning restore S2326
