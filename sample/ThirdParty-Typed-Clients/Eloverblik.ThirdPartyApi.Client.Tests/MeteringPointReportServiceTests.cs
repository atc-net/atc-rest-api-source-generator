namespace Eloverblik.ThirdPartyApi.Client.Tests;

/// <summary>
/// Contract-level tests. The generated <see cref="IElOverblikThirdPartyApiClient"/>
/// interface lets consumer domain code be tested with a substitute - no HTTP,
/// no JSON, no message handler.
/// </summary>
public sealed class MeteringPointReportServiceTests
{
    [Fact]
    public async Task BuildReportAsync_ReturnsOneLinePerMeteringPoint()
    {
        // Arrange
        var client = Substitute.For<IElOverblikThirdPartyApiClient>();

        client
            .GetThirdpartyapiApiAuthorizationAuthorizationMeteringpointsScopeIdentifierAsync(
                Arg.Any<GetThirdpartyapiApiAuthorizationAuthorizationMeteringpointsScopeIdentifierParameters>(),
                Arg.Any<CancellationToken>())
            .Returns(new MeteringPointThirdPartyDtoListApiResponse(
            [
                ModelBuilder.MeteringPoint("571313", "Vejnavn", "1"),
                ModelBuilder.MeteringPoint("571314", "Andenvej", "2"),
            ]));

        var sut = new MeteringPointReportService(client);

        // Act
        var report = await sut.BuildReportAsync("CVR", "12345678", TestContext.Current.CancellationToken);

        // Assert
        report.Should().BeEquivalentTo(
        [
            "571313: Vejnavn 1",
            "571314: Andenvej 2",
        ]);
    }

    [Fact]
    public async Task BuildReportAsync_PassesScopeAndIdentifierThrough()
    {
        // Arrange
        var client = Substitute.For<IElOverblikThirdPartyApiClient>();

        client
            .GetThirdpartyapiApiAuthorizationAuthorizationMeteringpointsScopeIdentifierAsync(
                Arg.Any<GetThirdpartyapiApiAuthorizationAuthorizationMeteringpointsScopeIdentifierParameters>(),
                Arg.Any<CancellationToken>())
            .Returns(new MeteringPointThirdPartyDtoListApiResponse([]));

        var sut = new MeteringPointReportService(client);

        // Act
        await sut.BuildReportAsync("CVR", "12345678", TestContext.Current.CancellationToken);

        // Assert
        await client
            .Received(1)
            .GetThirdpartyapiApiAuthorizationAuthorizationMeteringpointsScopeIdentifierAsync(
                Arg.Is<GetThirdpartyapiApiAuthorizationAuthorizationMeteringpointsScopeIdentifierParameters>(
                    x => x.Scope == "CVR" && x.Identifier == "12345678"),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BuildReportAsync_HandlesNullResult()
    {
        // Arrange
        var client = Substitute.For<IElOverblikThirdPartyApiClient>();

        client
            .GetThirdpartyapiApiAuthorizationAuthorizationMeteringpointsScopeIdentifierAsync(
                Arg.Any<GetThirdpartyapiApiAuthorizationAuthorizationMeteringpointsScopeIdentifierParameters>(),
                Arg.Any<CancellationToken>())
            .Returns(new MeteringPointThirdPartyDtoListApiResponse(Result: null));

        var sut = new MeteringPointReportService(client);

        // Act
        var report = await sut.BuildReportAsync("CVR", "12345678", TestContext.Current.CancellationToken);

        // Assert
        report.Should().BeEmpty();
    }
}
