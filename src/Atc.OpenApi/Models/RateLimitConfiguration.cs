namespace Atc.OpenApi.Models;

/// <summary>
/// Represents the rate limit configuration extracted from OpenAPI extensions.
/// </summary>
public record RateLimitConfiguration
{
    /// <summary>
    /// Gets a value indicating whether rate limiting is enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Gets the name of the rate limit policy.
    /// </summary>
    public string? Policy { get; init; }

    /// <summary>
    /// Gets the maximum number of requests allowed per window.
    /// </summary>
    public int PermitLimit { get; init; } = 100;

    /// <summary>
    /// Gets the time window in seconds.
    /// </summary>
    public int WindowSeconds { get; init; } = 60;

    /// <summary>
    /// Gets the maximum number of queued requests.
    /// </summary>
    public int QueueLimit { get; init; }

    /// <summary>
    /// Gets the rate limiting algorithm.
    /// </summary>
    public RateLimitAlgorithm Algorithm { get; init; } = RateLimitAlgorithm.Fixed;
}
