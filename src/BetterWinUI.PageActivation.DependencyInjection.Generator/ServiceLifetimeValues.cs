namespace BetterWinUI.PageActivation.DependencyInjection.Generator;

/// <summary>Mirrors the numeric values of Microsoft DI's ServiceLifetime enum.</summary>
/// <remarks>
/// The generator reads lifetimes from attribute arguments as integers and does not reference the DI abstractions
/// assembly, which a compiler host would have to load alongside it.
/// </remarks>
internal static class ServiceLifetimeValues
{
    public const int Singleton = 0;

    public const int Scoped = 1;

    public const int Transient = 2;

    /// <summary>Gets the ServiceLifetime member name for a validated lifetime value.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lifetime" /> is not a defined lifetime.</exception>
    public static string GetName(int lifetime)
    {
        return lifetime switch
        {
            Singleton => "Singleton",
            Scoped => "Scoped",
            Transient => "Transient",
            _ => throw new ArgumentOutOfRangeException(
                nameof(lifetime),
                lifetime,
                "Unsupported Microsoft DI service lifetime.")
        };
    }
}
