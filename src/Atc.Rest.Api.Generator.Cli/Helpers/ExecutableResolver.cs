namespace Atc.Rest.Api.Generator.Cli.Helpers;

/// <summary>
/// Resolves a bare executable name to its absolute path by searching the PATH
/// environment variable, rather than handing an unqualified command name to
/// <see cref="System.Diagnostics.Process"/> and relying on its implicit search.
/// </summary>
internal static class ExecutableResolver
{
    /// <summary>
    /// Resolves <paramref name="executableName"/> to an absolute path by searching the
    /// directories in the PATH environment variable (and PATHEXT extensions on Windows).
    /// Returns the original name unchanged if it could not be resolved, so callers can
    /// still attempt to start the process and handle the resulting failure.
    /// </summary>
    /// <param name="executableName">The bare executable name (e.g. "git").</param>
    /// <returns>The resolved absolute path, or <paramref name="executableName"/> if not found.</returns>
    public static string Resolve(string executableName)
    {
        if (Path.IsPathRooted(executableName))
        {
            return executableName;
        }

        var pathVariable = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathVariable))
        {
            return executableName;
        }

        var extensions = OperatingSystem.IsWindows()
            ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD")
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
            : [string.Empty];

        foreach (var directory in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var extension in extensions)
            {
                var candidate = Path.Combine(directory, executableName + extension);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return executableName;
    }
}