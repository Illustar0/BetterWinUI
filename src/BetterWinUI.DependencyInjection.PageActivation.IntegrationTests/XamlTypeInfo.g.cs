using Microsoft.UI.Xaml.Markup;

namespace BetterWinUI.DependencyInjection.PageActivation.IntegrationTests;

public sealed partial class App
{
    private NativeXamlMetadataProvider _AppProvider { get; } = new();

    private sealed partial class NativeXamlMetadataProvider : IXamlMetadataProvider
    {
        public IXamlType GetXamlType(Type type) => null!;

        public IXamlType GetXamlType(string fullName) => null!;

        public XmlnsDefinition[] GetXmlnsDefinitions() => [];
    }
}
