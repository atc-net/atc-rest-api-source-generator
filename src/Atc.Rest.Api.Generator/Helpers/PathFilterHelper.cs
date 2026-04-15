namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// Filters OpenAPI paths based on include/exclude glob patterns.
/// Supports wildcard patterns: * matches a single segment, ** matches any number of segments.
/// </summary>
public static class PathFilterHelper
{
    /// <summary>
    /// Filters an OpenAPI document's paths based on include/exclude patterns.
    /// Returns the set of paths that should be processed.
    /// </summary>
    /// <param name="allPaths">All paths from the OpenAPI document.</param>
    /// <param name="includePaths">Glob patterns to include (null = include all).</param>
    /// <param name="excludePaths">Glob patterns to exclude (null = exclude none).</param>
    /// <returns>The filtered set of paths.</returns>
    public static HashSet<string> FilterPaths(
        IEnumerable<string> allPaths,
        IList<string>? includePaths,
        IList<string>? excludePaths)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in allPaths)
        {
            // Apply include filter (null means include all)
            if (includePaths is { Count: > 0 } &&
                !MatchesAnyPattern(path, includePaths))
            {
                continue;
            }

            // Apply exclude filter
            if (excludePaths is { Count: > 0 } &&
                MatchesAnyPattern(path, excludePaths))
            {
                continue;
            }

            result.Add(path);
        }

        return result;
    }

    /// <summary>
    /// Checks if a path matches any of the given glob patterns.
    /// </summary>
    public static bool MatchesAnyPattern(
        string path,
        IList<string> patterns)
    {
        foreach (var pattern in patterns)
        {
            if (MatchesPattern(path, pattern))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if a path matches a glob pattern.
    /// Supports: * (single segment), ** (any segments), literal text.
    /// </summary>
    [SuppressMessage("", "S1075:URIs should not be hardcoded", Justification = "OpenAPI path separators, not file paths.")]
    public static bool MatchesPattern(
        string path,
        string pattern)
    {
        // Normalize: ensure both start with /
        if (!path.StartsWith("/", StringComparison.Ordinal))
        {
            path = "/" + path;
        }

        if (!pattern.StartsWith("/", StringComparison.Ordinal))
        {
            pattern = "/" + pattern;
        }

        var pathSegments = path.Split('/');
        var patternSegments = pattern.Split('/');

        return MatchSegments(pathSegments, 0, patternSegments, 0);
    }

    private static bool MatchSegments(
        string[] pathSegments,
        int pathIndex,
        string[] patternSegments,
        int patternIndex)
    {
        while (patternIndex < patternSegments.Length)
        {
            var patternSeg = patternSegments[patternIndex];

            if (patternSeg == "**")
            {
                // ** matches zero or more segments
                // Try matching the rest of the pattern against every suffix of the path
                for (var i = pathIndex; i <= pathSegments.Length; i++)
                {
                    if (MatchSegments(pathSegments, i, patternSegments, patternIndex + 1))
                    {
                        return true;
                    }
                }

                return false;
            }

            if (pathIndex >= pathSegments.Length)
            {
                return false;
            }

            if (patternSeg == "*")
            {
                // * matches exactly one segment
                pathIndex++;
                patternIndex++;
                continue;
            }

            // Literal match (case-insensitive)
            if (!string.Equals(pathSegments[pathIndex], patternSeg, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            pathIndex++;
            patternIndex++;
        }

        return pathIndex >= pathSegments.Length;
    }
}