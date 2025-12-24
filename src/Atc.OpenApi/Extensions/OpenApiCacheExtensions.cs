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
            => ExtractStringValue(extensions, CacheExtensionNameConstants.Type);

        /// <summary>
        /// Extracts the x-cache-policy value from extensions.
        /// </summary>
        /// <returns>The policy name, or null if not specified.</returns>
        public string? ExtractCachePolicy()
            => ExtractStringValue(extensions, CacheExtensionNameConstants.Policy);

        /// <summary>
        /// Extracts the x-cache-enabled value from extensions.
        /// </summary>
        /// <returns>True if enabled, false if explicitly disabled, null if not specified.</returns>
        public bool? ExtractCacheEnabled()
            => ExtractBoolValue(extensions, CacheExtensionNameConstants.Enabled);

        /// <summary>
        /// Extracts the x-cache-expiration-seconds value from extensions.
        /// </summary>
        /// <returns>The expiration in seconds, or null if not specified.</returns>
        public int? ExtractCacheExpirationSeconds()
            => ExtractIntValue(extensions, CacheExtensionNameConstants.ExpirationSeconds);

        /// <summary>
        /// Extracts the x-cache-tags value from extensions.
        /// </summary>
        /// <returns>The cache tags array, or null if not specified.</returns>
        public IReadOnlyList<string>? ExtractCacheTags()
            => ExtractStringArrayValue(extensions, CacheExtensionNameConstants.Tags);

        /// <summary>
        /// Extracts the x-cache-vary-by-query value from extensions.
        /// </summary>
        /// <returns>The query parameters array, or null if not specified.</returns>
        public IReadOnlyList<string>? ExtractCacheVaryByQuery()
            => ExtractStringArrayValue(extensions, CacheExtensionNameConstants.VaryByQuery);

        /// <summary>
        /// Extracts the x-cache-vary-by-header value from extensions.
        /// </summary>
        /// <returns>The headers array, or null if not specified.</returns>
        public IReadOnlyList<string>? ExtractCacheVaryByHeader()
            => ExtractStringArrayValue(extensions, CacheExtensionNameConstants.VaryByHeader);

        /// <summary>
        /// Extracts the x-cache-vary-by-route value from extensions (Output Caching only).
        /// </summary>
        /// <returns>The route values array, or null if not specified.</returns>
        public IReadOnlyList<string>? ExtractCacheVaryByRoute()
            => ExtractStringArrayValue(extensions, CacheExtensionNameConstants.VaryByRoute);

        /// <summary>
        /// Extracts the x-cache-mode value from extensions (HybridCache only).
        /// </summary>
        /// <returns>The cache mode string, or null if not specified.</returns>
        public string? ExtractCacheMode()
            => ExtractStringValue(extensions, CacheExtensionNameConstants.Mode);

        /// <summary>
        /// Extracts the x-cache-sliding-expiration-seconds value from extensions (HybridCache only).
        /// </summary>
        /// <returns>The sliding expiration in seconds, or null if not specified.</returns>
        public int? ExtractCacheSlidingExpirationSeconds()
            => ExtractIntValue(extensions, CacheExtensionNameConstants.SlidingExpirationSeconds);

        /// <summary>
        /// Extracts the x-cache-key-prefix value from extensions (HybridCache only).
        /// </summary>
        /// <returns>The key prefix, or null if not specified.</returns>
        public string? ExtractCacheKeyPrefix()
            => ExtractStringValue(extensions, CacheExtensionNameConstants.KeyPrefix);
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
        OpenApiPathItem pathItem,
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
        if (document.Paths == null)
        {
            return false;
        }

        foreach (var pathPair in document.Paths)
        {
            if (pathPair.Value is not OpenApiPathItem pathItem)
            {
                continue;
            }

            // Check path-level
            if (!string.IsNullOrEmpty(pathItem.Extensions.ExtractCachePolicy()))
            {
                return true;
            }

            // Check operation-level
            if (pathItem.Operations == null)
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
        if (document.Paths == null)
        {
            return false;
        }

        foreach (var pathPair in document.Paths)
        {
            if (pathPair.Value is not OpenApiPathItem pathItem)
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
            if (pathItem.Operations == null)
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

    /// <summary>
    /// Merges tags from all levels (document, path, operation).
    /// </summary>
    private static IReadOnlyList<string> MergeTags(
        IReadOnlyList<string>? documentTags,
        IReadOnlyList<string>? pathTags,
        IReadOnlyList<string>? operationTags)
    {
        var merged = new HashSet<string>(StringComparer.Ordinal);

        if (documentTags != null)
        {
            foreach (var tag in documentTags)
            {
                merged.Add(tag);
            }
        }

        if (pathTags != null)
        {
            foreach (var tag in pathTags)
            {
                merged.Add(tag);
            }
        }

        if (operationTags != null)
        {
            foreach (var tag in operationTags)
            {
                merged.Add(tag);
            }
        }

        return [.. merged.OrderBy(t => t, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Parses a cache type string to the enum value.
    /// </summary>
    private static CacheType ParseCacheType(string? typeString)
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
    /// Parses a cache mode string to the enum value.
    /// </summary>
    private static CacheMode ParseCacheMode(string? modeString)
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
    /// Extracts a string value from an OpenAPI extension.
    /// </summary>
    private static string? ExtractStringValue(
        IDictionary<string, IOpenApiExtension>? extensions,
        string key)
    {
        if (extensions is null ||
            !extensions.TryGetValue(key, out var extension) ||
            extension is null)
        {
            return null;
        }

        // Use reflection to access Node property (Microsoft.OpenApi v3.0.1 pattern)
        var extensionType = extension.GetType();
        var nodeProperty = extensionType.GetProperty("Node");
        if (nodeProperty == null)
        {
            return null;
        }

        var node = nodeProperty.GetValue(extension);
        if (node is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var stringValue))
        {
            return stringValue;
        }

        return null;
    }

    /// <summary>
    /// Extracts a boolean value from an OpenAPI extension.
    /// </summary>
    private static bool? ExtractBoolValue(
        IDictionary<string, IOpenApiExtension>? extensions,
        string key)
    {
        if (extensions is null ||
            !extensions.TryGetValue(key, out var extension) ||
            extension is null)
        {
            return null;
        }

        // Use reflection to access Node property (Microsoft.OpenApi v3.0.1 pattern)
        var extensionType = extension.GetType();
        var nodeProperty = extensionType.GetProperty("Node");
        if (nodeProperty == null)
        {
            return null;
        }

        var node = nodeProperty.GetValue(extension);
        if (node is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out var boolValue))
        {
            return boolValue;
        }

        return null;
    }

    /// <summary>
    /// Extracts an integer value from an OpenAPI extension.
    /// </summary>
    private static int? ExtractIntValue(
        IDictionary<string, IOpenApiExtension>? extensions,
        string key)
    {
        if (extensions is null ||
            !extensions.TryGetValue(key, out var extension) ||
            extension is null)
        {
            return null;
        }

        // Use reflection to access Node property (Microsoft.OpenApi v3.0.1 pattern)
        var extensionType = extension.GetType();
        var nodeProperty = extensionType.GetProperty("Node");
        if (nodeProperty == null)
        {
            return null;
        }

        var node = nodeProperty.GetValue(extension);
        if (node is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<int>(out var intValue))
            {
                return intValue;
            }

            // Also try long and convert to int
            if (jsonValue.TryGetValue<long>(out var longValue))
            {
                return (int)longValue;
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts a string array value from an OpenAPI extension.
    /// </summary>
    private static IReadOnlyList<string>? ExtractStringArrayValue(
        IDictionary<string, IOpenApiExtension>? extensions,
        string key)
    {
        if (extensions is null ||
            !extensions.TryGetValue(key, out var extension) ||
            extension is null)
        {
            return null;
        }

        // Use reflection to access Node property (Microsoft.OpenApi v3.0.1 pattern)
        var extensionType = extension.GetType();
        var nodeProperty = extensionType.GetProperty("Node");
        if (nodeProperty == null)
        {
            return null;
        }

        var node = nodeProperty.GetValue(extension);
        if (node is JsonArray jsonArray)
        {
            var result = new List<string>();
            foreach (var item in jsonArray)
            {
                if (item is JsonValue itemValue && itemValue.TryGetValue<string>(out var stringValue))
                {
                    result.Add(stringValue);
                }
            }

            return result.Count > 0 ? result : null;
        }

        return null;
    }
}
