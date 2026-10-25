using BetterWinUI.PageActivation.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using Shouldly;

namespace BetterWinUI.IntegrationTests;

/// <summary>Exercises generated activation through the real WinUI metadata and native Frame.</summary>
[TestClass]
public sealed class PageActivationTests
{
    private const string ClockKey = "clock";

    private static App Current => (App)Application.Current;

    [UITestMethod]
    public void GetRequiredService_ScopedPageInSameScope_ReturnsSameInstance()
    {
        using var scope = Current.Services.CreateScope();
        var first = scope.ServiceProvider.GetRequiredService<ScopedPage>();

        var second = scope.ServiceProvider.GetRequiredService<ScopedPage>();

        second.ShouldBeSameAs(first);
    }

    [UITestMethod]
    public void GetRequiredService_ScopedPage_UsesScopeDependency()
    {
        using var scope = Current.Services.CreateScope();

        var page = scope.ServiceProvider.GetRequiredService<ScopedPage>();

        page.Dependency.ShouldBeSameAs(scope.ServiceProvider.GetRequiredService<ScopedDependency>());
    }

    [UITestMethod]
    public void GetRequiredService_ScopedPageInDifferentScopes_ReturnsDistinctInstances()
    {
        using var firstScope = Current.Services.CreateScope();
        using var secondScope = Current.Services.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<ScopedPage>();

        var second = secondScope.ServiceProvider.GetRequiredService<ScopedPage>();

        second.ShouldSatisfyAllConditions(
            () => second.ShouldNotBeSameAs(first),
            () => second.Dependency.ShouldNotBeSameAs(first.Dependency));
    }

    [UITestMethod]
    public void DisposeScope_ScopedDependency_DisposesDependency()
    {
        var scope = Current.Services.CreateScope();
        var dependency = scope.ServiceProvider.GetRequiredService<ScopedPage>().Dependency;

        scope.Dispose();

        dependency.IsDisposed.ShouldBeTrue();
    }

    [UITestMethod]
    public void DisposeScope_OtherScopeDependency_RemainsUndisposed()
    {
        using var survivingScope = Current.Services.CreateScope();
        var surviving = survivingScope.ServiceProvider.GetRequiredService<ScopedDependency>();
        var disposedScope = Current.Services.CreateScope();
        disposedScope.ServiceProvider.GetRequiredService<ScopedDependency>();

        disposedScope.Dispose();

        surviving.IsDisposed.ShouldBeFalse();
    }

    [UITestMethod]
    public void GetRequiredService_TransientRecordViewModel_ReturnsDistinctInstances()
    {
        var first = Current.Services.GetRequiredService<RecordViewModel>();

        var second = Current.Services.GetRequiredService<RecordViewModel>();

        second.ShouldNotBeSameAs(first);
    }

    [UITestMethod]
    public void Navigate_InjectedPage_ResolvesConstructorAndKeyedDependencies()
    {
        var frame = new Frame();

        var navigated = frame.Navigate(typeof(FirstPage));

        var page = frame.Content.ShouldBeOfType<FirstPage>();
        page.ShouldSatisfyAllConditions(
            () => navigated.ShouldBeTrue(),
            () => page.ViewModel.ShouldNotBeNull(),
            () => page.Clock.ShouldBeSameAs(Current.Services.GetRequiredKeyedService<TestClock>(ClockKey)),
            () => page.DataContext.ShouldBeNull());
    }

    [UITestMethod]
    public void Navigate_TransientPageInTwoFrames_CreatesDistinctPagesAndViewModels()
    {
        var first = new Frame();
        var second = new Frame();

        first.Navigate(typeof(FirstPage));
        second.Navigate(typeof(FirstPage));

        var firstPage = first.Content.ShouldBeOfType<FirstPage>();
        var secondPage = second.Content.ShouldBeOfType<FirstPage>();
        secondPage.ShouldSatisfyAllConditions(
            () => secondPage.ShouldNotBeSameAs(firstPage),
            () => secondPage.ViewModel.ShouldNotBeSameAs(firstPage.ViewModel));
    }

    [UITestMethod]
    public void GetRequiredService_SingletonPage_ReusesPageAndViewModel()
    {
        var first = Current.Services.GetRequiredService<SingletonPage>();

        var second = Current.Services.GetRequiredService<SingletonPage>();

        second.ShouldSatisfyAllConditions(
            () => second.ShouldBeSameAs(first),
            () => second.ViewModel.ShouldBeSameAs(Current.Services.GetRequiredService<SingletonViewModel>()),
            () => second.DataContext.ShouldBeNull());
    }

    [UITestMethod]
    public void Navigate_ExplicitViewModelRegistration_UsesExplicitInstance()
    {
        var frame = new Frame();

        frame.Navigate(typeof(OverridePage));

        frame.Content.ShouldBeOfType<OverridePage>().ViewModel
            .ShouldBeSameAs(Current.Services.GetRequiredService<OverrideViewModel>());
    }

    [UITestMethod]
    public void Navigate_ManuallyRegisteredPage_ActivatesPage()
    {
        var frame = new Frame();

        var navigated = frame.Navigate(typeof(ManualPage));

        frame.ShouldSatisfyAllConditions(
            () => navigated.ShouldBeTrue(),
            () => frame.Content.ShouldBeOfType<ManualPage>());
    }

    [UITestMethod]
    public void Navigate_UnregisteredPage_ThrowsWithoutNativeFallback()
    {
        var frame = new Frame();

        Should.Throw<InvalidOperationException>(() => frame.Navigate(typeof(UnregisteredPage)));

        frame.Content.ShouldBeNull();
    }

    [UITestMethod]
    public void UsePageActivation_AlreadyInstalled_ThrowsInvalidOperationException() =>
        Should.Throw<InvalidOperationException>(() => Current.UsePageActivation(Current.Services));

    [UITestMethod]
    public void Navigate_AfterRejectedReinstallation_UsesActiveProvider()
    {
        Should.Throw<InvalidOperationException>(() => Current.UsePageActivation(Current.Services));
        var frame = new Frame();

        var navigated = frame.Navigate(typeof(FirstPage));

        navigated.ShouldBeTrue();
    }

    [UITestMethod]
    public void GetXamlTypeByType_InjectedPage_ActivatesPage()
    {
        var metadata = (IXamlMetadataProvider)Application.Current;

        var page = metadata.GetXamlType(typeof(FirstPage)).ActivateInstance();

        page.ShouldBeOfType<FirstPage>();
    }

    [UITestMethod]
    public void GetXamlTypeByName_InjectedPage_ActivatesPage()
    {
        var metadata = (IXamlMetadataProvider)Application.Current;

        var page = metadata.GetXamlType(typeof(FirstPage).FullName!).ActivateInstance();

        page.ShouldBeOfType<FirstPage>();
    }
}

/// <summary>A real Page registered with an application-owned scoped dependency.</summary>
[BetterWinUI.PageActivation.DependencyInjection.View(ServiceLifetime.Scoped)]
public sealed class ScopedPage(ScopedDependency dependency) : Page
{
    /// <summary>Gets the dependency belonging to this Page's scope.</summary>
    public ScopedDependency Dependency { get; } = dependency;
}

/// <summary>Observes disposal of a generated scoped registration.</summary>
[BetterWinUI.PageActivation.DependencyInjection.ViewModel(ServiceLifetime.Scoped)]
public sealed class ScopedDependency : IDisposable
{
    /// <summary>Gets whether the owning scope disposed this instance.</summary>
    public bool IsDisposed { get; private set; }

    /// <inheritdoc />
    public void Dispose() => IsDisposed = true;
}
