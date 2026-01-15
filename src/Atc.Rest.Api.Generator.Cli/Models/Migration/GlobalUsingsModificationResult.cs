namespace Atc.Rest.Api.Generator.Cli.Models.Migration;

/// <summary>
/// Result of modifying GlobalUsings.cs during migration.
/// </summary>
internal sealed class GlobalUsingsModificationResult
{
    /// <summary>
    /// Gets or sets the path to the GlobalUsings.cs file.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the file was modified.
    /// </summary>
    public bool WasModified { get; set; }

    /// <summary>
    /// Gets the list of using statements that were updated (namespace changed).
    /// </summary>
    public List<string> UpdatedUsings { get; } = [];

    /// <summary>
    /// Gets the list of using statements that were added.
    /// </summary>
    public List<string> AddedUsings { get; } = [];

    /// <summary>
    /// Gets the list of using statements that were removed (old CLI-generated patterns).
    /// </summary>
    public List<string> RemovedUsings { get; } = [];

    /// <summary>
    /// Gets or sets an error message if the operation failed.
    /// </summary>
    public string? Error { get; set; }
}