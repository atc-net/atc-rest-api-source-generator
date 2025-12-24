namespace Atc.OpenApi.Models;

/// <summary>
/// Represents the retry/resilience configuration extracted from OpenAPI extensions.
/// </summary>
public record RetryConfiguration
{
    /// <summary>
    /// Gets a value indicating whether retry is enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Gets the name of the resilience policy.
    /// </summary>
    public string? Policy { get; init; }

    /// <summary>
    /// Gets the maximum number of retry attempts.
    /// </summary>
    public int MaxAttempts { get; init; } = 3;

    /// <summary>
    /// Gets the initial delay between retries in seconds.
    /// </summary>
    public double DelaySeconds { get; init; } = 1.0;

    /// <summary>
    /// Gets the backoff strategy for retry delays.
    /// </summary>
    public RetryBackoffType BackoffType { get; init; } = RetryBackoffType.Exponential;

    /// <summary>
    /// Gets a value indicating whether to add jitter (randomness) to retry delays.
    /// </summary>
    public bool UseJitter { get; init; } = true;

    /// <summary>
    /// Gets the per-attempt timeout in seconds, or null if not specified.
    /// </summary>
    public double? TimeoutSeconds { get; init; }

    /// <summary>
    /// Gets a value indicating whether circuit breaker is enabled.
    /// </summary>
    public bool CircuitBreakerEnabled { get; init; }

    /// <summary>
    /// Gets the circuit breaker failure ratio threshold (0.0-1.0).
    /// Circuit opens when failure ratio exceeds this value during sampling window.
    /// </summary>
    public double CircuitBreakerFailureRatio { get; init; } = 0.5;

    /// <summary>
    /// Gets the circuit breaker sampling duration in seconds.
    /// The time window over which failures are measured.
    /// </summary>
    public double CircuitBreakerSamplingDurationSeconds { get; init; } = 30.0;

    /// <summary>
    /// Gets the minimum number of requests before the circuit breaker evaluates the failure ratio.
    /// </summary>
    public int CircuitBreakerMinimumThroughput { get; init; } = 10;

    /// <summary>
    /// Gets the duration in seconds the circuit breaker stays in the open state.
    /// </summary>
    public double CircuitBreakerBreakDurationSeconds { get; init; } = 30.0;

    /// <summary>
    /// Gets a value indicating whether to handle 429 responses with Retry-After header.
    /// </summary>
    public bool Handle429 { get; init; } = true;
}
