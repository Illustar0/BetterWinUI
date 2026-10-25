using Microsoft.Extensions.DependencyInjection;

namespace BetterWinUI.PageActivation.DependencyInjection;

/// <summary>Provides dependency injection as the Page activation policy of a hook application.</summary>
public static class PageActivationHookExtensions
{
    /// <summary>Installs a factory that resolves every requested Page from a service provider.</summary>
    /// <typeparam name="TPage">The WinUI Page base type.</typeparam>
    /// <param name="hook">The application that receives the factory.</param>
    /// <param name="services">The provider that creates Pages and owns their service lifetimes.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="hook" /> or <paramref name="services" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// A factory is already installed.
    /// -or-
    /// The application was compiled without WinUI XAML metadata.
    /// </exception>
    /// <remarks>
    /// Install the factory once, after the provider is built and before the first navigation. Activating a Page that
    /// is not registered throws <see cref="InvalidOperationException" />; there is no fallback to native activation.
    /// The application owns the provider and must dispose it.
    /// </remarks>
    public static void UsePageActivation<TPage>(this IPageActivationHook<TPage> hook, IServiceProvider services)
        where TPage : class
    {
        ArgumentNullException.ThrowIfNull(hook);
        ArgumentNullException.ThrowIfNull(services);
        hook.UsePageActivation(pageType => (TPage)services.GetRequiredService(pageType));
    }
}
