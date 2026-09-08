namespace Eloverblik.ThirdPartyApi.Client.Tests;

/// <summary>
/// Transport-level tests. These exercise the generated client end-to-end against a
/// fake <see cref="HttpMessageHandler"/>, proving URL construction, serialization
/// and error mapping. Use this seam when the thing under test IS the client.
/// </summary>
public sealed class ElOverblikThirdPartyApiClientTests
{
    [Fact]
    public async Task GetToken_ReturnsResult_AndCallsExpectedUrl()
    {
        // Arrange
        using var handler = new FakeHttpMessageHandler(
            () => HttpResponseMessageFactory.Json(new StringApiResponse("access-token")));

        var sut = new ElOverblikThirdPartyApiClient(handler.CreateClient());

        // Act
        var result = await sut.GetThirdpartyapiApiTokenAsync(
            new GetThirdpartyapiApiTokenParameters(),
            TestContext.Current.CancellationToken);

        // Assert
        result.Result.Should().Be("access-token");
        handler.Requests.Should().ContainSingle()
            .Which.RequestUri!.AbsolutePath.Should().Be("/thirdpartyapi/api/token");
    }

    [Fact]
    public async Task NonSuccessStatusCode_ThrowsHttpRequestException()
    {
        // Arrange - typed clients surface non-2xx as HttpRequestException rather
        // than a result wrapper, so that is the contract to assert on.
        using var handler = new FakeHttpMessageHandler(
            () => HttpResponseMessageFactory.StatusCode(HttpStatusCode.Unauthorized));

        var sut = new ElOverblikThirdPartyApiClient(handler.CreateClient());

        // Act
        var act = async () => await sut.GetThirdpartyapiApiTokenAsync(
            new GetThirdpartyapiApiTokenParameters(),
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<HttpRequestException>())
            .Which.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PathParameters_AreSubstitutedIntoTheUrl()
    {
        // Arrange
        using var handler = new FakeHttpMessageHandler(
            () => HttpResponseMessageFactory.Json(
                new MeteringPointThirdPartyDtoListApiResponse([])));

        var sut = new ElOverblikThirdPartyApiClient(handler.CreateClient());

        // Act
        await sut.GetThirdpartyapiApiAuthorizationAuthorizationMeteringpointsScopeIdentifierAsync(
            new GetThirdpartyapiApiAuthorizationAuthorizationMeteringpointsScopeIdentifierParameters(
                Scope: "CVR",
                Identifier: "12345678"),
            TestContext.Current.CancellationToken);

        // Assert
        handler.Requests.Should().ContainSingle()
            .Which.RequestUri!.AbsolutePath.Should()
            .Be("/thirdpartyapi/api/authorization/authorization/meteringpoints/CVR/12345678");
    }
}