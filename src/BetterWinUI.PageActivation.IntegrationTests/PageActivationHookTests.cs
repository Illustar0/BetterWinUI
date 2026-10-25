using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using Shouldly;

[assembly: DoNotParallelize]

namespace BetterWinUI.PageActivation.IntegrationTests;

/// <summary>Verifies the activation contract through real WinUI metadata and Frame navigation.</summary>
[TestClass]
public sealed class PageActivationHookTests
{
    private static App Current => (App)Application.Current;

    /// <summary>Native activation stays intact until a factory is installed.</summary>
    [UITestMethod]
    public void Navigate_BeforeFactoryInstalled_UsesNativeActivation() =>
        Current.NativeContent.ShouldBeOfType<NativePage>();

    /// <summary>Frame navigation passes the requested Page type to the factory and shows its Page.</summary>
    [UITestMethod]
    public void Navigate_FactoryPage_UsesApplicationFactory()
    {
        var frame = new Frame();

        var navigated = frame.Navigate(typeof(FactoryPage));

        frame.ShouldSatisfyAllConditions(
            () => navigated.ShouldBeTrue(),
            () => Current.LastRequestedType.ShouldBe(typeof(FactoryPage)),
            () => frame.Content.ShouldBeOfType<FactoryPage>().Dependency.ShouldBeSameAs(Current.Dependency));
    }

    /// <summary>Metadata obtained before installation uses the factory installed later.</summary>
    [UITestMethod]
    public void ActivateInstance_MetadataCachedBeforeInstallation_UsesInstalledFactory()
    {
        var page = Current.CachedMetadata.ActivateInstance();

        page.ShouldBeOfType<FactoryPage>().Dependency.ShouldBeSameAs(Current.Dependency);
    }

    /// <summary>Metadata looked up by full name uses the installed factory.</summary>
    [UITestMethod]
    public void GetXamlTypeByName_FactoryPage_UsesInstalledFactory()
    {
        var metadata = (IXamlMetadataProvider)Application.Current;

        var page = metadata.GetXamlType(typeof(FactoryPage).FullName!).ActivateInstance();

        page.ShouldBeOfType<FactoryPage>().Dependency.ShouldBeSameAs(Current.Dependency);
    }

    /// <summary>A factory cannot be installed twice.</summary>
    [UITestMethod]
    public void UsePageActivation_AlreadyInstalled_ThrowsInvalidOperationException() =>
        Should.Throw<InvalidOperationException>(() => Current.UsePageActivation(_ => throw new InvalidOperationException()));

    /// <summary>A rejected reinstallation keeps the original factory.</summary>
    [UITestMethod]
    public void Navigate_AfterRejectedReinstallation_UsesOriginalFactory()
    {
        Should.Throw<InvalidOperationException>(() => Current.UsePageActivation(_ => throw new InvalidOperationException()));
        var frame = new Frame();

        frame.Navigate(typeof(FactoryPage));

        frame.Content.ShouldBeOfType<FactoryPage>().Dependency.ShouldBeSameAs(Current.Dependency);
    }

    /// <summary>A null factory is rejected before installation.</summary>
    [UITestMethod]
    public void UsePageActivation_NullFactory_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => Current.UsePageActivation(null!));

    /// <summary>A null factory result fails without calling the Page's native constructor.</summary>
    [UITestMethod]
    public void Navigate_FactoryReturnsNull_ThrowsWithoutNativeFallback()
    {
        var constructions = NullPage.Constructions;
        var frame = new Frame();

        Should.Throw<InvalidOperationException>(() => frame.Navigate(typeof(NullPage)));

        frame.ShouldSatisfyAllConditions(
            () => frame.Content.ShouldBeNull(),
            () => NullPage.Constructions.ShouldBe(constructions));
    }

    /// <summary>An unrelated Page result fails instead of changing the requested type.</summary>
    [UITestMethod]
    public void Navigate_FactoryReturnsIncompatiblePage_ThrowsWithoutNativeFallback()
    {
        var constructions = WrongPage.Constructions;
        var frame = new Frame();

        Should.Throw<InvalidOperationException>(() => frame.Navigate(typeof(WrongPage)));

        frame.ShouldSatisfyAllConditions(
            () => frame.Content.ShouldBeNull(),
            () => WrongPage.Constructions.ShouldBe(constructions));
    }

    /// <summary>Factory exceptions reach the caller unchanged without native activation.</summary>
    [UITestMethod]
    public void Navigate_FactoryThrows_PropagatesSameExceptionWithoutNativeFallback()
    {
        var constructions = ThrowingPage.Constructions;
        var frame = new Frame();

        var exception = Should.Throw<InvalidOperationException>(() => frame.Navigate(typeof(ThrowingPage)));

        exception.ShouldSatisfyAllConditions(
            () => exception.ShouldBeSameAs(Current.FactoryFailure),
            () => ThrowingPage.Constructions.ShouldBe(constructions));
    }

    /// <summary>A native cache hit reuses the Page without calling the factory again.</summary>
    [UITestMethod]
    public void GoBack_CachedPage_ReusesPageWithoutInvokingFactory()
    {
        var frame = new Frame();
        frame.Navigate(typeof(FactoryPage));
        var page = frame.Content;
        frame.Navigate(typeof(NativePage));
        var calls = Current.FactoryCalls;

        frame.GoBack();

        frame.ShouldSatisfyAllConditions(
            () => frame.Content.ShouldBeSameAs(page),
            () => Current.FactoryCalls.ShouldBe(calls));
    }

    /// <summary>Direct construction bypasses the XAML activation hook.</summary>
    [UITestMethod]
    public void Constructor_DirectConstruction_DoesNotInvokeFactory()
    {
        var calls = Current.FactoryCalls;

        _ = new NativePage();

        Current.FactoryCalls.ShouldBe(calls);
    }
}
