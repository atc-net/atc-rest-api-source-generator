namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts rate limit policies from OpenAPI document and generates policy name constants.
/// </summary>
public static class RateLimitPoliciesExtractor
{
    /// <summary>
    /// Extracts rate limit policies from OpenAPI document.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="projectName">The project name for namespace.</param>
    /// <param name="includeDeprecated">Whether to include deprecated operations.</param>
    /// <returns>Generated code content for the rate limit policies class, or null if no policies needed.</returns>
    public static string? Extract(
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated = false)
    {
        if (openApiDoc is null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        // Collect all unique policies from rate limit extensions
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
    /// <param name="policyName">The policy name (e.g., "global", "create-user").</param>
    /// <returns>A valid C# identifier (e.g., "Global", "CreateUser").</returns>
    public static string GenerateConstantName(string policyName)
        => PolicyNamingHelper.ToConstantName(policyName);

    /// <summary>
    /// Collects all unique policies from rate limit extensions across all operations.
    /// </summary>
    internal static Dictionary<string, RateLimitConfiguration> CollectPolicies(
        OpenApiDocument openApiDoc,
        bool includeDeprecated)
    {
        var policies = new Dictionary<string, RateLimitConfiguration>(StringComparer.Ordinal);

        // Check document-level policy
        var documentPolicy = openApiDoc.Extensions.ExtractRateLimitPolicy();
        if (!string.IsNullOrEmpty(documentPolicy))
        {
            var config = CreateConfigFromExtensions(
                openApiDoc.Extensions,
                null,
                null);
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
            var pathPolicy = pathItem.Extensions.ExtractRateLimitPolicy();
            if (!string.IsNullOrEmpty(pathPolicy) && !policies.ContainsKey(pathPolicy!))
            {
                var config = CreateConfigFromExtensions(
                    openApiDoc.Extensions,
                    pathItem.Extensions,
                    null);
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
                if (!includeDeprecated && operation?.Deprecated == true)
                {
                    continue;
                }

                if (operation is null)
                {
                    continue;
                }

                // Check operation-level policy
                var operationPolicy = operation.Extensions.ExtractRateLimitPolicy();
                if (!string.IsNullOrEmpty(operationPolicy) && !policies.ContainsKey(operationPolicy!))
                {
                    var config = CreateConfigFromExtensions(
                        openApiDoc.Extensions,
                        pathItem.Extensions,
                        operation.Extensions);
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
    /// Creates a rate limit configuration from extension values at different levels.
    /// </summary>
    private static RateLimitConfiguration? CreateConfigFromExtensions(
        IDictionary<string, IOpenApiExtension>? documentExtensions,
        IDictionary<string, IOpenApiExtension>? pathExtensions,
        IDictionary<string, IOpenApiExtension>? operationExtensions)
        => OpenApiRateLimitExtensions.ResolveRateLimit(new OpenApiExtensionScope(operationExtensions, pathExtensions, documentExtensions));

    /// <summary>
    /// Generates the complete file content.
    /// </summary>
    private static string GenerateFileContent(
        string projectName,
        Dictionary<string, RateLimitConfiguration> policies)
        => PolicyConstantsClassBuilder.Build(
            projectName,
            "RateLimiting",
            "RateLimitPolicies",
            "Rate limit policy name constants generated from OpenAPI extensions.",
            policies
                .OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new PolicyConstant(GenerateConstantName(p.Key), p.Key, $"Policy: {p.Value.Algorithm} window, {p.Value.PermitLimit} requests/{p.Value.WindowSeconds}s")));
}