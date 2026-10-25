namespace BetterWinUI.PageActivation;

/// <summary>Marks the WinUI application class that receives a generated Page activation hook.</summary>
/// <remarks>
/// The page activation generator implements <see cref="IPageActivationHook{TPage}" /> on the attributed class. The
/// class must be a concrete, non-generic, top-level, partial subclass of <c>Microsoft.UI.Xaml.Application</c>, and
/// an assembly can contain only one such class.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class GeneratePageActivationHookAttribute : Attribute;
