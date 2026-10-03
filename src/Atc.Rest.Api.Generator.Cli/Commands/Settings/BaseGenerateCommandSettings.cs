namespace Atc.Rest.Api.Generator.Cli.Commands.Settings;

/// <summary>
/// Base settings shared across all generate commands.
/// </summary>
public class BaseGenerateCommandSettings : CommandSettings
{
    /// <summary>
    /// Gets or sets the path to the OpenAPI specification file (YAML or JSON); resolved to an absolute path by <see cref="Validate"/>.
    /// </summary>
    [CommandOption("-s|--specification <PATH>")]
    [Description("Path to the OpenAPI specification file (YAML or JSON).")]
    public string SpecificationPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the output directory for the generated project; resolved to an absolute path by <see cref="Validate"/>.
    /// </summary>
    [CommandOption("-o|--output <PATH>")]
    [Description("Path to the output directory for the generated project.")]
    public string OutputPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets the name of the generated project, e.g. <c>Demo.Api.Contracts</c>.
    /// </summary>
    [CommandOption("-n|--name <NAME>")]
    [Description("Name of the project (e.g., 'Demo.Api.Contracts').")]
    public string ProjectName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets an explicit path to the ApiGeneratorOptions.json file or its directory; auto-discovered when not set.
    /// </summary>
    [CommandOption("--options <PATH>")]
    [Description("Path to ApiGeneratorOptions.json file (optional, auto-discovered if not specified).")]
    public string? OptionsPath { get; set; }

    /// <summary>
    /// Gets a value indicating whether the specification is validated with the standard strategy instead of the strict one.
    /// </summary>
    [CommandOption("--no-strict")]
    [Description("Validate with the Standard strategy: only the OpenAPI parser's errors and structural checks, not the ATC rules.")]
    [DefaultValue(false)]
    public bool DisableStrictMode { get; init; }

    /// <summary>
    /// Gets a value indicating whether deprecated operations and schemas are generated instead of skipped.
    /// </summary>
    [CommandOption("--include-deprecated")]
    [Description("Include deprecated operations and schemas in generated code.")]
    [DefaultValue(false)]
    public bool IncludeDeprecated { get; init; }

    /// <summary>
    /// Gets the namespace prefix for the generated code; derived from <see cref="ProjectName"/> when not set.
    /// </summary>
    [CommandOption("--namespace <NAMESPACE>")]
    [Description("Custom namespace prefix for generated code (auto-detected from project name if not specified).")]
    public string? Namespace { get; init; }

    /// <summary>
    /// Checks that the specification, output path and project name are usable and resolves the paths to absolute ones.
    /// </summary>
    /// <returns>A success result, or an error naming the first option that is missing or invalid.</returns>
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