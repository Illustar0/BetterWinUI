using BetterWinUI.Navigation.Frame;
using Shouldly;
using Xunit;

namespace BetterWinUI.Navigation.Tests;

/// <summary>Verifies detached navigator behavior without creating XAML objects.</summary>
public sealed class FrameNavigatorTests
{
    [Fact]
    public void IsAttached_NewNavigator_ReturnsFalse()
    {
        var navigator = new FrameNavigator(new PageMap());

        navigator.IsAttached.ShouldBeFalse();
    }

    [Fact]
    public void CanGoBack_DetachedNavigator_ReturnsFalse()
    {
        var navigator = new FrameNavigator(new PageMap());

        navigator.CanGoBack.ShouldBeFalse();
    }

    [Fact]
    public void CanGoForward_DetachedNavigator_ReturnsFalse()
    {
        var navigator = new FrameNavigator(new PageMap());

        navigator.CanGoForward.ShouldBeFalse();
    }

    [Fact]
    public void Navigate_DetachedNavigator_ThrowsWithoutInvokingResolver()
    {
        var resolverCalls = 0;
        var navigator = new FrameNavigator(PageResolver.Create((_, _) =>
        {
            resolverCalls++;
            return typeof(HomePage);
        }));

        Should.Throw<InvalidOperationException>(() => navigator.Navigate<HomeViewModel>());

        resolverCalls.ShouldBe(0);
    }

    [Fact]
    public void NavigateWithAssociatedParameter_DetachedNavigator_ThrowsInvalidOperationException()
    {
        var navigator = new FrameNavigator(new PageMap());

        Should.Throw<InvalidOperationException>(() => navigator.Navigate<DetailViewModel, DetailArgs>(new DetailArgs(0)));
    }

    [Fact]
    public void GoBack_DetachedNavigator_ThrowsInvalidOperationException()
    {
        var navigator = new FrameNavigator(new PageMap());

        Should.Throw<InvalidOperationException>(navigator.GoBack);
    }

    [Fact]
    public void GoForward_DetachedNavigator_ThrowsInvalidOperationException()
    {
        var navigator = new FrameNavigator(new PageMap());

        Should.Throw<InvalidOperationException>(navigator.GoForward);
    }
}
