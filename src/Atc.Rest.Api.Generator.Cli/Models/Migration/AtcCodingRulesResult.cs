namespace Atc.Rest.Api.Generator.Cli.Models.Migration;

/// <summary>
/// Result of an ATC coding rules check.
/// </summary>
public sealed class AtcCodingRulesResult
{
    /// <summary>
    /// Gets or sets a value indicating whether an atc-coding-rules-updater.json was found in the solution.
    /// </summary>
    public bool ConfigExists { get; set; }

    /// <summary>
    /// Gets or sets the path of the atc-coding-rules-updater.json that was found.
    /// </summary>
    public string? ConfigPath { get; set; }

    /// <summary>
    /// Gets or sets the <c>projectTarget</c> value read from the config file, e.g. <c>DotNet8</c>.
    /// </summary>
    public string? CurrentProjectTarget { get; set; }

    /// <summary>
    /// Gets a value indicating whether the config exists but its <c>projectTarget</c> is not <c>DotNet10</c> yet.
    /// </summary>
    public bool NeedsUpdate =>
        ConfigExists &&
        !string.IsNullOrEmpty(CurrentProjectTarget) &&
        !CurrentProjectTarget.Equals("DotNet10", StringComparison.OrdinalIgnoreCase);
}