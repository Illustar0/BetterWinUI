using Microsoft.Extensions.DependencyInjection;

namespace BetterWinUI.PageActivation.DependencyInjection;

/// <summary>Marks a ViewModel class for a generated dependency injection registration.</summary>
/// <param name="lifetime">The lifetime of the generated registration.</param>
/// <remarks>
/// The attribute is optional; registering the ViewModel directly with the service collection has the same effect.
/// It does not associate the ViewModel with a Page.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ViewModelAttribute(ServiceLifetime lifetime) : Attribute
{
    /// <summary>Gets the lifetime of the generated registration.</summary>
    public ServiceLifetime Lifetime { get; } = lifetime;
}
