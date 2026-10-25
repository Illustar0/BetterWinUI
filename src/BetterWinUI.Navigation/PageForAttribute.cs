namespace BetterWinUI.Navigation;

#pragma warning disable S2326 // The generator consumes the ViewModel type argument.

/// <summary>Declares that the attributed Page presents a ViewModel navigation target by default.</summary>
/// <typeparam name="TViewModel">The ViewModel type that identifies the navigation target.</typeparam>
/// <remarks>
/// The navigation generator turns these declarations into Page mappings. Apply the attribute more than once to
/// present several targets with the same Page.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class PageForAttribute<TViewModel> : Attribute where TViewModel : class
{
    /// <summary>Gets or sets the Page module that receives the mapping.</summary>
    /// <value>
    /// A type in the same assembly marked with <see cref="PageModuleAttribute" />, or <see langword="null" /> to
    /// add the mapping to the assembly's default group.
    /// </value>
    public Type? Module { get; set; }
}

#pragma warning restore S2326
