namespace Atc.Rest.Api.Generator.Cli.Models.Migration;

/// <summary>
/// Result of a cleanup operation.
/// </summary>
public sealed class CleanupResult
{
    /// <summary>
    /// Gets the generated folders that were deleted, each with the number of files it held.
    /// </summary>
    public List<DeletedItem> DeletedFolders { get; } = [];

    /// <summary>
    /// Gets the generated files deleted outside those folders.
    /// </summary>
    public List<string> DeletedFiles { get; } = [];

    /// <summary>
    /// Gets the hand-written files found inside the generated folders. They are never deleted.
    /// </summary>
    public List<string> PreservedFiles { get; } = [];

    /// <summary>
    /// Gets the number of files deleted in total, inside and outside the folders.
    /// </summary>
    public int TotalItemsDeleted
        => DeletedFolders.Sum(f => f.ItemCount) + DeletedFiles.Count;
}