using BetterWinUI.Navigation.Frame;
using Shouldly;
using Xunit;

namespace BetterWinUI.Navigation.Tests;

/// <summary>Verifies detached navigator behavior without creating XAML objects.</summary>
public sealed class FrameNavigatorTests
{
    /// <summary>A new navigator has no Frame.</summary>
    [Fact]
    public void IsAttached_NewNavigator_ReturnsFalse()
    {
        var navigator = new FrameNavigator(new PageMap());

        navigator.IsAttached.ShouldBeFalse();
    }

    /// <summary>A detached navigator has no back history.</summary>
    [Fact]
    public void CanGoBack_DetachedNavigator_ReturnsFalse()
    {
        var navigator = new FrameNavigator(new PageMap());

        navigator.CanGoBack.ShouldBeFalse();
    }

    /// <summary>A detached navigator has no forward history.</summary>
    [Fact]
    public void CanGoForward_DetachedNavigator_ReturnsFalse()
    {
        var navigator = new FrameNavigator(new PageMap());

        navigator.CanGoForward.ShouldBeFalse();
    }

    /// <summary>A detached navigator rejects navigation before consulting the resolver.</summary>
    [Fact]
    public void Navigate_DetachedNavigator_ThrowsWithoutInvokingResolver()
    {
        var resolverCalls = 0;
        var navigator = new FrameNavigator((_, _) =>
        {
            resolverCalls++;
            return typeof(HomePage);
        });

        Should.Throw<InvalidOperationException>(() => navigator.Navigate<HomeViewModel>());

        resolverCalls.ShouldBe(0);
    }

    /// <summary>A detached navigator rejects navigation with an associated parameter.</summary>
    [Fact]
    public void NavigateWithAssociatedParameter_DetachedNavigator_ThrowsInvalidOperationException()
    {
        var navigator = new FrameNavigator(new PageMap());

        Should.Throw<InvalidOperationException>(() => navigator.Navigate<DetailViewModel, DetailArgs>(new DetailArgs(0)));
    }

    /// <summary>A detached navigator rejects back navigation.</summary>
    [Fact]
    public void GoBack_DetachedNavigator_ThrowsInvalidOperationException()
    {
        var navigator = new FrameNavigator(new PageMap());

        Should.Throw<InvalidOperationException>(navigator.GoBack);
    }

    /// <summary>A detached navigator rejects forward navigation.</summary>
    [Fact]
    public void GoForward_DetachedNavigator_ThrowsInvalidOperationException()
    {
        var navigator = new FrameNavigator(new PageMap());

        Should.Throw<InvalidOperationException>(navigator.GoForward);
    }
}
