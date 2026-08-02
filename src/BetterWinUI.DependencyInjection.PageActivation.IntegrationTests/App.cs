using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace BetterWinUI.DependencyInjection.PageActivation.IntegrationTests;

/// <summary>
/// Compiles the generated adapter against the real Windows App SDK surface.
/// </summary>
[PageActivation]
public sealed partial class App : Application
{
    /// <summary>
    /// Exercises the generated registration interface.
    /// </summary>
    public static IServiceCollection Configure(IServiceCollection services)
    {
        return services.AddBetterPageActivation(static options =>
            options.UnregisteredPageBehavior =
                UnregisteredPageBehavior.FallbackToXamlActivator);
    }

    /// <summary>
    /// Exercises the generated one-time initialization interface.
    /// </summary>
    public void Initialize(IServiceProvider services)
    {
        InitializeBetterPageActivation(services);
    }
}