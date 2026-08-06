# BetterWinUI.DependencyInjection.PageActivation

NativeAOT-compatible constructor injection for WinUI `Page` navigation.

You can register explicitly in the DI composition root:

```csharp
public App()
{
    var services = new ServiceCollection();
    services.AddTransient<MainViewModel>();
    services.AddTransient<MainPage>();
    services.AddBetterPageActivation();

    Services = services.BuildServiceProvider();
    this.InitializeBetterPageActivation(Services);
}

public ServiceProvider Services { get; }
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

`[View]` and `[ViewModel]` are independent registration syntax sugar; they do not declare a navigation or
View-to-ViewModel relationship.

Generated registrations use standard Microsoft DI descriptors and `TryAdd`, so explicit registrations already present in
the service collection are preserved. Constructor selection and keyed constructor dependencies remain container-owned.
The package does not assign `DataContext`.

Unregistered Pages throw by default. Native XAML activation can be enabled explicitly:

```csharp
services.AddBetterPageActivation(static options =>
{
    options.UnregisteredPageBehavior =
        UnregisteredPageBehavior.FallbackToXamlActivator;
});
```

Call `InitializeBetterPageActivation` exactly once after the service provider is built and before navigating to a Page.

`ServiceLifetime.Scoped` is rejected until navigation scope ownership and disposal are defined.

## Diagnostics

### BWPA0001

The application type must be declared `partial` so the generator can extend it.

### BWPA0002

`[PageActivation]` must target a concrete, non-generic, top-level WinUI `Application` subclass.

### BWPA0003

Only one application type per assembly can use `[PageActivation]`.

### BWPA0004

The XAML compiler output must expose one native `IXamlMetadataProvider` property on the application type.

### BWPA0005

Rename a user-declared `InitializeBetterPageActivation` member so the generator can emit its initialization method.

### BWPA0006

`[View]` must target an accessible, concrete, non-generic WinUI `Page` subclass.

### BWPA0007

`[ViewModel]` must target an accessible, concrete, non-abstract, closed class.

### BWPA0009

Generated registrations cannot use `ServiceLifetime.Scoped` until navigation scope ownership and disposal are implemented.

### BWPA0010

A referenced generated view module uses an incompatible page activation contract version.

### BWPA0011

The generated ViewModel registration has no public constructor; provide an explicit DI factory before calling `AddBetterPageActivation`.

### BWPA0012

No `InitializeBetterPageActivation` call was found for the application type.

### BWPA0013

The referenced Windows App SDK XAML interfaces do not expose the interception contract required by the generator.

### BWPA0014

The generated Page registration has no public constructor; provide an explicit DI factory before calling `AddBetterPageActivation`.

### BWPA0015

Generated registrations support only `ServiceLifetime.Transient` and `ServiceLifetime.Singleton`.
