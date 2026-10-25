using System.ComponentModel;

namespace BetterWinUI.PageActivation.DependencyInjection;

/// <summary>Identifies the generated view module that an assembly exports to referencing assemblies.</summary>
/// <param name="moduleType">The generated public module type.</param>
/// <remarks>
/// The dependency injection generator emits this attribute and reads it from references to build
/// AddGeneratedPageServices. Application code does not use it.
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class PageActivationViewModuleAttribute(
    Type moduleType) : Attribute
{
    /// <summary>Gets the generated public module type.</summary>
    public Type ModuleType { get; } = moduleType ?? throw new ArgumentNullException(nameof(moduleType));
}
