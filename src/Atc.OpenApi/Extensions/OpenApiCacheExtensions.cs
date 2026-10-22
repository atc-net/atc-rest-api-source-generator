namespace Atc.OpenApi.Extensions;

/// <summary>
/// Extension methods for extracting cache configuration from OpenAPI extensions.
/// Supports ATC-specific extensions (x-cache-*) for configuring server-side caching.
/// </summary>
[SuppressMessage("", "CA1034:Do not nest type", Justification = "OK - CLang14 - extension")]
[SuppressMessage("", "S3398:Move this method inside", Justification = "OK - CLang14 - extension")]
public static class OpenApiCacheExtensions
{
    /// <param name="extensions">The OpenAPI extensions dictionary.</param>
    extension(IDictionary<string, IOpenApiExtension>? extensions)
    {
        /// <summary>
        /// Extracts the x-cache-type value from extensions.
        /// </summary>
        /// <returns>The cache type string, or null if not specified.</returns>
        public string? ExtractCacheType()
            => extensions.ReadString(CacheExtensionNameConstants.Type);

        /// <summary>
        /// Extracts the x-cache-policy value from extensions.
        /// </summary>
        /// <returns>The policy name, or null if not specified.</returns>
        public string? ExtractCachePolicy()
            => extensions.ReadString(CacheExtensionNameConstants.Policy);

        /// <summary>
        /// Extracts the x-cache-enabled value from extensions.
        /// </summary>
        /// <returns>True if enabled, false if explicitly disabled, null if not specified.</returns>
        public bool? ExtractCacheEnabled()
            => extensions.ReadBool(CacheExtensionNameConstants.Enabled);

        /// <summary>
        /// Extracts the x-cache-expiration-seconds value from extensions.
        /// </summary>
        /// <returns>The expiration in seconds, or null if not specified.</returns>
        public int? ExtractCacheExpirationSeconds()
            => extensions.ReadInt(CacheExtensionNameConstants.ExpirationSeconds);

        /// <summary>
        /// Extracts the x-cache-tags value from extensions.
        /// </summary>
        /// <returns>The cache tags array, or null if not specified.</returns>
        public IReadOnlyList<string>? ExtractCacheTags()
            => extensions.ReadStringArray(CacheExtensionNameConstants.Tags);

        /// <summary>
        /// Extracts the x-cache-vary-by-query value from extensions.
        /// </summary>
        /// <returns>The query parameters array, or null if not specified.</returns>
        public IReadOnlyList<string>? ExtractCacheVaryByQuery()
            => extensions.ReadStringArray(CacheExtensionNameConstants.VaryByQuery);

        /// <summary>
        /// Extracts the x-cache-vary-by-header value from extensions.
        /// </summary>
        /// <returns>The headers array, or null if not specified.</returns>
        public IReadOnlyList<string>? ExtractCacheVaryByHeader()
            => extensions.ReadStringArray(CacheExtensionNameConstants.VaryByHeader);

        /// <summary>
        /// Extracts the x-cache-vary-by-route value from extensions (Output Caching only).
        /// </summary>
        /// <returns>The route values array, or null if not specified.</returns>
        public IReadOnlyList<string>? ExtractCacheVaryByRoute()
            => extensions.ReadStringArray(CacheExtensionNameConstants.VaryByRoute);

        /// <summary>
        /// Extracts the x-cache-mode value from extensions (HybridCache only).
        /// </summary>
        /// <returns>The cache mode string, or null if not specified.</returns>
        public string? ExtractCacheMode()
            => extensions.ReadString(CacheExtensionNameConstants.Mode);

        /// <summary>
        /// Extracts the x-cache-sliding-expiration-seconds value from extensions (HybridCache only).
        /// </summary>
        /// <returns>The sliding expiration in seconds, or null if not specified.</returns>
        public int? ExtractCacheSlidingExpirationSeconds()
            => extensions.ReadInt(CacheExtensionNameConstants.SlidingExpirationSeconds);

        /// <summary>
        /// Extracts the x-cache-key-prefix value from extensions (HybridCache only).
        /// </summary>
        /// <returns>The key prefix, or null if not specified.</returns>
        public string? ExtractCacheKeyPrefix()
            => extensions.ReadString(CacheExtensionNameConstants.KeyPrefix);

        /// <summary>
        /// Extracts the x-cache-shared value from extensions (Output Caching only).
        /// </summary>
        /// <returns>True when the answer is the same for every caller, false if not, null if not specified.</returns>
        public bool? ExtractCacheShared()
            => extensions.ReadBool(CacheExtensionNameConstants.Shared);
    }

    /// <summary>
    /// Extracts the complete cache configuration for an operation.
    /// Applies hierarchical inheritance: operation overrides path, path overrides document.
    /// Tags are merged from all levels (not overridden).
    /// </summary>
    /// <param name="operation">The OpenAPI operation.</param>
    /// <param name="pathItem">The path item containing the operation.</param>
    /// <param name="document">The OpenAPI document (for root-level settings).</param>
    /// <returns>A CacheConfiguration, or null if no caching is configured.</returns>
    public static CacheConfiguration? ExtractCacheConfiguration(
        this OpenApiOperation operation,
        IOpenApiPathItem pathItem,
        OpenApiDocument document)
    {
        // Check for operation-level explicit disable
        var operationEnabled = operation.Extensions.ExtractCacheEnabled();
        if (operationEnabled == false)
        {
            return new CacheConfiguration { Enabled = false };
        }

        // Get effective policy (operation → path → document)
        var policy = operation.Extensions.ExtractCachePolicy()
                     ?? pathItem.Extensions.ExtractCachePolicy()
                     ?? document.Extensions.ExtractCachePolicy();

        if (string.IsNullOrEmpty(policy))
        {
            return null; // No caching configured
        }

        // Get cache type (operation → path → document), default to Output
        var cacheTypeString = operation.Extensions.ExtractCacheType()
                              ?? pathItem.Extensions.ExtractCacheType()
                              ?? document.Extensions.ExtractCacheType()
                              ?? "output";

        var cacheType = ParseCacheType(cacheTypeString);

        // Get expiration (operation → path → document)
        var expirationSeconds = operation.Extensions.ExtractCacheExpirationSeconds()
                                ?? pathItem.Extensions.ExtractCacheExpirationSeconds()
                                ?? document.Extensions.ExtractCacheExpirationSeconds()
                                ?? 300;

        // Merge tags from all levels (not override)
        var tags = MergeTags(
            document.Extensions.ExtractCacheTags(),
            pathItem.Extensions.ExtractCacheTags(),
            operation.Extensions.ExtractCacheTags());

        // Get vary-by-query (operation → path → document)
        var varyByQuery = operation.Extensions.ExtractCacheVaryByQuery()
                          ?? pathItem.Extensions.ExtractCacheVaryByQuery()
                          ?? document.Extensions.ExtractCacheVaryByQuery()
                          ?? [];

        // Get vary-by-header (operation → path → document)
        var varyByHeader = operation.Extensions.ExtractCacheVaryByHeader()
                           ?? pathItem.Extensions.ExtractCacheVaryByHeader()
                           ?? document.Extensions.ExtractCacheVaryByHeader()
                           ?? [];

        // Output Caching specific: vary-by-route
        var varyByRoute = operation.Extensions.ExtractCacheVaryByRoute()
                          ?? pathItem.Extensions.ExtractCacheVaryByRoute()
                          ?? document.Extensions.ExtractCacheVaryByRoute()
                          ?? [];

        // HybridCache specific
        var cacheModeString = operation.Extensions.ExtractCacheMode()
                              ?? pathItem.Extensions.ExtractCacheMode()
                              ?? document.Extensions.ExtractCacheMode()
                              ?? "hybrid";

        var cacheMode = ParseCacheMode(cacheModeString);

        var slidingExpirationSeconds = operation.Extensions.ExtractCacheSlidingExpirationSeconds()
                                       ?? pathItem.Extensions.ExtractCacheSlidingExpirationSeconds()
                                       ?? document.Extensions.ExtractCacheSlidingExpirationSeconds();

        var keyPrefix = operation.Extensions.ExtractCacheKeyPrefix()
                        ?? pathItem.Extensions.ExtractCacheKeyPrefix()
                        ?? document.Extensions.ExtractCacheKeyPrefix();

        // Output Caching specific: the same answer for every caller
        var shared = operation.Extensions.ExtractCacheShared()
                     ?? pathItem.Extensions.ExtractCacheShared()
                     ?? document.Extensions.ExtractCacheShared()
                     ?? false;

        return new CacheConfiguration
        {
            Enabled = true,
            Type = cacheType,
            Policy = policy,
            ExpirationSeconds = expirationSeconds,
            Tags = tags,
            VaryByQuery = varyByQuery,
            VaryByHeader = varyByHeader,
            VaryByRoute = varyByRoute,
            Mode = cacheMode,
            SlidingExpirationSeconds = slidingExpirationSeconds,
            KeyPrefix = keyPrefix,
            Shared = shared,
        };
    }

    /// <summary>
    /// Checks if the document has any caching configuration.
    /// </summary>
    /// <param name="document">The OpenAPI document.</param>
    /// <returns>True if caching is configured at any level.</returns>
    public static bool HasCaching(this OpenApiDocument document)
    {
        // Check document-level
        if (!string.IsNullOrEmpty(document.Extensions.ExtractCachePolicy()))
        {
            return true;
        }

        // Check path and operation levels
        if (document.Paths is null)
        {
            return false;
        }

        foreach (var pathPair in document.Paths)
        {
            if (pathPair.Value is not IOpenApiPathItem pathItem)
            {
                continue;
            }

            // Check path-level
            if (!string.IsNullOrEmpty(pathItem.Extensions.ExtractCachePolicy()))
            {
                return true;
            }

            // Check operation-level
            if (pathItem.Operations is null)
            {
                continue;
            }

            foreach (var operationPair in pathItem.Operations)
            {
                if (!string.IsNullOrEmpty(operationPair.Value?.Extensions.ExtractCachePolicy()))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if the document has any Output Caching configuration.
    /// </summary>
    /// <param name="document">The OpenAPI document.</param>
    /// <returns>True if Output Caching is configured at any level.</returns>
    public static bool HasOutputCaching(this OpenApiDocument document)
        => HasCachingOfType(document, CacheType.Output);

    /// <summary>
    /// Checks if the document has any HybridCache configuration.
    /// </summary>
    /// <param name="document">The OpenAPI document.</param>
    /// <returns>True if HybridCache is configured at any level.</returns>
    public static bool HasHybridCaching(this OpenApiDocument document)
        => HasCachingOfType(document, CacheType.Hybrid);

    /// <summary>
    /// Merges the <c>x-cache-tags</c> of the document, path and operation levels: tags are additive across
    /// levels (not overridden), de-duplicated and returned in ordinal order.
    /// </summary>
    /// <param name="documentTags">The document-level tags, or null.</param>
    /// <param name="pathTags">The path-level tags, or null.</param>
    /// <param name="operationTags">The operation-level tags, or null.</param>
    /// <returns>The distinct tags of all levels, sorted; empty when none are declared.</returns>
    public static IReadOnlyList<string> MergeTags(
        IReadOnlyList<string>? documentTags,
        IReadOnlyList<string>? pathTags,
        IReadOnlyList<string>? operationTags)
    {
        var merged = new HashSet<string>(StringComparer.Ordinal);

        if (documentTags is not null)
        {
            foreach (var tag in documentTags)
            {
                merged.Add(tag);
            }
        }

        if (pathTags is not null)
        {
            foreach (var tag in pathTags)
            {
                merged.Add(tag);
            }
        }

        if (operationTags is not null)
        {
            foreach (var tag in operationTags)
            {
                merged.Add(tag);
            }
        }

        return [.. merged.OrderBy(t => t, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Parses an <c>x-cache-type</c> value: <c>hybrid</c> / <c>hybridcache</c> (case-insensitive) is
    /// <see cref="CacheType.Hybrid"/>; anything else, empty or null is <see cref="CacheType.Output"/>.
    /// </summary>
    /// <param name="typeString">The declared cache type, or null.</param>
    /// <returns>The cache type.</returns>
    public static CacheType ParseCacheType(string? typeString)
    {
        if (string.IsNullOrEmpty(typeString))
        {
            return CacheType.Output;
        }

        return typeString.ToLowerInvariant() switch
        {
            "hybrid" or "hybridcache" => CacheType.Hybrid,
            _ => CacheType.Output,
        };
    }

    /// <summary>
    /// Parses an <c>x-cache-mode</c> value (HybridCache only): <c>in-memory</c> / <c>inmemory</c> / <c>memory</c> is
    /// <see cref="CacheMode.InMemory"/>, <c>distributed</c> / <c>l2</c> is <see cref="CacheMode.Distributed"/>;
    /// anything else, empty or null is <see cref="CacheMode.Hybrid"/>. Case-insensitive.
    /// </summary>
    /// <param name="modeString">The declared cache mode, or null.</param>
    /// <returns>The cache mode.</returns>
    public static CacheMode ParseCacheMode(string? modeString)
    {
        if (string.IsNullOrEmpty(modeString))
        {
            return CacheMode.Hybrid;
        }

        return modeString.ToLowerInvariant() switch
        {
            "in-memory" or "inmemory" or "memory" => CacheMode.InMemory,
            "distributed" or "l2" => CacheMode.Distributed,
            _ => CacheMode.Hybrid,
        };
    }

    /// <summary>
    /// Checks if the document has caching of a specific type.
    /// </summary>
    private static bool HasCachingOfType(
        OpenApiDocument document,
        CacheType targetType)
    {
        // Check document-level
        var docPolicy = document.Extensions.ExtractCachePolicy();
        if (!string.IsNullOrEmpty(docPolicy))
        {
            var docTypeString = document.Extensions.ExtractCacheType() ?? "output";
            if (ParseCacheType(docTypeString) == targetType)
            {
                return true;
            }
        }

        // Check path and operation levels
        if (document.Paths is null)
        {
            return false;
        }

        foreach (var pathPair in document.Paths)
        {
            if (pathPair.Value is not IOpenApiPathItem pathItem)
            {
                continue;
            }

            // Check path-level
            var pathPolicy = pathItem.Extensions.ExtractCachePolicy();
            if (!string.IsNullOrEmpty(pathPolicy))
            {
                var pathTypeString = pathItem.Extensions.ExtractCacheType()
                                     ?? document.Extensions.ExtractCacheType()
                                     ?? "output";
                if (ParseCacheType(pathTypeString) == targetType)
                {
                    return true;
                }
            }

            // Check operation-level
            if (pathItem.Operations is null)
            {
                continue;
            }

            foreach (var operationPair in pathItem.Operations)
            {
                var operation = operationPair.Value;
                var opPolicy = operation?.Extensions.ExtractCachePolicy();
                if (!string.IsNullOrEmpty(opPolicy))
                {
                    var opTypeString = operation?.Extensions.ExtractCacheType()
                                       ?? pathItem.Extensions.ExtractCacheType()
                                       ?? document.Extensions.ExtractCacheType()
                                       ?? "output";
                    if (ParseCacheType(opTypeString) == targetType)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
}