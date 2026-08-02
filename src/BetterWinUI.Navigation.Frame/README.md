# BetterWinUI.Navigation.Frame

ViewModel-first navigation for WinUI `Frame` hosts.

```csharp
public sealed class MainNavigationHost : FrameNavigationHost;

services.AddSingleton<MainNavigationHost>();
services.AddBetterFrameNavigation(builder =>
{
    builder.Register<HomeViewModel, HomePage>("home");
});
```

Attach the XAML Frame for its lifetime:

```csharp
IDisposable attachment = mainNavigationHost.Attach(contentFrame);
```

Then inject `FrameNavigationService<MainNavigationHost>` into application code.

The adapter mirrors WinUI Frame overloads, including parameterized navigation,
`NavigationTransitionInfo`, `GoBack()` / `GoBack(transition)`, and `GoForward()`. It does not activate Pages; WinUI or a
separate activation package owns that concern.
