namespace Atc.Rest.Api.Generator.Extensions;

/// <summary>
/// Extension members for path-related operations.
/// </summary>
[SuppressMessage("", "CA1034:Do not nest type", Justification = "OK - CLang14 - extension")]
public static class PathExtensions
{
    /// <param name="path">The API path (e.g., "/pets/{petId}").</param>
    extension(string path)
    {
        /// <summary>
        /// Checks if a path matches the specified path segment filter.
        /// </summary>
        /// <param name="pathSegment">The path segment filter (e.g., "Pets"). Null or empty means match all.</param>
        /// <returns>True if the path matches the filter, false otherwise.</returns>
        public bool MatchesPathSegment(string? pathSegment)
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
        /// <param name="pathSegment">The path segment filter.</param>
        /// <returns>True if the path should be skipped, false if it should be processed.</returns>
        public bool ShouldSkipForPathSegment(string? pathSegment)
            => !path.MatchesPathSegment(pathSegment);
    }
}