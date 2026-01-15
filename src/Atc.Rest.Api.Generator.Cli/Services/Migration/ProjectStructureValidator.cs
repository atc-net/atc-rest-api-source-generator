namespace Atc.Rest.Api.Generator.Cli.Services.Migration;

/// <summary>
/// Validates and analyzes the project structure of an old-generated API project.
/// </summary>
internal static class ProjectStructureValidator
{
    /// <summary>
    /// Validates the project structure at the given path.
    /// </summary>
    /// <param name="solutionPath">Path to solution file or directory.</param>
    /// <returns>The project structure validation result.</returns>
    public static ProjectStructureResult Validate(string solutionPath)
    {
        var result = new ProjectStructureResult();
        var rootDirectory = GetRootDirectory(solutionPath);

        // Find solution file
        result.SolutionFile = FindSolutionFile(solutionPath, rootDirectory);

        // Find all project files
        result.AllProjects = FindAllProjects(rootDirectory);

        // Detect project name from projects
        result.DetectedProjectName = DetectProjectName(result.AllProjects);

        if (!string.IsNullOrEmpty(result.DetectedProjectName))
        {
            // Find specific project types
            result.ApiGeneratedProject = FindProjectByPattern(result.AllProjects, $"{result.DetectedProjectName}.Api.Generated");
            result.ApiClientGeneratedProject = FindProjectByPattern(result.AllProjects, $"{result.DetectedProjectName}.ApiClient.Generated");
            result.DomainProject = FindProjectByPattern(result.AllProjects, $"{result.DetectedProjectName}.Domain");
            result.HostApiProject = FindHostApiProject(result.AllProjects, result.DetectedProjectName);
        }

        return result;
    }

    private static string GetRootDirectory(string solutionPath)
    {
        if (Directory.Exists(solutionPath))
        {
            return solutionPath;
        }

        return Path.GetDirectoryName(solutionPath) ?? solutionPath;
    }

    private static string? FindSolutionFile(string solutionPath, string rootDirectory)
    {
        // If the path is directly to a solution file
        if (File.Exists(solutionPath))
        {
            var extension = Path.GetExtension(solutionPath).ToLowerInvariant();
            if (extension is ".sln" or ".slnx")
            {
                return solutionPath;
            }
        }

        // Search for solution files in the directory
        var slnFiles = Directory.GetFiles(rootDirectory, "*.sln", SearchOption.TopDirectoryOnly);
        var slnxFiles = Directory.GetFiles(rootDirectory, "*.slnx", SearchOption.TopDirectoryOnly);

        var allSolutionFiles = slnFiles.Concat(slnxFiles).ToList();

        // Prefer .slnx over .sln
        return allSolutionFiles.FirstOrDefault(f => f.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
               ?? allSolutionFiles.FirstOrDefault();
    }

    private static List<string> FindAllProjects(string rootDirectory)
    {
        try
        {
            return Directory
                .GetFiles(rootDirectory, "*.csproj", SearchOption.AllDirectories)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static string? DetectProjectName(List<string> projects)
    {
        // Look for Api.Generated project to extract project name
        var apiGeneratedProject = projects.FirstOrDefault(p =>
            p.EndsWith(".Api.Generated.csproj", StringComparison.OrdinalIgnoreCase));

        if (apiGeneratedProject != null)
        {
            var fileName = Path.GetFileNameWithoutExtension(apiGeneratedProject);
            // Remove ".Api.Generated" suffix to get the base project name
            var projectName = fileName.Replace(".Api.Generated", string.Empty, StringComparison.OrdinalIgnoreCase);
            return projectName;
        }

        // Try to detect from Domain project
        var domainProject = projects.FirstOrDefault(p =>
            p.EndsWith(".Domain.csproj", StringComparison.OrdinalIgnoreCase) &&
            !p.Contains("Test", StringComparison.OrdinalIgnoreCase));

        if (domainProject != null)
        {
            var fileName = Path.GetFileNameWithoutExtension(domainProject);
            var projectName = fileName.Replace(".Domain", string.Empty, StringComparison.OrdinalIgnoreCase);
            return projectName;
        }

        // Try to detect from any Api project
        var apiProject = projects.FirstOrDefault(p =>
            p.EndsWith(".Api.csproj", StringComparison.OrdinalIgnoreCase) &&
            !p.Contains("Generated", StringComparison.OrdinalIgnoreCase) &&
            !p.Contains("Test", StringComparison.OrdinalIgnoreCase));

        if (apiProject != null)
        {
            var fileName = Path.GetFileNameWithoutExtension(apiProject);
            var projectName = fileName.Replace(".Api", string.Empty, StringComparison.OrdinalIgnoreCase);
            return projectName;
        }

        return null;
    }

    private static string? FindProjectByPattern(List<string> projects, string pattern)
    {
        return projects.FirstOrDefault(p =>
        {
            var fileName = Path.GetFileNameWithoutExtension(p);
            return fileName.Equals(pattern, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static string? FindHostApiProject(List<string> projects, string projectName)
    {
        // Look for {ProjectName}.Api.csproj that is NOT the generated project
        return projects.FirstOrDefault(p =>
        {
            var fileName = Path.GetFileNameWithoutExtension(p);
            return fileName.Equals($"{projectName}.Api", StringComparison.OrdinalIgnoreCase) &&
                   !p.Contains("Generated", StringComparison.OrdinalIgnoreCase);
        });
    }
}
