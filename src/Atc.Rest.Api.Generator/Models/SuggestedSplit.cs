namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// Suggested split configuration.
/// </summary>
public sealed class SuggestedSplit
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SuggestedSplit"/> class.
    /// </summary>
    public SuggestedSplit(
        string fileName,
        string description,
        string? partName = null,
        int estimatedOperations = 0,
        int estimatedLines = 0)
    {
        FileName = fileName;
        Description = description;
        PartName = partName;
        EstimatedOperations = estimatedOperations;
        EstimatedLines = estimatedLines;
    }

    /// <summary>
    /// The suggested file name.
    /// </summary>
    public string FileName { get; }

    /// <summary>
    /// The part name (e.g., "Accounts", "Users", "Common").
    /// </summary>
    public string? PartName { get; }

    /// <summary>
    /// Description of what will be in this file.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Estimated number of operations.
    /// </summary>
    public int EstimatedOperations { get; }

    /// <summary>
    /// Estimated line count.
    /// </summary>
    public int EstimatedLines { get; }
}