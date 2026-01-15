namespace Atc.Rest.Api.Generator.Cli.Models.Migration;

/// <summary>
/// Represents a file with uncommitted changes.
/// </summary>
public sealed class GitFileStatus
{
    public string Status { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;
}