namespace Atc.Rest.Api.Generator.Cli.Models.Migration;

/// <summary>
/// Result of a project file modification.
/// </summary>
public sealed class ProjectModificationResult
{
    public string ProjectPath { get; set; } = string.Empty;

    public bool WasModified { get; set; }

    public string? Error { get; set; }

    public List<string> AddedPackages { get; } = [];

    public List<string> AddedAdditionalFiles { get; } = [];

    public List<string> UpdatedReferences { get; } = [];
}