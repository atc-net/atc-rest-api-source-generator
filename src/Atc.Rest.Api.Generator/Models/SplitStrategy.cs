namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// Strategy for splitting an OpenAPI specification.
/// </summary>
public enum SplitStrategy
{
    /// <summary>
    /// Split by OpenAPI operation tags.
    /// </summary>
    ByTag,

    /// <summary>
    /// Split by first path segment (e.g., /users, /accounts).
    /// </summary>
    ByPathSegment,

    /// <summary>
    /// Smart domain-based splitting using schema relationships.
    /// </summary>
    ByDomain,
}