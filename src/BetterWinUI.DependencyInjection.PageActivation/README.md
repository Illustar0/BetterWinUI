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
