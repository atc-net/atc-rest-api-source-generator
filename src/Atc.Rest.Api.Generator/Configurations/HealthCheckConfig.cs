namespace Atc.Rest.Api.Generator.Configurations;

/// <summary>
/// Configuration for auto-generated health check endpoints.
/// When enabled, generates /health, /health/live, and /health/ready endpoints
/// with optional API key security.
/// </summary>
public class HealthCheckConfig
{
    /// <summary>
    /// Whether to generate health check endpoints. Default: false.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Base path for health check endpoints. Default: "/health".
    /// </summary>
    public string Path { get; set; } = "/health";

    /// <summary>
    /// Whether to generate a liveness endpoint at {path}/live.
    /// Liveness checks indicate the application process is running.
    /// Default: true.
    /// </summary>
    public bool IncludeLiveness { get; set; } = true;

    /// <summary>
    /// Whether to generate a readiness endpoint at {path}/ready.
    /// Readiness checks indicate the application can handle requests (dependencies are available).
    /// Default: true.
    /// </summary>
    public bool IncludeReadiness { get; set; } = true;

    /// <summary>
    /// Security mode for health check endpoints.
    /// "none" (default) = no security, endpoints are open.
    /// "apiKey" = require API key via query parameter or header.
    /// The API key value is read from IConfiguration["HealthChecks:ApiKey"] at runtime.
    /// </summary>
    public string Security { get; set; } = "none";

    /// <summary>
    /// Name of the query parameter for API key authentication.
    /// Used when security is "apiKey". Default: "api-key".
    /// Example: GET /health/live?api-key=my-secret
    /// </summary>
    public string ApiKeyQueryParameterName { get; set; } = "api-key";

    /// <summary>
    /// Name of the HTTP header for API key authentication.
    /// Used when security is "apiKey". Default: "X-Health-Api-Key".
    /// Example: X-Health-Api-Key: my-secret
    /// </summary>
    public string ApiKeyHeaderName { get; set; } = "X-Health-Api-Key";
}