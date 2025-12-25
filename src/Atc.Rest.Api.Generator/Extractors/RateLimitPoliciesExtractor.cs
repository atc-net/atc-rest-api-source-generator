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
        if (openApiDoc == null)
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
    /// Collects all unique policies from rate limit extensions across all operations.
    /// </summary>
    private static Dictionary<string, RateLimitConfiguration> CollectPolicies(
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
            var pathPolicy = pathItem.Extensions.ExtractRateLimitPolicy();
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
                var operationPolicy = operation.Extensions.ExtractRateLimitPolicy();
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
    /// Creates a rate limit configuration from extension values at different levels.
    /// </summary>
    private static RateLimitConfiguration? CreateConfigFromExtensions(
        IDictionary<string, IOpenApiExtension>? documentExtensions,
        IDictionary<string, IOpenApiExtension>? pathExtensions,
        IDictionary<string, IOpenApiExtension>? operationExtensions)
    {
        var policy = operationExtensions.ExtractRateLimitPolicy()
                     ?? pathExtensions.ExtractRateLimitPolicy()
                     ?? documentExtensions.ExtractRateLimitPolicy();

        if (string.IsNullOrEmpty(policy))
        {
            return null;
        }

        var permitLimit = operationExtensions.ExtractPermitLimit()
                          ?? pathExtensions.ExtractPermitLimit()
                          ?? documentExtensions.ExtractPermitLimit()
                          ?? 100;

        var windowSeconds = operationExtensions.ExtractWindowSeconds()
                            ?? pathExtensions.ExtractWindowSeconds()
                            ?? documentExtensions.ExtractWindowSeconds()
                            ?? 60;

        var queueLimit = operationExtensions.ExtractQueueLimit()
                         ?? pathExtensions.ExtractQueueLimit()
                         ?? documentExtensions.ExtractQueueLimit()
                         ?? 0;

        var algorithmString = operationExtensions.ExtractRateLimitAlgorithm()
                              ?? pathExtensions.ExtractRateLimitAlgorithm()
                              ?? documentExtensions.ExtractRateLimitAlgorithm()
                              ?? "fixed";

        return new RateLimitConfiguration
        {
            Enabled = true,
            Policy = policy,
            PermitLimit = permitLimit,
            WindowSeconds = windowSeconds,
            QueueLimit = queueLimit,
            Algorithm = ParseAlgorithm(algorithmString),
        };
    }

    /// <summary>
    /// Parses a rate limit algorithm string to the enum value.
    /// </summary>
    private static RateLimitAlgorithm ParseAlgorithm(string? algorithmString)
    {
        if (string.IsNullOrEmpty(algorithmString))
        {
            return RateLimitAlgorithm.Fixed;
        }

        return algorithmString!.ToLowerInvariant() switch
        {
            "sliding" or "sliding-window" => RateLimitAlgorithm.Sliding,
            "token-bucket" or "tokenbucket" => RateLimitAlgorithm.TokenBucket,
            "concurrency" => RateLimitAlgorithm.Concurrency,
            _ => RateLimitAlgorithm.Fixed,
        };
    }

    /// <summary>
    /// Generates the complete file content.
    /// </summary>
    private static string GenerateFileContent(
        string projectName,
        Dictionary<string, RateLimitConfiguration> policies)
    {
        var builder = new StringBuilder();

        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.AppendLine("using System.CodeDom.Compiler;");
        builder.AppendLine();
        builder.AppendLine($"namespace {projectName}.Generated.RateLimiting;");
        builder.AppendLine();
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// Rate limit policy name constants generated from OpenAPI extensions.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        builder.AppendLine("public static class RateLimitPolicies");
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
            var description = $"Policy: {config.Algorithm} window, {config.PermitLimit} requests/{config.WindowSeconds}s";

            builder.AppendLine(4, "/// <summary>");
            builder.AppendLine(4, $"/// {description}");
            builder.AppendLine(4, "/// </summary>");
            builder.AppendLine(4, $"public const string {constantName} = \"{policyName}\";");
        }

        builder.AppendLine("}");

        return builder.ToString();
    }

    /// <summary>
    /// Generates a valid C# constant name from a policy name.
    /// </summary>
    /// <param name="policyName">The policy name (e.g., "global", "create-user").</param>
    /// <returns>A valid C# identifier (e.g., "Global", "CreateUser").</returns>
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