# BetterWinUI.Navigation

ViewModel-first Page mappings for WinUI. This package targets .NET 8 for Windows; it contains no routes, destination registry, or target-wide parameter contracts.

## Mutable and frozen maps

```csharp
using BetterWinUI.Navigation;

var pages = new PageMap();
pages.Add<HomeViewModel, HomePage>();
bool added = pages.TryAdd<DetailViewModel, DetailPage>();
pages.Set<HomeViewModel, CustomHomePage>();
bool found = pages.TryGetValue<HomeViewModel>(out Type? pageType);
bool removed = pages.Remove<DetailViewModel>();

FrozenPageMap snapshot = pages.Freeze();
```

`Add` throws `ArgumentException` for an existing ViewModel key. `TryAdd` returns false without replacing it. `Set` adds or replaces. `Resolve(Type)` throws `KeyNotFoundException` when no mapping exists. Different ViewModels may share a Page. Page types must be concrete, closed Page subclasses; constructor injection does not require a parameterless constructor.

`PageMap` is mutable and not thread-safe. Coordinate mutations and navigation on the owning UI thread. A navigator using it sees subsequent edits on new navigations. `Freeze()` creates an independent immutable snapshot; it does not freeze the source map. Neither map holds a Frame instance or owns a Page or ViewModel instance.

Both maps implement `IReadOnlyDictionary<Type, Type>`, supporting indexers, `Keys`, `Values`, `ContainsKey`, `TryGetValue(Type, out Type?)`, enumeration, and LINQ. The read-only interface does not make `PageMap` immutable; use `Freeze()` when an independent immutable snapshot is required.

## Generated default group

```csharp
[PageFor<HomeViewModel>]
public sealed partial class HomePage : Page;

pages.AddGeneratedPages();
```

The parameterless extension loads only ungrouped `PageFor` declarations from the assembly where that generated extension is compiled. It does not include named modules or referenced assemblies' default groups. No runtime assembly scanning is used.

## Explicit modules

```csharp
[PageModule]
public sealed partial class DesktopPages;

[PageModule]
public sealed partial class CompactPages;

[PageFor<DetailViewModel>(Module = typeof(DesktopPages))]
public sealed partial class DetailPage : Page;

[PageFor<DetailViewModel>(Module = typeof(CompactPages))]
public sealed partial class CompactDetailPage : Page;

var desktop = new PageMap();
desktop.AddGeneratedPages();
desktop.AddGeneratedPages<DesktopPages>();

var compact = new PageMap();
compact.AddGeneratedPages<CompactPages>();
```

Modules are top-level, accessible, non-generic, non-static, non-abstract partial classes. The generator supplies `IPageModule.Register(PageMap)`. The module must be declared in the same assembly as its Page attributes. Public modules can be loaded explicitly by referencing assemblies using `AddGeneratedPages<TModule>()`. Modules are registration groups, not host identities or DI scopes.

Multiple `PageFor` attributes on a Page are supported. A ViewModel may occur in different modules, but duplicate target mappings inside one group are compile-time errors. Loading groups with overlapping targets into the same map throws. Loading is atomic: registration failures and conflicts leave the map unchanged. Loading an already loaded nonempty module is a conflict, not an implicit no-op. Use `Set` for deliberate overrides after loading a nonconflicting group.

## Parameters and activation

`PageFor` describes only a default ViewModel-to-Page mapping. It does not assign DataContext, create a ViewModel, register DI services, or specify a route or parameter type. `[View]` and `[ViewModel]` from PageActivation independently register DI services.

`INavigationParameter<TViewModel>` is optional. It enables an explicitly chosen strict overload in the Frame navigator; it does not restrict ordinary open-parameter calls.

## Generator diagnostics

| ID | Meaning |
|---|---|
| BWNAV001 | Invalid or inaccessible Page mapping types |
| BWNAV002 | Unsupported local module declaration or module reference |
| BWNAV003 | Duplicate ViewModel mappings within one module/default group |

Mapping source generation uses embedded Scriban templates and targets Roslyn 4.8. Handwritten maps and manually implemented `IPageModule` types work without the generator.
