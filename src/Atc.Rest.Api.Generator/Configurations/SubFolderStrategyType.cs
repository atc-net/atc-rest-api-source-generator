namespace Atc.Rest.Api.Generator.Configurations;

/// <summary>
/// Defines the strategy for organizing handlers into sub-folders.
/// </summary>
public enum SubFolderStrategyType
{
    /// <summary>
    /// No sub-folders - all handlers in root ApiHandlers folder.
    /// </summary>
    None = 0,

    /// <summary>
    /// Group by first path segment (e.g., /pets/{id} → Pets).
    /// </summary>
    FirstPathSegment = 1,

    /// <summary>
    /// Group by OpenAPI operation tag.
    /// </summary>
    OpenApiTag = 2,
}