namespace Atc.Rest.Api.Generator.Cli.Commands.Settings;

/// <summary>
/// Settings for the spec validate command.
/// </summary>
public sealed class SpecValidateCommandSettings : CommandSettings
{
    /// <summary>
    /// Gets or sets the specification file to validate (YAML or JSON); resolved to an absolute path by <see cref="Validate"/>.
    /// </summary>
    [CommandOption("-s|--specification <PATH>")]
    [Description("Path to the OpenAPI specification file (YAML or JSON).")]
    public string SpecificationPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the standard strategy is used instead of the strict one; see <see cref="StrictMode"/>.
    /// </summary>
    [CommandOption("--no-strict")]
    [Description("Validate with the Standard strategy: only the OpenAPI parser's errors and structural checks, not the ATC rules.")]
    [DefaultValue(false)]
    public bool DisableStrictMode { get; init; }

    /// <summary>
    /// Gets a value indicating whether the part files next to the specification are discovered and merged before validation.
    /// </summary>
    [CommandOption("--multi-part")]
    [Description("Enable multi-part mode: auto-discover and merge part files before validation.")]
    [DefaultValue(false)]
    public bool MultiPartMode { get; init; }

    /// <summary>
    /// Gets a comma-separated list of files to validate as one multi-part specification instead of discovering them.
    /// </summary>
    [CommandOption("--files <LIST>")]
    [Description("Explicit comma-separated list of files to validate as multi-part (overrides auto-discovery).")]
    public string? ExplicitFiles { get; init; }

    /// <summary>
    /// Gets a value indicating whether strict validation mode is enabled.
    /// Strict mode is enabled by default and can be disabled with --no-strict.
    /// </summary>
    public bool StrictMode => !DisableStrictMode;

    /// <summary>
    /// Requires a specification path or file list and, when a path is given, that it exists and is YAML or JSON; resolves the path.
    /// </summary>
    /// <returns>A success result, or an error naming the first option that is missing or invalid.</returns>
    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(SpecificationPath) && string.IsNullOrWhiteSpace(ExplicitFiles))
        {
            return ValidationResult.Error("Specification path is required. Use -s or --specification, or --files for explicit list.");
        }

        if (!string.IsNullOrWhiteSpace(SpecificationPath))
        {
            SpecificationPath = PathHelper.ResolveRelativePath(SpecificationPath);
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