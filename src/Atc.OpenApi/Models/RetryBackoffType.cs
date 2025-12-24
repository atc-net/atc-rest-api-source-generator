namespace Atc.OpenApi.Models;

/// <summary>
/// Specifies the backoff strategy for retry delays.
/// </summary>
public enum RetryBackoffType
{
    /// <summary>
    /// Constant delay between retries.
    /// Each retry waits the same amount of time.
    /// </summary>
    Constant,

    /// <summary>
    /// Linearly increasing delay between retries.
    /// Delay increases by the initial delay amount with each retry.
    /// </summary>
    Linear,

    /// <summary>
    /// Exponentially increasing delay between retries.
    /// Delay doubles (approximately) with each retry.
    /// </summary>
    Exponential,
}
