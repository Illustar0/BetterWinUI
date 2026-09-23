# BetterWinUI.Navigation.Frame

ViewModel-first navigation that directly uses WinUI Frame execution and history.

```csharp
using BetterWinUI.Navigation;
using BetterWinUI.Navigation.Frame;
using Microsoft.UI.Xaml.Media.Animation;

var pages = new PageMap();
pages.Add<HomeViewModel, HomePage>();
pages.Add<DetailViewModel, DetailPage>();
var navigator = new FrameNavigator(pages);

// Keep this lease for the lifetime of the window or control hosting the Frame.
IDisposable attachment = navigator.Attach(contentFrame);

navigator.Navigate<HomeViewModel>();
navigator.Navigate<DetailViewModel>(new DetailArgs(42));
navigator.Navigate<DetailViewModel>(args, new DrillInNavigationTransitionInfo());
navigator.Navigate<HomeViewModel>(transitionInfo: new EntranceNavigationTransitionInfo());
```

There is no transition-only positional overload: the first positional argument is always the navigation parameter. `Navigate` returns the native Frame result; mapping, attachment, and navigation exceptions are not converted to false. This return value does not represent completion of asynchronous Page data loading.

## Requests and parameters

```csharp
FrameNavigationRequest request = FrameNavigationRequest.For<DetailViewModel>(args, transition);
navigator.Navigate(request);

public sealed record DetailArgs(int Id) : INavigationParameter<DetailViewModel>;

// Optional compile-time target/parameter association:
navigator.Navigate<DetailViewModel, DetailArgs>(args, transition);
```

Requests expose read-only `ViewModelType`, `Parameter`, and `TransitionInfo`. Parameter and transition objects are retained by reference, not cloned. Null represents no parameter. Ordinary single-generic calls accept arbitrary objects; only the explicit two-generic overload enforces the marker association.

The Page receives the original parameter in `NavigationEventArgs.Parameter`, not the request object. Navigation does not establish a target-wide parameter type.

## Presentation policy

Each navigator can use a separate mutable map, a shared map, an explicit frozen snapshot (`new FrameNavigator(pages.Freeze())`), or an application resolver:

```csharp
var navigator = new FrameNavigator((viewModelType, parameter) =>
    viewModelType == typeof(DetailViewModel) && layout.IsCompact
        ? typeof(CompactDetailPage)
        : pages.Resolve(viewModelType));
```

Maps and resolvers select Page types; Page construction remains native XAML or PageActivation/DI behavior. They do not automatically bind a ViewModel instance.

## Attachment and history

A navigator has at most one attached Frame. Attach, detach, and navigation run on that Frame's UI thread. Dispose the lease before attaching another Frame. A detached navigator reports false for `CanGoBack`/`CanGoForward` and rejects execution.

`GoBack()`, `GoBack(transitionInfo)`, and `GoForward()` use native Frame history. Changing a map affects new navigation, not Page types already recorded in history. No ViewModel history, request persistence, guard pipeline, or per-navigation DI scope is supplied. Complex parameters can be used in memory, but native `Frame.GetNavigationState()` supports only its documented basic parameter types.

## DI and routes

Register the concrete navigator using normal DI factories, making map ownership explicit:

```csharp
services.AddSingleton<FrameNavigator>(_ => new FrameNavigator(pages));
```

Use separate factories or keyed registrations for separate hosts as needed. There is no generated global navigation registration or singleton map policy.

Routes belong to the application. Parse an address into a ViewModel and arguments, then call the same navigator (or construct a request). Multiple addresses may lead to the same ViewModel; no route is required for direct navigation.
