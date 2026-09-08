namespace Eloverblik.ThirdPartyApi.Client.Tests;

/// <summary>
/// Stands in for real consumer domain code. It depends on the generated
/// <see cref="IElOverblikThirdPartyApiClient"/> interface rather than the concrete
/// client, which is what makes it unit-testable without any HTTP plumbing.
/// </summary>
public sealed class MeteringPointReportService(
    IElOverblikThirdPartyApiClient client)
{
    public async Task<IReadOnlyList<string>> BuildReportAsync(
        string scope,
        string identifier,
        CancellationToken cancellationToken = default)
    {
        var response = await client
            .GetThirdpartyapiApiAuthorizationAuthorizationMeteringpointsScopeIdentifierAsync(
                new GetThirdpartyapiApiAuthorizationAuthorizationMeteringpointsScopeIdentifierParameters(
                    Scope: scope,
                    Identifier: identifier),
                cancellationToken)
            .ConfigureAwait(false);

        return (response.Result ?? [])
            .Select(x => $"{x.MeteringPointId}: {x.StreetName} {x.BuildingNumber}".TrimEnd())
            .ToList();
    }
}