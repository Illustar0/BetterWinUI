namespace BetterWinUI.Navigation;

/// <summary>Marks a partial class that receives a generated group of Page mappings.</summary>
/// <remarks>
/// The navigation generator implements <see cref="IPageModule" /> on the attributed class with the mappings whose
/// <see cref="PageForAttribute{TViewModel}.Module" /> names it. Load the group with
/// <see cref="PageMap.AddGeneratedPages{TModule}" />.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class PageModuleAttribute : Attribute;

/// <summary>Defines a group of Page mappings that can be loaded without assembly scanning.</summary>
public interface IPageModule
{
    /// <summary>Adds the module's mappings to a Page map.</summary>
    /// <param name="pages">An empty staging map supplied by <see cref="PageMap.AddGeneratedPages{TModule}" />.</param>
    static abstract void Register(PageMap pages);
}
