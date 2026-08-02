using Microsoft.Extensions.DependencyInjection;

namespace BetterWinUI.DependencyInjection.PageActivation;

/// <summary>
/// Adds a generated Microsoft DI registration for a WinUI Page.
/// </summary>
/// <remarks>
/// This attribute is optional registration syntax sugar. It does not associate the
/// Page with a view model. Omit it to register the Page directly with the DI container.
/// </remarks>
/// <param name="lifetime">
/// The generated Microsoft DI registration lifetime. The default is
/// <see cref="ServiceLifetime.Transient"/>.
/// </param>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ViewAttribute(
    ServiceLifetime lifetime = ServiceLifetime.Transient) : Attribute
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