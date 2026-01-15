namespace Atc.Rest.Api.Generator.Cli.Models.Migration;

/// <summary>
/// Result of a solution file modification.
/// </summary>
public sealed class SolutionModificationResult
{
    public string SolutionPath { get; set; } = string.Empty;

    public bool WasModified { get; set; }

    public string? Error { get; set; }

    public List<string> UpdatedReferences { get; } = [];
}