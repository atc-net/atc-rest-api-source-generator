namespace Atc.OpenApi.Models;

/// <summary>
/// Specifies the cache storage mode for HybridCache.
/// </summary>
public enum CacheMode
{
    /// <summary>
    /// In-Memory (L1) cache only.
    /// Process-local cache, fastest but not shared across instances.
    /// Best for single-server scenarios or rarely changing data.
    /// </summary>
    InMemory,

    /// <summary>
    /// Distributed (L2) cache only.
    /// Shared cache via Redis, SQL Server, etc.
    /// Best for multi-server scenarios requiring consistency.
    /// </summary>
    Distributed,

    /// <summary>
    /// Hybrid (L1+L2) cache.
    /// Combines in-memory speed with distributed consistency.
    /// Default mode providing best of both worlds.
    /// </summary>
    Hybrid,
}
