# BetterWinUI.PageActivation.DependencyInjection

Constructor injection for WinUI Pages, built on `BetterWinUI.PageActivation`. The base package owns XAML interception; this package supplies DI registration and a factory that resolves every requested Page using `GetRequiredService`.

## Installation

Add the package and a service container, such as Microsoft's, to the WinUI application project:

```shell
dotnet add package BetterWinUI.PageActivation.DependencyInjection
dotnet add package Microsoft.Extensions.DependencyInjection
```

The package brings in `BetterWinUI.PageActivation` and `Microsoft.Extensions.DependencyInjection.Abstractions`.

## Requirements

- Everything that [BetterWinUI.PageActivation requires](https://github.com/Illustar0/BetterWinUI/blob/main/src/BetterWinUI.PageActivation/README.md#requirements): a WinUI 3 application project on .NET 8 or later whose `App` class is a top-level `partial` class marked with `[GeneratePageActivationHook]`.
- A service container that builds an `IServiceProvider` from `IServiceCollection`, such as `Microsoft.Extensions.DependencyInjection`.
- The .NET 8 SDK or later, which the source generators need to load.

## Usage

You can register explicitly in the DI composition root:

```csharp
using BetterWinUI.PageActivation;
using BetterWinUI.PageActivation.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

[GeneratePageActivationHook]
public sealed partial class App : Application
{
    public App()
    {
        var services = new ServiceCollection();
        services.AddTransient<MainViewModel>();
        services.AddTransient<MainPage>();
        services.AddGeneratedPageServices();

        Services = services.BuildServiceProvider();
        this.UsePageActivation(Services);
        InitializeComponent();
    }

    public ServiceProvider Services { get; }
}
```

```csharp
public sealed partial class MainPage : Page
{
    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
    }

    public MainViewModel ViewModel { get; }
}

public sealed class MainViewModel;
```

Or, you can use the optional registration attributes:

```csharp
[View] // ServiceLifetime.Transient by default
public sealed partial class MainPage : Page
{
    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
    }

    public MainViewModel ViewModel { get; }
}

[ViewModel(ServiceLifetime.Transient)]
public sealed class MainViewModel;
```

`[View]` and `[ViewModel]` are independent registration syntax sugar; they do not declare a navigation or View-to-ViewModel relationship.

Generated registrations use standard Microsoft DI descriptors and `TryAdd`, so explicit registrations already present in the service collection are preserved. Constructor selection and keyed constructor dependencies remain container-owned. The package does not assign `DataContext`.

Unregistered Pages throw, including Pages with default constructors. Factory and constructor exceptions propagate. There is no native activation fallback or options object.

`UsePageActivation(provider)` is an extension method on the base package's `IPageActivationHook<TPage>`, which the generated App implements. Call it exactly once after the service provider is built and before navigating to a Page. It installs the base package's hook, so it cannot be combined with another `UsePageActivation(factory)` registration on the same Application. An instance method with the same signature on the App takes precedence, as usual in C#. The application owns the service provider and must dispose it at shutdown.

`AddGeneratedPageServices` is generated in the assembly that declares `[GeneratePageActivationHook]`. It adds the local and referenced generated registrations.

`ServiceLifetime.Scoped` is supported. The application owns scope creation and disposal and chooses which provider to pass to `UsePageActivation`. Keep that scope alive while its Pages and dependencies are in use; the hook does not create a scope per navigation.

## Diagnostics

Application and XAML contract diagnostics are documented in [BetterWinUI.PageActivation](https://github.com/Illustar0/BetterWinUI/blob/main/src/BetterWinUI.PageActivation/README.md#diagnostics).

### BWPA0002

`[View]` must target an accessible, concrete, non-generic WinUI `Page` subclass.

### BWPA0003

`[ViewModel]` must target an accessible, concrete, non-abstract, closed class.

### BWPA0004

A referenced module must be accessible, non-generic, and declare `public static void Register(IServiceCollection services)`. The generator validates the actual method contract before emitting a call; modules do not carry a separate protocol version.

### BWPA0005

The generated ViewModel registration has no public constructor; provide an explicit DI factory before calling `AddGeneratedPageServices`.

### BWPA0006

The generated Page registration has no public constructor; provide an explicit DI factory before calling `AddGeneratedPageServices`.

### BWPA0007

Generated registrations support `ServiceLifetime.Transient`, `ServiceLifetime.Scoped`, and `ServiceLifetime.Singleton`.
