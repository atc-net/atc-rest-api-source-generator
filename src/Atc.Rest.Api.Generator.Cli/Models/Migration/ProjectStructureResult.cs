namespace Atc.Rest.Api.Generator.Cli.Models.Migration;

/// <summary>
/// Result of project structure validation.
/// </summary>
public sealed class ProjectStructureResult
{
    /// <summary>
    /// Gets or sets the solution file path if found.
    /// </summary>
    public string? SolutionFile { get; set; }

    /// <summary>
    /// Gets or sets the Api.Generated project path if found.
    /// </summary>
    public string? ApiGeneratedProject { get; set; }

    /// <summary>
    /// Gets or sets the ApiClient.Generated project path if found.
    /// </summary>
    public string? ApiClientGeneratedProject { get; set; }

    /// <summary>
    /// Gets or sets the Domain project path if found.
    /// </summary>
    public string? DomainProject { get; set; }

    /// <summary>
    /// Gets or sets the Host API project path if found.
    /// </summary>
    public string? HostApiProject { get; set; }

    /// <summary>
    /// Gets or sets all discovered project files.
    /// </summary>
    public List<string> AllProjects { get; set; } = [];

    /// <summary>
    /// Gets or sets the detected project name prefix (e.g., "KL.IoT.D365").
    /// </summary>
    public string? DetectedProjectName { get; set; }

    /// <summary>
    /// Gets a value indicating whether a solution file was found.
    /// </summary>
    public bool HasSolutionFile => !string.IsNullOrEmpty(SolutionFile);

    /// <summary>
    /// Gets a value indicating whether an Api.Generated project was found.
    /// </summary>
    public bool HasApiGeneratedProject => !string.IsNullOrEmpty(ApiGeneratedProject);

    /// <summary>
    /// Gets a value indicating whether an ApiClient.Generated project was found.
    /// </summary>
    public bool HasApiClientGeneratedProject => !string.IsNullOrEmpty(ApiClientGeneratedProject);

    /// <summary>
    /// Gets a value indicating whether a Domain project was found.
    /// </summary>
    public bool HasDomainProject => !string.IsNullOrEmpty(DomainProject);

    /// <summary>
    /// Gets a value indicating whether a Host API project was found.
    /// </summary>
    public bool HasHostApiProject => !string.IsNullOrEmpty(HostApiProject);

    /// <summary>
    /// Gets a value indicating whether the minimum required structure is present.
    /// </summary>
    public bool HasMinimumRequiredStructure =>
        HasSolutionFile && HasApiGeneratedProject && HasHostApiProject;
}
