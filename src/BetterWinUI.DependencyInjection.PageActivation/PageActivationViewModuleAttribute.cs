using System.ComponentModel;

namespace BetterWinUI.DependencyInjection.PageActivation;

/// <summary>
/// Describes a generated cross-assembly page activation view module.
/// </summary>
/// <remarks>
/// This attribute is infrastructure for the source generator and is not intended for application code.
/// </remarks>
/// <param name="contractVersion">The generated module contract version.</param>
/// <param name="moduleType">The generated public module type.</param>
[AttributeUsage(AttributeTargets.Assembly)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class PageActivationViewModuleAttribute(
    int contractVersion,
    Type moduleType) : Attribute
{
    /// <summary>
    /// Gets the current generated module contract version.
    /// </summary>
    public const int CurrentContractVersion = 1;

    /// <summary>
    /// Gets the generated module contract version.
    /// </summary>
    public int ContractVersion { get; } = contractVersion;

    /// <summary>
    /// Gets the generated public module type.
    /// </summary>
    public Type ModuleType { get; } = moduleType ?? throw new ArgumentNullException(nameof(moduleType));
}