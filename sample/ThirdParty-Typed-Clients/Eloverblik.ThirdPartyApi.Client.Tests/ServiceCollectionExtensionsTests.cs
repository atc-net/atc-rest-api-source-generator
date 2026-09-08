namespace Eloverblik.ThirdPartyApi.Client.Tests;

/// <summary>
/// Verifies the generated DI extension. It is emitted only because this project
/// references Microsoft.Extensions.Http, and it registers the client through the
/// generated interface so consumer code can depend on the contract.
/// </summary>
public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddClient_ResolvesThroughTheGeneratedInterface()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddElOverblikThirdPartyApiClient(client =>
            client.BaseAddress = new Uri("https://api.eloverblik.dk"));

        using var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IElOverblikThirdPartyApiClient>();

        // Assert
        sut.Should().BeOfType<ElOverblikThirdPartyApiClient>();
    }

    [Fact]
    public void AddClient_WithoutConfigureCallback_StillResolves()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddElOverblikThirdPartyApiClient();

        using var provider = services.BuildServiceProvider();

        // Assert
        provider.GetRequiredService<IElOverblikThirdPartyApiClient>().Should().NotBeNull();
    }

    [Fact]
    public void AddClient_ReturnsBuilder_SoHandlersCanBeChained()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var builder = services.AddElOverblikThirdPartyApiClient();

        // Assert
        builder.Should().NotBeNull();
    }
}
