namespace Eloverblik.ThirdPartyApi.Client.Tests.DependencyInjection;

/// <summary>
/// Guards the generated <c>AddEloverblikApiThirdPartyApiEndpoints()</c> extension:
/// every endpoint interface must be resolvable from the container.
/// </summary>
public sealed class ServiceRegistrationTests
{
    public static TheoryData<Type> EndpointInterfaces
        => new(
            typeof(IGetThirdpartyapiApiIsaliveEndpoint),
            typeof(IGetThirdpartyapiApiTokenEndpoint),
            typeof(IGetThirdpartyapiApiAuthorizationAuthorizationMeteringpointsScopeIdentifierEndpoint),
            typeof(IPostThirdpartyapiApiMeteringpointGetdetailsEndpoint));

    [Theory]
    [MemberData(nameof(EndpointInterfaces))]
    public void AddEndpoints_RegistersEveryEndpointInterface(
        Type endpointInterface)
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddEloverblikApiThirdPartyApiEndpoints();
        services.AddHttpClient(Constants.HttpClientName);

        // Act
        using var provider = services.BuildServiceProvider(validateScopes: true);

        // Assert
        provider.GetService(endpointInterface).Should().NotBeNull();
    }

    [Fact]
    public void AddEndpoints_RegistersAtcRestClientCore()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddEloverblikApiThirdPartyApiEndpoints();

        // Act
        using var provider = services.BuildServiceProvider(validateScopes: true);

        // Assert
        provider.GetService<IHttpMessageFactory>().Should().NotBeNull();
        provider.GetService<IContractSerializer>().Should().NotBeNull();
    }

    [Fact]
    public void Constants_ExposesTheConfiguredHttpClientName()
        => Constants.HttpClientName.Should().Be("Eloverblik-ApiClient");
}
