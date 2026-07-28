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

    /// <summary>
    /// Gets a value indicating whether a Retry-After header is emitted when a request is rejected.
    /// </summary>
    public bool EmitRetryAfter { get; init; } = true;

    /// <summary>
    /// Gets the rate limit partitioning strategy.
    /// </summary>
    public RateLimitPartitionStrategy Partition { get; init; } = RateLimitPartitionStrategy.Global;

    /// <summary>
    /// Gets the claim type used when <see cref="Partition"/> is <see cref="RateLimitPartitionStrategy.User"/>.
    /// Only meaningful when <see cref="Partition"/> is <see cref="RateLimitPartitionStrategy.User"/>.
    /// A null value means the default claim "sub" is used.
    /// </summary>
    public string? PartitionClaim { get; init; }
}