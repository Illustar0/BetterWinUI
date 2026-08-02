using System.Collections.Concurrent;
using System.Reflection;
using Scriban;

namespace BetterWinUI.DependencyInjection.PageActivation.Generator;

/// <summary>
/// Loads embedded Scriban templates and exposes one small rendering seam.
/// </summary>
internal static class TemplateRenderer
{
    private static readonly Assembly Assembly = typeof(TemplateRenderer).Assembly;

    private static readonly ConcurrentDictionary<string, Template> Templates =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Renders an embedded template with public model member names preserved.
    /// </summary>
    /// <param name="templateName">The embedded template file name.</param>
    /// <param name="model">The template model.</param>
    /// <returns>The rendered C# source.</returns>
    public static string Render(string templateName, object model)
    {
        var template = Templates.GetOrAdd(templateName, ParseTemplate);
        return template.Render(model, static member => member.Name);
    }

    private static Template ParseTemplate(string templateName)
    {
        var template = Template.Parse(ReadTemplate(templateName), templateName);
        if (template.HasErrors)
            throw new InvalidOperationException(
                $"Scriban template '{templateName}' is invalid: {template.Messages}");

        return template;
    }

    private static string ReadTemplate(string templateName)
    {
        var suffix = $".Templates.{templateName}";
        var resourceName = Assembly
            .GetManifestResourceNames()
            .Single(name => name.EndsWith(suffix, StringComparison.Ordinal));
        using var stream = Assembly.GetManifestResourceStream(resourceName) ??
                           throw new InvalidOperationException(
                               $"Embedded Scriban template '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}