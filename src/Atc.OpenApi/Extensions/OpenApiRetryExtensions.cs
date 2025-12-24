namespace Atc.OpenApi.Extensions;

/// <summary>
/// Extension methods for extracting retry/resilience configuration from OpenAPI extensions.
/// Supports ATC-specific extensions (x-retry-*) for configuring client-side resilience.
/// </summary>
[SuppressMessage("", "CA1034:Do not nest type", Justification = "OK - CLang14 - extension")]
[SuppressMessage("", "S3398:Move this method inside", Justification = "OK - CLang14 - extension")]
public static class OpenApiRetryExtensions
{
    /// <param name="extensions">The OpenAPI extensions dictionary.</param>
    extension(IDictionary<string, IOpenApiExtension>? extensions)
    {
        /// <summary>
        /// Extracts the x-retry-policy value from extensions.
        /// </summary>
        /// <returns>The policy name, or null if not specified.</returns>
        public string? ExtractRetryPolicy()
            => ExtractStringValue(extensions, RetryExtensionNameConstants.Policy);

        /// <summary>
        /// Extracts the x-retry-enabled value from extensions.
        /// </summary>
        /// <returns>True if enabled, false if explicitly disabled, null if not specified.</returns>
        public bool? ExtractRetryEnabled()
            => ExtractBoolValue(extensions, RetryExtensionNameConstants.Enabled);

        /// <summary>
        /// Extracts the x-retry-max-attempts value from extensions.
        /// </summary>
        /// <returns>The max attempts, or null if not specified.</returns>
        public int? ExtractMaxRetryAttempts()
            => ExtractIntValue(extensions, RetryExtensionNameConstants.MaxAttempts);

        /// <summary>
        /// Extracts the x-retry-delay-seconds value from extensions.
        /// </summary>
        /// <returns>The delay in seconds, or null if not specified.</returns>
        public double? ExtractRetryDelaySeconds()
            => ExtractDoubleValue(extensions, RetryExtensionNameConstants.DelaySeconds);

        /// <summary>
        /// Extracts the x-retry-backoff value from extensions.
        /// </summary>
        /// <returns>The backoff type string, or null if not specified.</returns>
        public string? ExtractRetryBackoff()
            => ExtractStringValue(extensions, RetryExtensionNameConstants.Backoff);

        /// <summary>
        /// Extracts the x-retry-use-jitter value from extensions.
        /// </summary>
        /// <returns>True if jitter enabled, false if disabled, null if not specified.</returns>
        public bool? ExtractRetryUseJitter()
            => ExtractBoolValue(extensions, RetryExtensionNameConstants.UseJitter);

        /// <summary>
        /// Extracts the x-retry-timeout-seconds value from extensions.
        /// </summary>
        /// <returns>The timeout in seconds, or null if not specified.</returns>
        public double? ExtractRetryTimeoutSeconds()
            => ExtractDoubleValue(extensions, RetryExtensionNameConstants.TimeoutSeconds);

        /// <summary>
        /// Extracts the x-retry-circuit-breaker value from extensions.
        /// </summary>
        /// <returns>True if circuit breaker enabled, false if disabled, null if not specified.</returns>
        public bool? ExtractRetryCircuitBreaker()
            => ExtractBoolValue(extensions, RetryExtensionNameConstants.CircuitBreaker);

        /// <summary>
        /// Extracts the x-retry-cb-failure-ratio value from extensions.
        /// </summary>
        /// <returns>The failure ratio (0.0-1.0), or null if not specified.</returns>
        public double? ExtractCircuitBreakerFailureRatio()
            => ExtractDoubleValue(extensions, RetryExtensionNameConstants.CircuitBreakerFailureRatio);

        /// <summary>
        /// Extracts the x-retry-cb-sampling-duration-seconds value from extensions.
        /// </summary>
        /// <returns>The sampling duration in seconds, or null if not specified.</returns>
        public double? ExtractCircuitBreakerSamplingDurationSeconds()
            => ExtractDoubleValue(extensions, RetryExtensionNameConstants.CircuitBreakerSamplingDurationSeconds);

        /// <summary>
        /// Extracts the x-retry-cb-minimum-throughput value from extensions.
        /// </summary>
        /// <returns>The minimum throughput, or null if not specified.</returns>
        public int? ExtractCircuitBreakerMinimumThroughput()
            => ExtractIntValue(extensions, RetryExtensionNameConstants.CircuitBreakerMinimumThroughput);

        /// <summary>
        /// Extracts the x-retry-cb-break-duration-seconds value from extensions.
        /// </summary>
        /// <returns>The break duration in seconds, or null if not specified.</returns>
        public double? ExtractCircuitBreakerBreakDurationSeconds()
            => ExtractDoubleValue(extensions, RetryExtensionNameConstants.CircuitBreakerBreakDurationSeconds);

        /// <summary>
        /// Extracts the x-retry-handle-429 value from extensions.
        /// </summary>
        /// <returns>True if 429 handling enabled, false if disabled, null if not specified.</returns>
        public bool? ExtractRetryHandle429()
            => ExtractBoolValue(extensions, RetryExtensionNameConstants.Handle429);
    }

    /// <summary>
    /// Extracts the complete retry configuration for an operation.
    /// Applies hierarchical inheritance: operation overrides path, path overrides document.
    /// </summary>
    /// <param name="operation">The OpenAPI operation.</param>
    /// <param name="pathItem">The path item containing the operation.</param>
    /// <param name="document">The OpenAPI document (for root-level settings).</param>
    /// <returns>A RetryConfiguration, or null if no retry is configured.</returns>
    public static RetryConfiguration? ExtractRetryConfiguration(
        this OpenApiOperation operation,
        OpenApiPathItem pathItem,
        OpenApiDocument document)
    {
        // Check for operation-level explicit disable
        var operationEnabled = operation.Extensions.ExtractRetryEnabled();
        if (operationEnabled == false)
        {
            // Return a disabled configuration with timeout if specified
            var timeout = operation.Extensions.ExtractRetryTimeoutSeconds()
                          ?? pathItem.Extensions.ExtractRetryTimeoutSeconds()
                          ?? document.Extensions.ExtractRetryTimeoutSeconds();

            return new RetryConfiguration
            {
                Enabled = false,
                TimeoutSeconds = timeout,
            };
        }

        // Get effective policy (operation → path → document)
        var policy = operation.Extensions.ExtractRetryPolicy()
                     ?? pathItem.Extensions.ExtractRetryPolicy()
                     ?? document.Extensions.ExtractRetryPolicy();

        if (string.IsNullOrEmpty(policy))
        {
            return null; // No retry configured
        }

        // Get configuration values (operation → path → document)
        var maxAttempts = operation.Extensions.ExtractMaxRetryAttempts()
                          ?? pathItem.Extensions.ExtractMaxRetryAttempts()
                          ?? document.Extensions.ExtractMaxRetryAttempts()
                          ?? 3;

        var delaySeconds = operation.Extensions.ExtractRetryDelaySeconds()
                           ?? pathItem.Extensions.ExtractRetryDelaySeconds()
                           ?? document.Extensions.ExtractRetryDelaySeconds()
                           ?? 1.0;

        var backoffString = operation.Extensions.ExtractRetryBackoff()
                            ?? pathItem.Extensions.ExtractRetryBackoff()
                            ?? document.Extensions.ExtractRetryBackoff()
                            ?? "exponential";

        var useJitter = operation.Extensions.ExtractRetryUseJitter()
                        ?? pathItem.Extensions.ExtractRetryUseJitter()
                        ?? document.Extensions.ExtractRetryUseJitter()
                        ?? true;

        var timeoutSeconds = operation.Extensions.ExtractRetryTimeoutSeconds()
                             ?? pathItem.Extensions.ExtractRetryTimeoutSeconds()
                             ?? document.Extensions.ExtractRetryTimeoutSeconds();

        var circuitBreaker = operation.Extensions.ExtractRetryCircuitBreaker()
                             ?? pathItem.Extensions.ExtractRetryCircuitBreaker()
                             ?? document.Extensions.ExtractRetryCircuitBreaker()
                             ?? false;

        var circuitBreakerFailureRatio = operation.Extensions.ExtractCircuitBreakerFailureRatio()
                                          ?? pathItem.Extensions.ExtractCircuitBreakerFailureRatio()
                                          ?? document.Extensions.ExtractCircuitBreakerFailureRatio()
                                          ?? 0.5;

        var circuitBreakerSamplingDuration = operation.Extensions.ExtractCircuitBreakerSamplingDurationSeconds()
                                              ?? pathItem.Extensions.ExtractCircuitBreakerSamplingDurationSeconds()
                                              ?? document.Extensions.ExtractCircuitBreakerSamplingDurationSeconds()
                                              ?? 30.0;

        var circuitBreakerMinimumThroughput = operation.Extensions.ExtractCircuitBreakerMinimumThroughput()
                                               ?? pathItem.Extensions.ExtractCircuitBreakerMinimumThroughput()
                                               ?? document.Extensions.ExtractCircuitBreakerMinimumThroughput()
                                               ?? 10;

        var circuitBreakerBreakDuration = operation.Extensions.ExtractCircuitBreakerBreakDurationSeconds()
                                           ?? pathItem.Extensions.ExtractCircuitBreakerBreakDurationSeconds()
                                           ?? document.Extensions.ExtractCircuitBreakerBreakDurationSeconds()
                                           ?? 30.0;

        var handle429 = operation.Extensions.ExtractRetryHandle429()
                        ?? pathItem.Extensions.ExtractRetryHandle429()
                        ?? document.Extensions.ExtractRetryHandle429()
                        ?? true;

        var backoffType = ParseBackoffType(backoffString);

        return new RetryConfiguration
        {
            Enabled = true,
            Policy = policy,
            MaxAttempts = maxAttempts,
            DelaySeconds = delaySeconds,
            BackoffType = backoffType,
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
    /// Checks if the document has any retry/resilience configuration.
    /// </summary>
    /// <param name="document">The OpenAPI document.</param>
    /// <returns>True if retry is configured at any level.</returns>
    public static bool HasRetryConfiguration(this OpenApiDocument document)
    {
        // Check document-level
        if (!string.IsNullOrEmpty(document.Extensions.ExtractRetryPolicy()))
        {
            return true;
        }

        // Check path and operation levels
        if (document.Paths == null)
        {
            return false;
        }

        foreach (var pathPair in document.Paths)
        {
            if (pathPair.Value is not OpenApiPathItem pathItem)
            {
                continue;
            }

            // Check path-level
            if (!string.IsNullOrEmpty(pathItem.Extensions.ExtractRetryPolicy()))
            {
                return true;
            }

            // Check operation-level
            if (pathItem.Operations == null)
            {
                continue;
            }

            foreach (var operationPair in pathItem.Operations)
            {
                if (!string.IsNullOrEmpty(operationPair.Value?.Extensions.ExtractRetryPolicy()))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Extracts a string value from an OpenAPI extension.
    /// </summary>
    private static string? ExtractStringValue(
        IDictionary<string, IOpenApiExtension>? extensions,
        string key)
    {
        if (extensions is null ||
            !extensions.TryGetValue(key, out var extension) ||
            extension is null)
        {
            return null;
        }

        // Use reflection to access Node property (Microsoft.OpenApi v3.0.1 pattern)
        var extensionType = extension.GetType();
        var nodeProperty = extensionType.GetProperty("Node");
        if (nodeProperty == null)
        {
            return null;
        }

        var node = nodeProperty.GetValue(extension);
        if (node is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var stringValue))
        {
            return stringValue;
        }

        return null;
    }

    /// <summary>
    /// Extracts a boolean value from an OpenAPI extension.
    /// </summary>
    private static bool? ExtractBoolValue(
        IDictionary<string, IOpenApiExtension>? extensions,
        string key)
    {
        if (extensions is null ||
            !extensions.TryGetValue(key, out var extension) ||
            extension is null)
        {
            return null;
        }

        // Use reflection to access Node property (Microsoft.OpenApi v3.0.1 pattern)
        var extensionType = extension.GetType();
        var nodeProperty = extensionType.GetProperty("Node");
        if (nodeProperty == null)
        {
            return null;
        }

        var node = nodeProperty.GetValue(extension);
        if (node is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out var boolValue))
        {
            return boolValue;
        }

        return null;
    }

    /// <summary>
    /// Extracts an integer value from an OpenAPI extension.
    /// </summary>
    private static int? ExtractIntValue(
        IDictionary<string, IOpenApiExtension>? extensions,
        string key)
    {
        if (extensions is null ||
            !extensions.TryGetValue(key, out var extension) ||
            extension is null)
        {
            return null;
        }

        // Use reflection to access Node property (Microsoft.OpenApi v3.0.1 pattern)
        var extensionType = extension.GetType();
        var nodeProperty = extensionType.GetProperty("Node");
        if (nodeProperty == null)
        {
            return null;
        }

        var node = nodeProperty.GetValue(extension);
        if (node is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<int>(out var intValue))
            {
                return intValue;
            }

            // Also try long and convert to int
            if (jsonValue.TryGetValue<long>(out var longValue))
            {
                return (int)longValue;
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts a double value from an OpenAPI extension.
    /// </summary>
    private static double? ExtractDoubleValue(
        IDictionary<string, IOpenApiExtension>? extensions,
        string key)
    {
        if (extensions is null ||
            !extensions.TryGetValue(key, out var extension) ||
            extension is null)
        {
            return null;
        }

        // Use reflection to access Node property (Microsoft.OpenApi v3.0.1 pattern)
        var extensionType = extension.GetType();
        var nodeProperty = extensionType.GetProperty("Node");
        if (nodeProperty == null)
        {
            return null;
        }

        var node = nodeProperty.GetValue(extension);
        if (node is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<double>(out var doubleValue))
            {
                return doubleValue;
            }

            // Also try int/long and convert to double
            if (jsonValue.TryGetValue<int>(out var intValue))
            {
                return intValue;
            }

            if (jsonValue.TryGetValue<long>(out var longValue))
            {
                return longValue;
            }
        }

        return null;
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
}
