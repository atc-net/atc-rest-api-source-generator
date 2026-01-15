namespace Atc.Rest.Api.Generator.Cli.Services.Migration;

/// <summary>
/// Modifies solution files during migration.
/// </summary>
internal static class SolutionModifier
{
    /// <summary>
    /// Updates project references in a solution file.
    /// </summary>
    /// <param name="solutionPath">Path to the solution file (.sln or .slnx).</param>
    /// <param name="oldName">The old project name (e.g., "Api.Generated").</param>
    /// <param name="newName">The new project name (e.g., "Api.Contracts").</param>
    /// <param name="dryRun">If true, only returns what would be modified.</param>
    /// <returns>The result of the modification.</returns>
    public static SolutionModificationResult UpdateProjectReference(
        string solutionPath,
        string oldName,
        string newName,
        bool dryRun = false)
    {
        var result = new SolutionModificationResult { SolutionPath = solutionPath };

        if (!File.Exists(solutionPath))
        {
            result.Error = "Solution file not found.";
            return result;
        }

        var content = File.ReadAllText(solutionPath);

        if (!content.Contains(oldName, StringComparison.OrdinalIgnoreCase))
        {
            return result;
        }

        var modified = content.Replace(oldName, newName, StringComparison.OrdinalIgnoreCase);
        result.UpdatedReferences.Add($"{oldName} → {newName}");

        if (!dryRun && modified != content)
        {
            File.WriteAllText(solutionPath, modified);
        }

        result.WasModified = modified != content;
        return result;
    }

    /// <summary>
    /// Updates all project references for migration.
    /// </summary>
    /// <param name="solutionPath">Path to the solution file.</param>
    /// <param name="projectName">The base project name.</param>
    /// <param name="dryRun">If true, only returns what would be modified.</param>
    /// <returns>The combined result of all modifications.</returns>
    public static SolutionModificationResult UpdateAllReferences(
        string solutionPath,
        string projectName,
        bool dryRun = false)
    {
        var combinedResult = new SolutionModificationResult { SolutionPath = solutionPath };

        if (!File.Exists(solutionPath))
        {
            combinedResult.Error = "Solution file not found.";
            return combinedResult;
        }

        var content = File.ReadAllText(solutionPath);
        var modified = content;

        // Update Api.Generated → Api.Contracts
        var oldServerName = $"{projectName}.Api.Generated";
        var newServerName = $"{projectName}.Api.Contracts";
        if (modified.Contains(oldServerName, StringComparison.OrdinalIgnoreCase))
        {
            modified = modified.Replace(oldServerName, newServerName, StringComparison.OrdinalIgnoreCase);
            combinedResult.UpdatedReferences.Add($"{oldServerName} → {newServerName}");
        }

        // Update ApiClient.Generated → ApiClient
        var oldClientName = $"{projectName}.ApiClient.Generated";
        var newClientName = $"{projectName}.ApiClient";
        if (modified.Contains(oldClientName, StringComparison.OrdinalIgnoreCase))
        {
            modified = modified.Replace(oldClientName, newClientName, StringComparison.OrdinalIgnoreCase);
            combinedResult.UpdatedReferences.Add($"{oldClientName} → {newClientName}");
        }

        if (!dryRun && modified != content)
        {
            File.WriteAllText(solutionPath, modified);
        }

        combinedResult.WasModified = modified != content;
        return combinedResult;
    }

    /// <summary>
    /// Updates all project files in the solution to use new project names.
    /// </summary>
    /// <param name="rootDirectory">The root directory of the solution.</param>
    /// <param name="projectFiles">List of all project files.</param>
    /// <param name="projectName">The base project name.</param>
    /// <param name="dryRun">If true, only returns what would be modified.</param>
    /// <returns>List of modification results.</returns>
    public static List<ProjectModificationResult> UpdateAllProjectReferences(
        string rootDirectory,
        IReadOnlyList<string> projectFiles,
        string projectName,
        bool dryRun = false)
    {
        var results = new List<ProjectModificationResult>();

        var oldServerName = $"{projectName}.Api.Generated";
        var newServerName = $"{projectName}.Api.Contracts";
        var oldClientName = $"{projectName}.ApiClient.Generated";
        var newClientName = $"{projectName}.ApiClient";

        foreach (var projectFile in projectFiles)
        {
            // Skip the projects being renamed themselves
            var fileName = Path.GetFileName(projectFile);
            if (fileName.Contains("Api.Generated", StringComparison.OrdinalIgnoreCase) ||
                fileName.Contains("ApiClient.Generated", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var result = new ProjectModificationResult { ProjectPath = projectFile };

            if (!File.Exists(projectFile))
            {
                continue;
            }

            var content = File.ReadAllText(projectFile);
            var modified = content;

            // Update server project reference
            if (modified.Contains(oldServerName, StringComparison.OrdinalIgnoreCase))
            {
                modified = modified.Replace(oldServerName, newServerName, StringComparison.OrdinalIgnoreCase);
                result.UpdatedReferences.Add($"{oldServerName} → {newServerName}");
            }

            // Update client project reference
            if (modified.Contains(oldClientName, StringComparison.OrdinalIgnoreCase))
            {
                modified = modified.Replace(oldClientName, newClientName, StringComparison.OrdinalIgnoreCase);
                result.UpdatedReferences.Add($"{oldClientName} → {newClientName}");
            }

            if (modified != content)
            {
                result.WasModified = true;
                if (!dryRun)
                {
                    File.WriteAllText(projectFile, modified);
                }

                results.Add(result);
            }
        }

        return results;
    }
}