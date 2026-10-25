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

    [UITestMethod]
    public void CanGoBack_AfterSecondNavigation_ReturnsTrue()
    {
        using var attached = CreateTwoEntryJournal();

        attached.Navigator.CanGoBack.ShouldBeTrue();
    }

    [UITestMethod]
    public void GoBack_AfterSecondNavigation_RestoresFirstPage()
    {
        using var attached = CreateTwoEntryJournal();

        attached.Navigator.GoBack();

        attached.Frame.Content.ShouldBeOfType<FirstPage>();
    }

    [UITestMethod]
    public void GoBackWithTransition_MappingRemoved_RestoresJournalPage()
    {
        var pages = CreateDetailMap<FirstPage>();
        using var attached = CreateTwoEntryJournal(pages);
        pages.Remove<DetailViewModel>();

        attached.Navigator.GoBack(new SuppressNavigationTransitionInfo());

        attached.Frame.Content.ShouldBeOfType<FirstPage>();
    }

    [UITestMethod]
    public void CanGoForward_AfterGoBack_ReturnsTrue()
    {
        using var attached = CreateTwoEntryJournal();
        attached.Navigator.GoBack();

        attached.Navigator.CanGoForward.ShouldBeTrue();
    }

    [UITestMethod]
    public void GoForward_AfterGoBack_RestoresSecondPage()
    {
        using var attached = CreateTwoEntryJournal();
        attached.Navigator.GoBack();

        attached.Navigator.GoForward();

        attached.Frame.Content.ShouldBeOfType<SecondPage>();
    }

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

    [UITestMethod]
    public void Navigate_ResolverReturnsNonPage_ThrowsWithoutNavigating()
    {
        using var attached = Attach((_, _) => typeof(object));

        Should.Throw<ArgumentException>(() => attached.Navigator.Navigate<DetailViewModel>());

        attached.Frame.Content.ShouldBeNull();
    }

    [UITestMethod]
    public void Navigate_ResolverThrows_PropagatesSameException()
    {
        var failure = new InvalidOperationException("Policy failure");
        using var attached = Attach((_, _) => throw failure);

        var exception = Should.Throw<InvalidOperationException>(() => attached.Navigator.Navigate<DetailViewModel>());

        exception.ShouldBeSameAs(failure);
    }

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

    [UITestMethod]
    public void Navigate_PageConstructorThrows_PropagatesException()
    {
        using var attached = Attach((_, _) => typeof(FailingPage));

        var exception = Should.Throw<InvalidOperationException>(() => attached.Navigator.Navigate<DetailViewModel>());

        exception.Message.ShouldBe(FailingPage.FailureMessage);
    }

    [UITestMethod]
    public void CanGoBack_EmptyJournal_ReturnsFalse()
    {
        using var attached = Attach(new PageMap());

        attached.Navigator.CanGoBack.ShouldBeFalse();
    }

    [UITestMethod]
    public void CanGoForward_EmptyJournal_ReturnsFalse()
    {
        using var attached = Attach(new PageMap());

        attached.Navigator.CanGoForward.ShouldBeFalse();
    }

    [UITestMethod]
    public void GoBack_EmptyJournal_ThrowsInvalidOperationException()
    {
        using var attached = Attach(new PageMap());

        Should.Throw<InvalidOperationException>(attached.Navigator.GoBack);
    }

    [UITestMethod]
    public void GoBackWithTransition_EmptyJournal_ThrowsInvalidOperationException()
    {
        using var attached = Attach(new PageMap());

        Should.Throw<InvalidOperationException>(() => attached.Navigator.GoBack(new SuppressNavigationTransitionInfo()));
    }

    [UITestMethod]
    public void GoForward_EmptyJournal_ThrowsInvalidOperationException()
    {
        using var attached = Attach(new PageMap());

        Should.Throw<InvalidOperationException>(attached.Navigator.GoForward);
    }

    [UITestMethod]
    public void Attach_FrameAlreadyAttached_ThrowsInvalidOperationException()
    {
        using var attached = Attach(new PageMap());

        Should.Throw<InvalidOperationException>(() => attached.Navigator.Attach(new Frame()));
    }

    [UITestMethod]
    public void DisposeLease_AttachedFrame_Detaches()
    {
        var navigator = new FrameNavigator(new PageMap());
        var lease = navigator.Attach(new Frame());

        lease.Dispose();

        navigator.IsAttached.ShouldBeFalse();
    }

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

    [UITestMethod]
    public async Task Navigate_FromWorkerThread_ThrowsInvalidOperationException()
    {
        using var attached = Attach((_, _) => typeof(FirstPage));

        var navigation = Task.Run(() => attached.Navigator.Navigate<DetailViewModel>());

        await Should.ThrowAsync<InvalidOperationException>(navigation);
    }

    [UITestMethod]
    public async Task DisposeLease_FromWorkerThread_ThrowsAndKeepsAttachment()
    {
        using var attached = Attach((_, _) => typeof(FirstPage));

        var disposal = Task.Run(attached.Lease.Dispose);

        await Should.ThrowAsync<InvalidOperationException>(disposal);
        attached.Navigator.IsAttached.ShouldBeTrue();
    }

    [UITestMethod]
    public async Task DisposeLease_RetriedAfterWorkerThreadFailure_Detaches()
    {
        using var attached = Attach((_, _) => typeof(FirstPage));
        await Should.ThrowAsync<InvalidOperationException>(Task.Run(attached.Lease.Dispose));

        attached.Lease.Dispose();

        attached.Navigator.IsAttached.ShouldBeFalse();
    }

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

    private static PageMap CreateDetailMap<TPage>() where TPage : Page
    {
        var pages = new PageMap();
        pages.Add<DetailViewModel, TPage>();
        return pages;
    }

    private static AttachedNavigator Attach(PageMap pages) => Attach(new FrameNavigator(pages));

    private static AttachedNavigator Attach(Func<Type, object?, Type> resolvePage) =>
        Attach(new FrameNavigator(PageResolver.Create(resolvePage)));

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
