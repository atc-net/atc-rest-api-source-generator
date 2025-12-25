namespace Atc.Rest.Api.CliGenerator.Commands.Settings;

/// <summary>
/// Base settings shared across all generate commands.
/// </summary>
public class BaseGenerateCommandSettings : CommandSettings
{
    [CommandOption("-s|--specification <PATH>")]
    [Description("Path to the OpenAPI specification file (YAML or JSON).")]
    public string SpecificationPath { get; set; } = string.Empty;

    [CommandOption("-o|--output <PATH>")]
    [Description("Path to the output directory for the generated project.")]
    public string OutputPath { get; set; } = string.Empty;

    [CommandOption("-n|--name <NAME>")]
    [Description("Name of the project (e.g., 'Demo.Api.Contracts').")]
    public string ProjectName { get; init; } = string.Empty;

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

    [CommandOption("--namespace <NAMESPACE>")]
    [Description("Custom namespace prefix for generated code (auto-detected from project name if not specified).")]
    public string? Namespace { get; init; }

    /// <summary>
    /// Gets a value indicating whether strict validation mode is enabled.
    /// Strict mode is enabled by default and can be disabled with --no-strict.
    /// </summary>
    public bool StrictMode => !DisableStrictMode;

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

        if (string.IsNullOrWhiteSpace(ProjectName))
        {
            return ValidationResult.Error("Project name is required. Use -n or --name.");
        }

        // Validate project name format (basic validation)
        if (ProjectName.Contains(' ', StringComparison.Ordinal))
        {
            return ValidationResult.Error("Project name cannot contain spaces.");
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