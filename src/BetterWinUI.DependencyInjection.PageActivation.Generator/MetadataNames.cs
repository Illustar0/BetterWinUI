namespace BetterWinUI.DependencyInjection.PageActivation.Generator;

/// <summary>
/// Contains metadata names shared by generator discovery and emission.
/// </summary>
internal static class MetadataNames
{
    /// <summary>
    /// The page activation marker attribute metadata name.
    /// </summary>
    public const string PageActivationAttribute =
        "BetterWinUI.DependencyInjection.PageActivation.PageActivationAttribute";

    /// <summary>
    /// The view marker attribute metadata name.
    /// </summary>
    public const string ViewAttribute =
        "BetterWinUI.DependencyInjection.PageActivation.ViewAttribute";

    /// <summary>
    /// The view model marker attribute metadata name.
    /// </summary>
    public const string ViewModelAttribute =
        "BetterWinUI.DependencyInjection.PageActivation.ViewModelAttribute";

    /// <summary>
    /// The generated view module attribute metadata name.
    /// </summary>
    public const string ViewModuleAttribute =
        "BetterWinUI.DependencyInjection.PageActivation.PageActivationViewModuleAttribute";

    /// <summary>
    /// The WinUI application metadata name.
    /// </summary>
    public const string Application = "Microsoft.UI.Xaml.Application";

    /// <summary>
    /// The WinUI page metadata name.
    /// </summary>
    public const string Page = "Microsoft.UI.Xaml.Controls.Page";

    /// <summary>
    /// The WinUI XAML metadata provider interface metadata name.
    /// </summary>
    public const string XamlMetadataProvider = "Microsoft.UI.Xaml.Markup.IXamlMetadataProvider";

    /// <summary>
    /// The WinUI XAML type interface metadata name.
    /// </summary>
    public const string XamlType = "Microsoft.UI.Xaml.Markup.IXamlType";

    /// <summary>
    /// The generated runtime namespace.
    /// </summary>
    public const string RuntimeNamespace = "BetterWinUI.DependencyInjection.PageActivation";

    /// <summary>
    /// The generated implementation namespace.
    /// </summary>
    public const string GeneratedNamespace =
        "BetterWinUI.DependencyInjection.PageActivation.Generated";
}