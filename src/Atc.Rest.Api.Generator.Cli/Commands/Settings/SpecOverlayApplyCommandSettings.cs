namespace Atc.Rest.Api.Generator.Cli.Commands.Settings;

/// <summary>
/// Settings for the spec overlay apply command.
/// </summary>
public sealed class SpecOverlayApplyCommandSettings : CommandSettings
{
    /// <summary>
    /// Gets or sets the specification the overlays are applied to (a multi-part base file is merged first, as the build
    /// does); resolved to an absolute path by <see cref="Validate"/>.
    /// </summary>
    [CommandOption("-s|--specification <PATH>")]
    [Description("Path to the OpenAPI specification file (YAML or JSON); part files next to it are merged first, as the build does.")]
    public string SpecificationPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets the overlay files, applied in the order given.
    /// </summary>
    [CommandOption("--overlay <PATH>")]
    [Description("An OpenAPI Overlay file (1.0 to 1.2). Repeat the option to apply several, in order.")]
    public string[] Overlays { get; init; } = [];

    /// <summary>
    /// Gets or sets the file the effective specification is written to; YAML unless it ends in <c>.json</c>.
    /// </summary>
    [CommandOption("-o|--output <PATH>")]
    [Description("Output file for the effective specification: YAML, or JSON when the name ends in .json.")]
    public string OutputPath { get; set; } = string.Empty;

    /// <summary>
    /// Requires an existing specification, at least one existing overlay and an output path; resolves the paths.
    /// </summary>
    /// <returns>A success result, or an error naming the first option that is missing or invalid.</returns>
    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(SpecificationPath))
        {
            return ValidationResult.Error("Specification path is required. Use -s or --specification.");
        }

        SpecificationPath = Path.GetFullPath(PathHelper.ResolveRelativePath(SpecificationPath));
        if (!File.Exists(SpecificationPath))
        {
            return ValidationResult.Error($"Specification file not found: {SpecificationPath}");
        }

        if (Overlays.Length == 0)
        {
            return ValidationResult.Error("At least one overlay is required. Use --overlay.");
        }

        if (OverlayCliHelper.FindMissing(Overlays) is { } missing)
        {
            return ValidationResult.Error(missing);
        }

        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            return ValidationResult.Error("Output path is required. Use -o or --output.");
        }

        OutputPath = Path.GetFullPath(PathHelper.ResolveRelativePath(OutputPath));
        return ValidationResult.Success();
    }
}