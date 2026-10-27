namespace Atc.Rest.Api.Generator.Cli.Models.Migration;

/// <summary>
/// Represents a deleted folder with its item count.
/// </summary>
public sealed class DeletedItem
{
    /// <summary>
    /// Gets or sets the path of the deleted folder.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of generated files that were deleted from the folder.
    /// </summary>
    public int ItemCount { get; set; }
}