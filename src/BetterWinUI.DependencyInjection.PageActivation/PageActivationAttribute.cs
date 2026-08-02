namespace BetterWinUI.DependencyInjection.PageActivation;

/// <summary>
/// Marks the WinUI application class that receives generated page activation support.
/// </summary>
/// <remarks>
/// The attributed class must be a concrete, top-level, partial subclass of
/// <c>Microsoft.UI.Xaml.Application</c>.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class PageActivationAttribute : Attribute;