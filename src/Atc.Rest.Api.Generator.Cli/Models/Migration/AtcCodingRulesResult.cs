namespace Atc.Rest.Api.Generator.Cli.Models.Migration;

/// <summary>
/// Result of an ATC coding rules check.
/// </summary>
public sealed class AtcCodingRulesResult
{
    public bool ConfigExists { get; set; }

    public string? ConfigPath { get; set; }

    public string? CurrentProjectTarget { get; set; }

    public bool NeedsUpdate =>
        ConfigExists &&
        !string.IsNullOrEmpty(CurrentProjectTarget) &&
        !CurrentProjectTarget.Equals("DotNet10", StringComparison.OrdinalIgnoreCase);
}