using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace BetterWinUI.IntegrationTests;

/// <summary>Exercises generated activation through the real WinUI metadata and native Frame.</summary>
[TestClass]
public sealed class PageActivationTests
{
    /// <summary>Generated scoped registrations follow application-owned scope identity and disposal.</summary>
    [UITestMethod]
    public void ScopedRegistrationsFollowApplicationOwnedScopes()
    {
        var services = ((App)Application.Current).Services;
        using var secondScope = services.CreateScope();
        ScopedDependency dependency;
        using (var firstScope = services.CreateScope())
        {
            var first = firstScope.ServiceProvider.GetRequiredService<ScopedPage>();
            dependency = first.Dependency;
            Assert.AreSame(first, firstScope.ServiceProvider.GetRequiredService<ScopedPage>());
            Assert.AreSame(dependency, firstScope.ServiceProvider.GetRequiredService<ScopedDependency>());
            var second = secondScope.ServiceProvider.GetRequiredService<ScopedPage>();
            Assert.AreNotSame(first, second);
            Assert.AreNotSame(dependency, second.Dependency);
            Assert.IsFalse(dependency.IsDisposed);
        }

        Assert.IsTrue(dependency.IsDisposed);
        Assert.IsFalse(secondScope.ServiceProvider.GetRequiredService<ScopedDependency>().IsDisposed);
    }

    /// <summary>Generated registration makes an attributed record resolvable without manual setup.</summary>
    [UITestMethod]
    public void RecordViewModelCanBeResolved()
    {
        var services = ((App)Application.Current).Services;
        Assert.AreNotSame(services.GetRequiredService<RecordViewModel>(), services.GetRequiredService<RecordViewModel>());
    }

    /// <summary>Resolves constructor and keyed dependencies without assigning DataContext.</summary>
    [UITestMethod]
    public void FrameActivatesPageThroughDependencyInjection()
    {
        var frame = new Frame();
        Assert.IsTrue(frame.Navigate(typeof(FirstPage)));
        var page = (FirstPage)frame.Content;
        Assert.IsNotNull(page.ViewModel);
        Assert.AreSame(((App)Application.Current).Services.GetRequiredKeyedService<TestClock>("clock"), page.Clock);
        Assert.IsNull(page.DataContext);
    }

    /// <summary>Generated transient registrations produce distinct Pages and ViewModels.</summary>
    [UITestMethod]
    public void TransientPagesAndViewModelsAreDistinct()
    {
        var first = new Frame();
        var second = new Frame();
        first.Navigate(typeof(FirstPage));
        second.Navigate(typeof(FirstPage));
        Assert.AreNotSame(first.Content, second.Content);
        Assert.AreNotSame(((FirstPage)first.Content).ViewModel, ((FirstPage)second.Content).ViewModel);
    }

    /// <summary>Generated singleton lifetimes remain controlled by DI.</summary>
    [UITestMethod]
    public void SingletonRegistrationReusesPageAndViewModel()
    {
        var services = ((App)Application.Current).Services;
        var first = services.GetRequiredService<SingletonPage>();
        var second = services.GetRequiredService<SingletonPage>();
        Assert.AreSame(first, second);
        Assert.AreSame(services.GetRequiredService<SingletonViewModel>(), first.ViewModel);
        Assert.IsNull(first.DataContext);
    }

    /// <summary>Explicit ViewModel registration overrides the generated transient default.</summary>
    [UITestMethod]
    public void ExplicitRegistrationTakesPrecedence()
    {
        var frame = new Frame();
        frame.Navigate(typeof(OverridePage));
        Assert.AreSame(((App)Application.Current).Services.GetRequiredService<OverrideViewModel>(),
            ((OverridePage)frame.Content).ViewModel);
    }

    /// <summary>A Page registered manually needs no View attribute.</summary>
    [UITestMethod]
    public void ManuallyRegisteredPageCanNavigate()
    {
        var frame = new Frame();
        Assert.IsTrue(frame.Navigate(typeof(ManualPage)));
        Assert.IsInstanceOfType<ManualPage>(frame.Content);
    }

    /// <summary>A missing registration fails even when the Page has a native default constructor.</summary>
    [UITestMethod]
    public void UnregisteredPageDoesNotFallBackToNativeActivation()
    {
        var frame = new Frame();
        Assert.ThrowsExactly<InvalidOperationException>(() => frame.Navigate(typeof(UnregisteredPage)));
        Assert.IsNull(frame.Content);
    }

    /// <summary>Rejects repeated initialization without corrupting the active provider.</summary>
    [UITestMethod]
    public void ActivationCanOnlyBeInitializedOnce()
    {
        var app = (App)Application.Current;
        Assert.ThrowsExactly<InvalidOperationException>(() => app.UsePageActivation(app.Services));
        var frame = new Frame();
        Assert.IsTrue(frame.Navigate(typeof(FirstPage)));
    }

    /// <summary>Both public metadata lookup overloads activate a real injected Page.</summary>
    [UITestMethod]
    public void MetadataLookupSupportsTypeAndName()
    {
        var metadata = (IXamlMetadataProvider)Application.Current;
        Assert.IsInstanceOfType<FirstPage>(metadata.GetXamlType(typeof(FirstPage)).ActivateInstance());
        Assert.IsInstanceOfType<FirstPage>(metadata.GetXamlType(typeof(FirstPage).FullName!).ActivateInstance());
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
