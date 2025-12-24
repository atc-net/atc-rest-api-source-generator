namespace Atc.OpenApi;

/// <summary>
/// Constant names for custom OpenAPI rate limiting extension tags.
/// </summary>
/// <remarks>
/// These extensions are specific to the ATC generator and allow configuration of
/// ASP.NET Core rate limiting at the root, path, and operation levels:
/// - x-ratelimit-policy: Named policy to apply
/// - x-ratelimit-enabled: Enables/disables rate limiting
/// - x-ratelimit-permit-limit: Maximum requests per window
/// - x-ratelimit-window-seconds: Time window in seconds
/// - x-ratelimit-queue-limit: Maximum queued requests
/// - x-ratelimit-algorithm: Rate limiting algorithm (fixed, sliding, token-bucket, concurrency)
/// </remarks>
public static class RateLimitExtensionNameConstants
{
    /// <summary>
    /// Extension tag for specifying the rate limit policy name.
    /// Type: string
    /// Example: x-ratelimit-policy: "global"
    /// </summary>
    public const string Policy = "x-ratelimit-policy";

    /// <summary>
    /// Extension tag for enabling or disabling rate limiting.
    /// Type: boolean
    /// Example: x-ratelimit-enabled: false
    /// </summary>
    public const string Enabled = "x-ratelimit-enabled";

    /// <summary>
    /// Extension tag for specifying the maximum number of requests per window.
    /// Type: integer
    /// Example: x-ratelimit-permit-limit: 100
    /// </summary>
    public const string PermitLimit = "x-ratelimit-permit-limit";

    /// <summary>
    /// Extension tag for specifying the time window in seconds.
    /// Type: integer
    /// Example: x-ratelimit-window-seconds: 60
    /// </summary>
    public const string WindowSeconds = "x-ratelimit-window-seconds";

    /// <summary>
    /// Extension tag for specifying the maximum queued requests.
    /// Type: integer
    /// Example: x-ratelimit-queue-limit: 10
    /// </summary>
    public const string QueueLimit = "x-ratelimit-queue-limit";

    /// <summary>
    /// Extension tag for specifying the rate limiting algorithm.
    /// Type: string (fixed, sliding, token-bucket, concurrency)
    /// Example: x-ratelimit-algorithm: "sliding"
    /// </summary>
    public const string Algorithm = "x-ratelimit-algorithm";
}
