using Microsoft.UI.Xaml.Controls;

namespace BetterWinUI.Navigation;

/// <summary>Chooses the Page type that presents a ViewModel-first navigation target.</summary>
/// <remarks>
/// <see cref="PageMap" /> and <see cref="FrozenPageMap" /> are resolvers, and <see cref="Create" /> adapts an
/// application-defined policy. A resolver selects Page types only; it does not construct Pages or bind ViewModels.
/// </remarks>
public abstract class PageResolver
{
    // Derivation is limited to this assembly so that every resolver goes through the Page check in Resolve.
    private protected PageResolver()
    {
    }

    /// <summary>Selects the Page type for a navigation target.</summary>
    /// <param name="viewModelType">The ViewModel type that identifies the navigation target.</param>
    /// <param name="parameter">The original navigation parameter, or <see langword="null" /> if none is supplied.</param>
    /// <returns>A concrete, closed Page subclass.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="viewModelType" /> is <see langword="null" />.
    /// -or-
    /// The resolver selected <see langword="null" />.
    /// </exception>
    /// <exception cref="ArgumentException">The selected type is not a concrete, closed Page subclass.</exception>
    /// <exception cref="KeyNotFoundException">A map does not contain <paramref name="viewModelType" />.</exception>
    /// <remarks>Exceptions thrown by a policy from <see cref="Create" /> propagate unchanged.</remarks>
    public Type Resolve(Type viewModelType, object? parameter)
    {
        ArgumentNullException.ThrowIfNull(viewModelType);
        return EnsurePage(ResolveCore(viewModelType, parameter));
    }

    /// <summary>Creates a resolver from an application-defined Page selection policy.</summary>
    /// <param name="resolve">
    /// A function that receives the ViewModel type and the original navigation parameter and returns a Page type.
    /// </param>
    /// <returns>A resolver that checks every Page type the policy returns.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="resolve" /> is <see langword="null" />.</exception>
    public static PageResolver Create(Func<Type, object?, Type> resolve)
    {
        ArgumentNullException.ThrowIfNull(resolve);
        return new PolicyPageResolver(resolve);
    }

    private protected abstract Type ResolveCore(Type viewModelType, object? parameter);

    /// <summary>Rejects types that WinUI cannot activate as a Page.</summary>
    private protected static Type EnsurePage(Type pageType)
    {
        ArgumentNullException.ThrowIfNull(pageType);
        if (pageType == typeof(Page) || !typeof(Page).IsAssignableFrom(pageType) ||
            pageType.IsAbstract || pageType.ContainsGenericParameters)
            throw new ArgumentException($"Type '{pageType}' must be a concrete, closed Page subclass.", nameof(pageType));
        return pageType;
    }

    private sealed class PolicyPageResolver(Func<Type, object?, Type> resolve) : PageResolver
    {
        private protected override Type ResolveCore(Type viewModelType, object? parameter) =>
            resolve(viewModelType, parameter);
    }
}
