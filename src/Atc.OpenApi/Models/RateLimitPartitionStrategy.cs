namespace Atc.OpenApi.Models;

/// <summary>
/// Specifies the rate limit partitioning strategy to use.
/// </summary>
public enum RateLimitPartitionStrategy
{
    /// <summary>
    /// One shared bucket per policy for all callers.
    /// This is the default and matches the current, non-partitioned behavior.
    /// </summary>
    Global,

    /// <summary>
    /// Partitions by the remote IP address of the caller.
    /// </summary>
    Ip,

    /// <summary>
    /// Partitions by an authenticated-user claim.
    /// </summary>
    User,
}
