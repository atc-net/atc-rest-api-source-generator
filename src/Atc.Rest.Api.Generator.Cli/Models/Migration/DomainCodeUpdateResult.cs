namespace Atc.Rest.Api.Generator.Cli.Models.Migration;

/// <summary>
/// Result of updating namespace references in domain code files.
/// </summary>
public sealed class DomainCodeUpdateResult
{
    /// <summary>
    /// Gets the list of files that were updated.
    /// </summary>
    public List<DomainFileUpdate> UpdatedFiles { get; } = [];

    /// <summary>
    /// Gets or sets the total number of replacements made.
    /// </summary>
    public int TotalReplacements { get; set; }

    /// <summary>
    /// Gets or sets an error message if the operation failed.
    /// </summary>
    public string? Error { get; set; }
}