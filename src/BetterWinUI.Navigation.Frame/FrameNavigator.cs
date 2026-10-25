using Microsoft.UI.Xaml.Media.Animation;

namespace BetterWinUI.Navigation.Frame;

/// <summary>Navigates an attached WinUI Frame to the Page that presents a ViewModel target.</summary>
/// <remarks>
/// A navigator has at most one attached Frame at a time. Attach, navigate, and detach on that Frame's UI thread.
/// Back and forward navigation use the Frame's native history.
/// </remarks>
public sealed class FrameNavigator
{
    private Microsoft.UI.Xaml.Controls.Frame? _frame;
    private readonly PageResolver _pages;

    /// <summary>Initializes a new instance of the <see cref="FrameNavigator" /> class.</summary>
    /// <param name="pages">
    /// The resolver that selects Page types: a <see cref="PageMap" />, whose later changes affect future navigation;
    /// a <see cref="FrozenPageMap" /> snapshot; or an application policy from <see cref="PageResolver.Create" />.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="pages" /> is <see langword="null" />.</exception>
    public FrameNavigator(PageResolver pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        _pages = pages;
    }

    /// <summary>Gets the attached Frame.</summary>
    /// <value>The attached Frame, or <see langword="null" /> if no Frame is attached.</value>
    public Microsoft.UI.Xaml.Controls.Frame? Frame => _frame;

    /// <summary>Gets a value that indicates whether a Frame is attached.</summary>
    public bool IsAttached => _frame is not null;

    /// <summary>Gets a value that indicates whether the attached Frame has a back history entry.</summary>
    /// <value>
    /// <see langword="true" /> if a Frame is attached and has a back entry; otherwise, <see langword="false" />.
    /// </value>
    /// <exception cref="InvalidOperationException">The property is read off the attached Frame's UI thread.</exception>
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

    /// <summary>Gets a value that indicates whether the attached Frame has a forward history entry.</summary>
    /// <value>
    /// <see langword="true" /> if a Frame is attached and has a forward entry; otherwise, <see langword="false" />.
    /// </value>
    /// <exception cref="InvalidOperationException">The property is read off the attached Frame's UI thread.</exception>
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

    /// <summary>Attaches a Frame to this navigator.</summary>
    /// <param name="frame">The Frame that receives navigation.</param>
    /// <returns>A lease that detaches the Frame when disposed on the Frame's UI thread.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="frame" /> is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException">
    /// The method is called off the Frame's UI thread.
    /// -or-
    /// Another Frame is attached.
    /// </exception>
    /// <remarks>
    /// Keep the lease for the lifetime of the host. Disposing it again, or after another Frame is attached, has no
    /// effect.
    /// </remarks>
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

    /// <summary>Navigates to the Page that presents a ViewModel target.</summary>
    /// <typeparam name="TViewModel">The ViewModel type that identifies the navigation target.</typeparam>
    /// <param name="parameter">The parameter passed unchanged to the Page, or <see langword="null" /> for none.</param>
    /// <param name="transitionInfo">The transition to play, or <see langword="null" /> for the default transition.</param>
    /// <returns>
    /// <see langword="true" /> if the Frame navigated; <see langword="false" /> if the navigation was canceled.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// No Frame is attached.
    /// -or-
    /// The method is called off the attached Frame's UI thread.
    /// -or-
    /// The attachment changed while the Page type was being resolved.
    /// </exception>
    /// <remarks>Exceptions from the resolver and from Frame navigation propagate unchanged.</remarks>
    public bool Navigate<TViewModel>(object? parameter = null, NavigationTransitionInfo? transitionInfo = null)
        where TViewModel : class => Navigate(FrameNavigationRequest.For<TViewModel>(parameter, transitionInfo));

    /// <summary>Navigates to the Page that presents a ViewModel target with a parameter associated with it.</summary>
    /// <typeparam name="TViewModel">The ViewModel type that identifies the navigation target.</typeparam>
    /// <typeparam name="TParameter">A parameter type that implements <see cref="INavigationParameter{TViewModel}" />.</typeparam>
    /// <param name="parameter">The parameter passed unchanged to the Page.</param>
    /// <param name="transitionInfo">The transition to play, or <see langword="null" /> for the default transition.</param>
    /// <returns>
    /// <see langword="true" /> if the Frame navigated; <see langword="false" /> if the navigation was canceled.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// No Frame is attached.
    /// -or-
    /// The method is called off the attached Frame's UI thread.
    /// -or-
    /// The attachment changed while the Page type was being resolved.
    /// </exception>
    /// <remarks>Exceptions from the resolver and from Frame navigation propagate unchanged.</remarks>
    public bool Navigate<TViewModel, TParameter>(TParameter parameter, NavigationTransitionInfo? transitionInfo = null)
        where TViewModel : class where TParameter : INavigationParameter<TViewModel> =>
        Navigate(FrameNavigationRequest.For<TViewModel>(parameter, transitionInfo));

    /// <summary>Navigates to the Page that presents the target of a request.</summary>
    /// <param name="request">The navigation request.</param>
    /// <returns>
    /// <see langword="true" /> if the Frame navigated; <see langword="false" /> if the navigation was canceled.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="request" /> is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException">
    /// No Frame is attached.
    /// -or-
    /// The method is called off the attached Frame's UI thread.
    /// -or-
    /// The attachment changed while the Page type was being resolved.
    /// </exception>
    /// <remarks>
    /// The Page receives <see cref="FrameNavigationRequest.Parameter" /> itself, not the request. Exceptions from the
    /// resolver and from Frame navigation propagate unchanged.
    /// </remarks>
    public bool Navigate(FrameNavigationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var frame = GetAttachedFrame();
        var pageType = _pages.Resolve(request.ViewModelType, request.Parameter);
        // An application policy can run arbitrary code, including disposing the lease of this Frame.
        if (!ReferenceEquals(frame, _frame))
            throw new InvalidOperationException("The Frame attachment changed during Page resolution.");
        return request.TransitionInfo is null
            ? frame.Navigate(pageType, request.Parameter)
            : frame.Navigate(pageType, request.Parameter, request.TransitionInfo);
    }

    /// <summary>Navigates to the previous entry in the attached Frame's history.</summary>
    /// <exception cref="InvalidOperationException">
    /// No Frame is attached.
    /// -or-
    /// The method is called off the attached Frame's UI thread.
    /// -or-
    /// The Frame has no back entry.
    /// </exception>
    public void GoBack()
    {
        var current = GetAttachedFrame();
        EnsureCanGoBack(current);
        current.GoBack();
    }

    /// <summary>Navigates to the previous entry in the attached Frame's history with a transition.</summary>
    /// <param name="transitionInfo">The transition to play.</param>
    /// <exception cref="ArgumentNullException"><paramref name="transitionInfo" /> is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException">
    /// No Frame is attached.
    /// -or-
    /// The method is called off the attached Frame's UI thread.
    /// -or-
    /// The Frame has no back entry.
    /// </exception>
    public void GoBack(NavigationTransitionInfo transitionInfo)
    {
        ArgumentNullException.ThrowIfNull(transitionInfo);
        var current = GetAttachedFrame();
        EnsureCanGoBack(current);
        current.GoBack(transitionInfo);
    }

    /// <summary>Navigates to the next entry in the attached Frame's history.</summary>
    /// <exception cref="InvalidOperationException">
    /// No Frame is attached.
    /// -or-
    /// The method is called off the attached Frame's UI thread.
    /// -or-
    /// The Frame has no forward entry.
    /// </exception>
    public void GoForward()
    {
        var current = GetAttachedFrame();
        if (!current.CanGoForward)
            throw new InvalidOperationException(
                "The attached Frame has no forward navigation entry.");

        current.GoForward();
    }

    private static void EnsureCanGoBack(Microsoft.UI.Xaml.Controls.Frame frame)
    {
        if (!frame.CanGoBack)
            throw new InvalidOperationException(
                "The attached Frame has no back navigation entry.");
    }

    private static void EnsureThreadAccess(Microsoft.UI.Xaml.Controls.Frame frame)
    {
        if (!frame.DispatcherQueue.HasThreadAccess)
            throw new InvalidOperationException(
                "Frame navigation must be performed on the Frame's UI thread.");
    }

    private Microsoft.UI.Xaml.Controls.Frame GetAttachedFrame()
    {
        var current = _frame ??
                      throw new InvalidOperationException(
                          "No Frame is attached to this navigation host.");
        EnsureThreadAccess(current);
        return current;
    }

    /// <summary>Detaches a Frame only if it is still the attached one.</summary>
    /// <remarks>A stale lease must not detach a Frame attached after it was released.</remarks>
    private void Detach(Microsoft.UI.Xaml.Controls.Frame attachedFrame)
    {
        EnsureThreadAccess(attachedFrame);
        if (ReferenceEquals(_frame, attachedFrame)) _frame = null;
    }

    private sealed class FrameAttachment(
        FrameNavigator owner,
        Microsoft.UI.Xaml.Controls.Frame attachedFrame) : IDisposable
    {
        private bool _isDisposed;

        public void Dispose()
        {
            if (_isDisposed) return;

            // Detach throws off the UI thread; mark the lease disposed only afterwards so the call can be retried.
            owner.Detach(attachedFrame);
            _isDisposed = true;
        }
    }
}
