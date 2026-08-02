using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BetterWinUI.DependencyInjection.PageActivation.IntegrationTests;

/// <summary>
/// Verifies generated registration against the real Windows App SDK surface.
/// </summary>
public sealed class PageActivationIntegrationTests
{
    /// <summary>
    /// Verifies that generated registrations contain the attributed Page and ViewModel.
    /// </summary>
    [Fact]
    public void GeneratedRegistrationContainsAttributedTypes()
    {
        var services = new ServiceCollection();

        App.Configure(services);

        Assert.Contains(
            services,
            static descriptor => descriptor.ServiceType == typeof(MainPage));
        Assert.Contains(
            services,
            static descriptor => descriptor.ServiceType == typeof(MainViewModel));
    }
}