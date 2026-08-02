# BetterWinUI

**Make WinUI better.**

BetterWinUI is a collection of focused components and NuGet packages for
building cleaner, safer, and more maintainable WinUI 3 applications.

## Packages

| Package                                          | Purpose                                                    | Target             |
| ------------------------------------------------ | ---------------------------------------------------------- | ------------------ |
| `BetterWinUI.Navigation`                         | Strongly typed ViewModel and route registration            | .NET 8             |
| `BetterWinUI.Navigation.Frame`                   | WinUI `Frame` adapter for registered destinations          | .NET 8 for Windows |
| `BetterWinUI.DependencyInjection.PageActivation` | NativeAOT-compatible constructor injection for WinUI pages | .NET 8             |

## Navigation

Register the navigation host and destinations explicitly:

```csharp
public sealed class MainNavigationHost : FrameNavigationHost;

services.AddSingleton<MainNavigationHost>();
services.AddBetterFrameNavigation(builder =>
{
    builder.Register<HomeViewModel, HomePage>("home");
});
```

Or, you can use attributes to generate the destination registrations:

```csharp
[ViewFor<HomeViewModel>("home")]
public sealed partial class HomePage;

services.AddSingleton<MainNavigationHost>();
services.AddBetterFrameNavigation();
```

Build the provider, resolve (or inject) the host-bound navigation service,
attach the application `Frame`, then navigate by ViewModel:

```csharp
ServiceProvider provider = services.BuildServiceProvider();

MainNavigationHost navigationHost =
    provider.GetRequiredService<MainNavigationHost>();
FrameNavigationService<MainNavigationHost> navigation =
    provider.GetRequiredService<FrameNavigationService<MainNavigationHost>>();

IDisposable attachment = navigationHost.Attach(contentFrame);
navigation.NavigateToViewModel<HomeViewModel>();
```

Typed parameters, exact routes, transitions, and history navigation are
documented in
[BetterWinUI.Navigation](src/BetterWinUI.Navigation/README.md) and
[BetterWinUI.Navigation.Frame](src/BetterWinUI.Navigation.Frame/README.md).

## Page activation

Register navigation, pages, and ViewModels in the same Microsoft dependency
injection container:

```csharp
var services = new ServiceCollection();
services.AddSingleton<MainNavigationHost>();
services.AddBetterFrameNavigation(builder =>
{
    builder.Register<HomeViewModel, HomePage>("home");
});
services.AddTransient<MainViewModel>();
services.AddTransient<MainPage>();
services.AddBetterPageActivation();

ServiceProvider provider = services.BuildServiceProvider();
this.InitializeBetterPageActivation(provider);
```

The activated Page can receive services registered by other BetterWinUI
packages:

```csharp
public sealed partial class MainPage : Page
{
    public MainPage(
        MainViewModel viewModel,
        FrameNavigationService<MainNavigationHost> navigation)
    {
        InitializeComponent();
        ViewModel = viewModel;
        Navigation = navigation;
    }

    public MainViewModel ViewModel { get; }

    public FrameNavigationService<MainNavigationHost> Navigation { get; }
}
```

Or, keep the navigation setup and use attributes to generate the Page and
ViewModel registrations:

```csharp
[View]
public sealed partial class MainPage : Page
{
    public MainPage(
        MainViewModel viewModel,
        FrameNavigationService<MainNavigationHost> navigation)
    {
        InitializeComponent();
        ViewModel = viewModel;
        Navigation = navigation;
    }

    public MainViewModel ViewModel { get; }

    public FrameNavigationService<MainNavigationHost> Navigation { get; }
}

[ViewModel(ServiceLifetime.Transient)]
public sealed class MainViewModel;
```

Explicit registrations take precedence over generated registrations.
Unregistered-page behavior and other details are documented in
[BetterWinUI.DependencyInjection.PageActivation](src/BetterWinUI.DependencyInjection.PageActivation/README.md).

## Build

Building the complete solution requires Windows and the .NET 10 SDK:

```powershell
dotnet restore src/BetterWinUI.slnx
dotnet build src/BetterWinUI.slnx --configuration Release --no-restore
dotnet test src/BetterWinUI.slnx --configuration Release --no-build
```

Releases use Conventional Commits, git-cliff semantic versioning, and NuGet.org
Trusted Publishing through GitHub Actions.

## Acknowledgements

Special thanks to
[gabor-budai/WinUI.DependencyInjection](https://github.com/gabor-budai/WinUI.DependencyInjection)
for the prior art and inspiration behind dependency-injected WinUI page
activation.

## License

BetterWinUI is licensed under the [MIT License](LICENSE).
