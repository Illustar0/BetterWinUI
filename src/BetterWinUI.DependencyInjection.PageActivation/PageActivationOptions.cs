namespace BetterWinUI.DependencyInjection.PageActivation;

/// <summary>
/// Configures generated WinUI page activation behavior.
/// </summary>
public sealed class PageActivationOptions
{
    /// <summary>
    /// Gets or sets the behavior used when a requested page is not registered in the service provider.
    /// </summary>
    public UnregisteredPageBehavior UnregisteredPageBehavior { get; set; } =
        UnregisteredPageBehavior.Throw;
}