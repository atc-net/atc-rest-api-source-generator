namespace Atc.OpenApi.Models;

/// <summary>
/// The retry behavior of one operation, resolved from the <c>x-retry-*</c> extensions of the operation,
/// its path item and the document, and from its HTTP method.
/// </summary>
/// <param name="Mode">How the client applies retry.</param>
/// <param name="PolicyName">The operation's own policy (declared on the operation or its path), or null for the client default.</param>
/// <param name="TimeoutSeconds">A per-attempt timeout declared next to a disabled retry, or null to keep the policy's.</param>
public sealed record OperationRetry(
    OperationRetryMode Mode,
    string? PolicyName = null,
    double? TimeoutSeconds = null)
{
    /// <summary>
    /// Gets the resolution for an operation that follows the client default.
    /// </summary>
    public static OperationRetry Default { get; } = new(OperationRetryMode.Default);

    /// <summary>
    /// Returns true when the client already behaves this way without being told: the default, or no retry
    /// with the default policy for a method that is not retried by default (POST, PATCH).
    /// </summary>
    /// <param name="httpMethod">The HTTP method of the operation.</param>
    /// <returns>True when nothing needs to be emitted for the operation.</returns>
    public bool IsClientDefault(string httpMethod)
        => Mode == OperationRetryMode.Default ||
           (Mode == OperationRetryMode.NoRetry &&
            PolicyName is null &&
            TimeoutSeconds is null &&
            OpenApiRetryExtensions.IsRetryUnsafeHttpMethod(httpMethod));
}
