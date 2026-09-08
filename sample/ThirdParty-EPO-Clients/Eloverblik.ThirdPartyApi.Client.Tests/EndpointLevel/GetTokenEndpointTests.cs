namespace Eloverblik.ThirdPartyApi.Client.Tests.EndpointLevel;

/// <summary>
/// Endpoint-level tests: the REAL generated endpoint is exercised over a fake transport.
/// This verifies routing, serialization and status-to-result mapping in one go.
/// </summary>
public sealed class GetTokenEndpointTests
{
    [Fact]
    public async Task ExecuteAsync_MapsOk_AndDeserializesPayload()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(
            () => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new StringApiResponse("access-token")),
            });

        var sut = CreateEndpoint(handler);

        // Act
        var result = await sut.ExecuteAsync(
            new GetThirdpartyapiApiTokenParameters(),
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.IsOk.Should().BeTrue();
        result.OkContent.Result.Should().Be("access-token");
        handler.Requests.Should().ContainSingle();
        handler.Requests[0].RequestUri!.AbsolutePath.Should().Be("/thirdpartyapi/api/token");
    }

    [Fact]
    public async Task ExecuteAsync_MapsUnauthorized_WithoutThrowing()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(
            () => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var sut = CreateEndpoint(handler);

        // Act
        var result = await sut.ExecuteAsync(
            new GetThirdpartyapiApiTokenParameters(),
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert - EndpointPerOperation surfaces failures as data, not exceptions.
        result.IsOk.Should().BeFalse();
        result.IsUnauthorized.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ExecuteAsync_RequestsTheGeneratedHttpClientName()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(
            () => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new StringApiResponse("access-token")),
            });

        var factory = new StubHttpClientFactory(handler.CreateClient());
        var sut = new GetThirdpartyapiApiTokenEndpoint(factory, CreateHttpMessageFactory());

        // Act
        await sut.ExecuteAsync(
            new GetThirdpartyapiApiTokenParameters(),
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert - the default argument comes from the generated Constants class.
        factory.RequestedNames.Should().ContainSingle().Which.Should().Be(Constants.HttpClientName);
    }

    private static GetThirdpartyapiApiTokenEndpoint CreateEndpoint(
        FakeHttpMessageHandler handler)
        => new(
            new StubHttpClientFactory(handler.CreateClient()),
            CreateHttpMessageFactory());

    private static IHttpMessageFactory CreateHttpMessageFactory()
    {
        var services = new ServiceCollection();
        services.AddEloverblikApiThirdPartyApiEndpoints();
        return services.BuildServiceProvider().GetRequiredService<IHttpMessageFactory>();
    }
}
