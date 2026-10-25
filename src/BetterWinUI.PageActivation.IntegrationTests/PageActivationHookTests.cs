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

    [UITestMethod]
    public void HookApplication_Generated_ImplementsPageActivationHook() =>
        Application.Current.ShouldBeAssignableTo<IPageActivationHook<Page>>();

    [UITestMethod]
    public void Navigate_BeforeFactoryInstalled_UsesNativeActivation() =>
        Current.NativeContent.ShouldBeOfType<NativePage>();

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

    [UITestMethod]
    public void ActivateInstance_MetadataCachedBeforeInstallation_UsesInstalledFactory()
    {
        var page = Current.CachedMetadata.ActivateInstance();

        page.ShouldBeOfType<FactoryPage>().Dependency.ShouldBeSameAs(Current.Dependency);
    }

    [UITestMethod]
    public void GetXamlTypeByName_FactoryPage_UsesInstalledFactory()
    {
        var metadata = (IXamlMetadataProvider)Application.Current;

        var page = metadata.GetXamlType(typeof(FactoryPage).FullName!).ActivateInstance();

        page.ShouldBeOfType<FactoryPage>().Dependency.ShouldBeSameAs(Current.Dependency);
    }

    [UITestMethod]
    public void UsePageActivation_AlreadyInstalled_ThrowsInvalidOperationException() =>
        Should.Throw<InvalidOperationException>(() => Current.UsePageActivation(_ => throw new InvalidOperationException()));

    [UITestMethod]
    public void Navigate_AfterRejectedReinstallation_UsesOriginalFactory()
    {
        Should.Throw<InvalidOperationException>(() => Current.UsePageActivation(_ => throw new InvalidOperationException()));
        var frame = new Frame();

        frame.Navigate(typeof(FactoryPage));

        frame.Content.ShouldBeOfType<FactoryPage>().Dependency.ShouldBeSameAs(Current.Dependency);
    }

    [UITestMethod]
    public void UsePageActivation_NullFactory_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => Current.UsePageActivation(null!));

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

    [UITestMethod]
    public void Constructor_DirectConstruction_DoesNotInvokeFactory()
    {
        var calls = Current.FactoryCalls;

        _ = new NativePage();

        Current.FactoryCalls.ShouldBe(calls);
    }
}
