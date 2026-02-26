namespace Atc.Rest.Api.Generator.Cli.Commands.Settings;

/// <summary>
/// Settings for the generate client-typescript command.
/// Does not extend BaseGenerateCommandSettings because TypeScript
/// generation does not require --name/--namespace options.
/// </summary>
public sealed class GenerateClientTypeScriptCommandSettings : CommandSettings
{
    [CommandOption("-s|--specification <PATH>")]
    [Description("Path to the OpenAPI specification file (YAML or JSON).")]
    public string SpecificationPath { get; set; } = string.Empty;

    [CommandOption("-o|--output <PATH>")]
    [Description("Path to the output directory for the generated TypeScript files.")]
    public string OutputPath { get; set; } = string.Empty;

    [CommandOption("--options <PATH>")]
    [Description("Path to ApiGeneratorOptions.json file (optional, auto-discovered if not specified).")]
    public string? OptionsPath { get; set; }

    [CommandOption("--no-strict")]
    [Description("Disable strict validation mode (skip additional naming convention checks).")]
    [DefaultValue(false)]
    public bool DisableStrictMode { get; init; }

    [CommandOption("--include-deprecated")]
    [Description("Include deprecated operations and schemas in generated code.")]
    [DefaultValue(false)]
    public bool IncludeDeprecated { get; init; }

    [CommandOption("--enum-style <STYLE>")]
    [Description("Enum generation style: Union (string union types, default) or Enum (TypeScript enum declarations).")]
    public string? EnumStyle { get; init; }

    [CommandOption("--report")]
    [Description("Generate a .generation-report.md file in the output directory.")]
    [DefaultValue(false)]
    public bool GenerateReport { get; init; }

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(SpecificationPath))
        {
            return ValidationResult.Error("Specification path is required. Use -s or --specification.");
        }

        SpecificationPath = PathHelper.ResolveRelativePath(SpecificationPath);

        if (!File.Exists(SpecificationPath))
        {
            return ValidationResult.Error($"Specification file not found: {SpecificationPath}");
        }

        var extension = Path.GetExtension(SpecificationPath).ToLowerInvariant();
        if (extension is not ".yaml" and not ".yml" and not ".json")
        {
            return ValidationResult.Error("Specification file must be a YAML (.yaml, .yml) or JSON (.json) file.");
        }

        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            return ValidationResult.Error("Output path is required. Use -o or --output.");
        }

        OutputPath = PathHelper.ResolveRelativePath(OutputPath);

        // Validate enum style if provided
        if (!string.IsNullOrWhiteSpace(EnumStyle) &&
            !Enum.TryParse<TypeScriptEnumStyle>(EnumStyle, ignoreCase: true, out _))
        {
            return ValidationResult.Error($"Invalid enum style: '{EnumStyle}'. Valid values: Union, Enum.");
        }

        // Validate options file if explicitly provided
        if (!string.IsNullOrWhiteSpace(OptionsPath))
        {
            OptionsPath = PathHelper.ResolveRelativePath(OptionsPath);

            if (!File.Exists(OptionsPath) &&
                !Directory.Exists(OptionsPath))
            {
                return ValidationResult.Error($"Options file or directory not found: {OptionsPath}");
            }
        }

        return ValidationResult.Success();
    }
}