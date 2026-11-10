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
    /// Returns a document that holds only the paths the include/exclude patterns allow.
    /// The given document is never changed: it may be a cached instance that other
    /// generators, projects and later builds read too. Without a filter it is returned as is.
    /// </summary>
    /// <param name="document">The parsed OpenAPI document.</param>
    /// <param name="includePaths">Glob patterns to include (null = include all).</param>
    /// <param name="excludePaths">Glob patterns to exclude (null = exclude none).</param>
    /// <returns>The document itself, or a copy with the filtered paths.</returns>
    public static OpenApiDocument WithFilteredPaths(
        OpenApiDocument document,
        IList<string>? includePaths,
        IList<string>? excludePaths)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (includePaths is not { Count: > 0 } && excludePaths is not { Count: > 0 })
        {
            return document;
        }

        var allowedPaths = FilterPaths(document.Paths.Keys, includePaths, excludePaths);

        var filteredPaths = new OpenApiPaths();
        foreach (var path in document.Paths)
        {
            if (allowedPaths.Contains(path.Key))
            {
                filteredPaths.Add(path.Key, path.Value);
            }
        }

        return new OpenApiDocument(document)
        {
            Paths = filteredPaths,
        };
    }

    /// <summary>
    /// Checks if a path matches any of the given glob patterns (see <see cref="MatchesPattern"/>).
    /// </summary>
    /// <param name="path">The OpenAPI path.</param>
    /// <param name="patterns">The glob patterns.</param>
    /// <returns><see langword="true"/> when at least one pattern matches.</returns>
    public static bool MatchesAnyPattern(
        string path,
        IList<string> patterns)
        => GlobHelper.IsMatchAny(path, patterns);

    /// <summary>
    /// Checks if a path matches a glob pattern: <c>**</c> matches any number of segments, <c>*</c> a single segment
    /// (or any characters within one), literal text matches regardless of case.
    /// </summary>
    /// <param name="path">The OpenAPI path.</param>
    /// <param name="pattern">The glob pattern.</param>
    /// <returns><see langword="true"/> when the pattern matches the whole path.</returns>
    public static bool MatchesPattern(
        string path,
        string pattern)
        => GlobHelper.IsMatch(path, pattern);
}