namespace Showcase.Aspire;

/// <summary>
/// Probes the API's secured health endpoint using the configured api-key in a request header
/// instead of a query string, so the key is not written to Aspire dashboards, HTTP logs or traces.
/// </summary>
internal sealed class ApiKeyHealthCheck(EndpointReference endpoint, string apiKey, string headerName)
    : IHealthCheck
{
    private static readonly HttpClient HttpClient = new();

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{endpoint.Url}/health/live");
            request.Headers.Add(headerName, apiKey);

            using var response = await HttpClient
                .SendAsync(request, cancellationToken)
                .ConfigureAwait(false);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy($"Status {(int)response.StatusCode}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy(ex.Message);
        }
    }
}