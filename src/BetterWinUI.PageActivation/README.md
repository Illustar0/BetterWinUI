# BetterWinUI.PageActivation

Application-owned Page factories for WinUI, without a DI container or navigation dependency.
The package includes the source generator that connects a partial WinUI Application
to the XAML metadata activation entry point.

```csharp
using BetterWinUI.PageActivation;
using Microsoft.UI.Xaml;

[GeneratePageActivationHook]
public sealed partial class App : Application
{
    public App()
    {
        this.UsePageActivation(pageType => pageType == typeof(HomePage)
            ? new HomePage(new HomeViewModel())
            : throw new InvalidOperationException($"No factory for {pageType}."));
        InitializeComponent();
    }
}
```

The generated method has this contract:

```csharp
public void UsePageActivation(Func<Type, Microsoft.UI.Xaml.Controls.Page> activate);
```

- Register once per Application instance, before the Pages that need the factory are activated.
  A second registration throws and leaves the first factory in place; a null delegate is rejected.
- The callback receives the actual requested Page type and must return a non-null, compatible Page.
  Null results and incompatible types throw `InvalidOperationException`; factory exceptions propagate.
- Before installation, activation uses the original XAML activator. After installation, the factory
  owns Page creation completely. There is no automatic fallback or continuation overload.
- Frame cache hits reuse existing Pages without invoking the factory. The hook does not intercept
  direct `new Page(...)` calls, assign DataContext, or own/dispose factory-created instances.
- The hook applies to Page activation through the Application's XAML metadata, not only one Frame.
  It runs synchronously on the activating thread; construct Pages on the XAML thread.
- The XAML compiler must expose metadata for the Page and the native metadata provider on the App.
  Installing a factory does not create missing metadata for arbitrary runtime types.

The generator uses Scriban and emits native interface forwarding and WinRT exposure code.
It does not use reflection to discover the App's private metadata provider at runtime.
The marker assembly targets .NET 8; generated activation code runs in the consuming WinUI application.
The generator targets `netstandard2.0` and Roslyn 4.8.

For Microsoft DI registration and constructor injection, use
[BetterWinUI.PageActivation.DependencyInjection](../BetterWinUI.PageActivation.DependencyInjection/README.md).

## Diagnostics

### BWPH0001

The application must be declared `partial`.

### BWPH0002

The application must be a concrete, non-generic, top-level WinUI Application in a named namespace.

### BWPH0003

Only one application per assembly may declare `[GeneratePageActivationHook]`.

### BWPH0004

The XAML compiler output is present but the App has no unique supported native metadata provider property.

### BWPH0005

A user declaration conflicts with `UsePageActivation(Func<Type, Page>)`. Other overloads are allowed.

### BWPH0006

The referenced XAML metadata interfaces cannot support the required activation interception.
