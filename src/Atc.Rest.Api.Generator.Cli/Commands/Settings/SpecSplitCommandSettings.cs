// ReSharper disable StringLiteralTypo

namespace Atc.Rest.Api.Generator.Cli.Commands.Settings;

/// <summary>
/// Settings for the spec split command.
/// </summary>
public sealed class SpecSplitCommandSettings : CommandSettings
{
    /// <summary>
    /// Gets or sets the specification file to split; resolved to an absolute path by <see cref="Validate"/>.
    /// </summary>
    [CommandOption("-s|--specification <PATH>")]
    [Description("Path to the OpenAPI specification file to split.")]
    public string SpecificationPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the directory the base and part files are written to; required unless <see cref="Preview"/> is set.
    /// </summary>
    [CommandOption("-o|--output <PATH>")]
    [Description("Output directory for the split files.")]
    public string? OutputPath { get; set; }

    /// <summary>
    /// Gets how operations are grouped into part files, as the name of a <see cref="Generator.Models.SplitStrategy"/> member.
    /// </summary>
    [CommandOption("--strategy <STRATEGY>")]
    [Description("Split strategy: ByTag (default), ByPathSegment, or ByDomain.")]
    [DefaultValue("ByTag")]
    public string Strategy { get; init; } = "ByTag";

    /// <summary>
    /// Gets a value indicating whether the resulting files are listed instead of written.
    /// </summary>
    [CommandOption("--preview")]
    [Description("Preview split without writing files.")]
    [DefaultValue(false)]
    public bool Preview { get; init; }

    /// <summary>
    /// Gets a value indicating whether schemas used by several parts go to a <c>{base}_Common.yaml</c> file (default).
    /// </summary>
    [CommandOption("--extract-common")]
    [Description("Extract schemas shared by several parts to a {base}_Common.yaml file (default).")]
    [DefaultValue(true)]
    public bool ExtractCommon { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether schemas used by several parts stay in the base file; overrides <see cref="ExtractCommon"/>.
    /// </summary>
    [CommandOption("--no-extract-common")]
    [Description("Keep schemas shared by several parts in the base file instead of a Common file.")]
    [DefaultValue(false)]
    public bool NoExtractCommon { get; init; }

    /// <summary>
    /// Gets the smallest number of operations a group needs to get its own part file; smaller groups land in <c>Other</c>.
    /// </summary>
    [CommandOption("--min-operations <N>")]
    [Description("Minimum operations per part file (smaller groups merged into Other).")]
    [DefaultValue(2)]
    public int MinOperations { get; init; } = 2;

    /// <summary>
    /// Gets whether shared schemas go to a Common file.
    /// </summary>
    public bool ShouldExtractCommon
        => ExtractCommon && !NoExtractCommon;

    /// <summary>
    /// Requires an existing specification, a known strategy, an output path unless previewing and a positive minimum; resolves the paths.
    /// </summary>
    /// <returns>A success result, or an error naming the first option that is missing or invalid.</returns>
    public override ValidationResult Validate()
    {
        // Specification path is required
        if (string.IsNullOrWhiteSpace(SpecificationPath))
        {
            return ValidationResult.Error("Specification path (-s) is required.");
        }

        SpecificationPath = PathHelper.ResolveRelativePath(SpecificationPath);

        // Verify file exists
        if (!File.Exists(SpecificationPath))
        {
            return ValidationResult.Error($"Specification file not found: {SpecificationPath}");
        }

        // Validate strategy
        var validStrategies = new[] { "ByTag", "ByPathSegment", "ByDomain" };
        if (!validStrategies.Any(s => s.Equals(Strategy, StringComparison.OrdinalIgnoreCase)))
        {
            return ValidationResult.Error($"Invalid split strategy. Valid options: {string.Join(", ", validStrategies)}");
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

        // Validate min operations
        if (MinOperations < 1)
        {
            return ValidationResult.Error("Minimum operations must be at least 1.");
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Gets the parsed split strategy enum value.
    /// </summary>
    public Generator.Models.SplitStrategy GetSplitStrategyEnum()
        => Strategy.ToLowerInvariant() switch
        {
            "bytag" => Generator.Models.SplitStrategy.ByTag,
            "bypathsegment" => Generator.Models.SplitStrategy.ByPathSegment,
            "bydomain" => Generator.Models.SplitStrategy.ByDomain,
            _ => Generator.Models.SplitStrategy.ByTag,
        };
}