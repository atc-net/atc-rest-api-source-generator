namespace Atc.Rest.Api.Generator.Cli.Helpers;

/// <summary>
/// Helper methods for resolving file paths in CLI commands.
/// </summary>
internal static class PathHelper
{
    /// <summary>
    /// Resolves a path argument to an absolute path against the current directory and nothing
    /// else: <c>api.yaml</c>, <c>./api.yaml</c> and <c>.\api.yaml</c> all mean the file in the
    /// current directory, and an absolute path is returned normalized.
    /// </summary>
    /// <remarks>
    /// Earlier versions stripped <c>src/&lt;Project&gt;/</c> and <c>bin/&lt;Config&gt;/&lt;tfm&gt;/</c>
    /// segments out of the current directory, so running from <c>C:\repo\src\MyApi</c> with
    /// <c>-s ./api.yaml</c> read <c>C:\repo\api.yaml</c>. The current directory is what the user
    /// sees; it is never rewritten.
    /// </remarks>
    /// <param name="path">The path to resolve.</param>
    /// <returns>The resolved absolute path.</returns>
    public static string ResolveRelativePath(string path)
        => Path.GetFullPath(path, Directory.GetCurrentDirectory());
}