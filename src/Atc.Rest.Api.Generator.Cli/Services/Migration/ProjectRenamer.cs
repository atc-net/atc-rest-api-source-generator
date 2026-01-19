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
    /// <returns>The result of the rename operation.</returns>
    public static RenameResult RenameProject(
        string projectDirectory,
        string oldName,
        string newName,
        bool dryRun = false)
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
            // Delete the old directory since it's obsolete
            if (Directory.Exists(projectDirectory))
            {
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

        // Rename the .csproj file inside the directory first
        var oldCsprojPath = Path.Combine(projectDirectory, $"{oldName}.csproj");
        var newCsprojName = $"{newName}.csproj";
        var newCsprojPath = Path.Combine(projectDirectory, newCsprojName);

        if (!dryRun)
        {
            // Rename .csproj file
            if (File.Exists(oldCsprojPath))
            {
                File.Move(oldCsprojPath, newCsprojPath);
            }

            // Rename directory
            if (!projectDirectory.Equals(newDirectory, StringComparison.OrdinalIgnoreCase))
            {
                Directory.Move(projectDirectory, newDirectory);
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
}