namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts Output Cache configuration and generates DI extension method for output cache policies.
/// </summary>
public static class OutputCacheDependencyInjectionExtractor
{
    /// <summary>
    /// Extracts Output Cache DI registration from OpenAPI document.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="projectName">The project name for namespace.</param>
    /// <param name="includeDeprecated">Whether to include deprecated operations.</param>
    /// <returns>Generated code content for the Output Cache DI extension class, or null if no policies needed.</returns>
    public static string? Extract(
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated = false)
    {
        if (openApiDoc == null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        // Collect all unique Output Cache policies
        var policies = CollectPolicies(openApiDoc, includeDeprecated);

        if (policies.Count == 0)
        {
            return null;
        }

        // Generate the complete file content
        return GenerateFileContent(projectName, policies);
    }

    /// <summary>
    /// Collects all unique Output Cache policies from cache extensions across all operations.
    /// </summary>
    private static Dictionary<string, CacheConfiguration> CollectPolicies(
        OpenApiDocument openApiDoc,
        bool includeDeprecated)
    {
        var policies = new Dictionary<string, CacheConfiguration>(StringComparer.Ordinal);

        // Check document-level policy
        var documentPolicy = openApiDoc.Extensions.ExtractCachePolicy();
        var documentType = openApiDoc.Extensions.ExtractCacheType() ?? "output";
        if (!string.IsNullOrEmpty(documentPolicy) && ParseCacheType(documentType) == CacheType.Output)
        {
            var config = CreateConfigFromExtensions(openApiDoc.Extensions, null, null);
            if (config != null && config.Type == CacheType.Output)
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
                if (config != null && config.Type == CacheType.Output)
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
                    if (config != null && config.Type == CacheType.Output)
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

        // Only process Output Cache type
        if (cacheType != CacheType.Output)
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

        var varyByRoute = operationExtensions.ExtractCacheVaryByRoute()
                          ?? pathExtensions.ExtractCacheVaryByRoute()
                          ?? documentExtensions.ExtractCacheVaryByRoute()
                          ?? [];

        return new CacheConfiguration
        {
            Enabled = true,
            Type = CacheType.Output,
            Policy = policy,
            ExpirationSeconds = expirationSeconds,
            Tags = tags,
            VaryByQuery = varyByQuery,
            VaryByHeader = varyByHeader,
            VaryByRoute = varyByRoute,
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
    /// Generates the complete file content.
    /// </summary>
    private static string GenerateFileContent(
        string projectName,
        Dictionary<string, CacheConfiguration> policies)
    {
        // Generate content first to analyze for required usings
        var contentBuilder = new StringBuilder();
        contentBuilder.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        contentBuilder.AppendLine("public static class OutputCachingServiceCollectionExtensions");
        contentBuilder.AppendLine("{");
        contentBuilder.AppendLine(4, "public static IServiceCollection AddApiOutputCaching(this IServiceCollection services)");
        contentBuilder.AppendLine(8, "services.AddOutputCache(options =>");
        contentBuilder.AppendLine(12, "options.AddPolicy(OutputCachePolicies.Default, policy =>");
        contentBuilder.AppendLine(16, "policy.Expire(TimeSpan.FromSeconds(60))");
        contentBuilder.AppendLine(16, "policy.SetVaryByQuery(\"page\")");
        contentBuilder.AppendLine(16, "policy.SetVaryByHeader(\"Accept\")");
        contentBuilder.AppendLine(16, "policy.Tag(\"products\")");
        contentBuilder.AppendLine(12, "WebApplication app;");
        contentBuilder.AppendLine("}");
        var content = contentBuilder.ToString();

        // Build header with only required usings
        var builder = new StringBuilder();
        builder.Append(UsingStatementHelper.BuildHeader(content, "System.CodeDom.Compiler"));
        builder.AppendLine($"using {projectName}.Generated.Caching;");
        builder.AppendLine();
        builder.AppendLine($"namespace {projectName}.Generated.Caching;");
        builder.AppendLine();
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// Extension methods for configuring API output caching policies.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        builder.AppendLine("public static class OutputCachingServiceCollectionExtensions");
        builder.AppendLine("{");
        builder.AppendLine(4, "/// <summary>");
        builder.AppendLine(4, "/// Registers output cache policies for the API based on OpenAPI extensions.");
        builder.AppendLine(4, "/// </summary>");
        builder.AppendLine(4, "/// <param name=\"services\">The service collection.</param>");
        builder.AppendLine(4, "/// <returns>The service collection for method chaining.</returns>");
        builder.AppendLine(4, "public static IServiceCollection AddApiOutputCaching(this IServiceCollection services)");
        builder.AppendLine(4, "{");

        // Generate method content
        var methodContent = GenerateMethodContent(policies);
        builder.Append(methodContent);

        builder.AppendLine(4, "}");
        builder.AppendLine("}");

        return builder.ToString();
    }

    /// <summary>
    /// Generates the method content for AddApiOutputCaching.
    /// </summary>
    private static string GenerateMethodContent(
        Dictionary<string, CacheConfiguration> policies)
    {
        var builder = new StringBuilder();

        builder.AppendLine(8, "services.AddOutputCache(options =>");
        builder.AppendLine(8, "{");

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
            var constantName = OutputCachePoliciesExtractor.GenerateConstantName(policyName);

            GeneratePolicyConfiguration(builder, constantName, config);
        }

        builder.AppendLine(8, "});");
        builder.AppendLine();
        builder.AppendLine(8, "return services;");

        return builder.ToString();
    }

    /// <summary>
    /// Generates the policy configuration for a specific policy.
    /// </summary>
    private static void GeneratePolicyConfiguration(
        StringBuilder builder,
        string constantName,
        CacheConfiguration config)
    {
        builder.AppendLine(12, $"options.AddPolicy(OutputCachePolicies.{constantName}, policy =>");
        builder.AppendLine(12, "{");
        builder.AppendLine(16, $"policy.Expire(TimeSpan.FromSeconds({config.ExpirationSeconds}));");

        // Add VaryByQuery
        if (config.VaryByQuery.Count > 0)
        {
            var queryParams = string.Join(", ", config.VaryByQuery.Select(q => $"\"{q}\""));
            builder.AppendLine(16, $"policy.SetVaryByQuery({queryParams});");
        }

        // Add VaryByHeader
        if (config.VaryByHeader.Count > 0)
        {
            var headers = string.Join(", ", config.VaryByHeader.Select(h => $"\"{h}\""));
            builder.AppendLine(16, $"policy.SetVaryByHeader({headers});");
        }

        // Add VaryByRouteValue
        foreach (var routeValue in config.VaryByRoute)
        {
            builder.AppendLine(16, $"policy.SetVaryByRouteValue(\"{routeValue}\");");
        }

        // Add Tags
        if (config.Tags.Count > 0)
        {
            var tags = string.Join(", ", config.Tags.Select(t => $"\"{t}\""));
            builder.AppendLine(16, $"policy.Tag({tags});");
        }

        builder.AppendLine(12, "});");
    }
}