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
        if (openApiDoc is null)
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
    /// Generates a valid C# constant name from a policy name.
    /// </summary>
    /// <param name="policyName">The policy name (e.g., "users", "user-detail").</param>
    /// <returns>A valid C# identifier (e.g., "Users", "UserDetail").</returns>
    public static string GenerateConstantName(string policyName)
        => PolicyNamingHelper.ToConstantName(policyName);

    /// <summary>
    /// Collects all unique HybridCache policies from cache extensions across all operations.
    /// </summary>
    internal static Dictionary<string, CacheConfiguration> CollectPolicies(
        OpenApiDocument openApiDoc,
        bool includeDeprecated)
        => CachePolicyCollector.CollectPolicies(openApiDoc, includeDeprecated, CacheType.Hybrid);

    /// <summary>
    /// Generates the complete file content.
    /// </summary>
    private static string GenerateFileContent(
        string projectName,
        Dictionary<string, CacheConfiguration> policies)
        => CacheTagConstants.AppendClass(
            PolicyConstantsClassBuilder.Build(
                projectName,
                "Caching",
                "CachePolicies",
                "HybridCache policy name constants generated from OpenAPI extensions.",
                policies
                    .OrderBy(p => p.Key, StringComparer.Ordinal)
                    .Select(p => new PolicyConstant(GenerateConstantName(p.Key), p.Key, GeneratePolicyDescription(p.Value)))),
            "CacheTags",
            "HybridCache tag constants (x-cache-tags): what HybridCache.RemoveByTagAsync removes by.",
            CacheTagConstants.Assign(policies.Values));

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
}