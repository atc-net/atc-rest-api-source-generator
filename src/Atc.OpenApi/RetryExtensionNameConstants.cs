namespace Atc.OpenApi;

/// <summary>
/// Constant names for custom OpenAPI retry/resilience extension tags.
/// </summary>
/// <remarks>
/// These extensions are specific to the ATC generator and allow configuration of
/// client-side resilience at the root, path, and operation levels using
/// Microsoft.Extensions.Http.Resilience (Polly v8):
/// - x-retry-policy: Named resilience policy to apply
/// - x-retry-enabled: Enables/disables retry
/// - x-retry-max-attempts: Maximum retry attempts
/// - x-retry-delay-seconds: Initial delay between retries
/// - x-retry-backoff: Backoff strategy (constant, linear, exponential)
/// - x-retry-use-jitter: Add randomness to delays
/// - x-retry-timeout-seconds: Per-attempt timeout
/// - x-retry-circuit-breaker: Enable circuit breaker
/// - x-retry-handle-429: Respect Retry-After header
/// </remarks>
public static class RetryExtensionNameConstants
{
    /// <summary>
    /// Extension tag for specifying the resilience policy name.
    /// Type: string
    /// Example: x-retry-policy: "standard"
    /// </summary>
    public const string Policy = "x-retry-policy";

    /// <summary>
    /// Extension tag for enabling or disabling retry.
    /// Type: boolean
    /// Example: x-retry-enabled: false
    /// </summary>
    public const string Enabled = "x-retry-enabled";

    /// <summary>
    /// Extension tag for specifying the maximum number of retry attempts.
    /// Type: integer
    /// Example: x-retry-max-attempts: 3
    /// </summary>
    public const string MaxAttempts = "x-retry-max-attempts";

    /// <summary>
    /// Extension tag for specifying the initial delay between retries in seconds.
    /// Type: number
    /// Example: x-retry-delay-seconds: 1.0
    /// </summary>
    public const string DelaySeconds = "x-retry-delay-seconds";

    /// <summary>
    /// Extension tag for specifying the backoff strategy.
    /// Type: string (constant, linear, exponential)
    /// Example: x-retry-backoff: "exponential"
    /// </summary>
    public const string Backoff = "x-retry-backoff";

    /// <summary>
    /// Extension tag for enabling jitter (randomness) in retry delays.
    /// Type: boolean
    /// Example: x-retry-use-jitter: true
    /// </summary>
    public const string UseJitter = "x-retry-use-jitter";

    /// <summary>
    /// Extension tag for specifying the per-attempt timeout in seconds.
    /// Type: number
    /// Example: x-retry-timeout-seconds: 30
    /// </summary>
    public const string TimeoutSeconds = "x-retry-timeout-seconds";

    /// <summary>
    /// Extension tag for enabling circuit breaker.
    /// Type: boolean
    /// Example: x-retry-circuit-breaker: true
    /// </summary>
    public const string CircuitBreaker = "x-retry-circuit-breaker";

    /// <summary>
    /// Extension tag for specifying the circuit breaker failure ratio threshold.
    /// Type: number (0.0-1.0)
    /// Example: x-retry-cb-failure-ratio: 0.5
    /// </summary>
    public const string CircuitBreakerFailureRatio = "x-retry-cb-failure-ratio";

    /// <summary>
    /// Extension tag for specifying the circuit breaker sampling duration in seconds.
    /// Type: number
    /// Example: x-retry-cb-sampling-duration-seconds: 30
    /// </summary>
    public const string CircuitBreakerSamplingDurationSeconds = "x-retry-cb-sampling-duration-seconds";

    /// <summary>
    /// Extension tag for specifying the circuit breaker minimum throughput.
    /// Type: integer
    /// Example: x-retry-cb-minimum-throughput: 10
    /// </summary>
    public const string CircuitBreakerMinimumThroughput = "x-retry-cb-minimum-throughput";

    /// <summary>
    /// Extension tag for specifying the circuit breaker break duration in seconds.
    /// Type: number
    /// Example: x-retry-cb-break-duration-seconds: 30
    /// </summary>
    public const string CircuitBreakerBreakDurationSeconds = "x-retry-cb-break-duration-seconds";

    /// <summary>
    /// Extension tag for enabling Retry-After header handling for 429 responses.
    /// Type: boolean
    /// Example: x-retry-handle-429: true
    /// </summary>
    public const string Handle429 = "x-retry-handle-429";
}
