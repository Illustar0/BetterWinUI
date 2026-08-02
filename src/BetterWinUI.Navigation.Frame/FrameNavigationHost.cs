using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace BetterWinUI.Navigation.Frame;

/// <summary>
/// Implements <see cref="IFrameNavigationHost"/> for a dynamically attached WinUI Frame.
/// </summary>
public class FrameNavigationHost : IFrameNavigationHost
{
    private Microsoft.UI.Xaml.Controls.Frame? _frame;

    /// <inheritdoc />
    public Microsoft.UI.Xaml.Controls.Frame? Frame => _frame;

    /// <inheritdoc />
    public bool IsAttached => _frame is not null;

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
    public bool Navigate(Type pageType)
    {
        ValidatePageType(pageType);
        return GetAttachedFrame().Navigate(pageType);
    }

    /// <inheritdoc />
    public bool Navigate(Type pageType, object? parameter)
    {
        ValidatePageType(pageType);
        return GetAttachedFrame().Navigate(pageType, parameter);
    }

    /// <inheritdoc />
    public bool Navigate(
        Type pageType,
        object? parameter,
        NavigationTransitionInfo transitionInfo)
    {
        ValidatePageType(pageType);
        ArgumentNullException.ThrowIfNull(transitionInfo);
        return GetAttachedFrame().Navigate(pageType, parameter, transitionInfo);
    }

    /// <inheritdoc />
    public void GoBack()
    {
        var current = GetAttachedFrame();
        EnsureCanGoBack(current);
        current.GoBack();
    }

    /// <inheritdoc />
    public void GoBack(NavigationTransitionInfo transitionInfo)
    {
        ArgumentNullException.ThrowIfNull(transitionInfo);
        var current = GetAttachedFrame();
        EnsureCanGoBack(current);
        current.GoBack(transitionInfo);
    }

    /// <inheritdoc />
    public void GoForward()
    {
        var current = GetAttachedFrame();
        if (!current.CanGoForward)
            throw new InvalidOperationException(
                "The attached Frame has no forward navigation entry.");

        current.GoForward();
    }

    private static void ValidatePageType(Type pageType)
    {
        ArgumentNullException.ThrowIfNull(pageType);
        if (!typeof(Page).IsAssignableFrom(pageType))
            throw new ArgumentException(
                $"Navigation type '{pageType}' must derive from '{typeof(Page)}'.",
                nameof(pageType));
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

    private void Detach(Microsoft.UI.Xaml.Controls.Frame attachedFrame)
    {
        EnsureThreadAccess(attachedFrame);
        if (ReferenceEquals(_frame, attachedFrame)) _frame = null;
    }

    private sealed class FrameAttachment(
        FrameNavigationHost owner,
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