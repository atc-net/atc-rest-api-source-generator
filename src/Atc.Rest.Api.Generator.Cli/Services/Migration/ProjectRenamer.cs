namespace Atc.Rest.Api.Generator.Cli.Services.Migration;

/// <summary>
/// Handles renaming of projects during migration.
/// </summary>
internal static class ProjectRenamer
{
    /// <summary>
    /// Renames a project folder and its .csproj file.
    /// </summary>
    /// <param name="projectDirectory">The current project directory path.</param>
    /// <param name="oldName">The old project name (e.g., "MyProject.Api.Generated").</param>
    /// <param name="newName">The new project name (e.g., "MyProject.Api.Contracts").</param>
    /// <param name="dryRun">If true, only returns what would be renamed.</param>
    /// <param name="wait">How to wait before the folder's move is tried again; the default sleeps.</param>
    /// <returns>The result of the rename operation.</returns>
    public static RenameResult RenameProject(
        string projectDirectory,
        string oldName,
        string newName,
        bool dryRun = false,
        Action<TimeSpan>? wait = null)
    {
        var result = new RenameResult
        {
            OldName = oldName,
            NewName = newName,
            OldPath = projectDirectory,
        };

        if (!Directory.Exists(projectDirectory))
        {
            result.Error = "Project directory not found.";
            return result;
        }

        var parentDirectory = Path.GetDirectoryName(projectDirectory);
        if (string.IsNullOrEmpty(parentDirectory))
        {
            result.Error = "Could not determine parent directory.";
            return result;
        }

        // Calculate new directory name
        var oldDirName = Path.GetFileName(projectDirectory);
        var newDirName = oldDirName.Replace(oldName, newName, StringComparison.OrdinalIgnoreCase);

        // Handle the case where the directory name matches the project name
        if (oldDirName.Equals(oldName, StringComparison.OrdinalIgnoreCase))
        {
            newDirName = newName;
        }

        var newDirectory = Path.Combine(parentDirectory, newDirName);
        result.NewPath = newDirectory;

        // Check if target directory already exists
        if (!projectDirectory.Equals(newDirectory, StringComparison.OrdinalIgnoreCase) &&
            Directory.Exists(newDirectory))
        {
            // Target directory already exists - this could mean:
            // 1. Migration was partially completed before
            // 2. User manually created the target directory
            // The old directory is only removed when nothing but build output is left in it:
            // anything else (hand-written files the cleanup kept) is the user's and stays.
            if (Directory.Exists(projectDirectory))
            {
                var remaining = Directory
                    .EnumerateFiles(projectDirectory, "*", SearchOption.AllDirectories)
                    .Where(f => !IsBuildOutput(projectDirectory, f))
                    .ToList();

                if (remaining.Count > 0)
                {
                    result.Error =
                        $"Both '{projectDirectory}' and '{newDirectory}' exist, and '{oldDirName}' still holds " +
                        $"{remaining.Count} file(s). Move them to '{newDirName}' and delete '{oldDirName}'.";
                    return result;
                }

                if (!dryRun)
                {
                    Directory.Delete(projectDirectory, recursive: true);
                }

                result.OldDirectoryDeleted = true;
            }

            result.Success = true;
            result.AlreadyRenamed = true;
            return result;
        }

        var newCsprojName = $"{newName}.csproj";

        if (!dryRun)
        {
            // The folder first: it is the step something else can block (an open file in it), and a folder that did
            // not move leaves the project exactly as it was - not half renamed
            var moveDirectory = !projectDirectory.Equals(newDirectory, StringComparison.OrdinalIgnoreCase);
            try
            {
                if (moveDirectory)
                {
                    MoveDirectory(projectDirectory, newDirectory, wait ?? Thread.Sleep);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                result.Error = $"Could not rename '{projectDirectory}' to '{newDirName}': {ex.Message} Close what has files in it open and run again.";
                return result;
            }

            var movedDirectory = moveDirectory ? newDirectory : projectDirectory;
            var movedCsprojPath = Path.Combine(movedDirectory, $"{oldName}.csproj");
            try
            {
                if (File.Exists(movedCsprojPath))
                {
                    File.Move(movedCsprojPath, Path.Combine(movedDirectory, newCsprojName));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                result.Error = $"'{movedDirectory}' is renamed, but its project file '{oldName}.csproj' could not be renamed to '{newCsprojName}': {ex.Message}";
                return result;
            }
        }

        result.Success = true;
        return result;
    }

    /// <summary>
    /// Renames Api.Generated to Api.Contracts.
    /// </summary>
    /// <param name="projectDirectory">The Api.Generated project directory.</param>
    /// <param name="projectName">The base project name (e.g., "MyProject").</param>
    /// <param name="dryRun">If true, only returns what would be renamed.</param>
    /// <returns>The result of the rename operation.</returns>
    public static RenameResult RenameServerProject(
        string projectDirectory,
        string projectName,
        bool dryRun = false)
    {
        var oldName = $"{projectName}.Api.Generated";
        var newName = $"{projectName}.Api.Contracts";
        return RenameProject(projectDirectory, oldName, newName, dryRun);
    }

    /// <summary>
    /// Renames ApiClient.Generated to the specified client suffix.
    /// </summary>
    /// <param name="projectDirectory">The ApiClient.Generated project directory.</param>
    /// <param name="projectName">The base project name (e.g., "MyProject").</param>
    /// <param name="clientSuffix">The client suffix to use (e.g., "ApiClient" or "Api.Client"). Default: "ApiClient".</param>
    /// <param name="dryRun">If true, only returns what would be renamed.</param>
    /// <returns>The result of the rename operation.</returns>
    public static RenameResult RenameClientProject(
        string projectDirectory,
        string projectName,
        string? clientSuffix = null,
        bool dryRun = false)
    {
        var suffix = string.IsNullOrEmpty(clientSuffix) ? "ApiClient" : clientSuffix;
        var oldName = $"{projectName}.ApiClient.Generated";
        var newName = $"{projectName}.{suffix}";
        return RenameProject(projectDirectory, oldName, newName, dryRun);
    }

    /// <summary>
    /// Moves a folder, waiting a moment for what blocks it: on Windows a file in it that a virus scanner, an indexer or
    /// a build server still has open refuses the move, usually for well under a second.
    /// </summary>
    private static void MoveDirectory(
        string source,
        string target,
        Action<TimeSpan> wait)
    {
        const int waits = 5;
        for (var attempt = 1; attempt <= waits; attempt++)
        {
            try
            {
                Directory.Move(source, target);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                wait(TimeSpan.FromMilliseconds(100 * attempt));
            }
        }

        Directory.Move(source, target);
    }

    /// <summary>
    /// A file under the project's <c>bin</c> or <c>obj</c> folder: it can be rebuilt, so losing it loses nothing.
    /// </summary>
    private static bool IsBuildOutput(
        string projectDirectory,
        string filePath)
    {
        var relativePath = Path.GetRelativePath(projectDirectory, filePath);
        var separatorIndex = relativePath.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
        var firstSegment = separatorIndex < 0 ? relativePath : relativePath[..separatorIndex];

        return firstSegment.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
               firstSegment.Equals("obj", StringComparison.OrdinalIgnoreCase);
    }
}