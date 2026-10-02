namespace Atc.Rest.Api.Generator.Cli.Models.Migration;

/// <summary>
/// Result of a cleanup operation.
/// </summary>
public sealed class CleanupResult
{
    public List<DeletedItem> DeletedFolders { get; } = [];

    public List<string> DeletedFiles { get; } = [];

    /// <summary>
    /// Gets the hand-written files found inside the generated folders. They are never deleted.
    /// </summary>
    public List<string> PreservedFiles { get; } = [];

    public int TotalItemsDeleted
        => DeletedFolders.Sum(f => f.ItemCount) + DeletedFiles.Count;

    public bool HasDeletedItems
        => DeletedFolders.Count > 0 || DeletedFiles.Count > 0;
}