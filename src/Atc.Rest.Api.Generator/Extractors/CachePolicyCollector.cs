namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Collects the named cache policies of a document - declared with <c>x-cache-policy</c> at the document, path or
/// operation level - as their configurations, for one cache type. The output cache and HybridCache extractors share
/// it: the levels, the precedence (operation over path over document) and the tag merging are the same for both;
/// only the type they keep differs.
/// </summary>
internal static class CachePolicyCollector
{
    /// <summary>
    /// Collects every policy of <paramref name="cacheType"/>, keyed by policy name, in document order (the first
    /// declaration of a name wins).
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="includeDeprecated">Whether a deprecated operation's policy counts.</param>
    /// <param name="cacheType">The cache type to keep.</param>
    /// <returns>The policies by name.</returns>
    public static Dictionary<string, CacheConfiguration> CollectPolicies(
        OpenApiDocument openApiDoc,
        bool includeDeprecated,
        CacheType cacheType)
    {
        var policies = new Dictionary<string, CacheConfiguration>(StringComparer.Ordinal);

        // Check document-level policy
        var documentPolicy = openApiDoc.Extensions.ExtractCachePolicy();
        var documentType = openApiDoc.Extensions.ExtractCacheType() ?? "output";
        if (!string.IsNullOrEmpty(documentPolicy) && OpenApiCacheExtensions.ParseCacheType(documentType) == cacheType)
        {
            var config = CreateConfigFromExtensions(openApiDoc.Extensions, null, null, cacheType);
            if (config is not null)
            {
                policies[documentPolicy!] = config;
            }
        }

        if (openApiDoc.Paths is null || openApiDoc.Paths.Count == 0)
        {
            return policies;
        }

        foreach (var pathPair in openApiDoc.Paths)
        {
            if (pathPair.Value is not IOpenApiPathItem pathItem)
            {
                continue;
            }

            // Check path-level policy
            var pathPolicy = pathItem.Extensions.ExtractCachePolicy();
            if (!string.IsNullOrEmpty(pathPolicy) && !policies.ContainsKey(pathPolicy!))
            {
                var config = CreateConfigFromExtensions(openApiDoc.Extensions, pathItem.Extensions, null, cacheType);
                if (config is not null)
                {
                    policies[pathPolicy!] = config;
                }
            }

            if (pathItem.Operations is null)
            {
                continue;
            }

            foreach (var operationPair in pathItem.Operations)
            {
                var operation = operationPair.Value;

                // Skip deprecated operations if not including them
                if (operation is null || (!includeDeprecated && operation.Deprecated))
                {
                    continue;
                }

                // Check operation-level policy
                var operationPolicy = operation.Extensions.ExtractCachePolicy();
                if (!string.IsNullOrEmpty(operationPolicy) && !policies.ContainsKey(operationPolicy!))
                {
                    var config = CreateConfigFromExtensions(openApiDoc.Extensions, pathItem.Extensions, operation.Extensions, cacheType);
                    if (config is not null)
                    {
                        policies[operationPolicy!] = config;
                    }
                }
            }
        }

        return policies;
    }

    /// <summary>
    /// The configuration the extensions of the three levels resolve to (operation over path over document; tags
    /// merged), or <see langword="null"/> when there is no policy or it is of another cache type.
    /// </summary>
    private static CacheConfiguration? CreateConfigFromExtensions(
        IDictionary<string, IOpenApiExtension>? documentExtensions,
        IDictionary<string, IOpenApiExtension>? pathExtensions,
        IDictionary<string, IOpenApiExtension>? operationExtensions,
        CacheType cacheType)
    {
        var policy = operationExtensions.ExtractCachePolicy()
                     ?? pathExtensions.ExtractCachePolicy()
                     ?? documentExtensions.ExtractCachePolicy();

        if (string.IsNullOrEmpty(policy))
        {
            return null;
        }

        var typeString = operationExtensions.ExtractCacheType()
                         ?? pathExtensions.ExtractCacheType()
                         ?? documentExtensions.ExtractCacheType()
                         ?? "output";

        if (OpenApiCacheExtensions.ParseCacheType(typeString) != cacheType)
        {
            return null;
        }

        var expirationSeconds = operationExtensions.ExtractCacheExpirationSeconds()
                                ?? pathExtensions.ExtractCacheExpirationSeconds()
                                ?? documentExtensions.ExtractCacheExpirationSeconds()
                                ?? 300;

        var tags = OpenApiCacheExtensions.MergeTags(
            documentExtensions.ExtractCacheTags(),
            pathExtensions.ExtractCacheTags(),
            operationExtensions.ExtractCacheTags());

        var varyByQuery = operationExtensions.ExtractCacheVaryByQuery()
                          ?? pathExtensions.ExtractCacheVaryByQuery()
                          ?? documentExtensions.ExtractCacheVaryByQuery()
                          ?? [];

        var varyByHeader = operationExtensions.ExtractCacheVaryByHeader()
                           ?? pathExtensions.ExtractCacheVaryByHeader()
                           ?? documentExtensions.ExtractCacheVaryByHeader()
                           ?? [];

        // Output cache: vary-by-route
        var varyByRoute = operationExtensions.ExtractCacheVaryByRoute()
                          ?? pathExtensions.ExtractCacheVaryByRoute()
                          ?? documentExtensions.ExtractCacheVaryByRoute()
                          ?? [];

        // HybridCache: mode, sliding expiration, key prefix
        var modeString = operationExtensions.ExtractCacheMode()
                         ?? pathExtensions.ExtractCacheMode()
                         ?? documentExtensions.ExtractCacheMode()
                         ?? "hybrid";

        var slidingExpirationSeconds = operationExtensions.ExtractCacheSlidingExpirationSeconds()
                                       ?? pathExtensions.ExtractCacheSlidingExpirationSeconds()
                                       ?? documentExtensions.ExtractCacheSlidingExpirationSeconds();

        var keyPrefix = operationExtensions.ExtractCacheKeyPrefix()
                        ?? pathExtensions.ExtractCacheKeyPrefix()
                        ?? documentExtensions.ExtractCacheKeyPrefix();

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
            Mode = OpenApiCacheExtensions.ParseCacheMode(modeString),
            SlidingExpirationSeconds = slidingExpirationSeconds,
            KeyPrefix = keyPrefix,
        };
    }
}