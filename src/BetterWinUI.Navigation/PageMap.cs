using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Microsoft.UI.Xaml.Controls;

namespace BetterWinUI.Navigation;

/// <summary>Stores mutable ViewModel-to-Page mappings for one or more navigators.</summary>
/// <remarks>This collection is not thread-safe. Coordinate changes with navigation on the owning UI thread.</remarks>
public sealed class PageMap : IReadOnlyDictionary<Type, Type>
{
    private Dictionary<Type, Type> _pages = [];

    /// <summary>
    /// Creates an immutable snapshot of the current mappings.
    /// </summary>
    /// <returns>
    /// A frozen map that is unaffected by subsequent changes to this map.
    /// </returns>
    public FrozenPageMap Freeze() => new(_pages);

    /// <summary>Gets the number of mapped ViewModel types.</summary>
    public int Count => _pages.Count;

    /// <inheritdoc />
    public Type this[Type key] => Resolve(key);

    /// <inheritdoc />
    public IEnumerable<Type> Keys => _pages.Keys;

    /// <inheritdoc />
    public IEnumerable<Type> Values => _pages.Values;

    /// <inheritdoc />
    public bool ContainsKey(Type key) => _pages.ContainsKey(key);

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<Type, Type>> GetEnumerator() => _pages.GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Adds a mapping, throwing ArgumentException if the ViewModel already exists.</summary>
    /// <typeparam name="TViewModel">The logical target.</typeparam>
    /// <typeparam name="TPage">The concrete Page type.</typeparam>
    public void Add<TViewModel, TPage>() where TViewModel : class where TPage : Page
    {
        ValidatePage(typeof(TPage));
        _pages.Add(typeof(TViewModel), typeof(TPage));
    }

    /// <summary>Adds a mapping only if the ViewModel is absent.</summary>
    /// <typeparam name="TViewModel">The logical target.</typeparam>
    /// <typeparam name="TPage">The concrete Page type.</typeparam>
    /// <returns>Whether the mapping was added.</returns>
    public bool TryAdd<TViewModel, TPage>() where TViewModel : class where TPage : Page
    {
        ValidatePage(typeof(TPage));
        return _pages.TryAdd(typeof(TViewModel), typeof(TPage));
    }

    /// <summary>Adds or replaces a mapping.</summary>
    /// <typeparam name="TViewModel">The logical target.</typeparam>
    /// <typeparam name="TPage">The concrete Page type.</typeparam>
    public void Set<TViewModel, TPage>() where TViewModel : class where TPage : Page
    {
        ValidatePage(typeof(TPage));
        _pages[typeof(TViewModel)] = typeof(TPage);
    }

    /// <summary>Removes a ViewModel mapping.</summary>
    /// <typeparam name="TViewModel">The logical target.</typeparam>
    /// <returns>Whether a mapping was removed.</returns>
    public bool Remove<TViewModel>() where TViewModel : class => _pages.Remove(typeof(TViewModel));

    /// <inheritdoc />
    public bool TryGetValue(Type key, [NotNullWhen(true)] out Type? value) =>
        _pages.TryGetValue(key, out value);

    /// <summary>Attempts to find a ViewModel's current Page mapping.</summary>
    /// <typeparam name="TViewModel">The logical target.</typeparam>
    /// <param name="pageType">The Page type when found.</param>
    /// <returns>Whether a mapping exists.</returns>
    public bool TryGetValue<TViewModel>([NotNullWhen(true)] out Type? pageType) where TViewModel : class =>
        _pages.TryGetValue(typeof(TViewModel), out pageType);

    /// <summary>Resolves a ViewModel or throws KeyNotFoundException when it is unmapped.</summary>
    /// <param name="viewModelType">The logical target.</param>
    /// <returns>The currently mapped Page type.</returns>
    public Type Resolve(Type viewModelType)
    {
        ArgumentNullException.ThrowIfNull(viewModelType);
        return _pages.TryGetValue(viewModelType, out var pageType)
            ? pageType
            : throw new KeyNotFoundException($"No Page is mapped for ViewModel '{viewModelType}'.");
    }

    /// <summary>Atomically adds a module, leaving this map unchanged if registration fails.</summary>
    /// <typeparam name="TModule">The generated or manually implemented module.</typeparam>
    public void AddGeneratedPages<TModule>() where TModule : IPageModule
    {
        var staged = new PageMap();
        TModule.Register(staged);
        _pages = _pages.Concat(staged._pages)
            .ToDictionary(static mapping => mapping.Key, static mapping => mapping.Value);
    }

    /// <summary>Checks that a resolved type is a concrete, closed Page subclass.</summary>
    private static void ValidatePage(Type pageType)
    {
        ArgumentNullException.ThrowIfNull(pageType);
        if (pageType == typeof(Page) || !typeof(Page).IsAssignableFrom(pageType) ||
            pageType.IsAbstract || pageType.ContainsGenericParameters)
            throw new ArgumentException($"Type '{pageType}' must be a concrete, closed Page subclass.", nameof(pageType));
    }
}
