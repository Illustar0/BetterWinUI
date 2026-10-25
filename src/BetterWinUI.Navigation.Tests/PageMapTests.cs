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

    /// <summary>Add rejects a second mapping for the same ViewModel.</summary>
    [Fact]
    public void Add_ExistingViewModel_ThrowsAndKeepsOriginalPage()
    {
        var pages = CreateHomeMap();

        Should.Throw<ArgumentException>(() => pages.Add<HomeViewModel, DetailPage>());

        pages.Resolve(typeof(HomeViewModel)).ShouldBe(typeof(HomePage));
    }

    /// <summary>TryAdd reports a duplicate without replacing the mapping.</summary>
    [Fact]
    public void TryAdd_ExistingViewModel_ReturnsFalseAndKeepsOriginalPage()
    {
        var pages = CreateHomeMap();

        var added = pages.TryAdd<HomeViewModel, DetailPage>();

        added.ShouldBeFalse();
        pages.Resolve(typeof(HomeViewModel)).ShouldBe(typeof(HomePage));
    }

    /// <summary>Set inserts a mapping for an unmapped ViewModel.</summary>
    [Fact]
    public void Set_UnmappedViewModel_AddsMapping()
    {
        var pages = new PageMap();

        pages.Set<HomeViewModel, HomePage>();

        pages.Resolve(typeof(HomeViewModel)).ShouldBe(typeof(HomePage));
    }

    /// <summary>Set replaces the Page of an existing mapping.</summary>
    [Fact]
    public void Set_MappedViewModel_ReplacesPage()
    {
        var pages = CreateHomeMap();

        pages.Set<HomeViewModel, DetailPage>();

        pages.Resolve(typeof(HomeViewModel)).ShouldBe(typeof(DetailPage));
    }

    /// <summary>Remove deletes an existing mapping.</summary>
    [Fact]
    public void Remove_MappedViewModel_ReturnsTrueAndRemovesMapping()
    {
        var pages = CreateHomeMap();

        var removed = pages.Remove<HomeViewModel>();

        removed.ShouldBeTrue();
        pages.TryGetValue<HomeViewModel>(out _).ShouldBeFalse();
    }

    /// <summary>Remove reports that an unmapped ViewModel was absent.</summary>
    [Fact]
    public void Remove_UnmappedViewModel_ReturnsFalse()
    {
        var pages = new PageMap();

        var removed = pages.Remove<HomeViewModel>();

        removed.ShouldBeFalse();
    }

    /// <summary>Resolve rejects a ViewModel without a mapping.</summary>
    [Fact]
    public void Resolve_UnmappedViewModel_ThrowsKeyNotFoundException()
    {
        var pages = new PageMap();

        Should.Throw<KeyNotFoundException>(() => pages.Resolve(typeof(HomeViewModel)));
    }

    /// <summary>Add rejects the Page base type because it is not a concrete subclass.</summary>
    [Fact]
    public void Add_BasePageType_ThrowsArgumentException()
    {
        var pages = new PageMap();

        Should.Throw<ArgumentException>(() => pages.Add<HomeViewModel, Page>());
    }

    /// <summary>Set rejects an abstract Page type.</summary>
    [Fact]
    public void Set_AbstractPage_ThrowsArgumentException()
    {
        var pages = new PageMap();

        Should.Throw<ArgumentException>(() => pages.Set<HomeViewModel, AbstractPage>());
    }

    /// <summary>Add accepts Pages that require constructor injection.</summary>
    [Fact]
    public void Add_ConstructorInjectedPage_AddsMapping()
    {
        var pages = new PageMap();

        pages.Add<HomeViewModel, ConstructorInjectedPage>();

        pages.Resolve(typeof(HomeViewModel)).ShouldBe(typeof(ConstructorInjectedPage));
    }

    /// <summary>A frozen map keeps its mappings when the source map changes.</summary>
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

    /// <summary>The indexer returns the mapped Page.</summary>
    [Theory]
    [MemberData(nameof(HomeMaps), DisableDiscoveryEnumeration = true)]
    public void Indexer_MappedViewModel_ReturnsPage(IReadOnlyDictionary<Type, Type> map)
    {
        var page = map[typeof(HomeViewModel)];

        page.ShouldBe(typeof(HomePage));
    }

    /// <summary>The indexer rejects an unmapped ViewModel.</summary>
    [Theory]
    [MemberData(nameof(HomeMaps), DisableDiscoveryEnumeration = true)]
    public void Indexer_UnmappedViewModel_ThrowsKeyNotFoundException(IReadOnlyDictionary<Type, Type> map) =>
        Should.Throw<KeyNotFoundException>(() => map[typeof(DetailViewModel)]);

    /// <summary>The indexer rejects a null key.</summary>
    [Theory]
    [MemberData(nameof(HomeMaps), DisableDiscoveryEnumeration = true)]
    public void Indexer_NullKey_ThrowsArgumentNullException(IReadOnlyDictionary<Type, Type> map) =>
        Should.Throw<ArgumentNullException>(() => map[null!]);

    /// <summary>ContainsKey finds a mapped ViewModel.</summary>
    [Theory]
    [MemberData(nameof(HomeMaps), DisableDiscoveryEnumeration = true)]
    public void ContainsKey_MappedViewModel_ReturnsTrue(IReadOnlyDictionary<Type, Type> map)
    {
        var contains = map.ContainsKey(typeof(HomeViewModel));

        contains.ShouldBeTrue();
    }

    /// <summary>TryGetValue returns the mapped Page.</summary>
    [Theory]
    [MemberData(nameof(HomeMaps), DisableDiscoveryEnumeration = true)]
    public void TryGetValue_MappedViewModel_ReturnsPage(IReadOnlyDictionary<Type, Type> map)
    {
        var found = map.TryGetValue(typeof(HomeViewModel), out var page);

        found.ShouldBeTrue();
        page.ShouldBe(typeof(HomePage));
    }

    /// <summary>TryGetValue reports an unmapped ViewModel without a Page.</summary>
    [Theory]
    [MemberData(nameof(HomeMaps), DisableDiscoveryEnumeration = true)]
    public void TryGetValue_UnmappedViewModel_ReturnsFalseAndNull(IReadOnlyDictionary<Type, Type> map)
    {
        var found = map.TryGetValue(typeof(DetailViewModel), out var page);

        found.ShouldBeFalse();
        page.ShouldBeNull();
    }

    /// <summary>Enumeration, keys, and values expose the same single mapping.</summary>
    [Theory]
    [MemberData(nameof(HomeMaps), DisableDiscoveryEnumeration = true)]
    public void Enumeration_SingleMapping_ExposesMapping(IReadOnlyDictionary<Type, Type> map) =>
        map.ShouldSatisfyAllConditions(
            () => map.ShouldHaveSingleItem().ShouldBe(new KeyValuePair<Type, Type>(typeof(HomeViewModel), typeof(HomePage))),
            () => map.Keys.ShouldHaveSingleItem().ShouldBe(typeof(HomeViewModel)),
            () => map.Values.ShouldHaveSingleItem().ShouldBe(typeof(HomePage)));

    /// <summary>The default group adds only mappings without a named module.</summary>
    [Fact]
    public void AddGeneratedPages_DefaultGroup_AddsOnlyUngroupedMappings()
    {
        var pages = new PageMap();

        pages.AddGeneratedPages();

        pages.ShouldHaveSingleItem().ShouldBe(new KeyValuePair<Type, Type>(typeof(HomeViewModel), typeof(HomePage)));
    }

    /// <summary>A selected named module adds its own mappings.</summary>
    [Fact]
    public void AddGeneratedPages_NamedModule_AddsModuleMappings()
    {
        var pages = new PageMap();

        pages.AddGeneratedPages<DesktopPages>();

        pages.ShouldHaveSingleItem().ShouldBe(new KeyValuePair<Type, Type>(typeof(DetailViewModel), typeof(DetailPage)));
    }

    /// <summary>A Page declared for several ViewModels maps each of them.</summary>
    [Fact]
    public void AddGeneratedPages_SharedPage_MapsEveryViewModel()
    {
        var pages = new PageMap();

        pages.AddGeneratedPages<CompactPages>();

        pages.ShouldSatisfyAllConditions(
            () => pages.Resolve(typeof(DetailViewModel)).ShouldBe(typeof(CompactPage)),
            () => pages.Resolve(typeof(OtherViewModel)).ShouldBe(typeof(CompactPage)));
    }

    /// <summary>A module that conflicts with an existing mapping adds none of its mappings.</summary>
    [Fact]
    public void AddGeneratedPages_ConflictingModule_ThrowsAndLeavesMapUnchanged()
    {
        var pages = new PageMap();
        pages.AddGeneratedPages<DesktopPages>();

        Should.Throw<ArgumentException>(() => pages.AddGeneratedPages<CompactPages>());

        pages.ShouldHaveSingleItem().ShouldBe(new KeyValuePair<Type, Type>(typeof(DetailViewModel), typeof(DetailPage)));
    }

    /// <summary>A module that throws during registration adds none of its staged mappings.</summary>
    [Fact]
    public void AddGeneratedPages_ThrowingModule_PropagatesAndLeavesMapUnchanged()
    {
        var pages = CreateHomeMap();

        Should.Throw<InvalidOperationException>(() => pages.AddGeneratedPages<ThrowingPages>());

        pages.ShouldHaveSingleItem().ShouldBe(new KeyValuePair<Type, Type>(typeof(HomeViewModel), typeof(HomePage)));
    }

    /// <summary>Creates a map containing only the home mapping.</summary>
    private static PageMap CreateHomeMap()
    {
        var pages = new PageMap();
        pages.Add<HomeViewModel, HomePage>();
        return pages;
    }
}
