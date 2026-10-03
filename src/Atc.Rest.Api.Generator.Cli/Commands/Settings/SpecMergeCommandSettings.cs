// ReSharper disable StringLiteralTypo

namespace Atc.Rest.Api.Generator.Cli.Commands.Settings;

/// <summary>
/// Settings for the spec merge command.
/// </summary>
public sealed class SpecMergeCommandSettings : CommandSettings
{
    /// <summary>
    /// Gets or sets the base specification file whose <c>{base}_*.yaml</c> part files are discovered; resolved by <see cref="Validate"/>.
    /// </summary>
    [CommandOption("-s|--specification <PATH>")]
    [Description("Path to the base OpenAPI specification file (auto-discovers part files by naming convention).")]
    public string SpecificationPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file the merged specification is written to; required unless <see cref="Preview"/> is set.
    /// </summary>
    [CommandOption("-o|--output <PATH>")]
    [Description("Output file path for the merged specification.")]
    public string? OutputPath { get; set; }

    /// <summary>
    /// Gets a comma-separated list of files to merge instead of the discovered part files.
    /// </summary>
    [CommandOption("--files <LIST>")]
    [Description("Explicit comma-separated list of files to merge (overrides auto-discovery).")]
    public string? ExplicitFiles { get; init; }

    /// <summary>
    /// Gets a value indicating whether the merged document is printed instead of written.
    /// </summary>
    [CommandOption("--preview")]
    [Description("Preview merged output without writing file.")]
    [DefaultValue(false)]
    public bool Preview { get; init; }

    /// <summary>
    /// Gets a value indicating whether the merged document is validated after merging.
    /// </summary>
    [CommandOption("--validate")]
    [Description("Validate the merged specification after merging.")]
    [DefaultValue(false)]
    public bool ValidateAfterMerge { get; init; }

    /// <summary>
    /// Gets the serialization format of the merged document: <c>yaml</c> (default) or <c>json</c>.
    /// </summary>
    [CommandOption("--format <FORMAT>")]
    [Description("Output format: yaml (default) or json.")]
    [DefaultValue("yaml")]
    public string OutputFormat { get; init; } = "yaml";

    /// <summary>
    /// Gets how duplicate paths and schemas are handled, as the name of a <see cref="Generator.Models.MergeStrategy"/> member.
    /// </summary>
    [CommandOption("--strategy <STRATEGY>")]
    [Description("Merge strategy for duplicates: ErrorOnDuplicate (default), MergeIfIdentical, AppendUnique, FirstWins, LastWins.")]
    [DefaultValue("ErrorOnDuplicate")]
    public string MergeStrategy { get; init; } = "ErrorOnDuplicate";

    /// <summary>
    /// Requires a specification or file list, a known format and strategy, and an output path unless previewing; resolves the paths.
    /// </summary>
    /// <returns>A success result, or an error naming the first option that is missing or invalid.</returns>
    public override ValidationResult Validate()
    {
        // Either specification path or explicit files must be provided
        if (string.IsNullOrWhiteSpace(SpecificationPath) && string.IsNullOrWhiteSpace(ExplicitFiles))
        {
            return ValidationResult.Error("Either specification path (-s) or explicit files (--files) must be provided.");
        }

        if (!string.IsNullOrWhiteSpace(SpecificationPath))
        {
            SpecificationPath = PathHelper.ResolveRelativePath(SpecificationPath);
        }

        // If specification path is provided, verify it exists
        if (!string.IsNullOrWhiteSpace(SpecificationPath) && !File.Exists(SpecificationPath))
        {
            return ValidationResult.Error($"Specification file not found: {SpecificationPath}");
        }

        // Validate output format
        if (OutputFormat.ToLowerInvariant() is not "yaml" and not "json")
        {
            return ValidationResult.Error("Output format must be 'yaml' or 'json'.");
        }

        // Validate merge strategy
        var validStrategies = new[] { "ErrorOnDuplicate", "MergeIfIdentical", "AppendUnique", "FirstWins", "LastWins" };
        if (!validStrategies.Any(s => s.Equals(MergeStrategy, StringComparison.OrdinalIgnoreCase)))
        {
            return ValidationResult.Error($"Invalid merge strategy. Valid options: {string.Join(", ", validStrategies)}");
        }

        // Output path is required unless preview mode is enabled
        if (!Preview && string.IsNullOrWhiteSpace(OutputPath))
        {
            return ValidationResult.Error("Output path (-o) is required unless using --preview mode.");
        }

        if (!string.IsNullOrWhiteSpace(OutputPath))
        {
            OutputPath = PathHelper.ResolveRelativePath(OutputPath);
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Gets the parsed merge strategy enum value.
    /// </summary>
    public Generator.Models.MergeStrategy GetMergeStrategyEnum()
        => MergeStrategy.ToLowerInvariant() switch
        {
            "erroronduplicate" => Generator.Models.MergeStrategy.ErrorOnDuplicate,
            "mergeifidentical" => Generator.Models.MergeStrategy.MergeIfIdentical,
            "appendunique" => Generator.Models.MergeStrategy.AppendUnique,
            "firstwins" => Generator.Models.MergeStrategy.FirstWins,
            "lastwins" => Generator.Models.MergeStrategy.LastWins,
            _ => Generator.Models.MergeStrategy.ErrorOnDuplicate,
        };
}