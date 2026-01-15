namespace Atc.Rest.Api.Generator.Cli.Models.Migration;

/// <summary>
/// Result of a git status check.
/// </summary>
public sealed class GitStatusResult
{
    public bool IsGitRepository { get; set; } = true;

    public bool GitCommandFailed { get; set; }

    public string? ErrorMessage { get; set; }

    public List<GitFileStatus> UncommittedFiles { get; } = [];

    public bool HasUncommittedChanges => UncommittedFiles.Count > 0;
}