namespace Atc.Rest.Api.Generator.Cli.Models.Migration;

/// <summary>
/// Result of a git status check.
/// </summary>
public sealed class GitStatusResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the solution directory is inside a git repository.
    /// </summary>
    public bool IsGitRepository { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the <c>git status</c> process could not run or exited with an error.
    /// </summary>
    public bool GitCommandFailed { get; set; }

    /// <summary>
    /// Gets or sets the git error output or exception message when <see cref="GitCommandFailed"/> is set.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets the files with uncommitted changes.
    /// </summary>
    public List<GitFileStatus> UncommittedFiles { get; } = [];

    /// <summary>
    /// Gets a value indicating whether any file has uncommitted changes.
    /// </summary>
    public bool HasUncommittedChanges => UncommittedFiles.Count > 0;
}