namespace Atc.OpenApi.Models;

/// <summary>
/// Represents the cache configuration extracted from OpenAPI extensions.
/// </summary>
public record CacheConfiguration
{
    /// <summary>
    /// Gets a value indicating whether caching is enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Gets the cache type (Output or Hybrid).
    /// </summary>
    public CacheType Type { get; init; } = CacheType.Output;

    /// <summary>
    /// Gets the name of the cache policy.
    /// </summary>
    public string? Policy { get; init; }

    /// <summary>
    /// Gets the absolute expiration time in seconds.
    /// </summary>
    public int ExpirationSeconds { get; init; } = 300;

    /// <summary>
    /// Gets the cache tags for invalidation.
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>
    /// Gets the query parameters to include in the cache key.
    /// </summary>
    public IReadOnlyList<string> VaryByQuery { get; init; } = [];

    /// <summary>
    /// Gets the headers to include in the cache key.
    /// </summary>
    public IReadOnlyList<string> VaryByHeader { get; init; } = [];

    // Output Caching specific

    /// <summary>
    /// Gets the route values to include in the cache key (Output Caching only).
    /// </summary>
    public IReadOnlyList<string> VaryByRoute { get; init; } = [];

    // HybridCache specific

    /// <summary>
    /// Gets the cache storage mode (HybridCache only).
    /// </summary>
    public CacheMode Mode { get; init; } = CacheMode.Hybrid;

    /// <summary>
    /// Gets the sliding expiration in seconds (HybridCache only).
    /// </summary>
    public int? SlidingExpirationSeconds { get; init; }

    /// <summary>
    /// Gets the custom cache key prefix (HybridCache only).
    /// </summary>
    public string? KeyPrefix { get; init; }
}
