namespace Atc.Rest.Api.Generator.Cli.Models.Migration;

/// <summary>
/// Result of migrating parameter property names in handler files.
/// </summary>
internal sealed class ParameterMigrationResult
{
    /// <summary>
    /// Gets the list of files that were modified.
    /// </summary>
    public List<string> ModifiedFiles { get; } = [];

    /// <summary>
    /// Gets the list of parameter replacements made (e.g., "Continuation → ContinuationToken in GetAccountsHandler.cs").
    /// </summary>
    public List<string> ReplacedParameters { get; } = [];

    /// <summary>
    /// Gets or sets an error message if the operation failed.
    /// </summary>
    public string? Error { get; set; }
}