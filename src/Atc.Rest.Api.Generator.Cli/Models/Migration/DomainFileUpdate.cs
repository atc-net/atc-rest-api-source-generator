namespace Atc.Rest.Api.Generator.Cli.Models.Migration;

/// <summary>
/// Details of namespace updates made to a single file.
/// </summary>
public sealed class DomainFileUpdate
{
    /// <summary>
    /// Gets or sets the path to the updated file.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets the list of replacement descriptions (old → new).
    /// </summary>
    public List<string> Replacements { get; } = [];
}