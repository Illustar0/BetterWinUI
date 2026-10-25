namespace BetterWinUI.PageActivation;

/// <summary>
/// Installs the application-owned factory that WinUI uses to activate Pages.
/// </summary>
/// <typeparam name="TPage">The WinUI Page base type returned by the factory.</typeparam>
/// <remarks>
/// The page activation generator implements this interface on the class marked with
/// <see cref="GeneratePageActivationHookAttribute" />. Other packages build activation
/// policies, such as dependency injection, on top of it.
/// </remarks>
public interface IPageActivationHook<in TPage>
    where TPage : class
{
    /// <summary>Installs the factory used for subsequent Page activations.</summary>
    /// <param name="activate">Receives the requested Page type and returns a compatible, non-null Page.</param>
    /// <remarks>Install once on the XAML thread before navigating. Existing cached Pages are unaffected.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="activate" /> is null.</exception>
    /// <exception cref="InvalidOperationException">A factory is already installed, or XAML metadata is not available yet.</exception>
    void UsePageActivation(Func<Type, TPage> activate);
}
