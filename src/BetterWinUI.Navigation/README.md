# BetterWinUI.Navigation

Strongly typed, ViewModel-first navigation destination registration.

This core package maps ViewModels and exact route identifiers to View types. It does not execute navigation. Adapter
packages such as
`BetterWinUI.Navigation.Frame` consume the immutable `NavigationRegistry` and define their own execution semantics.

```csharp
var builder = new NavigationRegistryBuilder();
builder.Register<HomeViewModel, HomePage>("home");
builder.Register<DetailViewModel, DetailPage, DetailArgs>("detail");

NavigationRegistry registry = builder.Build();
```

Or, you can use `ViewFor` to generate the registrations:

```csharp
[ViewFor<HomeViewModel>("home")]
public sealed partial class HomePage;

[ViewFor<DetailViewModel, DetailArgs>("detail")]
public sealed partial class DetailPage;
```
