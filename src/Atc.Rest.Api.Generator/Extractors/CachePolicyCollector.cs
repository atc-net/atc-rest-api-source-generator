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
        => OpenApiCacheExtensions.ResolveCache(new OpenApiExtensionScope(operationExtensions, pathExtensions, documentExtensions)) is { } configuration &&
           configuration.Type == cacheType
            ? configuration
            : null;
}