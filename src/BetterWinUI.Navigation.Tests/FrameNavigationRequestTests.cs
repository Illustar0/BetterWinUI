using BetterWinUI.Navigation.Frame;
using Shouldly;
using Xunit;

namespace BetterWinUI.Navigation.Tests;

/// <summary>Verifies the values captured by a Frame navigation request.</summary>
public sealed class FrameNavigationRequestTests
{
    private const string UnassociatedParameter = "unassociated";

    [Fact]
    public void For_AssociatedParameter_PreservesTargetAndParameter()
    {
        var parameter = new DetailArgs(0);

        var request = FrameNavigationRequest.For<DetailViewModel>(parameter);

        request.ShouldSatisfyAllConditions(
            () => request.ViewModelType.ShouldBe(typeof(DetailViewModel)),
            () => request.Parameter.ShouldBeSameAs(parameter),
            () => request.TransitionInfo.ShouldBeNull());
    }

    [Fact]
    public void For_NoParameter_HasNullParameter()
    {
        var request = FrameNavigationRequest.For<DetailViewModel>();

        request.Parameter.ShouldBeNull();
    }

    [Fact]
    public void For_UnassociatedParameter_PreservesParameter()
    {
        var request = FrameNavigationRequest.For<DetailViewModel>(UnassociatedParameter);

        request.Parameter.ShouldBe(UnassociatedParameter);
    }
}
