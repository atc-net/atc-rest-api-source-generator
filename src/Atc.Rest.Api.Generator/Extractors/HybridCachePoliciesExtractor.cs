namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts HybridCache policies from OpenAPI document and generates policy name constants.
/// </summary>
public static class HybridCachePoliciesExtractor
{
    /// <summary>
    /// Extracts HybridCache policies from OpenAPI document.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="projectName">The project name for namespace.</param>
    /// <param name="includeDeprecated">Whether to include deprecated operations.</param>
    /// <returns>Generated code content for the HybridCache policies class, or null if no policies needed.</returns>
    public static string? Extract(
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated = false)
    {
        if (openApiDoc == null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        // Collect all unique HybridCache policies
        var policies = CollectPolicies(openApiDoc, includeDeprecated);

        if (policies.Count == 0)
        {
            return null;
        }

        // Generate the complete class content
        return GenerateFileContent(projectName, policies);
    }

    /// <summary>
    /// Collects all unique HybridCache policies from cache extensions across all operations.
    /// </summary>
    private static Dictionary<string, CacheConfiguration> CollectPolicies(
        OpenApiDocument openApiDoc,
        bool includeDeprecated)
    {
        var policies = new Dictionary<string, CacheConfiguration>(StringComparer.Ordinal);

        // Check document-level policy
        var documentPolicy = openApiDoc.Extensions.ExtractCachePolicy();
        var documentType = openApiDoc.Extensions.ExtractCacheType() ?? "output";
        if (!string.IsNullOrEmpty(documentPolicy) && ParseCacheType(documentType) == CacheType.Hybrid)
        {
            var config = CreateConfigFromExtensions(openApiDoc.Extensions, null, null);
            if (config != null && config.Type == CacheType.Hybrid)
            {
                policies[documentPolicy!] = config;
            }
        }

        if (openApiDoc.Paths == null || openApiDoc.Paths.Count == 0)
        {
            return policies;
        }

        foreach (var pathPair in openApiDoc.Paths)
        {
            if (pathPair.Value is not OpenApiPathItem pathItem)
            {
                continue;
            }

            // Check path-level policy
            var pathPolicy = pathItem.Extensions.ExtractCachePolicy();
            if (!string.IsNullOrEmpty(pathPolicy) && !policies.ContainsKey(pathPolicy!))
            {
                var config = CreateConfigFromExtensions(openApiDoc.Extensions, pathItem.Extensions, null);
                if (config != null && config.Type == CacheType.Hybrid)
                {
                    policies[pathPolicy!] = config;
                }
            }

            if (pathItem.Operations == null)
            {
                continue;
            }

            foreach (var operationPair in pathItem.Operations)
            {
                var operation = operationPair.Value;

                // Skip deprecated operations if not including them
                if (!includeDeprecated && operation?.Deprecated == true)
                {
                    continue;
                }

                if (operation == null)
                {
                    continue;
                }

                // Check operation-level policy
                var operationPolicy = operation.Extensions.ExtractCachePolicy();
                if (!string.IsNullOrEmpty(operationPolicy) && !policies.ContainsKey(operationPolicy!))
                {
                    var config = CreateConfigFromExtensions(
                        openApiDoc.Extensions,
                        pathItem.Extensions,
                        operation.Extensions);
                    if (config != null && config.Type == CacheType.Hybrid)
                    {
                        policies[operationPolicy!] = config;
                    }
                }
            }
        }

        return policies;
    }

    /// <summary>
    /// Creates a cache configuration from extension values at different levels.
    /// </summary>
    private static CacheConfiguration? CreateConfigFromExtensions(
        IDictionary<string, IOpenApiExtension>? documentExtensions,
        IDictionary<string, IOpenApiExtension>? pathExtensions,
        IDictionary<string, IOpenApiExtension>? operationExtensions)
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

        var cacheType = ParseCacheType(typeString);

        // Only process HybridCache type
        if (cacheType != CacheType.Hybrid)
        {
            return null;
        }

        var expirationSeconds = operationExtensions.ExtractCacheExpirationSeconds()
                                ?? pathExtensions.ExtractCacheExpirationSeconds()
                                ?? documentExtensions.ExtractCacheExpirationSeconds()
                                ?? 300;

        // Merge tags from all levels
        var tags = MergeTags(
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

        // HybridCache specific
        var modeString = operationExtensions.ExtractCacheMode()
                         ?? pathExtensions.ExtractCacheMode()
                         ?? documentExtensions.ExtractCacheMode()
                         ?? "hybrid";

        var cacheMode = ParseCacheMode(modeString);

        var slidingExpirationSeconds = operationExtensions.ExtractCacheSlidingExpirationSeconds()
                                       ?? pathExtensions.ExtractCacheSlidingExpirationSeconds()
                                       ?? documentExtensions.ExtractCacheSlidingExpirationSeconds();

        var keyPrefix = operationExtensions.ExtractCacheKeyPrefix()
                        ?? pathExtensions.ExtractCacheKeyPrefix()
                        ?? documentExtensions.ExtractCacheKeyPrefix();

        return new CacheConfiguration
        {
            Enabled = true,
            Type = CacheType.Hybrid,
            Policy = policy,
            ExpirationSeconds = expirationSeconds,
            Tags = tags,
            VaryByQuery = varyByQuery,
            VaryByHeader = varyByHeader,
            Mode = cacheMode,
            SlidingExpirationSeconds = slidingExpirationSeconds,
            KeyPrefix = keyPrefix,
        };
    }

    /// <summary>
    /// Merges tags from all levels.
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

        return typeString!.ToLowerInvariant() switch
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

        return modeString!.ToLowerInvariant() switch
        {
            "in-memory" or "inmemory" or "memory" => CacheMode.InMemory,
            "distributed" or "l2" => CacheMode.Distributed,
            _ => CacheMode.Hybrid,
        };
    }

    /// <summary>
    /// Generates the complete file content.
    /// </summary>
    private static string GenerateFileContent(
        string projectName,
        Dictionary<string, CacheConfiguration> policies)
    {
        var builder = new StringBuilder();

        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.AppendLine("using System.CodeDom.Compiler;");
        builder.AppendLine();
        builder.AppendLine($"namespace {projectName}.Generated.Caching;");
        builder.AppendLine();
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// HybridCache policy name constants generated from OpenAPI extensions.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        builder.AppendLine("public static class CachePolicies");
        builder.AppendLine("{");

        var sortedPolicies = policies
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .ToList();

        var isFirst = true;

        foreach (var policyKvp in sortedPolicies)
        {
            if (!isFirst)
            {
                builder.AppendLine();
            }

            isFirst = false;

            var policyName = policyKvp.Key;
            var config = policyKvp.Value;
            var constantName = GenerateConstantName(policyName);
            var description = GeneratePolicyDescription(config);

            builder.AppendLine(4, "/// <summary>");
            builder.AppendLine(4, $"/// {description}");
            builder.AppendLine(4, "/// </summary>");
            builder.AppendLine(4, $"public const string {constantName} = \"{policyName}\";");
        }

        builder.AppendLine("}");

        return builder.ToString();
    }

    /// <summary>
    /// Generates a description for the policy based on configuration.
    /// </summary>
    private static string GeneratePolicyDescription(CacheConfiguration config)
    {
        var parts = new List<string>
        {
            $"{config.ExpirationSeconds}s expiration",
            $"mode: {config.Mode}",
        };

        if (config.SlidingExpirationSeconds.HasValue)
        {
            parts.Add($"sliding: {config.SlidingExpirationSeconds.Value}s");
        }

        if (config.VaryByQuery.Count > 0)
        {
            parts.Add($"vary by query: [{string.Join(", ", config.VaryByQuery)}]");
        }

        if (config.VaryByHeader.Count > 0)
        {
            parts.Add($"vary by header: [{string.Join(", ", config.VaryByHeader)}]");
        }

        if (config.Tags.Count > 0)
        {
            parts.Add($"tags: [{string.Join(", ", config.Tags)}]");
        }

        if (!string.IsNullOrEmpty(config.KeyPrefix))
        {
            parts.Add($"key prefix: {config.KeyPrefix}");
        }

        return $"Policy: {string.Join(", ", parts)}";
    }

    /// <summary>
    /// Generates a valid C# constant name from a policy name.
    /// </summary>
    /// <param name="policyName">The policy name (e.g., "users", "user-detail").</param>
    /// <returns>A valid C# identifier (e.g., "Users", "UserDetail").</returns>
    public static string GenerateConstantName(string policyName)
    {
        // Split by '-', '_', and other separators
        var parts = policyName.Split(['-', '_', ':', ' '], StringSplitOptions.RemoveEmptyEntries);

        var result = new StringBuilder();

        foreach (var part in parts)
        {
            // Convert each part to PascalCase
            var pascalPart = part.ToPascalCaseForDotNet();
            result.Append(pascalPart);
        }

        return result.ToString();
    }
}