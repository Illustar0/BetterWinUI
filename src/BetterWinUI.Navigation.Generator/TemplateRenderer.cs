using System.Reflection;
using Scriban;

namespace BetterWinUI.Navigation.Generator;

/// <summary>
/// Loads, validates, and renders the generator's embedded Scriban templates.
/// </summary>
internal static class TemplateRenderer
{
    private const string ResourcePrefix = "BetterWinUI.Navigation.Generator.Templates.";

    private static readonly Lazy<Template> NavigationViewModule =
        new(() => Load("NavigationViewModule.scriban"));

    private static readonly Lazy<Template> NavigationComposition =
        new(() => Load("NavigationComposition.scriban"));

    /// <summary>
    /// Renders the navigation view module template.
    /// </summary>
    /// <param name="model">The immutable template input.</param>
    /// <returns>The generated C# source.</returns>
    internal static string RenderNavigationViewModule(object model)
    {
        return NavigationViewModule.Value.Render(model);
    }

    /// <summary>
    /// Renders the navigation composition template.
    /// </summary>
    /// <param name="model">The immutable template input.</param>
    /// <returns>The generated C# source.</returns>
    internal static string RenderNavigationComposition(object model)
    {
        return NavigationComposition.Value.Render(model);
    }

    private static Template Load(string name)
    {
        var assembly = typeof(TemplateRenderer).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourcePrefix + name)
                           ?? throw new InvalidOperationException($"Embedded template '{name}' was not found.");
        using var reader = new StreamReader(stream);
        var template = Template.Parse(reader.ReadToEnd(), name);
        if (!template.HasErrors) return template;

        var errors = string.Join(
            "\n",
            template.Messages.Select(static message => message.ToString()));
        throw new InvalidOperationException($"Scriban template '{name}' is invalid:\n{errors}");
    }
}