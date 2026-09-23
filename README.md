# BetterWinUI

**Make WinUI better.**

Focused packages for ViewModel-first WinUI navigation and application-owned Page activation.

| Package | Purpose | Target |
|---|---|---|
| `BetterWinUI.Navigation` | Mutable/frozen Page maps and generated mapping modules | .NET 8 for Windows |
| `BetterWinUI.Navigation.Frame` | Frame navigation, requests, transitions, and native history | .NET 8 for Windows |
| `BetterWinUI.PageActivation` | Application-owned Page factory hooks and XAML integration | .NET 8 |
| `BetterWinUI.PageActivation.DependencyInjection` | NativeAOT-compatible Page constructor injection | .NET 8 |

## Navigation

```csharp
using BetterWinUI.Navigation;
using BetterWinUI.Navigation.Frame;

var pages = new PageMap();
pages.Add<HomeViewModel, HomePage>();
pages.Add<DetailViewModel, DetailPage>();

var navigator = new FrameNavigator(pages);
IDisposable attachment = navigator.Attach(contentFrame);

navigator.Navigate<HomeViewModel>();
navigator.Navigate<DetailViewModel>(new DetailArgs(42), transition);
```

Keep the attachment for the host's lifetime and dispose it on the UI thread. Use `pages.Freeze()` when a navigator should use an immutable snapshot instead. ViewModel targets, per-call parameters, and Page selection are separate concerns; routes are application-owned and are never required by a Page mapping.

Optional attributes generate explicit registration groups:

```csharp
[PageFor<HomeViewModel>]
public sealed partial class HomePage : Page;

[PageModule]
public sealed partial class DesktopPages;

[PageFor<DetailViewModel>(Module = typeof(DesktopPages))]
public sealed partial class DetailPage : Page;

pages.AddGeneratedPages();               // This assembly's ungrouped mappings
pages.AddGeneratedPages<DesktopPages>(); // One explicitly selected module
```

Each group loads atomically. Public named modules can be selected across assembly references; default groups are not automatically aggregated across assemblies.

See [Page maps and modules](src/BetterWinUI.Navigation/README.md) and [Frame navigation](src/BetterWinUI.Navigation.Frame/README.md) for mutation, requests, strict parameter overloads, custom resolvers, and history semantics.

## Page activation and DI

Navigation selects a Page type; Page activation independently constructs the Page. On an App marked with `[GeneratePageActivationHook]` from `BetterWinUI.PageActivation`, install an application factory without requiring DI:

```csharp
this.UsePageActivation(pageType => pageType == typeof(HomePage)
    ? new HomePage(homeViewModel, navigator)
    : throw new InvalidOperationException($"No factory for {pageType}."));
```

Once installed, the factory owns activation: failures never fall back to native construction. See [Page activation hooks](src/BetterWinUI.PageActivation/README.md). Alternatively, use `BetterWinUI.PageActivation.DependencyInjection` to register the map/navigator and install a DI-backed factory:

```csharp
var pages = new PageMap();
pages.Add<HomeViewModel, HomePage>();

services.AddSingleton<FrameNavigator>(_ => new FrameNavigator(pages));
services.AddTransient<HomeViewModel>();
services.AddTransient<HomePage>();
services.AddGeneratedPageServices();

ServiceProvider provider = services.BuildServiceProvider();
this.UsePageActivation(provider);

FrameNavigator navigator = provider.GetRequiredService<FrameNavigator>();
IDisposable attachment = navigator.Attach(contentFrame);
```

On a partial WinUI App marked `[GeneratePageActivationHook]`, the generator supplies the activation methods. A Page constructor can receive its ViewModel and navigator:

```csharp
public sealed partial class HomePage : Page
{
    public HomePage(HomeViewModel viewModel, FrameNavigator navigator)
    {
        InitializeComponent();
        ViewModel = viewModel;
        Navigator = navigator;
    }

    public HomeViewModel ViewModel { get; }
    public FrameNavigator Navigator { get; }
}
```

`[View]` and `[ViewModel]` optionally generate DI registrations; `[PageFor]` only generates Page mappings. Neither navigation nor activation assigns DataContext. See [Page activation](src/BetterWinUI.PageActivation.DependencyInjection/README.md).

## Build

The solution requires Windows and the .NET 10 SDK:

```powershell
./build.ps1 --target Test
./build.ps1 --target Pack
```

NUKE restores and builds the solution, runs ordinary and real WinUI tests, and packs the four libraries. See [Testing](docs/testing.md) for individual targets.

Releases use Conventional Commits, git-cliff semantic versioning, and NuGet.org Trusted Publishing.

## Acknowledgements

Thanks to [gabor-budai/WinUI.DependencyInjection](https://github.com/gabor-budai/WinUI.DependencyInjection) for the prior art behind dependency-injected WinUI Page activation.

## License

BetterWinUI is licensed under the [MIT License](LICENSE).
