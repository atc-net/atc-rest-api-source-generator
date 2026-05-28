namespace Atc.Rest.Api.Generator.Cli.Helpers;

/// <summary>
/// Helper methods for resolving file paths in CLI commands.
/// </summary>
internal static class PathHelper
{
    // Pattern to strip bin output folder: bin/Debug/net10.0/ or bin/Release/net8.0/ etc.
    private static readonly Regex BinOutputPattern = new(
        @"[/\\]bin[/\\](?:Debug|Release)[/\\]net\d+\.\d+[/\\]",
        RegexOptions.IgnoreCase | RegexOptions.NonBacktracking);

    // Pattern to strip source project folder: src/ProjectName/
    private static readonly Regex SourceProjectPattern = new(
        @"[/\\]src[/\\][^/\\]+[/\\]",
        RegexOptions.IgnoreCase | RegexOptions.NonBacktracking);

    /// <summary>
    /// Resolves a relative path to an absolute path. When the current working directory
    /// matches a build-output (<c>bin/&lt;Config&gt;/net*/</c>) or source-project
    /// (<c>src/&lt;Project&gt;/</c>) pattern — typically because the user invoked the CLI
    /// via <c>dotnet run</c> from inside its own project — the matched segments are
    /// stripped from the CWD prefix of the result so spec/output paths resolve against
    /// the repo root instead of the build-output folder.
    /// </summary>
    /// <remarks>
    /// The strip is anchored to the CWD prefix only. Segments the user typed in the
    /// argument itself are preserved verbatim — so a spec at <c>src/specs/api.yaml</c>
    /// is reachable from a sibling project under <c>src/</c>.
    /// </remarks>
    /// <param name="path">The path to resolve.</param>
    /// <returns>The resolved absolute path.</returns>
    public static string ResolveRelativePath(string path)
    {
        if (!path.StartsWith('.'))
        {
            return path;
        }

        var resolvedPath = Path.GetFullPath(path);
        var sep = Path.DirectorySeparatorChar.ToString();

        // The bin/src regexes require [/\\] on BOTH sides; CWD from Directory.GetCurrentDirectory
        // does not have a trailing separator. Append one before pattern-matching, or a CWD
        // that ENDS with the matched segment (e.g. C:\repo\src\MyProject) won't fire.
        var cwdWithSep = Directory.GetCurrentDirectory();
        cwdWithSep = Path.GetFullPath(cwdWithSep);
        if (!cwdWithSep.EndsWith(sep, StringComparison.Ordinal))
        {
            cwdWithSep += sep;
        }

        var cleanedCwd = SourceProjectPattern.Replace(
            BinOutputPattern.Replace(cwdWithSep, sep),
            sep);

        // No strippable shape in CWD — nothing to rewrite.
        if (string.Equals(cleanedCwd, cwdWithSep, StringComparison.Ordinal))
        {
            return resolvedPath;
        }

        // Only rewrite when the resolved path actually starts with the dirty CWD. A
        // relative path that navigated UP out of the CWD (e.g. ../../sibling/file.yaml)
        // produces a resolvedPath that does NOT share the CWD prefix, and the original
        // bug was caused by stripping segments outside that prefix.
        if (resolvedPath.StartsWith(cwdWithSep, StringComparison.OrdinalIgnoreCase))
        {
            return string.Concat(cleanedCwd, resolvedPath.AsSpan(cwdWithSep.Length));
        }

        return resolvedPath;
    }
}