using System.Collections.Concurrent;
using System.Reflection;
using Scriban;

namespace BetterWinUI.PageActivation.Generators.Internal;

internal static class TemplateRenderer
{
    private static readonly Assembly Assembly = typeof(TemplateRenderer).Assembly;

    private static readonly ConcurrentDictionary<string, Template> Templates =
        new(StringComparer.Ordinal);

    /// <summary>Renders an embedded template with a model whose members keep their C# names.</summary>
    /// <param name="templateName">The template file name under the generator's Templates folder.</param>
    /// <param name="model">The object whose public members the template reads.</param>
    /// <returns>The rendered C# source.</returns>
    /// <remarks>Scriban renames members to snake_case by default; the templates use the original names.</remarks>
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
        // Resource names start with the generator's root namespace, so match on the folder and file name only.
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