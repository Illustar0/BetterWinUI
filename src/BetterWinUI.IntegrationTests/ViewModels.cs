using BetterWinUI.PageActivation.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace BetterWinUI.IntegrationTests;

/// <summary>Provides a transient constructor dependency.</summary>
[ViewModel(ServiceLifetime.Transient)]
public sealed class FirstViewModel;
/// <summary>Provides a singleton constructor dependency.</summary>
[ViewModel(ServiceLifetime.Singleton)]
public sealed class SingletonViewModel;
/// <summary>Has a generated transient default overridden by the test composition root.</summary>
[ViewModel(ServiceLifetime.Transient)]
public sealed class OverrideViewModel;
/// <summary>Verifies that record ViewModels participate in generated registration.</summary>
[ViewModel(ServiceLifetime.Transient)]
public sealed record RecordViewModel;
/// <summary>Identifies the keyed service supplied to Page constructors.</summary>
#pragma warning disable S2094 // An identity-only service fixture tests keyed resolution.
public sealed class TestClock;
#pragma warning restore S2094
