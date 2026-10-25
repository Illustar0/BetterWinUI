using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Microsoft.UI.Xaml.Controls;

namespace BetterWinUI.Navigation;

/// <summary>Stores mutable mappings from ViewModel types to the Page types that present them.</summary>
/// <remarks>
/// A navigator that uses this map sees later changes on its next navigation. Call <see cref="Freeze" /> for an
/// independent snapshot. The map is not thread-safe; change it on the UI thread that owns its navigators.
/// </remarks>
public sealed class PageMap : PageResolver, IReadOnlyDictionary<Type, Type>
{
    private Dictionary<Type, Type> _pages = [];

    /// <summary>Creates an immutable snapshot of the current mappings.</summary>
    /// <returns>A frozen map that later changes to this map do not affect.</returns>
    public FrozenPageMap Freeze() => new(_pages);

    /// <summary>Gets the number of mapped ViewModel types.</summary>
    public int Count => _pages.Count;

    /// <summary>Gets the Page type mapped to a ViewModel type.</summary>
    /// <param name="key">The ViewModel type.</param>
    /// <returns>The mapped Page type.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key" /> is <see langword="null" />.</exception>
    /// <exception cref="KeyNotFoundException"><paramref name="key" /> is not mapped.</exception>
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

    /// <summary>Maps a ViewModel type to a Page type.</summary>
    /// <typeparam name="TViewModel">The ViewModel type that identifies the navigation target.</typeparam>
    /// <typeparam name="TPage">The concrete Page type that presents the target.</typeparam>
    /// <exception cref="ArgumentException">
    /// <typeparamref name="TViewModel" /> is already mapped.
    /// -or-
    /// <typeparamref name="TPage" /> is the Page base type or abstract.
    /// </exception>
    public void Add<TViewModel, TPage>() where TViewModel : class where TPage : Page
    {
        _pages.Add(typeof(TViewModel), EnsurePage(typeof(TPage)));
    }

    /// <summary>Maps a ViewModel type to a Page type unless the ViewModel type is already mapped.</summary>
    /// <typeparam name="TViewModel">The ViewModel type that identifies the navigation target.</typeparam>
    /// <typeparam name="TPage">The concrete Page type that presents the target.</typeparam>
    /// <returns>
    /// <see langword="true" /> if the mapping was added; <see langword="false" /> if
    /// <typeparamref name="TViewModel" /> was already mapped, in which case its mapping is kept.
    /// </returns>
    /// <exception cref="ArgumentException"><typeparamref name="TPage" /> is the Page base type or abstract.</exception>
    public bool TryAdd<TViewModel, TPage>() where TViewModel : class where TPage : Page
    {
        return _pages.TryAdd(typeof(TViewModel), EnsurePage(typeof(TPage)));
    }

    /// <summary>Maps a ViewModel type to a Page type, replacing any existing mapping.</summary>
    /// <typeparam name="TViewModel">The ViewModel type that identifies the navigation target.</typeparam>
    /// <typeparam name="TPage">The concrete Page type that presents the target.</typeparam>
    /// <exception cref="ArgumentException"><typeparamref name="TPage" /> is the Page base type or abstract.</exception>
    public void Set<TViewModel, TPage>() where TViewModel : class where TPage : Page
    {
        _pages[typeof(TViewModel)] = EnsurePage(typeof(TPage));
    }

    /// <summary>Removes the mapping of a ViewModel type.</summary>
    /// <typeparam name="TViewModel">The ViewModel type to unmap.</typeparam>
    /// <returns><see langword="true" /> if a mapping was removed; otherwise, <see langword="false" />.</returns>
    public bool Remove<TViewModel>() where TViewModel : class => _pages.Remove(typeof(TViewModel));

    /// <inheritdoc />
    public bool TryGetValue(Type key, [NotNullWhen(true)] out Type? value) =>
        _pages.TryGetValue(key, out value);

    /// <summary>Gets the Page type mapped to a ViewModel type.</summary>
    /// <typeparam name="TViewModel">The ViewModel type to look up.</typeparam>
    /// <param name="pageType">
    /// When this method returns, contains the mapped Page type if the ViewModel type is mapped; otherwise,
    /// <see langword="null" />.
    /// </param>
    /// <returns><see langword="true" /> if the ViewModel type is mapped; otherwise, <see langword="false" />.</returns>
    public bool TryGetValue<TViewModel>([NotNullWhen(true)] out Type? pageType) where TViewModel : class =>
        _pages.TryGetValue(typeof(TViewModel), out pageType);

    /// <summary>Gets the Page type mapped to a ViewModel type.</summary>
    /// <param name="viewModelType">The ViewModel type to look up.</param>
    /// <returns>The mapped Page type.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="viewModelType" /> is <see langword="null" />.</exception>
    /// <exception cref="KeyNotFoundException"><paramref name="viewModelType" /> is not mapped.</exception>
    public Type Resolve(Type viewModelType)
    {
        ArgumentNullException.ThrowIfNull(viewModelType);
        return _pages.TryGetValue(viewModelType, out var pageType)
            ? pageType
            : throw new KeyNotFoundException($"No Page is mapped for ViewModel '{viewModelType}'.");
    }

    /// <summary>Adds every mapping of a Page module, or none of them if registration fails.</summary>
    /// <typeparam name="TModule">The generated or handwritten Page module.</typeparam>
    /// <exception cref="ArgumentException">The module maps a ViewModel type that this map already contains.</exception>
    /// <remarks>Exceptions thrown by the module's registration propagate, and this map stays unchanged.</remarks>
    public void AddGeneratedPages<TModule>() where TModule : IPageModule
    {
        // The module registers into a staging map so that a conflict or a failure partway through
        // registration leaves this map unchanged.
        var staged = new PageMap();
        TModule.Register(staged);
        _pages = _pages.Concat(staged._pages)
            .ToDictionary(static mapping => mapping.Key, static mapping => mapping.Value);
    }

    /// <inheritdoc />
    private protected override Type ResolveCore(Type viewModelType, object? parameter) => Resolve(viewModelType);
}
