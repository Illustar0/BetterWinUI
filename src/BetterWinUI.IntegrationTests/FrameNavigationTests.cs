using BetterWinUI.Navigation;
using BetterWinUI.Navigation.Frame;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using Shouldly;

[assembly: DoNotParallelize]

namespace BetterWinUI.IntegrationTests;

/// <summary>Verifies navigation against real Frame events, Pages, history, and thread affinity.</summary>
[TestClass]
public sealed class FrameNavigationTests
{
    /// <summary>The open overload passes the original parameter to the native Frame.</summary>
    [UITestMethod]
    public void Navigate_OpenParameter_PassesOriginalParameterToFrame()
    {
        using var attached = Attach((_, _) => typeof(FirstPage));
        var parameter = new DetailArgs(0);
        object? received = null;
        attached.Frame.Navigated += (_, args) => received = args.Parameter;

        var navigated = attached.Navigator.Navigate<DetailViewModel>(parameter);

        attached.ShouldSatisfyAllConditions(
            () => navigated.ShouldBeTrue(),
            () => attached.Frame.Content.ShouldBeOfType<FirstPage>(),
            () => received.ShouldBeSameAs(parameter));
    }

    /// <summary>The associated-parameter overload passes the original parameter to the native Frame.</summary>
    [UITestMethod]
    public void NavigateWithAssociatedParameter_ParameterSupplied_PassesOriginalParameterToFrame()
    {
        using var attached = Attach((_, _) => typeof(FirstPage));
        var parameter = new DetailArgs(0);
        object? received = null;
        attached.Frame.Navigated += (_, args) => received = args.Parameter;

        var navigated = attached.Navigator.Navigate<DetailViewModel, DetailArgs>(parameter);

        attached.ShouldSatisfyAllConditions(
            () => navigated.ShouldBeTrue(),
            () => received.ShouldBeSameAs(parameter));
    }

    /// <summary>A request transition is recorded in the native journal even without a parameter.</summary>
    [UITestMethod]
    public void NavigateRequest_TransitionWithoutParameter_RecordsTransitionInJournal()
    {
        using var attached = Attach((_, _) => typeof(FirstPage));
        var transition = new SuppressNavigationTransitionInfo();

        attached.Navigator.Navigate(FrameNavigationRequest.For<DetailViewModel>(transitionInfo: transition));
        attached.Navigator.Navigate<DetailViewModel>();

        var entry = attached.Frame.BackStack.ShouldHaveSingleItem();
        entry.ShouldSatisfyAllConditions(
            () => entry.NavigationTransitionInfo.ShouldBeSameAs(transition),
            () => entry.Parameter.ShouldBeNull());
    }

    /// <summary>New navigation uses a replaced mapping.</summary>
    [UITestMethod]
    public void Navigate_MappingReplaced_UsesNewPage()
    {
        var pages = CreateDetailMap<FirstPage>();
        using var attached = Attach(pages);
        attached.Navigator.Navigate<DetailViewModel>();
        pages.Set<DetailViewModel, SecondPage>();

        attached.Navigator.Navigate<DetailViewModel>();

        attached.Frame.Content.ShouldBeOfType<SecondPage>();
    }

    /// <summary>A second navigation creates back history.</summary>
    [UITestMethod]
    public void CanGoBack_AfterSecondNavigation_ReturnsTrue()
    {
        using var attached = CreateTwoEntryJournal();

        attached.Navigator.CanGoBack.ShouldBeTrue();
    }

    /// <summary>Back navigation follows the native journal.</summary>
    [UITestMethod]
    public void GoBack_AfterSecondNavigation_RestoresFirstPage()
    {
        using var attached = CreateTwoEntryJournal();

        attached.Navigator.GoBack();

        attached.Frame.Content.ShouldBeOfType<FirstPage>();
    }

    /// <summary>Back navigation with a transition uses the journal after its mapping is removed.</summary>
    [UITestMethod]
    public void GoBackWithTransition_MappingRemoved_RestoresJournalPage()
    {
        var pages = CreateDetailMap<FirstPage>();
        using var attached = CreateTwoEntryJournal(pages);
        pages.Remove<DetailViewModel>();

        attached.Navigator.GoBack(new SuppressNavigationTransitionInfo());

        attached.Frame.Content.ShouldBeOfType<FirstPage>();
    }

    /// <summary>Back navigation creates forward history.</summary>
    [UITestMethod]
    public void CanGoForward_AfterGoBack_ReturnsTrue()
    {
        using var attached = CreateTwoEntryJournal();
        attached.Navigator.GoBack();

        attached.Navigator.CanGoForward.ShouldBeTrue();
    }

    /// <summary>Forward navigation follows the native journal.</summary>
    [UITestMethod]
    public void GoForward_AfterGoBack_RestoresSecondPage()
    {
        using var attached = CreateTwoEntryJournal();
        attached.Navigator.GoBack();

        attached.Navigator.GoForward();

        attached.Frame.Content.ShouldBeOfType<SecondPage>();
    }

    /// <summary>A navigator over a frozen map ignores later source changes.</summary>
    [UITestMethod]
    public void Navigate_FrozenMapSourceChanged_UsesSnapshotPage()
    {
        var pages = CreateDetailMap<FirstPage>();
        var navigator = new FrameNavigator(pages.Freeze());
        pages.Set<DetailViewModel, SecondPage>();
        var frame = new Frame();
        using var lease = navigator.Attach(frame);

        navigator.Navigate<DetailViewModel>();

        frame.Content.ShouldBeOfType<FirstPage>();
    }

    /// <summary>The application resolver receives the logical target and original parameter.</summary>
    [UITestMethod]
    public void Navigate_ApplicationResolver_ReceivesTargetAndParameter()
    {
        var parameter = new DetailArgs(0);
        Type? receivedTarget = null;
        object? receivedParameter = null;
        using var attached = Attach((target, args) =>
        {
            receivedTarget = target;
            receivedParameter = args;
            return typeof(SecondPage);
        });

        attached.Navigator.Navigate<DetailViewModel>(parameter);

        attached.ShouldSatisfyAllConditions(
            () => receivedTarget.ShouldBe(typeof(DetailViewModel)),
            () => receivedParameter.ShouldBeSameAs(parameter),
            () => attached.Frame.Content.ShouldBeOfType<SecondPage>());
    }

    /// <summary>A resolver result that is not a Page is rejected before native navigation.</summary>
    [UITestMethod]
    public void Navigate_ResolverReturnsNonPage_ThrowsWithoutNavigating()
    {
        using var attached = Attach((_, _) => typeof(object));

        Should.Throw<ArgumentException>(() => attached.Navigator.Navigate<DetailViewModel>());

        attached.Frame.Content.ShouldBeNull();
    }

    /// <summary>Resolver exceptions reach the caller unchanged.</summary>
    [UITestMethod]
    public void Navigate_ResolverThrows_PropagatesSameException()
    {
        var failure = new InvalidOperationException("Policy failure");
        using var attached = Attach((_, _) => throw failure);

        var exception = Should.Throw<InvalidOperationException>(() => attached.Navigator.Navigate<DetailViewModel>());

        exception.ShouldBeSameAs(failure);
    }

    /// <summary>A canceled navigation returns the native result instead of reporting a page switch.</summary>
    [UITestMethod]
    public void Navigate_NavigatingCanceled_ReturnsNativeResultWithoutNavigating()
    {
        var direct = new Frame();
        direct.Navigating += (_, args) => args.Cancel = true;
        var expected = direct.Navigate(typeof(FirstPage));
        using var attached = Attach((_, _) => typeof(FirstPage));
        attached.Frame.Navigating += (_, args) => args.Cancel = true;

        var navigated = attached.Navigator.Navigate<DetailViewModel>();

        attached.ShouldSatisfyAllConditions(
            () => navigated.ShouldBe(expected),
            () => attached.Frame.Content.ShouldBeNull(),
            () => attached.Frame.BackStack.ShouldBeEmpty());
    }

    /// <summary>A reentrant navigation receives the native failure result while the outer call completes.</summary>
    [UITestMethod]
    public void Navigate_ReentrantCall_ReturnsNativeFailureForNestedCall()
    {
        using var attached = Attach((_, _) => typeof(FirstPage));
        bool? nestedResult = null;
        attached.Frame.Navigating += (_, _) => nestedResult = attached.Navigator.Navigate<DetailViewModel>();

        var navigated = attached.Navigator.Navigate<DetailViewModel>();

        attached.ShouldSatisfyAllConditions(
            () => navigated.ShouldBeTrue(),
            () => nestedResult.ShouldBe(false),
            () => attached.Frame.Content.ShouldBeOfType<FirstPage>());
    }

    /// <summary>Page constructor failures propagate instead of becoming a false result.</summary>
    [UITestMethod]
    public void Navigate_PageConstructorThrows_PropagatesException()
    {
        using var attached = Attach((_, _) => typeof(FailingPage));

        var exception = Should.Throw<InvalidOperationException>(() => attached.Navigator.Navigate<DetailViewModel>());

        exception.Message.ShouldBe(FailingPage.FailureMessage);
    }

    /// <summary>An empty journal has no back entry.</summary>
    [UITestMethod]
    public void CanGoBack_EmptyJournal_ReturnsFalse()
    {
        using var attached = Attach(new PageMap());

        attached.Navigator.CanGoBack.ShouldBeFalse();
    }

    /// <summary>An empty journal has no forward entry.</summary>
    [UITestMethod]
    public void CanGoForward_EmptyJournal_ReturnsFalse()
    {
        using var attached = Attach(new PageMap());

        attached.Navigator.CanGoForward.ShouldBeFalse();
    }

    /// <summary>Back navigation is rejected without a back entry.</summary>
    [UITestMethod]
    public void GoBack_EmptyJournal_ThrowsInvalidOperationException()
    {
        using var attached = Attach(new PageMap());

        Should.Throw<InvalidOperationException>(attached.Navigator.GoBack);
    }

    /// <summary>Back navigation with a transition is rejected without a back entry.</summary>
    [UITestMethod]
    public void GoBackWithTransition_EmptyJournal_ThrowsInvalidOperationException()
    {
        using var attached = Attach(new PageMap());

        Should.Throw<InvalidOperationException>(() => attached.Navigator.GoBack(new SuppressNavigationTransitionInfo()));
    }

    /// <summary>Forward navigation is rejected without a forward entry.</summary>
    [UITestMethod]
    public void GoForward_EmptyJournal_ThrowsInvalidOperationException()
    {
        using var attached = Attach(new PageMap());

        Should.Throw<InvalidOperationException>(attached.Navigator.GoForward);
    }

    /// <summary>Only one Frame can be attached at a time.</summary>
    [UITestMethod]
    public void Attach_FrameAlreadyAttached_ThrowsInvalidOperationException()
    {
        using var attached = Attach(new PageMap());

        Should.Throw<InvalidOperationException>(() => attached.Navigator.Attach(new Frame()));
    }

    /// <summary>Disposing the lease detaches its Frame.</summary>
    [UITestMethod]
    public void DisposeLease_AttachedFrame_Detaches()
    {
        var navigator = new FrameNavigator(new PageMap());
        var lease = navigator.Attach(new Frame());

        lease.Dispose();

        navigator.IsAttached.ShouldBeFalse();
    }

    /// <summary>Disposing a released lease again leaves a replacement attachment intact.</summary>
    [UITestMethod]
    public void DisposeLease_AfterReplacementAttached_KeepsReplacement()
    {
        var navigator = new FrameNavigator(new PageMap());
        var lease = navigator.Attach(new Frame());
        lease.Dispose();
        var replacement = new Frame();
        using var replacementLease = navigator.Attach(replacement);

        lease.Dispose();

        navigator.Frame.ShouldBeSameAs(replacement);
    }

    /// <summary>Navigation from a worker thread is rejected.</summary>
    [UITestMethod]
    public async Task Navigate_FromWorkerThread_ThrowsInvalidOperationException()
    {
        using var attached = Attach((_, _) => typeof(FirstPage));

        var navigation = Task.Run(() => attached.Navigator.Navigate<DetailViewModel>());

        await Should.ThrowAsync<InvalidOperationException>(navigation);
    }

    /// <summary>Disposing a lease from a worker thread fails and keeps the Frame attached.</summary>
    [UITestMethod]
    public async Task DisposeLease_FromWorkerThread_ThrowsAndKeepsAttachment()
    {
        using var attached = Attach((_, _) => typeof(FirstPage));

        var disposal = Task.Run(attached.Lease.Dispose);

        await Should.ThrowAsync<InvalidOperationException>(disposal);
        attached.Navigator.IsAttached.ShouldBeTrue();
    }

    /// <summary>A lease disposal that failed on a worker thread can be retried on the UI thread.</summary>
    [UITestMethod]
    public async Task DisposeLease_RetriedAfterWorkerThreadFailure_Detaches()
    {
        using var attached = Attach((_, _) => typeof(FirstPage));
        await Should.ThrowAsync<InvalidOperationException>(Task.Run(attached.Lease.Dispose));

        attached.Lease.Dispose();

        attached.Navigator.IsAttached.ShouldBeFalse();
    }

    /// <summary>A resolver that detaches the Frame stops the navigation.</summary>
    [UITestMethod]
    public void Navigate_ResolverDetachesFrame_ThrowsWithoutNavigating()
    {
        IDisposable? lease = null;
        var navigator = new FrameNavigator(PageResolver.Create((_, _) =>
        {
            lease!.Dispose();
            return typeof(FirstPage);
        }));
        var frame = new Frame();
        lease = navigator.Attach(frame);

        Should.Throw<InvalidOperationException>(() => navigator.Navigate<DetailViewModel>());

        frame.Content.ShouldBeNull();
    }

    /// <summary>The native cache restores the same Page instance even after its mapping changes.</summary>
    [UITestMethod]
    public void GoBack_CachedPageAfterMappingChange_RestoresSameInstance()
    {
        var pages = CreateDetailMap<CachedPage>();
        using var attached = Attach(pages);
        attached.Navigator.Navigate<DetailViewModel>();
        var page = attached.Frame.Content;
        pages.Set<DetailViewModel, SecondPage>();
        attached.Navigator.Navigate<DetailViewModel>();

        attached.Navigator.GoBack();

        attached.Frame.Content.ShouldBeSameAs(page);
    }

    /// <summary>Creates a map that resolves the detail target to one Page.</summary>
    private static PageMap CreateDetailMap<TPage>() where TPage : Page
    {
        var pages = new PageMap();
        pages.Add<DetailViewModel, TPage>();
        return pages;
    }

    /// <summary>Attaches a new Frame to a navigator backed by a Page map.</summary>
    private static AttachedNavigator Attach(PageMap pages) => Attach(new FrameNavigator(pages));

    /// <summary>Attaches a new Frame to a navigator backed by an application policy.</summary>
    private static AttachedNavigator Attach(Func<Type, object?, Type> resolvePage) =>
        Attach(new FrameNavigator(PageResolver.Create(resolvePage)));

    /// <summary>Attaches a new Frame to a navigator.</summary>
    private static AttachedNavigator Attach(FrameNavigator navigator)
    {
        var frame = new Frame();
        return new AttachedNavigator(navigator, frame, navigator.Attach(frame));
    }

    /// <summary>Navigates to FirstPage and then SecondPage through one mutable map.</summary>
    private static AttachedNavigator CreateTwoEntryJournal(PageMap? pages = null)
    {
        pages ??= CreateDetailMap<FirstPage>();
        var attached = Attach(pages);
        attached.Navigator.Navigate<DetailViewModel>();
        pages.Set<DetailViewModel, SecondPage>();
        attached.Navigator.Navigate<DetailViewModel>();
        return attached;
    }

    /// <summary>Owns one navigator attachment for the duration of a test.</summary>
    private sealed record AttachedNavigator(FrameNavigator Navigator, Frame Frame, IDisposable Lease) : IDisposable
    {
        /// <inheritdoc />
        public void Dispose() => Lease.Dispose();
    }
}

/// <summary>Identifies a logical navigation target independent of its Page.</summary>
#pragma warning disable S2094 // The target is identified by its type, not state.
public sealed class DetailViewModel;
#pragma warning restore S2094

/// <summary>Supplies a compile-time parameter association.</summary>
public sealed record DetailArgs(int Id) : INavigationParameter<DetailViewModel>;
