using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace BetterWinUI.Navigation.Frame;

/// <summary>
/// Executes ViewModel-first navigation through a dynamically attached WinUI Frame.
/// </summary>
public sealed class FrameNavigator
{
    private Microsoft.UI.Xaml.Controls.Frame? _frame;
    private readonly Func<Type, object?, Type> _resolvePage;

    /// <summary>
    /// Creates a navigator that resolves Pages from the supplied mutable map.
    /// Subsequent changes to the map affect future navigation.
    /// </summary>
    /// <param name="pages">The shared or navigator-specific Page map.</param>
    public FrameNavigator(PageMap pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        _resolvePage = (viewModelType, _) => pages.Resolve(viewModelType);
    }

    /// <summary>
    /// Creates a navigator that resolves Pages from an immutable map snapshot.
    /// </summary>
    /// <param name="pages">The frozen Page map.</param>
    public FrameNavigator(FrozenPageMap pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        _resolvePage = (viewModelType, _) => pages.Resolve(viewModelType);
    }

    /// <summary>Creates a navigator with an application-defined Page selection policy.</summary>
    /// <param name="resolvePage">Selects a Page from the ViewModel type and original parameter.</param>
    public FrameNavigator(Func<Type, object?, Type> resolvePage)
    {
        _resolvePage = resolvePage ?? throw new ArgumentNullException(nameof(resolvePage));
    }

    /// <summary>Gets the attached Frame, or null when detached.</summary>
    public Microsoft.UI.Xaml.Controls.Frame? Frame => _frame;

    /// <summary>Gets whether a Frame is attached.</summary>
    public bool IsAttached => _frame is not null;

    /// <summary>Gets whether the attached Frame has a back entry.</summary>
    public bool CanGoBack
    {
        get
        {
            var current = _frame;
            if (current is null) return false;

            EnsureThreadAccess(current);
            return current.CanGoBack;
        }
    }

    /// <summary>Gets whether the attached Frame has a forward entry.</summary>
    public bool CanGoForward
    {
        get
        {
            var current = _frame;
            if (current is null) return false;

            EnsureThreadAccess(current);
            return current.CanGoForward;
        }
    }

    /// <summary>Attaches a Frame until the returned lease is disposed on its UI thread.</summary>
    /// <param name="frame">The Frame to attach.</param>
    /// <returns>The attachment lease.</returns>
    public IDisposable Attach(Microsoft.UI.Xaml.Controls.Frame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        EnsureThreadAccess(frame);

        if (_frame is not null)
            throw new InvalidOperationException(
                "A Frame is already attached. Dispose the active attachment before attaching another Frame.");

        _frame = frame;
        return new FrameAttachment(this, frame);
    }

    /// <summary>Navigates to a ViewModel with an optional original parameter and transition.</summary>
    /// <typeparam name="TViewModel">The logical target.</typeparam>
    /// <param name="parameter">The original parameter passed to the Page.</param>
    /// <param name="transitionInfo">The optional native transition.</param>
    /// <returns>The native Frame.Navigate result.</returns>
    public bool Navigate<TViewModel>(object? parameter = null, NavigationTransitionInfo? transitionInfo = null)
        where TViewModel : class => Navigate(FrameNavigationRequest.For<TViewModel>(parameter, transitionInfo));

    /// <summary>Navigates with an explicitly selected compile-time parameter association.</summary>
    /// <typeparam name="TViewModel">The logical target.</typeparam>
    /// <typeparam name="TParameter">The associated parameter type.</typeparam>
    /// <param name="parameter">The original parameter passed to the Page.</param>
    /// <param name="transitionInfo">The optional native transition.</param>
    /// <returns>The native Frame.Navigate result.</returns>
    public bool Navigate<TViewModel, TParameter>(TParameter parameter, NavigationTransitionInfo? transitionInfo = null)
        where TViewModel : class where TParameter : INavigationParameter<TViewModel> =>
        Navigate(FrameNavigationRequest.For<TViewModel>(parameter, transitionInfo));

    /// <summary>Resolves and executes a request without wrapping its native navigation parameter.</summary>
    /// <param name="request">The immutable navigation call.</param>
    /// <returns>The native Frame.Navigate result; exceptions are not converted to false.</returns>
    public bool Navigate(FrameNavigationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var frame = GetAttachedFrame();
        var pageType = _resolvePage(request.ViewModelType, request.Parameter);
        ValidatePage(pageType);
        if (!ReferenceEquals(frame, _frame))
            throw new InvalidOperationException("The Frame attachment changed during Page resolution.");
        return request.TransitionInfo is null
            ? frame.Navigate(pageType, request.Parameter)
            : frame.Navigate(pageType, request.Parameter, request.TransitionInfo);
    }

    /// <summary>Navigates backward using the native Frame history.</summary>
    public void GoBack()
    {
        var current = GetAttachedFrame();
        EnsureCanGoBack(current);
        current.GoBack();
    }

    /// <summary>Navigates backward with the supplied native transition.</summary>
    /// <param name="transitionInfo">The transition to use.</param>
    public void GoBack(NavigationTransitionInfo transitionInfo)
    {
        ArgumentNullException.ThrowIfNull(transitionInfo);
        var current = GetAttachedFrame();
        EnsureCanGoBack(current);
        current.GoBack(transitionInfo);
    }

    /// <summary>Navigates forward using the native Frame history.</summary>
    public void GoForward()
    {
        var current = GetAttachedFrame();
        if (!current.CanGoForward)
            throw new InvalidOperationException(
                "The attached Frame has no forward navigation entry.");

        current.GoForward();
    }

    /// <summary>Rejects back navigation when the native history is empty.</summary>
    private static void EnsureCanGoBack(Microsoft.UI.Xaml.Controls.Frame frame)
    {
        if (!frame.CanGoBack)
            throw new InvalidOperationException(
                "The attached Frame has no back navigation entry.");
    }

    /// <summary>Validates Page types returned by an application resolver.</summary>
    private static void ValidatePage(Type pageType)
    {
        ArgumentNullException.ThrowIfNull(pageType);
        if (pageType == typeof(Page) || !typeof(Page).IsAssignableFrom(pageType) ||
            pageType.IsAbstract || pageType.ContainsGenericParameters)
            throw new ArgumentException($"Type '{pageType}' must be a concrete, closed Page subclass.", nameof(pageType));
    }

    /// <summary>Checks access to the attached Frame's UI thread.</summary>
    private static void EnsureThreadAccess(Microsoft.UI.Xaml.Controls.Frame frame)
    {
        if (!frame.DispatcherQueue.HasThreadAccess)
            throw new InvalidOperationException(
                "Frame navigation must be performed on the Frame's UI thread.");
    }

    /// <summary>Gets the current attachment and checks its thread affinity.</summary>
    private Microsoft.UI.Xaml.Controls.Frame GetAttachedFrame()
    {
        var current = _frame ??
                      throw new InvalidOperationException(
                          "No Frame is attached to this navigation host.");
        EnsureThreadAccess(current);
        return current;
    }

    /// <summary>Releases only the Frame owned by the disposing lease.</summary>
    private void Detach(Microsoft.UI.Xaml.Controls.Frame attachedFrame)
    {
        EnsureThreadAccess(attachedFrame);
        if (ReferenceEquals(_frame, attachedFrame)) _frame = null;
    }

    /// <summary>Owns one attachment and allows disposal to be retried after a wrong-thread call.</summary>
    private sealed class FrameAttachment(
        FrameNavigator owner,
        Microsoft.UI.Xaml.Controls.Frame attachedFrame) : IDisposable
    {
        private int _isDisposed;

        /// <inheritdoc />
        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _isDisposed, 1, 0) != 0) return;

            try
            {
                owner.Detach(attachedFrame);
            }
            catch
            {
                Volatile.Write(ref _isDisposed, 0);
                throw;
            }
        }
    }
}
