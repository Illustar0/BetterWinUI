using System.Collections;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace BetterWinUI.Navigation;

/// <summary>
/// Represents an immutable snapshot of ViewModel-to-Page mappings.
/// </summary>
public sealed class FrozenPageMap : IReadOnlyDictionary<Type, Type>
{
    private readonly FrozenDictionary<Type, Type> _pages;

    /// <summary>Copies the supplied mappings into an immutable snapshot.</summary>
    internal FrozenPageMap(IEnumerable<KeyValuePair<Type, Type>> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        _pages = pages.ToFrozenDictionary();
    }

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

    /// <inheritdoc />
    public bool TryGetValue(Type key, [NotNullWhen(true)] out Type? value) =>
        _pages.TryGetValue(key, out value);

    /// <summary>Attempts to find a ViewModel's Page mapping.</summary>
    /// <typeparam name="TViewModel">The logical target.</typeparam>
    /// <param name="pageType">The Page type when found.</param>
    /// <returns>Whether a mapping exists.</returns>
    public bool TryGetValue<TViewModel>([NotNullWhen(true)] out Type? pageType)
        where TViewModel : class =>
        _pages.TryGetValue(typeof(TViewModel), out pageType);

    /// <summary>
    /// Resolves a ViewModel or throws KeyNotFoundException when it is unmapped.
    /// </summary>
    /// <param name="viewModelType">The logical target.</param>
    /// <returns>The mapped Page type.</returns>
    public Type Resolve(Type viewModelType)
    {
        ArgumentNullException.ThrowIfNull(viewModelType);

        return _pages.TryGetValue(viewModelType, out var pageType)
            ? pageType
            : throw new KeyNotFoundException(
                $"No Page is mapped for ViewModel '{viewModelType}'.");
    }
}
