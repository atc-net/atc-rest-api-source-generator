namespace Atc.Rest.Api.Generator.Cli.Models.Migration;

/// <summary>
/// Represents a file with uncommitted changes.
/// </summary>
public sealed class GitFileStatus
{
    /// <summary>
    /// Gets or sets the two-character status code from <c>git status --porcelain</c>, e.g. <c> M</c> or <c>??</c>.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file path as git reports it, relative to the repository root.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;
}