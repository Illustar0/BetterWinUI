using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

[assembly: DoNotParallelize]

namespace BetterWinUI.PageActivation.IntegrationTests;

/// <summary>Verifies the activation contract through real WinUI metadata and Frame navigation.</summary>
[TestClass]
public sealed class PageActivationHookTests
{
    private static App Current => (App)Application.Current;

    /// <summary>Leaves native activation intact until a factory is installed.</summary>
    [UITestMethod]
    public void NativeActivationWorksBeforeHookInstallation() =>
        Assert.IsInstanceOfType<NativePage>(Current.NativeContent);

    /// <summary>Passes the requested Page type and returns the factory's initialized Page.</summary>
    [UITestMethod]
    public void FrameUsesApplicationFactory()
    {
        var frame = new Frame();
        Assert.IsTrue(frame.Navigate(typeof(FactoryPage)));
        Assert.AreEqual(typeof(FactoryPage), Current.LastRequestedType);
        Assert.AreSame(Current.Dependency, ((FactoryPage)frame.Content).Dependency);
    }

    /// <summary>Metadata obtained before registration observes the subsequently installed factory.</summary>
    [UITestMethod]
    public void CachedMetadataUsesInstalledFactory()
    {
        var page = (FactoryPage)Current.CachedMetadata.ActivateInstance();
        Assert.AreSame(Current.Dependency, page.Dependency);
    }

    /// <summary>Type and name lookups both use the factory.</summary>
    [UITestMethod]
    public void MetadataLookupByNameUsesInstalledFactory()
    {
        var metadata = (IXamlMetadataProvider)Application.Current;
        var page = (FactoryPage)metadata.GetXamlType(typeof(FactoryPage).FullName!).ActivateInstance();
        Assert.AreSame(Current.Dependency, page.Dependency);
    }

    /// <summary>Rejects repeated registration without replacing the installed factory.</summary>
    [UITestMethod]
    public void RepeatedRegistrationPreservesOriginalFactory()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => Current.UsePageActivation(_ => throw new Exception()));
        var frame = new Frame();
        Assert.IsTrue(frame.Navigate(typeof(FactoryPage)));
        Assert.AreSame(Current.Dependency, ((FactoryPage)frame.Content).Dependency);
    }

    /// <summary>Rejects a null delegate before attempting to install it.</summary>
    [UITestMethod]
    public void NullFactoryIsRejected() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => Current.UsePageActivation(null!));

    /// <summary>A null result fails without calling the Page's native default constructor.</summary>
    [UITestMethod]
    public void NullResultDoesNotFallBack()
    {
        var constructions = NullPage.Constructions;
        var frame = new Frame();
        Assert.ThrowsExactly<InvalidOperationException>(() => frame.Navigate(typeof(NullPage)));
        Assert.IsNull(frame.Content);
        Assert.AreEqual(constructions, NullPage.Constructions);
    }

    /// <summary>An unrelated Page result fails instead of changing the requested type.</summary>
    [UITestMethod]
    public void IncompatibleResultDoesNotFallBack()
    {
        var constructions = WrongPage.Constructions;
        var frame = new Frame();
        Assert.ThrowsExactly<InvalidOperationException>(() => frame.Navigate(typeof(WrongPage)));
        Assert.IsNull(frame.Content);
        Assert.AreEqual(constructions, WrongPage.Constructions);
    }

    /// <summary>Preserves factory exceptions without invoking native activation.</summary>
    [UITestMethod]
    public void FactoryExceptionPropagates()
    {
        var constructions = ThrowingPage.Constructions;
        var frame = new Frame();
        var error = Assert.ThrowsExactly<InvalidOperationException>(() => frame.Navigate(typeof(ThrowingPage)));
        Assert.AreSame(Current.FactoryFailure, error);
        Assert.AreEqual(constructions, ThrowingPage.Constructions);
    }

    /// <summary>Native cache hits reuse the Page without calling the factory again.</summary>
    [UITestMethod]
    public void CacheHitDoesNotInvokeFactory()
    {
        var frame = new Frame();
        frame.Navigate(typeof(FactoryPage));
        var page = frame.Content;
        frame.Navigate(typeof(NativePage));
        var calls = Current.FactoryCalls;
        frame.GoBack();
        Assert.AreSame(page, frame.Content);
        Assert.AreEqual(calls, Current.FactoryCalls);
    }

    /// <summary>Direct construction is independent of the XAML activation hook.</summary>
    [UITestMethod]
    public void DirectConstructionDoesNotInvokeFactory()
    {
        var calls = Current.FactoryCalls;
        _ = new NativePage();
        Assert.AreEqual(calls, Current.FactoryCalls);
    }
}
