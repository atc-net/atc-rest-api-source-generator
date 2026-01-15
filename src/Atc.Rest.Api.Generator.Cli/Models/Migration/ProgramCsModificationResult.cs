namespace Atc.Rest.Api.Generator.Cli.Models.Migration;

/// <summary>
/// Result of Program.cs modification operations.
/// </summary>
internal sealed class ProgramCsModificationResult
{
    /// <summary>
    /// Gets or sets the path to the Program.cs file.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the file was modified.
    /// </summary>
    public bool WasModified { get; set; }

    /// <summary>
    /// Gets the list of statements that were removed.
    /// </summary>
    public List<string> RemovedStatements { get; } = [];

    /// <summary>
    /// Gets the list of statement replacements (before → after).
    /// </summary>
    public List<string> ReplacedStatements { get; } = [];

    /// <summary>
    /// Gets the list of statements that were added.
    /// </summary>
    public List<string> AddedStatements { get; } = [];

    /// <summary>
    /// Gets or sets any error message if the operation failed.
    /// </summary>
    public string? Error { get; set; }
}