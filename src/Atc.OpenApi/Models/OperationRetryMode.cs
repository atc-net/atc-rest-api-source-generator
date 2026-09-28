namespace Atc.OpenApi.Models;

/// <summary>
/// How a generated client applies retry to one operation.
/// </summary>
public enum OperationRetryMode
{
    /// <summary>
    /// Nothing to tell the client: it uses its default policy (a safe method) or no retry (POST/PATCH).
    /// </summary>
    Default,

    /// <summary>
    /// Retry with <see cref="OperationRetry.PolicyName"/> (the client default when it is null).
    /// </summary>
    Retry,

    /// <summary>
    /// Do not retry. The policy's timeout and circuit breaker still apply.
    /// </summary>
    NoRetry,
}
