using Microsoft.UI.Xaml.Controls;

namespace BetterWinUI.Navigation.Tests;

#pragma warning disable S2094 // Empty fixture classes are distinct logical navigation targets.
/// <summary>Identifies the home target.</summary>
public sealed class HomeViewModel;
/// <summary>Identifies the detail target.</summary>
public sealed class DetailViewModel;
/// <summary>Identifies another target sharing a Page.</summary>
public sealed class OtherViewModel;
#pragma warning restore S2094
/// <summary>Associates detail parameters with the detail target.</summary>
public sealed record DetailArgs(int Id) : INavigationParameter<DetailViewModel>;

/// <summary>Declares the default group.</summary>
[PageFor<HomeViewModel>]
public sealed partial class HomePage : Page;
/// <summary>Declares the desktop presentation.</summary>
[PageFor<DetailViewModel>(Module = typeof(DesktopPages))]
public sealed partial class DetailPage : Page;
/// <summary>Declares two logical targets sharing a compact Page.</summary>
[PageFor<OtherViewModel>(Module = typeof(CompactPages))]
[PageFor<DetailViewModel>(Module = typeof(CompactPages))]
public sealed partial class CompactPage : Page;
/// <summary>Contains the desktop mappings.</summary>
[PageModule]
public sealed partial class DesktopPages;
/// <summary>Contains the compact mappings.</summary>
[PageModule]
public sealed partial class CompactPages;
/// <summary>Supplies an invalid abstract Page type.</summary>
public abstract partial class AbstractPage : Page;
#pragma warning disable S2326 // The type parameter only makes the definition open.
/// <summary>Supplies an invalid open generic Page type definition.</summary>
/// <typeparam name="T">The unused type argument.</typeparam>
public sealed partial class GenericPage<T> : Page;
#pragma warning restore S2326
/// <summary>Supplies a Page without a parameterless constructor.</summary>
public sealed partial class ConstructorInjectedPage(HomeViewModel viewModel) : Page
{
    public HomeViewModel ViewModel { get; } = viewModel;
}
/// <summary>Supplies a registration failure after staging one valid mapping.</summary>
public sealed class ThrowingPages : IPageModule
{
    /// <inheritdoc />
    public static void Register(PageMap pages)
    {
        pages.Add<DetailViewModel, DetailPage>();
        throw new InvalidOperationException("Fixture registration failure.");
    }
}
