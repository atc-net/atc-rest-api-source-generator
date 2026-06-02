namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts HybridCache configuration and generates DI extension method for cache entry options.
/// </summary>
public static class HybridCacheDependencyInjectionExtractor
{
    /// <summary>
    /// Extracts HybridCache DI registration from OpenAPI document.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="projectName">The project name for namespace.</param>
    /// <param name="includeDeprecated">Whether to include deprecated operations.</param>
    /// <returns>Generated code content for the HybridCache DI extension class, or null if no policies needed.</returns>
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
        var policies = HybridCachePoliciesExtractor.CollectPolicies(openApiDoc, includeDeprecated);

        if (policies.Count == 0)
        {
            return null;
        }

        // Generate the complete file content
        return GenerateFileContent(projectName, policies);
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
        contentBuilder.AppendLine("public static class HybridCachingServiceCollectionExtensions");
        contentBuilder.AppendLine("{");
        contentBuilder.AppendLine(4, "public static IServiceCollection AddApiCaching(this IServiceCollection services)");
        contentBuilder.AppendLine(8, "services.AddHybridCache(options =>");
        contentBuilder.AppendLine(8, "HybridCacheEntryOptions options;");
        contentBuilder.AppendLine(8, "TimeSpan expiration;");
        contentBuilder.AppendLine("}");
        var content = contentBuilder.ToString();

        // Build header with only required usings
        var builder = new StringBuilder();
        builder.Append(UsingStatementHelper.BuildHeader(
            content,
            NamespaceConstants.SystemCodeDomCompiler,
            "Microsoft.Extensions.Caching.Hybrid",
            $"{projectName}.Generated.Caching"));
        builder.AppendLine($"namespace {projectName}.Generated.Caching;");
        builder.AppendLine();
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// Extension methods for configuring API HybridCache policies.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        builder.AppendLine("public static class HybridCachingServiceCollectionExtensions");
        builder.AppendLine("{");
        builder.AppendLine(4, "/// <summary>");
        builder.AppendLine(4, "/// Registers HybridCache with configured entry options for the API based on OpenAPI extensions.");
        builder.AppendLine(4, "/// </summary>");
        builder.AppendLine(4, "/// <param name=\"services\">The service collection.</param>");
        builder.AppendLine(4, "/// <param name=\"configureOptions\">Optional action to configure HybridCache options.</param>");
        builder.AppendLine(4, "/// <returns>The service collection for method chaining.</returns>");
        builder.AppendLine(4, "public static IServiceCollection AddApiCaching(");
        builder.AppendLine(8, "this IServiceCollection services,");
        builder.AppendLine(8, "Action<HybridCacheOptions>? configureOptions = null)");
        builder.AppendLine(4, "{");

        // Generate method content
        var methodContent = GenerateMethodContent(policies);
        builder.Append(methodContent);

        builder.AppendLine(4, "}");
        builder.AppendLine();

        // Generate GetCacheEntryOptions method
        builder.AppendLine(4, "/// <summary>");
        builder.AppendLine(4, "/// Gets the HybridCacheEntryOptions for a specific cache policy.");
        builder.AppendLine(4, "/// </summary>");
        builder.AppendLine(4, "/// <param name=\"policyName\">The cache policy name.</param>");
        builder.AppendLine(4, "/// <returns>The configured HybridCacheEntryOptions, or default options if policy not found.</returns>");
        builder.AppendLine(4, "public static HybridCacheEntryOptions GetCacheEntryOptions(string policyName)");
        builder.AppendLine(8, "=> policyName switch");
        builder.AppendLine(8, "{");

        var sortedPolicies = policies
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .ToList();

        foreach (var policyKvp in sortedPolicies)
        {
            var policyName = policyKvp.Key;
            var config = policyKvp.Value;
            var constantName = HybridCachePoliciesExtractor.GenerateConstantName(policyName);

            builder.AppendLine(12, $"CachePolicies.{constantName} => new HybridCacheEntryOptions");
            builder.AppendLine(12, "{");
            builder.AppendLine(16, $"Expiration = TimeSpan.FromSeconds({config.ExpirationSeconds}),");

            if (config.SlidingExpirationSeconds.HasValue)
            {
                builder.AppendLine(16, $"LocalCacheExpiration = TimeSpan.FromSeconds({config.SlidingExpirationSeconds.Value}),");
            }

            // Set flags based on mode
            switch (config.Mode)
            {
                case CacheMode.InMemory:
                    builder.AppendLine(16, "Flags = HybridCacheEntryFlags.DisableDistributedCache,");
                    break;
                case CacheMode.Distributed:
                    builder.AppendLine(16, "Flags = HybridCacheEntryFlags.DisableLocalCache,");
                    break;

                    // For Hybrid mode, don't set any flags (use both caches)
            }

            builder.AppendLine(12, "},");
        }

        builder.AppendLine(12, "_ => new HybridCacheEntryOptions");
        builder.AppendLine(12, "{");
        builder.AppendLine(16, "Expiration = TimeSpan.FromSeconds(300),");
        builder.AppendLine(12, "},");
        builder.AppendLine(8, "};");

        // Generate GetCacheTags method
        builder.AppendLine();
        builder.AppendLine(4, "/// <summary>");
        builder.AppendLine(4, "/// Gets the cache tags for a specific cache policy.");
        builder.AppendLine(4, "/// </summary>");
        builder.AppendLine(4, "/// <param name=\"policyName\">The cache policy name.</param>");
        builder.AppendLine(4, "/// <returns>The cache tags for the policy, or an empty array if policy not found.</returns>");
        builder.AppendLine(4, "public static string[] GetCacheTags(string policyName)");
        builder.AppendLine(8, "=> policyName switch");
        builder.AppendLine(8, "{");

        foreach (var policyKvp in sortedPolicies)
        {
            var policyName = policyKvp.Key;
            var config = policyKvp.Value;
            var constantName = HybridCachePoliciesExtractor.GenerateConstantName(policyName);

            if (config.Tags.Count > 0)
            {
                var tagsString = string.Join(", ", config.Tags.Select(t => $"\"{t}\""));
                builder.AppendLine(12, $"CachePolicies.{constantName} => [{tagsString}],");
            }
            else
            {
                builder.AppendLine(12, $"CachePolicies.{constantName} => [],");
            }
        }

        builder.AppendLine(12, "_ => [],");
        builder.AppendLine(8, "};");

        // Generate GenerateCacheKey method
        builder.AppendLine();
        builder.AppendLine(4, "/// <summary>");
        builder.AppendLine(4, "/// Generates a cache key based on policy configuration and request parameters.");
        builder.AppendLine(4, "/// </summary>");
        builder.AppendLine(4, "/// <param name=\"policyName\">The cache policy name.</param>");
        builder.AppendLine(4, "/// <param name=\"baseKey\">The base cache key (e.g., operation name).</param>");
        builder.AppendLine(4, "/// <param name=\"queryParams\">Optional query parameters to include in the key.</param>");
        builder.AppendLine(4, "/// <param name=\"headers\">Optional headers to include in the key.</param>");
        builder.AppendLine(4, "/// <returns>The generated cache key.</returns>");
        builder.AppendLine(4, "public static string GenerateCacheKey(");
        builder.AppendLine(8, "string policyName,");
        builder.AppendLine(8, "string baseKey,");
        builder.AppendLine(8, "IDictionary<string, string>? queryParams = null,");
        builder.AppendLine(8, "IDictionary<string, string>? headers = null)");
        builder.AppendLine(4, "{");
        builder.AppendLine(8, "var keyBuilder = new System.Text.StringBuilder();");
        builder.AppendLine();
        builder.AppendLine(8, "// Add key prefix if configured");
        builder.AppendLine(8, "var prefix = GetKeyPrefix(policyName);");
        builder.AppendLine(8, "if (!string.IsNullOrEmpty(prefix))");
        builder.AppendLine(8, "{");
        builder.AppendLine(12, "keyBuilder.Append(prefix);");
        builder.AppendLine(12, "keyBuilder.Append(':');");
        builder.AppendLine(8, "}");
        builder.AppendLine();
        builder.AppendLine(8, "keyBuilder.Append(baseKey);");
        builder.AppendLine();
        builder.AppendLine(8, "// Add vary-by-query parameters");
        builder.AppendLine(8, "var varyByQuery = GetVaryByQuery(policyName);");
        builder.AppendLine(8, "if (varyByQuery.Length > 0 && queryParams != null)");
        builder.AppendLine(8, "{");
        builder.AppendLine(12, "foreach (var param in varyByQuery.OrderBy(p => p, StringComparer.Ordinal))");
        builder.AppendLine(12, "{");
        builder.AppendLine(16, "if (queryParams.TryGetValue(param, out var value))");
        builder.AppendLine(16, "{");
        builder.AppendLine(20, "keyBuilder.Append(':');");
        builder.AppendLine(20, "keyBuilder.Append(param);");
        builder.AppendLine(20, "keyBuilder.Append('=');");
        builder.AppendLine(20, "keyBuilder.Append(value);");
        builder.AppendLine(16, "}");
        builder.AppendLine(12, "}");
        builder.AppendLine(8, "}");
        builder.AppendLine();
        builder.AppendLine(8, "// Add vary-by-header values");
        builder.AppendLine(8, "var varyByHeader = GetVaryByHeader(policyName);");
        builder.AppendLine(8, "if (varyByHeader.Length > 0 && headers != null)");
        builder.AppendLine(8, "{");
        builder.AppendLine(12, "foreach (var header in varyByHeader.OrderBy(h => h, StringComparer.Ordinal))");
        builder.AppendLine(12, "{");
        builder.AppendLine(16, "if (headers.TryGetValue(header, out var value))");
        builder.AppendLine(16, "{");
        builder.AppendLine(20, "keyBuilder.Append(':');");
        builder.AppendLine(20, "keyBuilder.Append(header);");
        builder.AppendLine(20, "keyBuilder.Append('=');");
        builder.AppendLine(20, "keyBuilder.Append(value);");
        builder.AppendLine(16, "}");
        builder.AppendLine(12, "}");
        builder.AppendLine(8, "}");
        builder.AppendLine();
        builder.AppendLine(8, "return keyBuilder.ToString();");
        builder.AppendLine(4, "}");

        // Generate helper methods for vary-by configuration
        builder.AppendLine();
        builder.AppendLine(4, "private static string[] GetVaryByQuery(string policyName)");
        builder.AppendLine(8, "=> policyName switch");
        builder.AppendLine(8, "{");

        foreach (var policyKvp in sortedPolicies)
        {
            var policyName = policyKvp.Key;
            var config = policyKvp.Value;
            var constantName = HybridCachePoliciesExtractor.GenerateConstantName(policyName);

            if (config.VaryByQuery.Count > 0)
            {
                var paramsString = string.Join(", ", config.VaryByQuery.Select(p => $"\"{p}\""));
                builder.AppendLine(12, $"CachePolicies.{constantName} => [{paramsString}],");
            }
            else
            {
                builder.AppendLine(12, $"CachePolicies.{constantName} => [],");
            }
        }

        builder.AppendLine(12, "_ => [],");
        builder.AppendLine(8, "};");

        builder.AppendLine();
        builder.AppendLine(4, "private static string[] GetVaryByHeader(string policyName)");
        builder.AppendLine(8, "=> policyName switch");
        builder.AppendLine(8, "{");

        foreach (var policyKvp in sortedPolicies)
        {
            var policyName = policyKvp.Key;
            var config = policyKvp.Value;
            var constantName = HybridCachePoliciesExtractor.GenerateConstantName(policyName);

            if (config.VaryByHeader.Count > 0)
            {
                var headersString = string.Join(", ", config.VaryByHeader.Select(h => $"\"{h}\""));
                builder.AppendLine(12, $"CachePolicies.{constantName} => [{headersString}],");
            }
            else
            {
                builder.AppendLine(12, $"CachePolicies.{constantName} => [],");
            }
        }

        builder.AppendLine(12, "_ => [],");
        builder.AppendLine(8, "};");

        builder.AppendLine();
        builder.AppendLine(4, "private static string? GetKeyPrefix(string policyName)");
        builder.AppendLine(8, "=> policyName switch");
        builder.AppendLine(8, "{");

        foreach (var policyKvp in sortedPolicies)
        {
            var policyName = policyKvp.Key;
            var config = policyKvp.Value;
            var constantName = HybridCachePoliciesExtractor.GenerateConstantName(policyName);

            if (!string.IsNullOrEmpty(config.KeyPrefix))
            {
                builder.AppendLine(12, $"CachePolicies.{constantName} => \"{config.KeyPrefix}\",");
            }
            else
            {
                builder.AppendLine(12, $"CachePolicies.{constantName} => null,");
            }
        }

        builder.AppendLine(12, "_ => null,");
        builder.AppendLine(8, "};");

        builder.AppendLine("}");

        return builder.ToString();
    }

    /// <summary>
    /// Generates the method content for AddApiCaching.
    /// </summary>
    private static string GenerateMethodContent(
        Dictionary<string, CacheConfiguration> policies)
    {
        var builder = new StringBuilder();

        builder.AppendLine(8, "services.AddHybridCache(options =>");
        builder.AppendLine(8, "{");
        builder.AppendLine(12, "// Configure default options");
        builder.AppendLine(12, "options.DefaultEntryOptions = new HybridCacheEntryOptions");
        builder.AppendLine(12, "{");
        builder.AppendLine(16, "Expiration = TimeSpan.FromSeconds(300),");
        builder.AppendLine(12, "};");
        builder.AppendLine();
        builder.AppendLine(12, "// Apply custom configuration if provided");
        builder.AppendLine(12, "configureOptions?.Invoke(options);");
        builder.AppendLine(8, "});");
        builder.AppendLine();
        builder.AppendLine(8, "return services;");

        return builder.ToString();
    }
}