using Microsoft.UI.Xaml.Controls;
using Shouldly;
using Xunit;

namespace BetterWinUI.Navigation.Tests;

/// <summary>Verifies mapping, mutation, snapshot, and module composition behavior.</summary>
public sealed class PageMapTests
{
    /// <summary>Supplies a mutable map and a frozen map that both contain the home mapping.</summary>
    public static TheoryData<IReadOnlyDictionary<Type, Type>> HomeMaps =>
        new() { CreateHomeMap(), CreateHomeMap().Freeze() };

    [Fact]
    public void Add_ExistingViewModel_ThrowsAndKeepsOriginalPage()
    {
        var pages = CreateHomeMap();

        Should.Throw<ArgumentException>(() => pages.Add<HomeViewModel, DetailPage>());

        pages.Resolve(typeof(HomeViewModel)).ShouldBe(typeof(HomePage));
    }

    [Fact]
    public void TryAdd_ExistingViewModel_ReturnsFalseAndKeepsOriginalPage()
    {
        var pages = CreateHomeMap();

        var added = pages.TryAdd<HomeViewModel, DetailPage>();

        added.ShouldBeFalse();
        pages.Resolve(typeof(HomeViewModel)).ShouldBe(typeof(HomePage));
    }

    [Fact]
    public void Set_UnmappedViewModel_AddsMapping()
    {
        var pages = new PageMap();

        pages.Set<HomeViewModel, HomePage>();

        pages.Resolve(typeof(HomeViewModel)).ShouldBe(typeof(HomePage));
    }

    [Fact]
    public void Set_MappedViewModel_ReplacesPage()
    {
        var pages = CreateHomeMap();

        pages.Set<HomeViewModel, DetailPage>();

        pages.Resolve(typeof(HomeViewModel)).ShouldBe(typeof(DetailPage));
    }

    [Fact]
    public void Remove_MappedViewModel_ReturnsTrueAndRemovesMapping()
    {
        var pages = CreateHomeMap();

        var removed = pages.Remove<HomeViewModel>();

        removed.ShouldBeTrue();
        pages.TryGetValue<HomeViewModel>(out _).ShouldBeFalse();
    }

    [Fact]
    public void Remove_UnmappedViewModel_ReturnsFalse()
    {
        var pages = new PageMap();

        var removed = pages.Remove<HomeViewModel>();

        removed.ShouldBeFalse();
    }

    [Fact]
    public void Resolve_UnmappedViewModel_ThrowsKeyNotFoundException()
    {
        var pages = new PageMap();

        Should.Throw<KeyNotFoundException>(() => pages.Resolve(typeof(HomeViewModel)));
    }

    [Fact]
    public void Add_BasePageType_ThrowsArgumentException()
    {
        var pages = new PageMap();

        Should.Throw<ArgumentException>(() => pages.Add<HomeViewModel, Page>());
    }

    [Fact]
    public void Set_AbstractPage_ThrowsArgumentException()
    {
        var pages = new PageMap();

        Should.Throw<ArgumentException>(() => pages.Set<HomeViewModel, AbstractPage>());
    }

    [Fact]
    public void Add_ConstructorInjectedPage_AddsMapping()
    {
        var pages = new PageMap();

        pages.Add<HomeViewModel, ConstructorInjectedPage>();

        pages.Resolve(typeof(HomeViewModel)).ShouldBe(typeof(ConstructorInjectedPage));
    }

    [Fact]
    public void Freeze_SourceChangedAfterFreeze_KeepsSnapshot()
    {
        var pages = CreateHomeMap();
        var frozen = pages.Freeze();

        pages.Set<HomeViewModel, DetailPage>();
        pages.Set<DetailViewModel, DetailPage>();

        frozen.ShouldSatisfyAllConditions(
            () => frozen.Resolve(typeof(HomeViewModel)).ShouldBe(typeof(HomePage)),
            () => frozen.TryGetValue<DetailViewModel>(out _).ShouldBeFalse(),
            () => frozen.Count.ShouldBe(1));
    }

    [Theory]
    [MemberData(nameof(HomeMaps), DisableDiscoveryEnumeration = true)]
    public void Indexer_MappedViewModel_ReturnsPage(IReadOnlyDictionary<Type, Type> map)
    {
        var page = map[typeof(HomeViewModel)];

        page.ShouldBe(typeof(HomePage));
    }

    [Theory]
    [MemberData(nameof(HomeMaps), DisableDiscoveryEnumeration = true)]
    public void Indexer_UnmappedViewModel_ThrowsKeyNotFoundException(IReadOnlyDictionary<Type, Type> map) =>
        Should.Throw<KeyNotFoundException>(() => map[typeof(DetailViewModel)]);

    [Theory]
    [MemberData(nameof(HomeMaps), DisableDiscoveryEnumeration = true)]
    public void Indexer_NullKey_ThrowsArgumentNullException(IReadOnlyDictionary<Type, Type> map) =>
        Should.Throw<ArgumentNullException>(() => map[null!]);

    [Theory]
    [MemberData(nameof(HomeMaps), DisableDiscoveryEnumeration = true)]
    public void ContainsKey_MappedViewModel_ReturnsTrue(IReadOnlyDictionary<Type, Type> map)
    {
        var contains = map.ContainsKey(typeof(HomeViewModel));

        contains.ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(HomeMaps), DisableDiscoveryEnumeration = true)]
    public void TryGetValue_MappedViewModel_ReturnsPage(IReadOnlyDictionary<Type, Type> map)
    {
        var found = map.TryGetValue(typeof(HomeViewModel), out var page);

        found.ShouldBeTrue();
        page.ShouldBe(typeof(HomePage));
    }

    [Theory]
    [MemberData(nameof(HomeMaps), DisableDiscoveryEnumeration = true)]
    public void TryGetValue_UnmappedViewModel_ReturnsFalseAndNull(IReadOnlyDictionary<Type, Type> map)
    {
        var found = map.TryGetValue(typeof(DetailViewModel), out var page);

        found.ShouldBeFalse();
        page.ShouldBeNull();
    }

    [Theory]
    [MemberData(nameof(HomeMaps), DisableDiscoveryEnumeration = true)]
    public void Enumeration_SingleMapping_ExposesMapping(IReadOnlyDictionary<Type, Type> map) =>
        map.ShouldSatisfyAllConditions(
            () => map.ShouldHaveSingleItem().ShouldBe(new KeyValuePair<Type, Type>(typeof(HomeViewModel), typeof(HomePage))),
            () => map.Keys.ShouldHaveSingleItem().ShouldBe(typeof(HomeViewModel)),
            () => map.Values.ShouldHaveSingleItem().ShouldBe(typeof(HomePage)));

    [Fact]
    public void AddGeneratedPages_DefaultGroup_AddsOnlyUngroupedMappings()
    {
        var pages = new PageMap();

        pages.AddGeneratedPages();

        pages.ShouldHaveSingleItem().ShouldBe(new KeyValuePair<Type, Type>(typeof(HomeViewModel), typeof(HomePage)));
    }

    [Fact]
    public void AddGeneratedPages_NamedModule_AddsModuleMappings()
    {
        var pages = new PageMap();

        pages.AddGeneratedPages<DesktopPages>();

        pages.ShouldHaveSingleItem().ShouldBe(new KeyValuePair<Type, Type>(typeof(DetailViewModel), typeof(DetailPage)));
    }

    [Fact]
    public void AddGeneratedPages_SharedPage_MapsEveryViewModel()
    {
        var pages = new PageMap();

        pages.AddGeneratedPages<CompactPages>();

        pages.ShouldSatisfyAllConditions(
            () => pages.Resolve(typeof(DetailViewModel)).ShouldBe(typeof(CompactPage)),
            () => pages.Resolve(typeof(OtherViewModel)).ShouldBe(typeof(CompactPage)));
    }

    [Fact]
    public void AddGeneratedPages_ConflictingModule_ThrowsAndLeavesMapUnchanged()
    {
        var pages = new PageMap();
        pages.AddGeneratedPages<DesktopPages>();

        Should.Throw<ArgumentException>(() => pages.AddGeneratedPages<CompactPages>());

        pages.ShouldHaveSingleItem().ShouldBe(new KeyValuePair<Type, Type>(typeof(DetailViewModel), typeof(DetailPage)));
    }

    [Fact]
    public void AddGeneratedPages_ThrowingModule_PropagatesAndLeavesMapUnchanged()
    {
        var pages = CreateHomeMap();

        Should.Throw<InvalidOperationException>(() => pages.AddGeneratedPages<ThrowingPages>());

        pages.ShouldHaveSingleItem().ShouldBe(new KeyValuePair<Type, Type>(typeof(HomeViewModel), typeof(HomePage)));
    }

    private static PageMap CreateHomeMap()
    {
        var pages = new PageMap();
        pages.Add<HomeViewModel, HomePage>();
        return pages;
    }
}
