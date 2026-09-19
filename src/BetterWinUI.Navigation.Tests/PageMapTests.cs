using Microsoft.UI.Xaml.Controls;
using Xunit;

namespace BetterWinUI.Navigation.Tests;

/// <summary>Verifies mapping, mutation, snapshot, and module composition behavior.</summary>
public sealed class PageMapTests
{
    /// <summary>Add rejects duplicate keys while TryAdd preserves the existing mapping.</summary>
    [Fact]
    public void DuplicateAddPreservesOriginalPage()
    {
        var pages = new PageMap();
        pages.Add<HomeViewModel, HomePage>();
        Assert.False(pages.TryAdd<HomeViewModel, DetailPage>());
        Assert.Throws<ArgumentException>(() => pages.Add<HomeViewModel, DetailPage>());
        Assert.Equal(typeof(HomePage), pages.Resolve(typeof(HomeViewModel)));
    }

    /// <summary>Set supports both insertion and replacement, and Remove reports its result.</summary>
    [Fact]
    public void SetAndRemoveUpdateMappings()
    {
        var pages = new PageMap();
        pages.Set<HomeViewModel, HomePage>();
        pages.Set<HomeViewModel, DetailPage>();
        Assert.Equal(typeof(DetailPage), pages.Resolve(typeof(HomeViewModel)));
        Assert.True(pages.Remove<HomeViewModel>());
        Assert.False(pages.Remove<HomeViewModel>());
        Assert.False(pages.TryGetValue<HomeViewModel>(out _));
        Assert.Throws<KeyNotFoundException>(() => pages.Resolve(typeof(HomeViewModel)));
    }

    /// <summary>Frozen maps preserve their values when the mutable source is edited.</summary>
    [Fact]
    public void FreezeCreatesAnIndependentSnapshot()
    {
        var pages = new PageMap();
        pages.Add<HomeViewModel, HomePage>();
        var frozen = pages.Freeze();
        pages.Set<HomeViewModel, DetailPage>();
        pages.Set<DetailViewModel, DetailPage>();
        Assert.Equal(typeof(HomePage), frozen.Resolve(typeof(HomeViewModel)));
        Assert.True(frozen.TryGetValue<HomeViewModel>(out var home));
        Assert.Equal(typeof(HomePage), home);
        Assert.False(frozen.TryGetValue<DetailViewModel>(out _));
        Assert.Single(frozen);
        Assert.Equal(2, pages.Count);
    }

    /// <summary>Both maps expose standard read-only dictionary lookup and enumeration.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReadOnlyDictionaryExposesMappings(bool freeze)
    {
        var pages = new PageMap();
        pages.Add<HomeViewModel, HomePage>();
        IReadOnlyDictionary<Type, Type> map = freeze ? pages.Freeze() : pages;
        Assert.Equal(typeof(HomePage), map[typeof(HomeViewModel)]);
        Assert.True(map.ContainsKey(typeof(HomeViewModel)));
        Assert.True(map.TryGetValue(typeof(HomeViewModel), out var value));
        Assert.Equal(typeof(HomePage), value);
        Assert.False(map.TryGetValue(typeof(DetailViewModel), out var missing));
        Assert.Null(missing);
        Assert.Equal(typeof(HomeViewModel), Assert.Single(map.Keys));
        Assert.Equal(typeof(HomePage), Assert.Single(map.Values));
        Assert.Equal(new KeyValuePair<Type, Type>(typeof(HomeViewModel), typeof(HomePage)), Assert.Single(map));
        Assert.Throws<KeyNotFoundException>(() => map[typeof(DetailViewModel)]);
        Assert.Throws<ArgumentNullException>(() => map[null!]);
    }

    /// <summary>Only concrete Page subclasses are accepted, including injected constructors.</summary>
    [Fact]
    public void PageValidationAllowsConstructorInjection()
    {
        var pages = new PageMap();
        Assert.Throws<ArgumentException>(() => pages.Add<HomeViewModel, Page>());
        Assert.Throws<ArgumentException>(() => pages.Set<HomeViewModel, AbstractPage>());
        pages.Add<HomeViewModel, ConstructorInjectedPage>();
        Assert.Equal(typeof(ConstructorInjectedPage), pages.Resolve(typeof(HomeViewModel)));
    }

    /// <summary>Default declarations and explicitly selected named modules compose independently.</summary>
    [Fact]
    public void NamedModulesRequireExplicitSelection()
    {
        var pages = new PageMap();
        pages.AddGeneratedPages();
        Assert.Single(pages);
        Assert.Equal(typeof(HomePage), pages.Resolve(typeof(HomeViewModel)));
        pages.AddGeneratedPages<DesktopPages>();
        Assert.Equal(typeof(DetailPage), pages.Resolve(typeof(DetailViewModel)));
        var compact = new PageMap();
        compact.AddGeneratedPages<CompactPages>();
        Assert.Equal(typeof(CompactPage), compact.Resolve(typeof(DetailViewModel)));
        Assert.Equal(typeof(CompactPage), compact.Resolve(typeof(OtherViewModel)));
    }

    /// <summary>A conflicting module does not leave partially added mappings.</summary>
    [Fact]
    public void ConflictingModuleLeavesMapUnchanged()
    {
        var pages = new PageMap();
        pages.AddGeneratedPages<DesktopPages>();
        Assert.Throws<ArgumentException>(() => pages.AddGeneratedPages<CompactPages>());
        Assert.Single(pages);
        Assert.False(pages.ContainsKey(typeof(OtherViewModel)));
        Assert.Equal(typeof(DetailPage), pages.Resolve(typeof(DetailViewModel)));
    }

    /// <summary>Exceptions during registration leave the destination map unchanged.</summary>
    [Fact]
    public void ThrowingModuleLeavesMapUnchanged()
    {
        var pages = new PageMap();
        pages.Add<HomeViewModel, HomePage>();
        Assert.Throws<InvalidOperationException>(() => pages.AddGeneratedPages<ThrowingPages>());
        Assert.Single(pages);
        Assert.Equal(typeof(HomePage), pages.Resolve(typeof(HomeViewModel)));
    }
}
