namespace Atc.Rest.Api.Generator.Cli.Models.Migration;

/// <summary>
/// Result of a solution file modification.
/// </summary>
public sealed class SolutionModificationResult
{
    /// <summary>
    /// Gets or sets the path of the .sln or .slnx file.
    /// </summary>
    public string SolutionPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the file content changed (or would change in a dry run).
    /// </summary>
    public bool WasModified { get; set; }

    /// <summary>
    /// Gets or sets why the file could not be modified, e.g. because it does not exist.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Gets the project references rewritten to the new project names, as <c>old → new</c> entries.
    /// </summary>
    public List<string> UpdatedReferences { get; } = [];
}