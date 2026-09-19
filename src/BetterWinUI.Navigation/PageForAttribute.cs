namespace BetterWinUI.Navigation;

#pragma warning disable S2326 // The generator consumes the ViewModel type argument.

/// <summary>Declares a default mapping from a ViewModel to the annotated Page.</summary>
/// <typeparam name="TViewModel">The logical ViewModel target.</typeparam>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class PageForAttribute<TViewModel> : Attribute where TViewModel : class
{
    /// <summary>Gets or sets the local generated module, or null for the assembly's default group.</summary>
    public Type? Module { get; set; }
}

#pragma warning restore S2326
