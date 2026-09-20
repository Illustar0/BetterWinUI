namespace BetterWinUI.PageActivation.Generator;

/// <summary>Names the WinUI contracts used by the activation generator.</summary>
internal static class MetadataNames
{
    /// <summary>The application marker.</summary>
    public const string GeneratePageActivationHookAttribute = "BetterWinUI.PageActivation.GeneratePageActivationHookAttribute";
    /// <summary>The native application base type.</summary>
    public const string Application = "Microsoft.UI.Xaml.Application";
    /// <summary>The metadata provider contract.</summary>
    public const string XamlMetadataProvider = "Microsoft.UI.Xaml.Markup.IXamlMetadataProvider";
    /// <summary>The XAML type contract.</summary>
    public const string XamlType = "Microsoft.UI.Xaml.Markup.IXamlType";
}
