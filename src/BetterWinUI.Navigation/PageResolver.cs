using Microsoft.UI.Xaml.Controls;

namespace BetterWinUI.Navigation;

/// <summary>Chooses the Page type that presents a ViewModel-first navigation target.</summary>
/// <remarks>
/// Page maps are resolvers; <see cref="Create" /> adapts an application-defined policy.
/// Every resolved type is checked before it reaches a navigator.
/// </remarks>
public abstract class PageResolver
{
    /// <summary>Restricts resolvers to the Page maps and <see cref="Create" />.</summary>
    private protected PageResolver()
    {
    }

    /// <summary>Resolves the Page type for a navigation target.</summary>
    /// <param name="viewModelType">The logical target.</param>
    /// <param name="parameter">The original navigation parameter, or null when no parameter is supplied.</param>
    /// <returns>A concrete, closed Page subclass.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="viewModelType" /> is null.</exception>
    /// <exception cref="ArgumentException">The resolved type is not a concrete, closed Page subclass.</exception>
    public Type Resolve(Type viewModelType, object? parameter)
    {
        ArgumentNullException.ThrowIfNull(viewModelType);
        return EnsurePage(ResolveCore(viewModelType, parameter));
    }

    /// <summary>Creates a resolver from an application-defined Page selection policy.</summary>
    /// <param name="resolve">Selects a Page type from the ViewModel type and original parameter.</param>
    /// <returns>A resolver that checks every Page type the policy returns.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="resolve" /> is null.</exception>
    public static PageResolver Create(Func<Type, object?, Type> resolve)
    {
        ArgumentNullException.ThrowIfNull(resolve);
        return new PolicyPageResolver(resolve);
    }

    /// <summary>Selects the Page type for a non-null ViewModel type.</summary>
    private protected abstract Type ResolveCore(Type viewModelType, object? parameter);

    /// <summary>Rejects types that WinUI cannot activate as a Page.</summary>
    /// <param name="pageType">The candidate Page type.</param>
    /// <returns>The validated Page type.</returns>
    private protected static Type EnsurePage(Type pageType)
    {
        ArgumentNullException.ThrowIfNull(pageType);
        if (pageType == typeof(Page) || !typeof(Page).IsAssignableFrom(pageType) ||
            pageType.IsAbstract || pageType.ContainsGenericParameters)
            throw new ArgumentException($"Type '{pageType}' must be a concrete, closed Page subclass.", nameof(pageType));
        return pageType;
    }

    /// <summary>Adapts an application-defined Page selection policy.</summary>
    private sealed class PolicyPageResolver(Func<Type, object?, Type> resolve) : PageResolver
    {
        /// <inheritdoc />
        private protected override Type ResolveCore(Type viewModelType, object? parameter) =>
            resolve(viewModelType, parameter);
    }
}
