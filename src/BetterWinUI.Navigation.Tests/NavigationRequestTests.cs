using BetterWinUI.Navigation.Frame;
using Xunit;

namespace BetterWinUI.Navigation.Tests;

/// <summary>Verifies request data and detached behavior without creating XAML objects.</summary>
public sealed class NavigationRequestTests
{
    /// <summary>The request preserves its logical target and original parameter.</summary>
    [Fact]
    public void RequestPreservesOriginalParameter()
    {
        var parameter = new DetailArgs(42);
        var request = FrameNavigationRequest.For<DetailViewModel>(parameter);
        Assert.Equal(typeof(DetailViewModel), request.ViewModelType);
        Assert.Same(parameter, request.Parameter);
        Assert.Null(request.TransitionInfo);
    }

    /// <summary>Open navigation accepts arbitrary or absent request parameters.</summary>
    [Fact]
    public void RequestDoesNotEnforceATargetParameterType()
    {
        Assert.Null(FrameNavigationRequest.For<DetailViewModel>().Parameter);
        Assert.Equal("external", FrameNavigationRequest.For<DetailViewModel>("external").Parameter);
    }

    /// <summary>A detached navigator rejects execution before consulting application policy.</summary>
    [Fact]
    public void DetachedNavigatorDoesNotInvokeResolver()
    {
        var calls = 0;
        var navigator = new FrameNavigator((_, _) => { calls++; return typeof(HomePage); });
        Assert.False(navigator.IsAttached);
        Assert.False(navigator.CanGoBack);
        Assert.False(navigator.CanGoForward);
        Assert.Throws<InvalidOperationException>(() => navigator.Navigate<HomeViewModel>());
        Assert.Throws<InvalidOperationException>(() => navigator.Navigate<DetailViewModel, DetailArgs>(new DetailArgs(42)));
        Assert.Throws<InvalidOperationException>(navigator.GoBack);
        Assert.Throws<InvalidOperationException>(navigator.GoForward);
        Assert.Equal(0, calls);
    }
}
