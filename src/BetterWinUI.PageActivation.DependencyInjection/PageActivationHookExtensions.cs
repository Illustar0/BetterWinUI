using Microsoft.Extensions.DependencyInjection;

namespace BetterWinUI.PageActivation.DependencyInjection;

/// <summary>
/// Installs dependency injection as the Page activation policy of a hook application.
/// </summary>
public static class PageActivationHookExtensions
{
    /// <summary>Installs a factory that resolves every requested Page from a service provider.</summary>
    /// <typeparam name="TPage">The WinUI Page base type.</typeparam>
    /// <param name="hook">The application that receives the factory.</param>
    /// <param name="services">The provider that owns Page creation and service lifetimes.</param>
    /// <remarks>
    /// Install once after the provider is built and before navigating. Unregistered Pages throw.
    /// The application owns and disposes the provider.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="hook" /> or <paramref name="services" /> is null.</exception>
    /// <exception cref="InvalidOperationException">A factory is already installed, or XAML metadata is not available yet.</exception>
    public static void UsePageActivation<TPage>(this IPageActivationHook<TPage> hook, IServiceProvider services)
        where TPage : class
    {
        ArgumentNullException.ThrowIfNull(hook);
        ArgumentNullException.ThrowIfNull(services);
        hook.UsePageActivation(pageType => (TPage)services.GetRequiredService(pageType));
    }
}
