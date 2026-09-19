using System.Collections.Concurrent;
using Scriban;

namespace BetterWinUI.Navigation.Generator;

/// <summary>Loads and renders the generator's embedded Scriban templates.</summary>
internal static class TemplateRenderer
{
    private static readonly ConcurrentDictionary<string, Template> Templates = new(StringComparer.Ordinal);

    /// <summary>Renders a template using the model's original public member names.</summary>
    internal static string Render(string name, object model) =>
        Templates.GetOrAdd(name, Load).Render(model, static member => member.Name);

    /// <summary>Loads and validates one embedded template.</summary>
    private static Template Load(string name)
    {
        using var stream = typeof(TemplateRenderer).Assembly.GetManifestResourceStream(
            "BetterWinUI.Navigation.Generator.Templates." + name) ??
            throw new InvalidOperationException($"Embedded template '{name}' was not found.");
        using var reader = new StreamReader(stream);
        var template = Template.Parse(reader.ReadToEnd(), name);
        if (template.HasErrors)
            throw new InvalidOperationException($"Invalid Scriban template '{name}': {template.Messages}");
        return template;
    }
}
