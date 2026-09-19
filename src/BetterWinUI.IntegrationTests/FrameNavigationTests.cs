using BetterWinUI.Navigation;
using BetterWinUI.Navigation.Frame;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

[assembly: DoNotParallelize]

namespace BetterWinUI.IntegrationTests;

/// <summary>Verifies navigation against real Frame events, Pages, history, and thread affinity.</summary>
[TestClass]
public sealed class FrameNavigationTests
{
    /// <summary>Passes the original parameter through both open and explicitly typed calls.</summary>
    [UITestMethod]
    public void NavigationPreservesOriginalParameter()
    {
        var navigator = new FrameNavigator((_, _) => typeof(FirstPage));
        var frame = new Frame();
        using var lease = navigator.Attach(frame);
        var parameter = new DetailArgs(42);
        object? received = null;
        frame.Navigated += (_, args) => received = args.Parameter;
        Assert.IsTrue(navigator.Navigate<DetailViewModel>(parameter));
        Assert.IsInstanceOfType<FirstPage>(frame.Content);
        Assert.AreSame(parameter, received);
        Assert.IsTrue(navigator.Navigate<DetailViewModel, DetailArgs>(parameter));
        Assert.AreSame(parameter, received);
    }

    /// <summary>Retains request transition overrides in the real native journal.</summary>
    [UITestMethod]
    public void RequestPreservesTransitionWithoutParameter()
    {
        var navigator = new FrameNavigator((_, _) => typeof(FirstPage));
        var frame = new Frame();
        using var lease = navigator.Attach(frame);
        var transition = new SuppressNavigationTransitionInfo();
        navigator.Navigate(FrameNavigationRequest.For<DetailViewModel>(transitionInfo: transition));
        navigator.Navigate<DetailViewModel>();
        Assert.AreSame(transition, frame.BackStack[0].NavigationTransitionInfo);
        Assert.IsNull(frame.BackStack[0].Parameter);
    }

    /// <summary>Uses new mappings for new navigation and preserves the native journal after removal.</summary>
    [UITestMethod]
    public void MappingChangesDoNotRewriteHistory()
    {
        var pages = new PageMap();
        pages.Add<DetailViewModel, FirstPage>();
        var navigator = new FrameNavigator(pages);
        var frame = new Frame();
        using var lease = navigator.Attach(frame);
        navigator.Navigate<DetailViewModel>();
        pages.Set<DetailViewModel, SecondPage>();
        navigator.Navigate<DetailViewModel>();
        Assert.IsInstanceOfType<SecondPage>(frame.Content);
        pages.Remove<DetailViewModel>();
        Assert.IsTrue(navigator.CanGoBack);
        navigator.GoBack(new SuppressNavigationTransitionInfo());
        Assert.IsInstanceOfType<FirstPage>(frame.Content);
        Assert.IsTrue(navigator.CanGoForward);
        navigator.GoForward();
        Assert.IsInstanceOfType<SecondPage>(frame.Content);
        navigator.GoBack();
        Assert.IsInstanceOfType<FirstPage>(frame.Content);
    }

    /// <summary>Uses the frozen snapshot when the source map is subsequently replaced.</summary>
    [UITestMethod]
    public void FrozenMappingRemainsIndependent()
    {
        var pages = new PageMap();
        pages.Add<DetailViewModel, FirstPage>();
        var navigator = new FrameNavigator(pages.Freeze());
        pages.Set<DetailViewModel, SecondPage>();
        var frame = new Frame();
        using var lease = navigator.Attach(frame);
        navigator.Navigate<DetailViewModel>();
        Assert.IsInstanceOfType<FirstPage>(frame.Content);
    }

    /// <summary>Supplies the logical target and original parameter to the application policy.</summary>
    [UITestMethod]
    public void ResolverReceivesNavigationIntent()
    {
        var parameter = new DetailArgs(42);
        var navigator = new FrameNavigator((target, args) =>
        {
            Assert.AreEqual(typeof(DetailViewModel), target);
            Assert.AreSame(parameter, args);
            return typeof(SecondPage);
        });
        var frame = new Frame();
        using var lease = navigator.Attach(frame);
        navigator.Navigate<DetailViewModel>(parameter);
        Assert.IsInstanceOfType<SecondPage>(frame.Content);
    }

    /// <summary>Rejects invalid policy results before native navigation begins.</summary>
    [UITestMethod]
    public void InvalidResolvedPageDoesNotNavigate()
    {
        var navigator = new FrameNavigator((_, _) => typeof(object));
        var frame = new Frame();
        using var lease = navigator.Attach(frame);
        Assert.ThrowsExactly<ArgumentException>(() => navigator.Navigate<DetailViewModel>());
        Assert.IsNull(frame.Content);
    }

    /// <summary>Leaves resolver exceptions intact for callers.</summary>
    [UITestMethod]
    public void ResolverExceptionPropagates()
    {
        var failure = new InvalidOperationException("Policy failure");
        var navigator = new FrameNavigator((_, _) => throw failure);
        using var lease = navigator.Attach(new Frame());
        Assert.AreSame(failure, Assert.ThrowsExactly<InvalidOperationException>(() => navigator.Navigate<DetailViewModel>()));
    }

    /// <summary>Matches native cancellation results instead of interpreting bool as a completed page switch.</summary>
    [UITestMethod]
    public void CancellationPreservesNativeResult()
    {
        var direct = new Frame();
        direct.Navigating += (_, args) => args.Cancel = true;
        var expected = direct.Navigate(typeof(FirstPage));
        var frame = new Frame();
        frame.Navigating += (_, args) => args.Cancel = true;
        var navigator = new FrameNavigator((_, _) => typeof(FirstPage));
        using var lease = navigator.Attach(frame);
        Assert.AreEqual(expected, navigator.Navigate<DetailViewModel>());
        Assert.IsNull(frame.Content);
        Assert.HasCount(0, frame.BackStack);
    }

    /// <summary>Preserves the native false result when a second navigation is attempted reentrantly.</summary>
    [UITestMethod]
    public void ReentrantNavigationPreservesNativeFailureResult()
    {
        var frame = new Frame();
        var navigator = new FrameNavigator((_, _) => typeof(FirstPage));
        using var lease = navigator.Attach(frame);
        bool? nestedResult = null;
        frame.Navigating += (_, _) => nestedResult = navigator.Navigate<DetailViewModel>();
        Assert.IsTrue(navigator.Navigate<DetailViewModel>());
        Assert.AreEqual(false, nestedResult);
        Assert.IsInstanceOfType<FirstPage>(frame.Content);
    }

    /// <summary>Propagates Page constructor failures instead of converting exceptions to false.</summary>
    [UITestMethod]
    public void NativeActivationExceptionPropagates()
    {
        var navigator = new FrameNavigator((_, _) => typeof(FailingPage));
        using var lease = navigator.Attach(new Frame());
        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => navigator.Navigate<DetailViewModel>());
        Assert.AreEqual("Page construction failed.", exception.Message, StringComparer.Ordinal);
    }

    /// <summary>Rejects empty history without delegating invalid native operations.</summary>
    [UITestMethod]
    public void EmptyHistoryRejectsBackAndForward()
    {
        var navigator = new FrameNavigator(new PageMap());
        using var lease = navigator.Attach(new Frame());
        Assert.IsFalse(navigator.CanGoBack);
        Assert.IsFalse(navigator.CanGoForward);
        Assert.ThrowsExactly<InvalidOperationException>(navigator.GoBack);
        Assert.ThrowsExactly<InvalidOperationException>(() => navigator.GoBack(new SuppressNavigationTransitionInfo()));
        Assert.ThrowsExactly<InvalidOperationException>(navigator.GoForward);
    }

    /// <summary>Releases one attachment once without detaching a later replacement.</summary>
    [UITestMethod]
    public void AttachmentLeaseOwnsOnlyItsFrame()
    {
        var navigator = new FrameNavigator(new PageMap());
        var first = new Frame();
        using var lease = navigator.Attach(first);
        Assert.AreSame(first, navigator.Frame);
        Assert.ThrowsExactly<InvalidOperationException>(() => navigator.Attach(new Frame()));
        lease.Dispose();
        Assert.IsFalse(navigator.IsAttached);
        var second = new Frame();
        using var replacement = navigator.Attach(second);
        lease.Dispose();
        Assert.AreSame(second, navigator.Frame);
    }

    /// <summary>Checks actual worker-thread access and permits disposal retry on the UI thread.</summary>
    [UITestMethod]
    public async Task WrongThreadDoesNotReleaseAttachment()
    {
        var navigator = new FrameNavigator((_, _) => typeof(FirstPage));
        using var lease = navigator.Attach(new Frame());
        await Task.Run(() =>
        {
            Assert.ThrowsExactly<InvalidOperationException>(() => navigator.Navigate<DetailViewModel>());
            Assert.ThrowsExactly<InvalidOperationException>(lease.Dispose);
        });
        Assert.IsTrue(navigator.IsAttached);
        lease.Dispose();
        Assert.IsFalse(navigator.IsAttached);
    }

    /// <summary>Rejects a resolver that changes the attachment while choosing a Page.</summary>
    [UITestMethod]
    public void AttachmentChangeDuringResolutionDoesNotNavigate()
    {
        IDisposable? attachment = null;
        var navigator = new FrameNavigator((_, _) =>
        {
            attachment!.Dispose();
            return typeof(FirstPage);
        });
        var frame = new Frame();
        using var lease = navigator.Attach(frame);
        attachment = lease;
        Assert.ThrowsExactly<InvalidOperationException>(() => navigator.Navigate<DetailViewModel>());
        Assert.IsNull(frame.Content);
    }

    /// <summary>Leaves Page caching to the native Frame.</summary>
    [UITestMethod]
    public void NativePageCachePreservesThePageInstance()
    {
        var frame = new Frame();
        var pages = new PageMap();
        pages.Add<DetailViewModel, CachedPage>();
        var navigator = new FrameNavigator(pages);
        using var lease = navigator.Attach(frame);
        navigator.Navigate<DetailViewModel>();
        var page = (CachedPage)frame.Content;
        pages.Set<DetailViewModel, SecondPage>();
        navigator.Navigate<DetailViewModel>();
        navigator.GoBack();
        Assert.AreSame(page, frame.Content);
    }
}

/// <summary>Identifies a logical navigation target independent of its Page.</summary>
#pragma warning disable S2094 // The target is identified by its type, not state.
public sealed class DetailViewModel;
#pragma warning restore S2094

/// <summary>Supplies a compile-time parameter association.</summary>
public sealed record DetailArgs(int Id) : INavigationParameter<DetailViewModel>;
