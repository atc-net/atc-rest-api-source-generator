namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts rate limit configuration and generates DI extension method for rate limiter policies.
/// </summary>
public static class RateLimitDependencyInjectionExtractor
{
    /// <summary>
    /// Extracts rate limit DI registration from OpenAPI document.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="projectName">The project name for namespace.</param>
    /// <param name="includeDeprecated">Whether to include deprecated operations.</param>
    /// <returns>Generated code content for the rate limit DI extension class, or null if no policies needed.</returns>
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

        // Generate the complete file content
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
        // Generate content first to analyze for required usings
        var contentBuilder = new StringBuilder();
        contentBuilder.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        contentBuilder.AppendLine("public static class RateLimitingServiceCollectionExtensions");
        contentBuilder.AppendLine("{");
        contentBuilder.AppendLine(4, "public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)");
        contentBuilder.AppendLine(8, "services.AddRateLimiter(options =>");  // Triggers Microsoft.AspNetCore.RateLimiting
        contentBuilder.AppendLine(12, "options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;");  // Triggers Microsoft.AspNetCore.Http
        contentBuilder.AppendLine(12, "options.AddFixedWindowLimiter(RateLimitPolicies.Default, opt =>");  // Triggers Microsoft.AspNetCore.RateLimiting
        contentBuilder.AppendLine(16, "opt.Window = TimeSpan.FromSeconds(60);");
        contentBuilder.AppendLine(16, "opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;");  // Triggers System.Threading.RateLimiting
        contentBuilder.AppendLine(12, "WebApplication app;");  // Triggers Microsoft.AspNetCore.Builder
        contentBuilder.AppendLine("}");
        var content = contentBuilder.ToString();

        // Build header with only required usings
        var builder = new StringBuilder();
        builder.Append(UsingStatementHelper.BuildHeader(content, "System.CodeDom.Compiler"));
        builder.AppendLine($"using {projectName}.Generated.RateLimiting;");
        builder.AppendLine();
        builder.AppendLine($"namespace {projectName}.Generated.RateLimiting;");
        builder.AppendLine();
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// Extension methods for configuring API rate limiting policies.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        builder.AppendLine("public static class RateLimitingServiceCollectionExtensions");
        builder.AppendLine("{");
        builder.AppendLine(4, "/// <summary>");
        builder.AppendLine(4, "/// Registers rate limiting policies for the API based on OpenAPI extensions.");
        builder.AppendLine(4, "/// </summary>");
        builder.AppendLine(4, "/// <param name=\"services\">The service collection.</param>");
        builder.AppendLine(4, "/// <returns>The service collection for method chaining.</returns>");
        builder.AppendLine(4, "public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)");
        builder.AppendLine(4, "{");

        // Generate method content
        var methodContent = GenerateMethodContent(policies);
        builder.Append(methodContent);

        builder.AppendLine(4, "}");
        builder.AppendLine("}");

        return builder.ToString();
    }

    /// <summary>
    /// Generates the method content for AddApiRateLimiting.
    /// </summary>
    private static string GenerateMethodContent(
        Dictionary<string, RateLimitConfiguration> policies)
    {
        var builder = new StringBuilder();

        builder.AppendLine(8, "services.AddRateLimiter(options =>");
        builder.AppendLine(8, "{");
        builder.AppendLine(12, "options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;");

        var sortedPolicies = policies.OrderBy(p => p.Key, StringComparer.Ordinal).ToList();

        foreach (var policyKvp in sortedPolicies)
        {
            builder.AppendLine();

            var policyName = policyKvp.Key;
            var config = policyKvp.Value;
            var constantName = RateLimitPoliciesExtractor.GenerateConstantName(policyName);

            GenerateLimiterConfiguration(builder, constantName, config);
        }

        builder.AppendLine(8, "});");
        builder.AppendLine();
        builder.AppendLine(8, "return services;");

        return builder.ToString();
    }

    /// <summary>
    /// Generates the limiter configuration for a specific policy.
    /// </summary>
    private static void GenerateLimiterConfiguration(
        StringBuilder builder,
        string constantName,
        RateLimitConfiguration config)
    {
        switch (config.Algorithm)
        {
            case RateLimitAlgorithm.Sliding:
                GenerateSlidingWindowLimiter(builder, constantName, config);
                break;
            case RateLimitAlgorithm.TokenBucket:
                GenerateTokenBucketLimiter(builder, constantName, config);
                break;
            case RateLimitAlgorithm.Concurrency:
                GenerateConcurrencyLimiter(builder, constantName, config);
                break;
            default:
                GenerateFixedWindowLimiter(builder, constantName, config);
                break;
        }
    }

    private static void GenerateFixedWindowLimiter(
        StringBuilder builder,
        string constantName,
        RateLimitConfiguration config)
    {
        builder.AppendLine(12, $"options.AddFixedWindowLimiter(RateLimitPolicies.{constantName}, opt =>");
        builder.AppendLine(12, "{");
        builder.AppendLine(16, $"opt.PermitLimit = {config.PermitLimit};");
        builder.AppendLine(16, $"opt.Window = TimeSpan.FromSeconds({config.WindowSeconds});");
        builder.AppendLine(16, "opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;");
        builder.AppendLine(16, $"opt.QueueLimit = {config.QueueLimit};");
        builder.AppendLine(12, "});");
    }

    private static void GenerateSlidingWindowLimiter(
        StringBuilder builder,
        string constantName,
        RateLimitConfiguration config)
    {
        // Default to 6 segments per window for sliding window
        var segmentsPerWindow = System.Math.Max(1, config.WindowSeconds / 10);
        if (segmentsPerWindow > 60)
        {
            segmentsPerWindow = 60;
        }

        builder.AppendLine(12, $"options.AddSlidingWindowLimiter(RateLimitPolicies.{constantName}, opt =>");
        builder.AppendLine(12, "{");
        builder.AppendLine(16, $"opt.PermitLimit = {config.PermitLimit};");
        builder.AppendLine(16, $"opt.Window = TimeSpan.FromSeconds({config.WindowSeconds});");
        builder.AppendLine(16, $"opt.SegmentsPerWindow = {segmentsPerWindow};");
        builder.AppendLine(16, "opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;");
        builder.AppendLine(16, $"opt.QueueLimit = {config.QueueLimit};");
        builder.AppendLine(12, "});");
    }

    private static void GenerateTokenBucketLimiter(
        StringBuilder builder,
        string constantName,
        RateLimitConfiguration config)
    {
        builder.AppendLine(12, $"options.AddTokenBucketLimiter(RateLimitPolicies.{constantName}, opt =>");
        builder.AppendLine(12, "{");
        builder.AppendLine(16, $"opt.TokenLimit = {config.PermitLimit};");
        builder.AppendLine(16, $"opt.ReplenishmentPeriod = TimeSpan.FromSeconds({config.WindowSeconds});");
        builder.AppendLine(16, $"opt.TokensPerPeriod = {config.PermitLimit};");
        builder.AppendLine(16, "opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;");
        builder.AppendLine(16, $"opt.QueueLimit = {config.QueueLimit};");
        builder.AppendLine(12, "});");
    }

    private static void GenerateConcurrencyLimiter(
        StringBuilder builder,
        string constantName,
        RateLimitConfiguration config)
    {
        builder.AppendLine(12, $"options.AddConcurrencyLimiter(RateLimitPolicies.{constantName}, opt =>");
        builder.AppendLine(12, "{");
        builder.AppendLine(16, $"opt.PermitLimit = {config.PermitLimit};");
        builder.AppendLine(16, "opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;");
        builder.AppendLine(16, $"opt.QueueLimit = {config.QueueLimit};");
        builder.AppendLine(12, "});");
    }
}