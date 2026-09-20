using System.ComponentModel;

namespace BetterWinUI.PageActivation.DependencyInjection;

/// <summary>
/// Describes a generated cross-assembly page activation view module.
/// </summary>
/// <remarks>
/// This attribute is infrastructure for the source generator and is not intended for application code.
/// </remarks>
/// <param name="moduleType">The generated public module type.</param>
[AttributeUsage(AttributeTargets.Assembly)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class PageActivationViewModuleAttribute(
    Type moduleType) : Attribute
{
    /// <summary>
    /// Gets the generated public module type.
    /// </summary>
    public Type ModuleType { get; } = moduleType ?? throw new ArgumentNullException(nameof(moduleType));
}
