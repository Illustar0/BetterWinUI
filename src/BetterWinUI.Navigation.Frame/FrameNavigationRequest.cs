using Microsoft.UI.Xaml.Media.Animation;

namespace BetterWinUI.Navigation.Frame;

/// <summary>Describes one ViewModel-first Frame navigation call.</summary>
/// <remarks>References are retained without copying the parameter or transition.</remarks>
public sealed class FrameNavigationRequest
{
    /// <summary>Captures the three independent values of a Frame navigation call.</summary>
    private FrameNavigationRequest(Type viewModelType, object? parameter, NavigationTransitionInfo? transitionInfo)
    {
        ViewModelType = viewModelType;
        Parameter = parameter;
        TransitionInfo = transitionInfo;
    }

    /// <summary>Gets the logical ViewModel target.</summary>
    public Type ViewModelType { get; }

    /// <summary>Gets the original parameter, or null when no parameter is supplied.</summary>
    public object? Parameter { get; }

    /// <summary>Gets the optional native Frame transition.</summary>
    public NavigationTransitionInfo? TransitionInfo { get; }

    /// <summary>Creates a request without imposing a target-specific parameter contract.</summary>
    /// <typeparam name="TViewModel">The logical ViewModel target.</typeparam>
    /// <param name="parameter">The original navigation parameter.</param>
    /// <param name="transitionInfo">The optional native transition.</param>
    /// <returns>The immutable request.</returns>
    public static FrameNavigationRequest For<TViewModel>(
        object? parameter = null,
        NavigationTransitionInfo? transitionInfo = null)
        where TViewModel : class => new(typeof(TViewModel), parameter, transitionInfo);
}
