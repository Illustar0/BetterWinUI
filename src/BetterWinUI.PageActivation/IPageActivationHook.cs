namespace BetterWinUI.PageActivation;

/// <summary>Defines the point where an application installs the factory that WinUI uses to create Pages.</summary>
/// <typeparam name="TPage">The WinUI Page base type that the factory returns.</typeparam>
/// <remarks>
/// The page activation generator implements this interface on the class marked with
/// <see cref="GeneratePageActivationHookAttribute" />. Other packages, such as dependency injection, build
/// activation policies on top of it.
/// </remarks>
public interface IPageActivationHook<in TPage>
    where TPage : class
{
    /// <summary>Installs the factory that creates every Page activated by XAML from now on.</summary>
    /// <param name="activate">
    /// A function that receives the requested Page type and returns a non-null instance assignable to it.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="activate" /> is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException">
    /// A factory is already installed.
    /// -or-
    /// The application was compiled without WinUI XAML metadata.
    /// </exception>
    /// <remarks>
    /// Install the factory once, before the first navigation. Pages that a Frame has already cached are not
    /// recreated. Activating a Page throws <see cref="InvalidOperationException" /> if the factory returns
    /// <see langword="null" /> or an instance that is not assignable to the requested type; other exceptions from the
    /// factory propagate.
    /// </remarks>
    void UsePageActivation(Func<Type, TPage> activate);
}
