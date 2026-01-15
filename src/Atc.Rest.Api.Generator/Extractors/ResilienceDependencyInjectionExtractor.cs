namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts resilience configuration and generates DI extension method for resilience handlers.
/// </summary>
public static class ResilienceDependencyInjectionExtractor
{
    /// <summary>
    /// Extracts resilience DI registration from OpenAPI document.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="projectName">The project name for namespace.</param>
    /// <param name="includeDeprecated">Whether to include deprecated operations.</param>
    /// <returns>Generated code content for the resilience DI extension class, or null if no policies needed.</returns>
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

        // Generate the complete file content
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
        // Generate content first to analyze for required usings
        var contentBuilder = new StringBuilder();
        contentBuilder.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        contentBuilder.AppendLine("public static class ResilienceServiceCollectionExtensions");
        contentBuilder.AppendLine("{");
        contentBuilder.AppendLine(4, "public static IHttpClientBuilder AddApiResilience(");
        contentBuilder.AppendLine(8, "this IHttpClientBuilder builder,");
        contentBuilder.AppendLine(8, "string policyName)");
        contentBuilder.AppendLine(12, "pipeline.AddRetry(new HttpRetryStrategyOptions");
        contentBuilder.AppendLine(16, "Delay = TimeSpan.FromSeconds(1.0),");
        contentBuilder.AppendLine(16, "BackoffType = DelayBackoffType.Exponential,");
        contentBuilder.AppendLine(16, "ShouldHandle = new PredicateBuilder<HttpResponseMessage>()");
        contentBuilder.AppendLine(20, ".HandleResult(response => response.StatusCode >= HttpStatusCode.InternalServerError ||");
        contentBuilder.AppendLine(46, "response.StatusCode == HttpStatusCode.RequestTimeout ||");
        contentBuilder.AppendLine(46, "response.StatusCode == HttpStatusCode.TooManyRequests)");
        contentBuilder.AppendLine(20, ".Handle<HttpRequestException>()");
        contentBuilder.AppendLine(20, ".Handle<TimeoutRejectedException>(),");
        contentBuilder.AppendLine(12, "pipeline.AddTimeout(TimeSpan.FromSeconds(30));");
        contentBuilder.AppendLine(12, "pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions");
        contentBuilder.AppendLine("}");
        var content = contentBuilder.ToString();

        // Build header with only required usings
        var builder = new StringBuilder();
        builder.Append(UsingStatementHelper.BuildHeader(content, NamespaceConstants.SystemCodeDomCompiler));
        builder.AppendLine($"using {projectName}.Generated.Resilience;");
        builder.AppendLine();
        builder.AppendLine($"namespace {projectName}.Generated.Resilience;");
        builder.AppendLine();
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// Extension methods for configuring API resilience policies.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        builder.AppendLine("public static class ResilienceServiceCollectionExtensions");
        builder.AppendLine("{");
        builder.AppendLine(4, "/// <summary>");
        builder.AppendLine(4, "/// Adds a resilience handler to the HTTP client builder based on OpenAPI extensions.");
        builder.AppendLine(4, "/// </summary>");
        builder.AppendLine(4, "/// <param name=\"builder\">The HTTP client builder.</param>");
        builder.AppendLine(4, "/// <param name=\"policyName\">The resilience policy name.</param>");
        builder.AppendLine(4, "/// <returns>The HTTP client builder for method chaining.</returns>");
        builder.AppendLine(4, "public static IHttpClientBuilder AddApiResilience(");
        builder.AppendLine(8, "this IHttpClientBuilder builder,");
        builder.AppendLine(8, "string policyName)");
        builder.AppendLine(4, "{");
        builder.AppendLine(8, "switch (policyName)");
        builder.AppendLine(8, "{");

        var sortedPolicies = policies
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .ToList();

        foreach (var policyKvp in sortedPolicies)
        {
            var constantName = ResiliencePoliciesExtractor.GenerateConstantName(policyKvp.Key);
            builder.AppendLine(12, $"case ResiliencePolicies.{constantName}:");
            builder.AppendLine(16, $"Configure{constantName}(builder);");
            builder.AppendLine(16, "break;");
        }

        builder.AppendLine(8, "}");
        builder.AppendLine();
        builder.AppendLine(8, "return builder;");
        builder.AppendLine(4, "}");

        // Generate individual configuration methods
        foreach (var policyKvp in sortedPolicies)
        {
            builder.AppendLine();
            GenerateConfigurationMethod(builder, policyKvp.Key, policyKvp.Value);
        }

        builder.AppendLine("}");

        return builder.ToString();
    }

    /// <summary>
    /// Generates a configuration method for a specific policy.
    /// </summary>
    private static void GenerateConfigurationMethod(
        StringBuilder builder,
        string policyName,
        RetryConfiguration config)
    {
        var constantName = ResiliencePoliciesExtractor.GenerateConstantName(policyName);

        builder.AppendLine(4, $"private static void Configure{constantName}(IHttpClientBuilder builder)");
        builder.AppendLine(4, "{");
        builder.AppendLine(8, $"builder.AddResilienceHandler(ResiliencePolicies.{constantName}, pipeline =>");
        builder.AppendLine(8, "{");

        // Add retry strategy
        builder.AppendLine(12, "pipeline.AddRetry(new HttpRetryStrategyOptions");
        builder.AppendLine(12, "{");
        builder.AppendLine(16, $"MaxRetryAttempts = {config.MaxAttempts},");
        builder.AppendLine(16, $"Delay = TimeSpan.FromSeconds({config.DelaySeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}),");
        builder.AppendLine(16, $"BackoffType = DelayBackoffType.{config.BackoffType},");
        builder.AppendLine(16, $"UseJitter = {(config.UseJitter ? "true" : "false")},");

        // Add DelayGenerator when Handle429 is enabled to respect Retry-After header
        if (config.Handle429)
        {
            builder.AppendLine(16, "DelayGenerator = static args =>");
            builder.AppendLine(16, "{");
            builder.AppendLine(20, "if (args.Outcome.Result?.StatusCode == HttpStatusCode.TooManyRequests)");
            builder.AppendLine(20, "{");
            builder.AppendLine(24, "var retryAfter = args.Outcome.Result.Headers.RetryAfter;");
            builder.AppendLine(24, "if (retryAfter?.Delta is { } delta)");
            builder.AppendLine(24, "{");
            builder.AppendLine(28, "return new ValueTask<TimeSpan?>(delta);");
            builder.AppendLine(24, "}");
            builder.AppendLine();
            builder.AppendLine(24, "if (retryAfter?.Date is { } date)");
            builder.AppendLine(24, "{");
            builder.AppendLine(28, "var delay = date - DateTimeOffset.UtcNow;");
            builder.AppendLine(28, "return new ValueTask<TimeSpan?>(delay > TimeSpan.Zero ? delay : null);");
            builder.AppendLine(24, "}");
            builder.AppendLine(20, "}");
            builder.AppendLine();
            builder.AppendLine(20, "return new ValueTask<TimeSpan?>((TimeSpan?)null);");
            builder.AppendLine(16, "},");
        }

        // Build ShouldHandle predicate - conditionally include TooManyRequests based on Handle429
        builder.AppendLine(16, "ShouldHandle = new PredicateBuilder<HttpResponseMessage>()");
        if (config.Handle429)
        {
            builder.AppendLine(20, ".HandleResult(response => response.StatusCode >= HttpStatusCode.InternalServerError ||");
            builder.AppendLine(46, "response.StatusCode == HttpStatusCode.RequestTimeout ||");
            builder.AppendLine(46, "response.StatusCode == HttpStatusCode.TooManyRequests)");
        }
        else
        {
            builder.AppendLine(20, ".HandleResult(response => response.StatusCode >= HttpStatusCode.InternalServerError ||");
            builder.AppendLine(46, "response.StatusCode == HttpStatusCode.RequestTimeout)");
        }

        builder.AppendLine(20, ".Handle<HttpRequestException>()");
        builder.AppendLine(20, ".Handle<TimeoutRejectedException>(),");
        builder.AppendLine(12, "});");

        // Add timeout if specified
        if (config.TimeoutSeconds.HasValue)
        {
            builder.AppendLine();
            builder.AppendLine(12, $"pipeline.AddTimeout(TimeSpan.FromSeconds({config.TimeoutSeconds.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}));");
        }

        // Add circuit breaker if enabled
        if (config.CircuitBreakerEnabled)
        {
            builder.AppendLine();
            builder.AppendLine(12, "pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions");
            builder.AppendLine(12, "{");
            builder.AppendLine(16, $"SamplingDuration = TimeSpan.FromSeconds({config.CircuitBreakerSamplingDurationSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}),");
            builder.AppendLine(16, $"FailureRatio = {config.CircuitBreakerFailureRatio.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
            builder.AppendLine(16, $"MinimumThroughput = {config.CircuitBreakerMinimumThroughput},");
            builder.AppendLine(16, $"BreakDuration = TimeSpan.FromSeconds({config.CircuitBreakerBreakDurationSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}),");
            builder.AppendLine(12, "});");
        }

        builder.AppendLine(8, "});");
        builder.AppendLine(4, "}");
    }
}