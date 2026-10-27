namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts Output Cache policies from OpenAPI document and generates policy name constants.
/// </summary>
public static class OutputCachePoliciesExtractor
{
    /// <summary>
    /// Extracts Output Cache policies from OpenAPI document.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="projectName">The project name for namespace.</param>
    /// <param name="includeDeprecated">Whether to include deprecated operations.</param>
    /// <returns>Generated code content for the Output Cache policies class, or null if no policies needed.</returns>
    public static string? Extract(
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated = false)
    {
        if (openApiDoc is null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        // Collect all unique Output Cache policies
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
    /// <param name="policyName">The policy name (e.g., "products", "product-list").</param>
    /// <returns>A valid C# identifier (e.g., "Products", "ProductList").</returns>
    public static string GenerateConstantName(string policyName)
        => PolicyNamingHelper.ToConstantName(policyName);

    /// <summary>
    /// Collects all unique Output Cache policies from cache extensions across all operations.
    /// </summary>
    internal static Dictionary<string, CacheConfiguration> CollectPolicies(
        OpenApiDocument openApiDoc,
        bool includeDeprecated)
        => CachePolicyCollector.CollectPolicies(openApiDoc, includeDeprecated, CacheType.Output);

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
                "OutputCachePolicies",
                "Output Cache policy name constants generated from OpenAPI extensions.",
                policies
                    .OrderBy(p => p.Key, StringComparer.Ordinal)
                    .Select(p => new PolicyConstant(GenerateConstantName(p.Key), p.Key, GeneratePolicyDescription(p.Value)))),
            "OutputCacheTags",
            "Output Cache tag constants (x-cache-tags): what IOutputCacheStore.EvictByTagAsync evicts by.",
            CacheTagConstants.Assign(policies.Values));

    /// <summary>
    /// Generates a description for the policy based on configuration.
    /// </summary>
    private static string GeneratePolicyDescription(CacheConfiguration config)
    {
        var parts = new List<string>
        {
            $"{config.ExpirationSeconds}s expiration",
        };

        if (config.VaryByQuery.Count > 0)
        {
            parts.Add($"vary by query: [{string.Join(", ", config.VaryByQuery)}]");
        }

        if (config.VaryByHeader.Count > 0)
        {
            parts.Add($"vary by header: [{string.Join(", ", config.VaryByHeader)}]");
        }

        if (config.VaryByRoute.Count > 0)
        {
            parts.Add($"vary by route: [{string.Join(", ", config.VaryByRoute)}]");
        }

        if (config.Tags.Count > 0)
        {
            parts.Add($"tags: [{string.Join(", ", config.Tags)}]");
        }

        return $"Policy: {string.Join(", ", parts)}";
    }
}