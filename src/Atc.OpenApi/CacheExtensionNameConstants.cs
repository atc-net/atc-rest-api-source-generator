namespace Atc.OpenApi;

/// <summary>
/// Constant names for custom OpenAPI caching extension tags.
/// </summary>
/// <remarks>
/// These extensions are specific to the ATC generator and allow configuration of
/// server-side caching at the root, path, and operation levels. Supports both:
/// - Output Caching: Endpoint-level automatic response caching
/// - HybridCache: Handler-level caching with L1/L2 support
/// </remarks>
public static class CacheExtensionNameConstants
{
    // Core extensions (both types)

    /// <summary>
    /// Extension tag for specifying the cache type: 'output' (endpoint) or 'hybrid' (handler).
    /// Type: string
    /// Default: "output"
    /// Example: x-cache-type: "hybrid"
    /// </summary>
    public const string Type = "x-cache-type";

    /// <summary>
    /// Extension tag for specifying the cache policy name.
    /// Type: string
    /// Example: x-cache-policy: "products"
    /// </summary>
    public const string Policy = "x-cache-policy";

    /// <summary>
    /// Extension tag for enabling or disabling caching.
    /// Type: boolean
    /// Example: x-cache-enabled: false
    /// </summary>
    public const string Enabled = "x-cache-enabled";

    /// <summary>
    /// Extension tag for specifying the cache expiration time in seconds.
    /// Type: integer
    /// Default: 300 (5 minutes)
    /// Example: x-cache-expiration-seconds: 600
    /// </summary>
    public const string ExpirationSeconds = "x-cache-expiration-seconds";

    /// <summary>
    /// Extension tag for specifying cache tags for invalidation.
    /// Type: array of strings
    /// Example: x-cache-tags: ["products", "catalog"]
    /// </summary>
    public const string Tags = "x-cache-tags";

    /// <summary>
    /// Extension tag for specifying query parameters to include in cache key.
    /// Type: array of strings
    /// Example: x-cache-vary-by-query: ["page", "limit"]
    /// </summary>
    public const string VaryByQuery = "x-cache-vary-by-query";

    /// <summary>
    /// Extension tag for specifying headers to include in cache key.
    /// Type: array of strings
    /// Example: x-cache-vary-by-header: ["Accept", "Accept-Language"]
    /// </summary>
    public const string VaryByHeader = "x-cache-vary-by-header";

    // Output Caching specific extensions

    /// <summary>
    /// Extension tag for specifying route values to include in cache key (Output Caching only).
    /// Type: array of strings
    /// Example: x-cache-vary-by-route: ["id", "category"]
    /// </summary>
    public const string VaryByRoute = "x-cache-vary-by-route";

    // HybridCache specific extensions

    /// <summary>
    /// Extension tag for specifying the cache storage mode (HybridCache only).
    /// Type: string (in-memory, distributed, hybrid)
    /// Default: "hybrid"
    /// Example: x-cache-mode: "distributed"
    /// </summary>
    public const string Mode = "x-cache-mode";

    /// <summary>
    /// Extension tag for specifying sliding expiration in seconds (HybridCache only).
    /// Type: integer
    /// Example: x-cache-sliding-expiration-seconds: 60
    /// </summary>
    public const string SlidingExpirationSeconds = "x-cache-sliding-expiration-seconds";

    /// <summary>
    /// Extension tag for specifying a custom cache key prefix (HybridCache only).
    /// Type: string
    /// Example: x-cache-key-prefix: "myapi"
    /// </summary>
    public const string KeyPrefix = "x-cache-key-prefix";
}
