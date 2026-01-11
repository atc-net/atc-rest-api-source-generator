namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// Contains information about which namespace types exist for a path segment.
/// </summary>
/// <param name="HasHandlers">Whether the segment has handler interfaces.</param>
/// <param name="HasResults">Whether the segment has result classes.</param>
/// <param name="HasParameters">Whether the segment has parameter records.</param>
/// <param name="HasModels">Whether the segment has segment-specific model classes.</param>
public record PathSegmentNamespaces(
    bool HasHandlers,
    bool HasResults,
    bool HasParameters,
    bool HasModels);