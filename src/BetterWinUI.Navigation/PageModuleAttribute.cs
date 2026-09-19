namespace BetterWinUI.Navigation;

/// <summary>Marks a partial class that receives a generated group of Page mappings.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class PageModuleAttribute : Attribute;

/// <summary>Provides statically callable Page registrations without assembly scanning.</summary>
public interface IPageModule
{
    /// <summary>Adds this module's mappings to an isolated staging map.</summary>
    /// <param name="pages">The staging map supplied by the module loader.</param>
    static abstract void Register(PageMap pages);
}
