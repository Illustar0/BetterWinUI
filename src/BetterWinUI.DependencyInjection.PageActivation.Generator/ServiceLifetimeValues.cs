namespace BetterWinUI.DependencyInjection.PageActivation.Generator;

/// <summary>
/// Mirrors the stable numeric values of Microsoft DI's ServiceLifetime enum
/// without adding a runtime DI dependency to the analyzer.
/// </summary>
internal static class ServiceLifetimeValues
{
    /// <summary>Represents ServiceLifetime.Singleton.</summary>
    public const int Singleton = 0;

    /// <summary>Represents ServiceLifetime.Scoped.</summary>
    public const int Scoped = 1;

    /// <summary>Represents ServiceLifetime.Transient.</summary>
    public const int Transient = 2;

    /// <summary>
    /// Gets the Microsoft DI enum member name for a numeric lifetime value.
    /// </summary>
    /// <param name="lifetime">The numeric Microsoft DI lifetime.</param>
    /// <returns>The matching <c>ServiceLifetime</c> member name.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="lifetime"/> is not a defined Microsoft DI lifetime.
    /// </exception>
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