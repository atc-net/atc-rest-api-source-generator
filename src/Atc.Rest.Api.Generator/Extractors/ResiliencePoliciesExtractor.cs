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
    /// <param name="includeDeprecated">Whether to include deprecated operations.</param>
    /// <returns>Generated code content for the resilience policies class, or null if no policies needed.</returns>
    public static string? Extract(
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated = false)
    {
        if (openApiDoc == null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        // Collect all unique policies from retry extensions
        var policies = CollectPolicies(openApiDoc, includeDeprecated);

        if (policies.Count == 0)
        {
            return null;
        }

        // Generate the complete class content
        return GenerateFileContent(projectName, policies);
    }

    /// <summary>
    /// Collects all unique policies from retry extensions across all operations.
    /// </summary>
    private static Dictionary<string, RetryConfiguration> CollectPolicies(
        OpenApiDocument openApiDoc,
        bool includeDeprecated)
    {
        var policies = new Dictionary<string, RetryConfiguration>(StringComparer.Ordinal);

        // Check document-level policy
        var documentPolicy = openApiDoc.Extensions.ExtractRetryPolicy();
        if (!string.IsNullOrEmpty(documentPolicy))
        {
            var config = CreateConfigFromExtensions(
                openApiDoc.Extensions,
                null,
                null);
            if (config != null)
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
            var pathPolicy = pathItem.Extensions.ExtractRetryPolicy();
            if (!string.IsNullOrEmpty(pathPolicy) && !policies.ContainsKey(pathPolicy!))
            {
                var config = CreateConfigFromExtensions(
                    openApiDoc.Extensions,
                    pathItem.Extensions,
                    null);
                if (config != null)
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
                var operationPolicy = operation.Extensions.ExtractRetryPolicy();
                if (!string.IsNullOrEmpty(operationPolicy) && !policies.ContainsKey(operationPolicy!))
                {
                    var config = CreateConfigFromExtensions(
                        openApiDoc.Extensions,
                        pathItem.Extensions,
                        operation.Extensions);
                    if (config != null)
                    {
                        policies[operationPolicy!] = config;
                    }
                }
            }
        }

        return policies;
    }

    /// <summary>
    /// Creates a retry configuration from extension values at different levels.
    /// </summary>
    private static RetryConfiguration? CreateConfigFromExtensions(
        IDictionary<string, IOpenApiExtension>? documentExtensions,
        IDictionary<string, IOpenApiExtension>? pathExtensions,
        IDictionary<string, IOpenApiExtension>? operationExtensions)
    {
        var policy = operationExtensions.ExtractRetryPolicy()
                     ?? pathExtensions.ExtractRetryPolicy()
                     ?? documentExtensions.ExtractRetryPolicy();

        if (string.IsNullOrEmpty(policy))
        {
            return null;
        }

        var maxAttempts = operationExtensions.ExtractMaxRetryAttempts()
                          ?? pathExtensions.ExtractMaxRetryAttempts()
                          ?? documentExtensions.ExtractMaxRetryAttempts()
                          ?? 3;

        var delaySeconds = operationExtensions.ExtractRetryDelaySeconds()
                           ?? pathExtensions.ExtractRetryDelaySeconds()
                           ?? documentExtensions.ExtractRetryDelaySeconds()
                           ?? 1.0;

        var backoffString = operationExtensions.ExtractRetryBackoff()
                            ?? pathExtensions.ExtractRetryBackoff()
                            ?? documentExtensions.ExtractRetryBackoff()
                            ?? "exponential";

        var useJitter = operationExtensions.ExtractRetryUseJitter()
                        ?? pathExtensions.ExtractRetryUseJitter()
                        ?? documentExtensions.ExtractRetryUseJitter()
                        ?? true;

        var timeoutSeconds = operationExtensions.ExtractRetryTimeoutSeconds()
                             ?? pathExtensions.ExtractRetryTimeoutSeconds()
                             ?? documentExtensions.ExtractRetryTimeoutSeconds();

        var circuitBreaker = operationExtensions.ExtractRetryCircuitBreaker()
                             ?? pathExtensions.ExtractRetryCircuitBreaker()
                             ?? documentExtensions.ExtractRetryCircuitBreaker()
                             ?? false;

        var circuitBreakerFailureRatio = operationExtensions.ExtractCircuitBreakerFailureRatio()
                                          ?? pathExtensions.ExtractCircuitBreakerFailureRatio()
                                          ?? documentExtensions.ExtractCircuitBreakerFailureRatio()
                                          ?? 0.5;

        var circuitBreakerSamplingDuration = operationExtensions.ExtractCircuitBreakerSamplingDurationSeconds()
                                              ?? pathExtensions.ExtractCircuitBreakerSamplingDurationSeconds()
                                              ?? documentExtensions.ExtractCircuitBreakerSamplingDurationSeconds()
                                              ?? 30.0;

        var circuitBreakerMinimumThroughput = operationExtensions.ExtractCircuitBreakerMinimumThroughput()
                                               ?? pathExtensions.ExtractCircuitBreakerMinimumThroughput()
                                               ?? documentExtensions.ExtractCircuitBreakerMinimumThroughput()
                                               ?? 10;

        var circuitBreakerBreakDuration = operationExtensions.ExtractCircuitBreakerBreakDurationSeconds()
                                           ?? pathExtensions.ExtractCircuitBreakerBreakDurationSeconds()
                                           ?? documentExtensions.ExtractCircuitBreakerBreakDurationSeconds()
                                           ?? 30.0;

        var handle429 = operationExtensions.ExtractRetryHandle429()
                        ?? pathExtensions.ExtractRetryHandle429()
                        ?? documentExtensions.ExtractRetryHandle429()
                        ?? true;

        return new RetryConfiguration
        {
            Enabled = true,
            Policy = policy,
            MaxAttempts = maxAttempts,
            DelaySeconds = delaySeconds,
            BackoffType = ParseBackoffType(backoffString),
            UseJitter = useJitter,
            TimeoutSeconds = timeoutSeconds,
            CircuitBreakerEnabled = circuitBreaker,
            CircuitBreakerFailureRatio = circuitBreakerFailureRatio,
            CircuitBreakerSamplingDurationSeconds = circuitBreakerSamplingDuration,
            CircuitBreakerMinimumThroughput = circuitBreakerMinimumThroughput,
            CircuitBreakerBreakDurationSeconds = circuitBreakerBreakDuration,
            Handle429 = handle429,
        };
    }

    /// <summary>
    /// Parses a backoff type string to the enum value.
    /// </summary>
    private static RetryBackoffType ParseBackoffType(string? backoffString)
    {
        if (string.IsNullOrEmpty(backoffString))
        {
            return RetryBackoffType.Exponential;
        }

        return backoffString!.ToLowerInvariant() switch
        {
            "constant" or "fixed" => RetryBackoffType.Constant,
            "linear" => RetryBackoffType.Linear,
            "exponential" => RetryBackoffType.Exponential,
            _ => RetryBackoffType.Exponential,
        };
    }

    /// <summary>
    /// Generates the complete file content.
    /// </summary>
    private static string GenerateFileContent(
        string projectName,
        Dictionary<string, RetryConfiguration> policies)
    {
        var builder = new StringBuilder();

        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.AppendLine("using System.CodeDom.Compiler;");
        builder.AppendLine();
        builder.AppendLine($"namespace {projectName}.Generated.Resilience;");
        builder.AppendLine();
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// Resilience policy name constants generated from OpenAPI extensions.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        builder.AppendLine("public static class ResiliencePolicies");
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

    /// <summary>
    /// Generates a valid C# constant name from a policy name.
    /// </summary>
    /// <param name="policyName">The policy name (e.g., "standard", "fast-retry").</param>
    /// <returns>A valid C# identifier (e.g., "Standard", "FastRetry").</returns>
    public static string GenerateConstantName(string policyName)
    {
        // Split by '-', '_', and other separators
        var parts = policyName.Split(new[] { '-', '_', ':', ' ' }, StringSplitOptions.RemoveEmptyEntries);

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