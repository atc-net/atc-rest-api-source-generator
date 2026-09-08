namespace Eloverblik.ThirdPartyApi.Client.Tests.TestDoubles;

/// <summary>
/// Builds concrete endpoint results for tests.
/// <para>
/// ⚠️ This helper only has to exist because <c>IXEndpoint.ExecuteAsync</c> returns the
/// <b>concrete</b> sealed <c>XEndpointResult</c> rather than the <c>IXEndpointResult</c>
/// interface. A test that mocks the endpoint therefore cannot hand back a substituted
/// result - it must construct a real one from an <see cref="EndpointResponse"/>, which
/// couples the test to Atc.Rest.Client internals.
/// </para>
/// <para>
/// See issues/client-testing.md §10.4 / Phase 2.1: once the generator emits static
/// factories (<c>XEndpointResult.Ok(...)</c>, <c>XEndpointResult.Unauthorized()</c>)
/// this whole file becomes unnecessary.
/// </para>
/// </summary>
public static class EndpointResultFactory
{
    public static GetThirdpartyapiApiTokenEndpointResult Token(
        StringApiResponse payload)
        => new(Response(HttpStatusCode.OK, payload));

    public static GetThirdpartyapiApiTokenEndpointResult TokenFailure(
        HttpStatusCode statusCode)
        => new(Response(statusCode, contentObject: null));

    public static GetThirdpartyapiApiAuthorizationAuthorizationMeteringpointsScopeIdentifierEndpointResult MeteringPoints(
        MeteringPointThirdPartyDtoListApiResponse payload)
        => new(Response(HttpStatusCode.OK, payload));

    private static EndpointResponse Response(
        HttpStatusCode statusCode,
        object? contentObject)
        => new(
            isSuccess: (int)statusCode is >= 200 and < 300,
            statusCode: statusCode,
            content: string.Empty,
            contentObject: contentObject,
            headers: new Dictionary<string, IEnumerable<string>>(StringComparer.Ordinal));
}