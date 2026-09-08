namespace Eloverblik.ThirdPartyApi.Client.Tests.Scenario;

/// <summary>
/// Stands in for real consumer domain code. It depends only on the generated endpoint
/// interfaces, which is what makes it unit-testable without any HTTP plumbing.
/// </summary>
public sealed class MeteringPointReportService(
    IGetThirdpartyapiApiTokenEndpoint tokenEndpoint,
    IGetThirdpartyapiApiAuthorizationAuthorizationMeteringpointsScopeIdentifierEndpoint meteringPointsEndpoint)
{
    public async Task<IReadOnlyList<string>> BuildReportAsync(
        string scope,
        string identifier,
        CancellationToken cancellationToken = default)
    {
        var tokenResult = await tokenEndpoint
            .ExecuteAsync(new GetThirdpartyapiApiTokenParameters(), cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        // Never assume success - the result wrapper forces the failure branch to be handled.
        if (!tokenResult.IsOk)
        {
            return [];
        }

        var meteringPointsResult = await meteringPointsEndpoint
            .ExecuteAsync(
                new GetThirdpartyapiApiAuthorizationAuthorizationMeteringpointsScopeIdentifierParameters(
                    Scope: scope,
                    Identifier: identifier),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (!meteringPointsResult.IsOk)
        {
            return [];
        }

        return (meteringPointsResult.OkContent.Result ?? [])
            .Select(x => $"{x.MeteringPointId}: {x.StreetName} {x.BuildingNumber}".TrimEnd())
            .ToList();
    }
}