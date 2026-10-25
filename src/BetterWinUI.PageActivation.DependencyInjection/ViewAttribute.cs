using Microsoft.Extensions.DependencyInjection;

namespace BetterWinUI.PageActivation.DependencyInjection;

/// <summary>Marks a WinUI Page for a generated dependency injection registration.</summary>
/// <param name="lifetime">The lifetime of the generated registration.</param>
/// <remarks>
/// The attribute is optional; registering the Page directly with the service collection has the same effect. It
/// does not associate the Page with a ViewModel.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ViewAttribute(
    ServiceLifetime lifetime = ServiceLifetime.Transient) : Attribute
{
    /// <summary>Gets the lifetime of the generated registration.</summary>
    /// <value>The service lifetime. The default is <see cref="ServiceLifetime.Transient" />.</value>
    public ServiceLifetime Lifetime { get; } = lifetime;
}
