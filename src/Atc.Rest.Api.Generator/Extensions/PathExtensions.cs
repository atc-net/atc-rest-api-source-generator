namespace Atc.Rest.Api.Generator.Extensions;

/// <summary>
/// Extension methods for path-related operations.
/// </summary>
public static class PathExtensions
{
    /// <summary>
    /// Checks if a path matches the specified path segment filter.
    /// </summary>
    /// <param name="path">The API path (e.g., "/pets/{petId}").</param>
    /// <param name="pathSegment">The path segment filter (e.g., "Pets"). Null or empty means match all.</param>
    /// <returns>True if the path matches the filter, false otherwise.</returns>
    public static bool MatchesPathSegment(
        this string path,
        string? pathSegment)
    {
        if (string.IsNullOrEmpty(pathSegment))
        {
            return true;
        }

        var currentSegment = PathSegmentHelper.GetFirstPathSegment(path);
        return currentSegment.Equals(pathSegment, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if a path should be skipped based on the path segment filter.
    /// </summary>
    /// <param name="path">The API path.</param>
    /// <param name="pathSegment">The path segment filter.</param>
    /// <returns>True if the path should be skipped, false if it should be processed.</returns>
    public static bool ShouldSkipForPathSegment(
        this string path,
        string? pathSegment)
        => !path.MatchesPathSegment(pathSegment);
}