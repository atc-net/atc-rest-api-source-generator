namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// Merge strategy for different OpenAPI sections.
/// </summary>
public enum MergeStrategy
{
    /// <summary>
    /// Error if duplicate found.
    /// </summary>
    ErrorOnDuplicate,

    /// <summary>
    /// Merge if values are identical, otherwise error.
    /// </summary>
    MergeIfIdentical,

    /// <summary>
    /// Append unique values (for arrays like tags).
    /// </summary>
    AppendUnique,

    /// <summary>
    /// First file wins (skip duplicates).
    /// </summary>
    FirstWins,

    /// <summary>
    /// Last file wins (overwrite duplicates).
    /// </summary>
    LastWins,
}