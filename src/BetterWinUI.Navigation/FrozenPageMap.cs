using System.Collections;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace BetterWinUI.Navigation;

/// <summary>Stores an immutable snapshot of mappings from ViewModel types to Page types.</summary>
/// <remarks>Create instances with <see cref="PageMap.Freeze" />. The snapshot is safe to read from any thread.</remarks>
public sealed class FrozenPageMap : PageResolver, IReadOnlyDictionary<Type, Type>
{
    private readonly FrozenDictionary<Type, Type> _pages;

    internal FrozenPageMap(IEnumerable<KeyValuePair<Type, Type>> pages) => _pages = pages.ToFrozenDictionary();

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
    public bool TryGetValue<TViewModel>([NotNullWhen(true)] out Type? pageType)
        where TViewModel : class =>
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
            : throw new KeyNotFoundException(
                $"No Page is mapped for ViewModel '{viewModelType}'.");
    }

    /// <inheritdoc />
    private protected override Type ResolveCore(Type viewModelType, object? parameter) => Resolve(viewModelType);
}
