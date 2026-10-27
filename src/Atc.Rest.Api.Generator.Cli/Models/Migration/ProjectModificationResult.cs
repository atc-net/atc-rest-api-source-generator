namespace Atc.Rest.Api.Generator.Cli.Models.Migration;

/// <summary>
/// Result of a project file modification.
/// </summary>
public sealed class ProjectModificationResult
{
    /// <summary>
    /// Gets or sets the path of the .csproj file.
    /// </summary>
    public string ProjectPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the file content changed (or would change in a dry run).
    /// </summary>
    public bool WasModified { get; set; }

    /// <summary>
    /// Gets or sets why the file could not be modified, e.g. because it does not exist.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Gets the package references that were added or bumped, as <c>id (version)</c> entries.
    /// </summary>
    public List<string> AddedPackages { get; } = [];

    /// <summary>
    /// Gets the <c>AdditionalFiles</c> entries added for the specification and marker file.
    /// </summary>
    public List<string> AddedAdditionalFiles { get; } = [];

    /// <summary>
    /// Gets the project references rewritten to the new project names, as <c>old → new</c> entries.
    /// </summary>
    public List<string> UpdatedReferences { get; } = [];
}