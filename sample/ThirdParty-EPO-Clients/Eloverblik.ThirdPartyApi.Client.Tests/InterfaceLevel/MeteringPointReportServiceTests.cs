namespace Eloverblik.ThirdPartyApi.Client.Tests.InterfaceLevel;

/// <summary>
/// Interface-level tests: consumer domain code is tested against mocked endpoint
/// interfaces, with no HTTP involved at all.
/// </summary>
public sealed class MeteringPointReportServiceTests
{
    [Fact]
    public async Task BuildReportAsync_SkipsMeteringPointCall_WhenTokenEndpointFails()
    {
        // Arrange
        var tokenEndpoint = Substitute.For<IGetThirdpartyapiApiTokenEndpoint>();
        var meteringPointsEndpoint = Substitute.For<IGetThirdpartyapiApiAuthorizationAuthorizationMeteringpointsScopeIdentifierEndpoint>();

        tokenEndpoint
            .ExecuteAsync(Arg.Any<GetThirdpartyapiApiTokenParameters>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(GetThirdpartyapiApiTokenEndpointResult.Unauthorized(
                ModelBuilder.ProblemDetails(HttpStatusCode.Unauthorized)));

        var sut = new MeteringPointReportService(tokenEndpoint, meteringPointsEndpoint);

        // Act
        var report = await sut.BuildReportAsync("customer", "12345678", TestContext.Current.CancellationToken);

        // Assert
        report.Should().BeEmpty();
        await meteringPointsEndpoint.ReceivedWithAnyArgs(0).ExecuteAsync(default!, default!, default);
    }

    [Fact]
    public async Task BuildReportAsync_ProjectsMeteringPoints_WhenBothCallsSucceed()
    {
        // Arrange
        var tokenEndpoint = Substitute.For<IGetThirdpartyapiApiTokenEndpoint>();
        var meteringPointsEndpoint = Substitute.For<IGetThirdpartyapiApiAuthorizationAuthorizationMeteringpointsScopeIdentifierEndpoint>();

        tokenEndpoint
            .ExecuteAsync(Arg.Any<GetThirdpartyapiApiTokenParameters>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(GetThirdpartyapiApiTokenEndpointResult.Ok(new StringApiResponse("access-token")));

        meteringPointsEndpoint
            .ExecuteAsync(Arg.Any<GetThirdpartyapiApiAuthorizationAuthorizationMeteringpointsScopeIdentifierParameters>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(GetThirdpartyapiApiAuthorizationAuthorizationMeteringpointsScopeIdentifierEndpointResult.Ok(
                new MeteringPointThirdPartyDtoListApiResponse(
                [
                    ModelBuilder.MeteringPoint("571313000000000001", "Hovedgaden", "1"),
                    ModelBuilder.MeteringPoint("571313000000000002", "Bygaden", "2"),
                ])));

        var sut = new MeteringPointReportService(tokenEndpoint, meteringPointsEndpoint);

        // Act
        var report = await sut.BuildReportAsync("customer", "12345678", TestContext.Current.CancellationToken);

        // Assert
        report.Should().Equal(
            "571313000000000001: Hovedgaden 1",
            "571313000000000002: Bygaden 2");
    }

    [Fact]
    public async Task BuildReportAsync_PassesScopeAndIdentifier_ToTheMeteringPointEndpoint()
    {
        // Arrange
        var tokenEndpoint = Substitute.For<IGetThirdpartyapiApiTokenEndpoint>();
        var meteringPointsEndpoint = Substitute.For<IGetThirdpartyapiApiAuthorizationAuthorizationMeteringpointsScopeIdentifierEndpoint>();

        tokenEndpoint
            .ExecuteAsync(Arg.Any<GetThirdpartyapiApiTokenParameters>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(GetThirdpartyapiApiTokenEndpointResult.Ok(new StringApiResponse("access-token")));

        meteringPointsEndpoint
            .ExecuteAsync(Arg.Any<GetThirdpartyapiApiAuthorizationAuthorizationMeteringpointsScopeIdentifierParameters>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(GetThirdpartyapiApiAuthorizationAuthorizationMeteringpointsScopeIdentifierEndpointResult.Ok(new MeteringPointThirdPartyDtoListApiResponse([])));

        var sut = new MeteringPointReportService(tokenEndpoint, meteringPointsEndpoint);

        // Act
        await sut.BuildReportAsync("customer", "12345678", TestContext.Current.CancellationToken);

        // Assert
        await meteringPointsEndpoint
            .Received(1)
            .ExecuteAsync(
                Arg.Is<GetThirdpartyapiApiAuthorizationAuthorizationMeteringpointsScopeIdentifierParameters>(
                    x => x.Scope == "customer" && x.Identifier == "12345678"),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }
}
