namespace Atc.Rest.Api.Generator.Cli.Models.Migration;

/// <summary>
/// Result of a project rename operation.
/// </summary>
public sealed class RenameResult
{
    /// <summary>
    /// Gets or sets the project name before the rename, e.g. <c>Demo.Api.Generated</c>.
    /// </summary>
    public string OldName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the project name after the rename, e.g. <c>Demo.Api.Contracts</c>.
    /// </summary>
    public string NewName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the project directory before the rename.
    /// </summary>
    public string OldPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the project directory after the rename.
    /// </summary>
    public string NewPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the directory and .csproj were renamed (or would be in a dry run).
    /// </summary>
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

    /// <summary>
    /// Gets or sets why the rename failed, e.g. because the old directory does not exist.
    /// </summary>
    public string? Error { get; set; }
}