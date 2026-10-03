namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts resilience policies from OpenAPI document and generates policy name constants.
/// </summary>
public static class ResiliencePoliciesExtractor
{
    /// <summary>
    /// Extracts resilience policies from OpenAPI document.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="projectName">The project name for namespace.</param>
    /// <param name="includeDeprecated">Not used: retry policies of deprecated operations are always included (see <c>CollectPolicies</c>).</param>
    /// <returns>Generated code content for the resilience policies class, or null if no policies needed.</returns>
    public static string? Extract(
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated = false)
    {
        if (openApiDoc is null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        // Collect all unique policies from retry extensions
        var policies = CollectPolicies(openApiDoc);

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
    /// <param name="policyName">The policy name (e.g., "standard", "fast-retry").</param>
    /// <returns>A valid C# identifier (e.g., "Standard", "FastRetry").</returns>
    public static string GenerateConstantName(string policyName)
        => PolicyNamingHelper.ToConstantName(policyName);

    /// <summary>
    /// Collects all unique policies from retry extensions across all operations
    /// (shared with the TypeScript client, see <see cref="OpenApiRetryExtensions.CollectNamedRetryPolicies"/>).
    /// </summary>
    /// <remarks>
    /// Deprecated operations are always included: the Resilience files must exist whenever the document has
    /// retry configuration (<see cref="OpenApiRetryExtensions.HasRetryConfiguration"/>), because that is what
    /// decides whether the generated clients reference <c>ApiResilienceRequest</c>. A policy constant for an
    /// operation that is not generated is harmless.
    /// </remarks>
    internal static Dictionary<string, RetryConfiguration> CollectPolicies(
        OpenApiDocument openApiDoc)
        => openApiDoc
            .CollectNamedRetryPolicies(includeDeprecated: true)
            .ToDictionary(p => p.Policy!, p => p, StringComparer.Ordinal);

    /// <summary>
    /// Generates the complete file content.
    /// </summary>
    private static string GenerateFileContent(
        string projectName,
        Dictionary<string, RetryConfiguration> policies)
        => PolicyConstantsClassBuilder.Build(
            projectName,
            "Resilience",
            "ResiliencePolicies",
            "Resilience policy name constants generated from OpenAPI extensions.",
            policies
                .OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new PolicyConstant(GenerateConstantName(p.Key), p.Key, GeneratePolicyDescription(p.Value))));

    /// <summary>
    /// Generates a human-readable description for a policy configuration.
    /// </summary>
    private static string GeneratePolicyDescription(RetryConfiguration config)
    {
        var parts = new List<string>
        {
            $"{config.BackoffType} backoff",
            $"{config.MaxAttempts} attempts",
            $"{config.DelaySeconds}s delay",
        };

        if (config.Handle429)
        {
            parts.Add("respects Retry-After header");
        }

        if (config.CircuitBreakerEnabled)
        {
            parts.Add($"circuit breaker (failure ratio: {config.CircuitBreakerFailureRatio:F1}, break: {config.CircuitBreakerBreakDurationSeconds}s)");
        }

        if (config.TimeoutSeconds.HasValue)
        {
            parts.Add($"timeout: {config.TimeoutSeconds.Value}s");
        }

        return "Policy: " + string.Join(", ", parts);
    }
}