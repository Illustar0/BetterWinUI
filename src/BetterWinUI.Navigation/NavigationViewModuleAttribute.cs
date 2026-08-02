using System.ComponentModel;

namespace BetterWinUI.Navigation;

/// <summary>
/// Describes a generated cross-assembly navigation registration module.
/// </summary>
/// <param name="contractVersion">The generated module contract version.</param>
/// <param name="moduleType">The generated public module type.</param>
[AttributeUsage(AttributeTargets.Assembly)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class NavigationViewModuleAttribute(
    int contractVersion,
    Type moduleType) : Attribute
{
    /// <summary>Gets the generated module contract version.</summary>
    public int ContractVersion { get; } = contractVersion;

    /// <summary>Gets the generated public module type.</summary>
    public Type ModuleType { get; } =
        moduleType ?? throw new ArgumentNullException(nameof(moduleType));
}