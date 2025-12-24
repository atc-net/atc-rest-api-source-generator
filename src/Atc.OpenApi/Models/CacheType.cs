namespace Atc.OpenApi.Models;

/// <summary>
/// Specifies the caching approach to use.
/// </summary>
public enum CacheType
{
    /// <summary>
    /// Output Caching - endpoint-level automatic response caching.
    /// Uses ASP.NET Core's built-in output caching with .CacheOutput().
    /// Best for simple GET endpoints with automatic cache management.
    /// </summary>
    Output,

    /// <summary>
    /// HybridCache - handler-level explicit caching.
    /// Uses Microsoft.Extensions.Caching.Hybrid for fine-grained control.
    /// Best for complex scenarios requiring programmatic cache management.
    /// </summary>
    Hybrid,
}
