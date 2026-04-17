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

        return MatchSegments(pathSegments, patternSegments);
    }

    // Dynamic programming match: dp[i, j] is true iff pathSegments[i..] matches patternSegments[j..].
    // Each state is evaluated once, giving O(pathLen * patternLen) worst case — avoids the
    // exponential backtracking the recursive version could incur on inputs like "/**/a/**/b/**/c".
    [SuppressMessage("Performance", "CA1814:Prefer jagged arrays over multidimensional", Justification = "Dense DP lookup with bounded small dimensions; rectangular bool[,] is clearer and avoids per-row allocation.")]
    private static bool MatchSegments(
        string[] pathSegments,
        string[] patternSegments)
    {
        var p = pathSegments.Length;
        var q = patternSegments.Length;

        var dp = new bool[p + 1, q + 1];
        dp[p, q] = true;

        for (var j = q - 1; j >= 0; j--)
        {
            var patternSeg = patternSegments[j];

            if (patternSeg == "**")
            {
                // ** matches zero or more path segments.
                for (var i = p; i >= 0; i--)
                {
                    dp[i, j] = dp[i, j + 1] || (i < p && dp[i + 1, j]);
                }
            }
            else
            {
                for (var i = p - 1; i >= 0; i--)
                {
                    var literalMatch = patternSeg == "*"
                        || string.Equals(pathSegments[i], patternSeg, StringComparison.OrdinalIgnoreCase);
                    dp[i, j] = literalMatch && dp[i + 1, j + 1];
                }
            }
        }

        return dp[0, 0];
    }
}