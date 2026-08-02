using Microsoft.Extensions.DependencyInjection;

namespace BetterWinUI.DependencyInjection.PageActivation;

/// <summary>
/// Adds a generated Microsoft DI registration for a concrete view model.
/// </summary>
/// <remarks>
/// This attribute is optional registration syntax sugar. It does not associate the
/// view model with a View. Omit it to register the view model directly with the DI container.
/// </remarks>
/// <param name="lifetime">The generated Microsoft DI registration lifetime.</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ViewModelAttribute(ServiceLifetime lifetime) : Attribute
{
    /// <summary>
    /// Gets the generated Microsoft DI registration lifetime.
    /// </summary>
    /// <remarks>
    /// <see cref="ServiceLifetime.Scoped"/> is reserved for a future navigation-scope
    /// implementation and is rejected by the generator today.
    /// </remarks>
    public ServiceLifetime Lifetime { get; } = lifetime;
}