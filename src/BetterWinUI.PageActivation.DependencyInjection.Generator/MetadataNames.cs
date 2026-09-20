namespace BetterWinUI.PageActivation.DependencyInjection.Generator;

/// <summary>
/// Contains metadata names shared by generator discovery and emission.
/// </summary>
internal static class MetadataNames
{
    /// <summary>
    /// The page activation marker attribute metadata name.
    /// </summary>
    public const string GeneratePageActivationHookAttribute =
        "BetterWinUI.PageActivation.GeneratePageActivationHookAttribute";

    /// <summary>
    /// The view marker attribute metadata name.
    /// </summary>
    public const string ViewAttribute =
        "BetterWinUI.PageActivation.DependencyInjection.ViewAttribute";

    /// <summary>
    /// The view model marker attribute metadata name.
    /// </summary>
    public const string ViewModelAttribute =
        "BetterWinUI.PageActivation.DependencyInjection.ViewModelAttribute";

    /// <summary>
    /// The generated view module attribute metadata name.
    /// </summary>
    public const string ViewModuleAttribute =
        "BetterWinUI.PageActivation.DependencyInjection.PageActivationViewModuleAttribute";

    /// <summary>
    /// The WinUI application metadata name.
    /// </summary>
    public const string Application = "Microsoft.UI.Xaml.Application";

    /// <summary>
    /// The WinUI page metadata name.
    /// </summary>
    public const string Page = "Microsoft.UI.Xaml.Controls.Page";

    /// <summary>
    /// The generated runtime namespace.
    /// </summary>
    public const string RuntimeNamespace = "BetterWinUI.PageActivation.DependencyInjection";

    /// <summary>
    /// The generated implementation namespace.
    /// </summary>
    public const string GeneratedNamespace =
        "BetterWinUI.PageActivation.DependencyInjection.Generated";
}
