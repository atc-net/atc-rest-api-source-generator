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
    /// Resolves a relative path to an absolute path, stripping build output folder patterns
    /// if the current working directory is within the project structure.
    /// </summary>
    /// <param name="path">The path to resolve.</param>
    /// <returns>The resolved absolute path.</returns>
    public static string ResolveRelativePath(string path)
    {
        if (!path.StartsWith('.'))
        {
            return path;
        }

        var resolvedPath = Path.GetFullPath(path);

        // Strip build output folder patterns if CWD is within the project structure
        resolvedPath = BinOutputPattern.Replace(resolvedPath, Path.DirectorySeparatorChar.ToString());
        resolvedPath = SourceProjectPattern.Replace(resolvedPath, Path.DirectorySeparatorChar.ToString());

        return resolvedPath;
    }
}