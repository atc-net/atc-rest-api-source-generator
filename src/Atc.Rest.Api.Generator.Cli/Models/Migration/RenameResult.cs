namespace Atc.Rest.Api.Generator.Cli.Models.Migration;

/// <summary>
/// Result of a project rename operation.
/// </summary>
public sealed class RenameResult
{
    public string OldName { get; set; } = string.Empty;

    public string NewName { get; set; } = string.Empty;

    public string OldPath { get; set; } = string.Empty;

    public string NewPath { get; set; } = string.Empty;

    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the project was already renamed
    /// (target directory already exists).
    /// </summary>
    public bool AlreadyRenamed { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the old directory was deleted
    /// (when target already existed).
    /// </summary>
    public bool OldDirectoryDeleted { get; set; }

    public string? Error { get; set; }
}