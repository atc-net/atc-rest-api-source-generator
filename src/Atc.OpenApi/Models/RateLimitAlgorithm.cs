namespace Atc.OpenApi.Models;

/// <summary>
/// Specifies the rate limiting algorithm to use.
/// </summary>
public enum RateLimitAlgorithm
{
    /// <summary>
    /// Fixed window rate limiter.
    /// Requests are counted within fixed time windows.
    /// </summary>
    Fixed,

    /// <summary>
    /// Sliding window rate limiter.
    /// Provides smoother distribution than fixed window.
    /// </summary>
    Sliding,

    /// <summary>
    /// Token bucket rate limiter.
    /// Allows bursts while maintaining average rate.
    /// </summary>
    TokenBucket,

    /// <summary>
    /// Concurrency limiter.
    /// Limits the number of concurrent requests.
    /// </summary>
    Concurrency,
}
