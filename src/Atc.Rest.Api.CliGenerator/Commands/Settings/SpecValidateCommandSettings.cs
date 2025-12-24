namespace Atc.Rest.Api.CliGenerator.Commands.Settings;

/// <summary>
/// Settings for the spec validate command.
/// </summary>
public sealed class SpecValidateCommandSettings : CommandSettings
{
    [CommandOption("-s|--specification <PATH>")]
    [Description("Path to the OpenAPI specification file (YAML or JSON).")]
    public string SpecificationPath { get; init; } = string.Empty;

    [CommandOption("--no-strict")]
    [Description("Disable strict validation mode (skip additional naming convention checks).")]
    [DefaultValue(false)]
    public bool DisableStrictMode { get; init; }

    [CommandOption("--multi-part")]
    [Description("Enable multi-part mode: auto-discover and merge part files before validation.")]
    [DefaultValue(false)]
    public bool MultiPartMode { get; init; }

    [CommandOption("--files <LIST>")]
    [Description("Explicit comma-separated list of files to validate as multi-part (overrides auto-discovery).")]
    public string? ExplicitFiles { get; init; }

    /// <summary>
    /// Gets a value indicating whether strict validation mode is enabled.
    /// Strict mode is enabled by default and can be disabled with --no-strict.
    /// </summary>
    public bool StrictMode => !DisableStrictMode;

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(SpecificationPath) && string.IsNullOrWhiteSpace(ExplicitFiles))
        {
            return ValidationResult.Error("Specification path is required. Use -s or --specification, or --files for explicit list.");
        }

        if (!string.IsNullOrWhiteSpace(SpecificationPath) && !File.Exists(SpecificationPath))
        {
            return ValidationResult.Error($"Specification file not found: {SpecificationPath}");
        }

        if (!string.IsNullOrWhiteSpace(SpecificationPath))
        {
            var extension = Path.GetExtension(SpecificationPath).ToLowerInvariant();
            if (extension is not ".yaml" and not ".yml" and not ".json")
            {
                return ValidationResult.Error("Specification file must be a YAML (.yaml, .yml) or JSON (.json) file.");
            }
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Gets a value indicating whether multi-part mode should be used.
    /// True if --multi-part flag is set OR --files is provided.
    /// </summary>
    public bool UseMultiPart
        => MultiPartMode ||
           !string.IsNullOrWhiteSpace(ExplicitFiles);
}