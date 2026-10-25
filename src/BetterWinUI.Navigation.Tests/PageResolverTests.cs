using Microsoft.UI.Xaml.Controls;
using Shouldly;
using Xunit;

namespace BetterWinUI.Navigation.Tests;

/// <summary>Verifies how every Page resolver selects and checks Page types.</summary>
public sealed class PageResolverTests
{
    /// <summary>Supplies a mutable map, a frozen map, and a policy that all resolve the home target.</summary>
    public static TheoryData<PageResolver> HomeResolvers =>
        new() { CreateHomeMap(), CreateHomeMap().Freeze(), PageResolver.Create((_, _) => typeof(HomePage)) };

    /// <summary>Supplies policy results that WinUI cannot activate as a Page.</summary>
    public static TheoryData<Type> InvalidPageTypes =>
        new() { typeof(object), typeof(Page), typeof(AbstractPage), typeof(GenericPage<>) };

    /// <summary>Every resolver returns the Page for a resolvable target.</summary>
    [Theory]
    [MemberData(nameof(HomeResolvers), DisableDiscoveryEnumeration = true)]
    public void Resolve_ResolvableTarget_ReturnsPage(PageResolver resolver)
    {
        var page = resolver.Resolve(typeof(HomeViewModel), parameter: null);

        page.ShouldBe(typeof(HomePage));
    }

    /// <summary>Every resolver rejects a null target.</summary>
    [Theory]
    [MemberData(nameof(HomeResolvers), DisableDiscoveryEnumeration = true)]
    public void Resolve_NullTarget_ThrowsArgumentNullException(PageResolver resolver) =>
        Should.Throw<ArgumentNullException>(() => resolver.Resolve(null!, parameter: null));

    /// <summary>A policy receives the target and the original parameter.</summary>
    [Fact]
    public void Resolve_Policy_ReceivesTargetAndParameter()
    {
        var parameter = new DetailArgs(0);
        Type? receivedTarget = null;
        object? receivedParameter = null;
        var resolver = PageResolver.Create((target, args) =>
        {
            receivedTarget = target;
            receivedParameter = args;
            return typeof(DetailPage);
        });

        resolver.Resolve(typeof(DetailViewModel), parameter);

        resolver.ShouldSatisfyAllConditions(
            () => receivedTarget.ShouldBe(typeof(DetailViewModel)),
            () => receivedParameter.ShouldBeSameAs(parameter));
    }

    /// <summary>A policy result that is not a concrete, closed Page subclass is rejected.</summary>
    [Theory]
    [MemberData(nameof(InvalidPageTypes), DisableDiscoveryEnumeration = true)]
    public void Resolve_PolicyReturnsInvalidPage_ThrowsArgumentException(Type pageType)
    {
        var resolver = PageResolver.Create((_, _) => pageType);

        Should.Throw<ArgumentException>(() => resolver.Resolve(typeof(HomeViewModel), parameter: null));
    }

    /// <summary>A policy that returns no Page type is rejected.</summary>
    [Fact]
    public void Resolve_PolicyReturnsNull_ThrowsArgumentNullException()
    {
        var resolver = PageResolver.Create((_, _) => null!);

        Should.Throw<ArgumentNullException>(() => resolver.Resolve(typeof(HomeViewModel), parameter: null));
    }

    /// <summary>A map rejects an unmapped target even when a parameter is supplied.</summary>
    [Fact]
    public void Resolve_UnmappedMapTarget_ThrowsKeyNotFoundException()
    {
        PageResolver resolver = CreateHomeMap();

        Should.Throw<KeyNotFoundException>(() => resolver.Resolve(typeof(DetailViewModel), new DetailArgs(0)));
    }

    /// <summary>Create rejects a null policy.</summary>
    [Fact]
    public void Create_NullPolicy_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => PageResolver.Create(null!));

    /// <summary>Creates a map containing only the home mapping.</summary>
    private static PageMap CreateHomeMap()
    {
        var pages = new PageMap();
        pages.Add<HomeViewModel, HomePage>();
        return pages;
    }
}
