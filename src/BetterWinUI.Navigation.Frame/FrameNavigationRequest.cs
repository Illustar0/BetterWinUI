using Microsoft.UI.Xaml.Media.Animation;

namespace BetterWinUI.Navigation.Frame;

/// <summary>Describes one navigation to a ViewModel target.</summary>
/// <remarks>The request keeps references to the parameter and the transition; it does not copy them.</remarks>
public sealed class FrameNavigationRequest
{
    private FrameNavigationRequest(Type viewModelType, object? parameter, NavigationTransitionInfo? transitionInfo)
    {
        ViewModelType = viewModelType;
        Parameter = parameter;
        TransitionInfo = transitionInfo;
    }

    /// <summary>Gets the ViewModel type that identifies the navigation target.</summary>
    public Type ViewModelType { get; }

    /// <summary>Gets the parameter passed unchanged to the Page.</summary>
    /// <value>The parameter, or <see langword="null" /> if none is supplied.</value>
    public object? Parameter { get; }

    /// <summary>Gets the transition to play.</summary>
    /// <value>The transition, or <see langword="null" /> for the Frame's default transition.</value>
    public NavigationTransitionInfo? TransitionInfo { get; }

    /// <summary>Creates a request to navigate to a ViewModel target.</summary>
    /// <typeparam name="TViewModel">The ViewModel type that identifies the navigation target.</typeparam>
    /// <param name="parameter">The parameter passed unchanged to the Page, or <see langword="null" /> for none.</param>
    /// <param name="transitionInfo">The transition to play, or <see langword="null" /> for the default transition.</param>
    /// <returns>A request for the target.</returns>
    /// <remarks>
    /// The parameter can be any object; implementing <see cref="INavigationParameter{TViewModel}" /> is not required.
    /// </remarks>
    public static FrameNavigationRequest For<TViewModel>(
        object? parameter = null,
        NavigationTransitionInfo? transitionInfo = null)
        where TViewModel : class => new(typeof(TViewModel), parameter, transitionInfo);
}
