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
        /// Extracts the x-retry-total-timeout-seconds value from extensions.
        /// </summary>
        /// <returns>The timeout over all attempts in seconds, or null if not specified.</returns>
        public double? ExtractRetryTotalTimeoutSeconds()
            => ExtractDoubleValue(extensions, RetryExtensionNameConstants.TotalTimeoutSeconds);

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
        IOpenApiPathItem pathItem,
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

        var totalTimeoutSeconds = operation.Extensions.ExtractRetryTotalTimeoutSeconds()
                                  ?? pathItem.Extensions.ExtractRetryTotalTimeoutSeconds()
                                  ?? document.Extensions.ExtractRetryTotalTimeoutSeconds();

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
            TotalTimeoutSeconds = totalTimeoutSeconds,
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
        if (document.Paths is null)
        {
            return false;
        }

        foreach (var pathPair in document.Paths)
        {
            if (pathPair.Value is not IOpenApiPathItem pathItem)
            {
                continue;
            }

            // Check path-level
            if (!string.IsNullOrEmpty(pathItem.Extensions.ExtractRetryPolicy()))
            {
                return true;
            }

            // Check operation-level
            if (pathItem.Operations is null)
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
    /// Returns true for the HTTP methods a client does not retry unless the operation opts in: POST and PATCH
    /// are not idempotent, so resending one after a timeout or 5xx can apply it twice.
    /// </summary>
    /// <param name="httpMethod">The HTTP method (any casing).</param>
    /// <returns>True for POST and PATCH.</returns>
    public static bool IsRetryUnsafeHttpMethod(string httpMethod)
        => string.Equals(httpMethod, "POST", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(httpMethod, "PATCH", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Resolves how a generated client applies retry to one operation.
    /// <list type="bullet">
    ///   <item><description><c>x-retry-enabled</c> inherits operation → path → document; <c>false</c> means no retry.</description></item>
    ///   <item><description>POST and PATCH are only retried when the <b>operation</b> opts in with <c>x-retry-enabled: true</c> or its own <c>x-retry-policy</c>.</description></item>
    ///   <item><description>The operation's policy is its own or its path's <c>x-retry-policy</c>; without one the client default applies.</description></item>
    ///   <item><description>"No retry" keeps the policy's timeout and circuit breaker; an operation (or policy-less path) <c>x-retry-timeout-seconds</c> next to it is carried along.</description></item>
    /// </list>
    /// </summary>
    /// <param name="operation">The OpenAPI operation.</param>
    /// <param name="pathItem">The path item containing the operation.</param>
    /// <param name="document">The OpenAPI document.</param>
    /// <param name="httpMethod">The HTTP method of the operation.</param>
    /// <returns>The resolved retry behavior.</returns>
    public static OperationRetry ResolveOperationRetry(
        this OpenApiOperation operation,
        IOpenApiPathItem pathItem,
        OpenApiDocument document,
        string httpMethod)
    {
        if (operation is null)
        {
            throw new ArgumentNullException(nameof(operation));
        }

        if (pathItem is null)
        {
            throw new ArgumentNullException(nameof(pathItem));
        }

        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        var operationPolicy = NullIfEmpty(operation.Extensions.ExtractRetryPolicy());
        var pathPolicy = NullIfEmpty(pathItem.Extensions.ExtractRetryPolicy());
        var ownPolicy = operationPolicy ?? pathPolicy;

        var operationEnabled = operation.Extensions.ExtractRetryEnabled();
        var enabled = operationEnabled
                      ?? pathItem.Extensions.ExtractRetryEnabled()
                      ?? document.Extensions.ExtractRetryEnabled();

        var isUnsafe = IsRetryUnsafeHttpMethod(httpMethod);
        var optedIn = operationEnabled == true || operationPolicy is not null;

        if (enabled == false || (isUnsafe && !optedIn))
        {
            var timeout = operation.Extensions.ExtractRetryTimeoutSeconds()
                          ?? (pathPolicy is null ? pathItem.Extensions.ExtractRetryTimeoutSeconds() : null);

            return new OperationRetry(OperationRetryMode.NoRetry, ownPolicy, timeout);
        }

        return ownPolicy is null && !isUnsafe
            ? OperationRetry.Default
            : new OperationRetry(OperationRetryMode.Retry, ownPolicy);
    }

    /// <summary>
    /// Collects every named retry policy (<c>x-retry-policy</c>) of the document, the document-level one first.
    /// A policy's values come from where its name is declared, inheriting operation → path → document;
    /// the first declaration of a name wins. Policies of disabled operations are included, because a
    /// "no retry" operation still uses its policy's timeout and circuit breaker.
    /// </summary>
    /// <param name="document">The OpenAPI document.</param>
    /// <param name="includeDeprecated">Whether to include deprecated operations.</param>
    /// <returns>The named policies in declaration order.</returns>
    public static IReadOnlyList<RetryConfiguration> CollectNamedRetryPolicies(
        this OpenApiDocument document,
        bool includeDeprecated = true)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var policies = new List<RetryConfiguration>();

        void Add(
            IDictionary<string, IOpenApiExtension>? operationExtensions,
            IDictionary<string, IOpenApiExtension>? pathExtensions)
        {
            var config = BuildRetryConfiguration(operationExtensions, pathExtensions, document.Extensions);
            if (config?.Policy is { } name && seen.Add(name))
            {
                policies.Add(config);
            }
        }

        if (NullIfEmpty(document.Extensions.ExtractRetryPolicy()) is not null)
        {
            Add(operationExtensions: null, pathExtensions: null);
        }

        if (document.Paths is null)
        {
            return policies;
        }

        foreach (var pathPair in document.Paths)
        {
            if (pathPair.Value is not IOpenApiPathItem pathItem)
            {
                continue;
            }

            if (NullIfEmpty(pathItem.Extensions.ExtractRetryPolicy()) is not null)
            {
                Add(operationExtensions: null, pathItem.Extensions);
            }

            if (pathItem.Operations is null)
            {
                continue;
            }

            foreach (var operationPair in pathItem.Operations)
            {
                var operation = operationPair.Value;
                if (operation is null ||
                    (!includeDeprecated && operation.Deprecated))
                {
                    continue;
                }

                if (NullIfEmpty(operation.Extensions.ExtractRetryPolicy()) is not null)
                {
                    Add(operation.Extensions, pathItem.Extensions);
                }
            }
        }

        return policies;
    }

    /// <summary>
    /// Builds the policy declared at the most specific of the given levels, with every value inherited
    /// operation → path → document. Returns null when no level names a policy.
    /// </summary>
    private static RetryConfiguration? BuildRetryConfiguration(
        IDictionary<string, IOpenApiExtension>? operationExtensions,
        IDictionary<string, IOpenApiExtension>? pathExtensions,
        IDictionary<string, IOpenApiExtension>? documentExtensions)
    {
        var policy = NullIfEmpty(operationExtensions.ExtractRetryPolicy())
                     ?? NullIfEmpty(pathExtensions.ExtractRetryPolicy())
                     ?? NullIfEmpty(documentExtensions.ExtractRetryPolicy());

        if (policy is null)
        {
            return null;
        }

        return new RetryConfiguration
        {
            Enabled = true,
            Policy = policy,
            MaxAttempts = operationExtensions.ExtractMaxRetryAttempts()
                          ?? pathExtensions.ExtractMaxRetryAttempts()
                          ?? documentExtensions.ExtractMaxRetryAttempts()
                          ?? 3,
            DelaySeconds = operationExtensions.ExtractRetryDelaySeconds()
                           ?? pathExtensions.ExtractRetryDelaySeconds()
                           ?? documentExtensions.ExtractRetryDelaySeconds()
                           ?? 1.0,
            BackoffType = ParseBackoffType(
                operationExtensions.ExtractRetryBackoff()
                ?? pathExtensions.ExtractRetryBackoff()
                ?? documentExtensions.ExtractRetryBackoff()),
            UseJitter = operationExtensions.ExtractRetryUseJitter()
                        ?? pathExtensions.ExtractRetryUseJitter()
                        ?? documentExtensions.ExtractRetryUseJitter()
                        ?? true,
            TimeoutSeconds = operationExtensions.ExtractRetryTimeoutSeconds()
                             ?? pathExtensions.ExtractRetryTimeoutSeconds()
                             ?? documentExtensions.ExtractRetryTimeoutSeconds(),
            TotalTimeoutSeconds = operationExtensions.ExtractRetryTotalTimeoutSeconds()
                                  ?? pathExtensions.ExtractRetryTotalTimeoutSeconds()
                                  ?? documentExtensions.ExtractRetryTotalTimeoutSeconds(),
            CircuitBreakerEnabled = operationExtensions.ExtractRetryCircuitBreaker()
                                    ?? pathExtensions.ExtractRetryCircuitBreaker()
                                    ?? documentExtensions.ExtractRetryCircuitBreaker()
                                    ?? false,
            CircuitBreakerFailureRatio = operationExtensions.ExtractCircuitBreakerFailureRatio()
                                         ?? pathExtensions.ExtractCircuitBreakerFailureRatio()
                                         ?? documentExtensions.ExtractCircuitBreakerFailureRatio()
                                         ?? 0.5,
            CircuitBreakerSamplingDurationSeconds = operationExtensions.ExtractCircuitBreakerSamplingDurationSeconds()
                                                    ?? pathExtensions.ExtractCircuitBreakerSamplingDurationSeconds()
                                                    ?? documentExtensions.ExtractCircuitBreakerSamplingDurationSeconds()
                                                    ?? 30.0,
            CircuitBreakerMinimumThroughput = operationExtensions.ExtractCircuitBreakerMinimumThroughput()
                                              ?? pathExtensions.ExtractCircuitBreakerMinimumThroughput()
                                              ?? documentExtensions.ExtractCircuitBreakerMinimumThroughput()
                                              ?? 10,
            CircuitBreakerBreakDurationSeconds = operationExtensions.ExtractCircuitBreakerBreakDurationSeconds()
                                                 ?? pathExtensions.ExtractCircuitBreakerBreakDurationSeconds()
                                                 ?? documentExtensions.ExtractCircuitBreakerBreakDurationSeconds()
                                                 ?? 30.0,
            Handle429 = operationExtensions.ExtractRetryHandle429()
                        ?? pathExtensions.ExtractRetryHandle429()
                        ?? documentExtensions.ExtractRetryHandle429()
                        ?? true,
        };
    }

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrEmpty(value) ? null : value;

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
        if (nodeProperty is null)
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
        if (nodeProperty is null)
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
        if (nodeProperty is null)
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

            // Also try long and clamp to int range
            if (jsonValue.TryGetValue<long>(out var longValue))
            {
                return longValue > int.MaxValue ? int.MaxValue : longValue < int.MinValue ? int.MinValue : (int)longValue;
            }

            // YAML-sourced integer scalars are boxed as decimal (JsonValuePrimitive<decimal>)
            // by the OpenAPI YAML reader, not int/long, so neither branch above ever matches
            // for a document parsed from YAML (the common case for this generator).
            if (jsonValue.TryGetValue<decimal>(out var decimalValue))
            {
                return decimalValue > int.MaxValue ? int.MaxValue : decimalValue < int.MinValue ? int.MinValue : (int)decimalValue;
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
        if (nodeProperty is null)
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

            // YAML-sourced numeric scalars (both whole and fractional) are boxed as
            // decimal (JsonValuePrimitive<decimal>) by the OpenAPI YAML reader, not
            // double/int/long, so none of the branches above ever match for a document
            // parsed from YAML (the common case for this generator).
            if (jsonValue.TryGetValue<decimal>(out var decimalValue))
            {
                return (double)decimalValue;
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