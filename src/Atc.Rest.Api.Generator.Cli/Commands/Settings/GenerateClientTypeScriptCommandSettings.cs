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

    [CommandOption("--hooks <STYLE>")]
    [Description("Hook generation style: None (default) or ReactQuery (TanStack Query hooks).")]
    public string? HooksStyle { get; init; }

    [CommandOption("--client-type <TYPE>")]
    [Description("HTTP client library: Fetch (native fetch API, default) or Axios.")]
    public string? ClientType { get; init; }

    [CommandOption("--naming-strategy <STRATEGY>")]
    [Description("Naming strategy for properties and parameters: CamelCase (default), Original, or PascalCase.")]
    public string? NamingStrategy { get; init; }

    [CommandOption("--convert-dates")]
    [Description("Convert date/date-time properties to Date objects with automatic JSON reviver/replacer.")]
    [DefaultValue(false)]
    public bool ConvertDates { get; init; }

    [CommandOption("--no-readonly")]
    [Description("Generate mutable model properties (omit readonly modifier).")]
    [DefaultValue(false)]
    public bool NoReadonly { get; init; }

    [CommandOption("--zod")]
    [Description("Generate Zod runtime validation schemas alongside model and enum files.")]
    [DefaultValue(false)]
    public bool GenerateZodSchemas { get; init; }

    [CommandOption("--dry-run")]
    [Description("Preview what would be generated without writing any files.")]
    [DefaultValue(false)]
    public bool DryRun { get; init; }

    [CommandOption("--watch")]
    [Description("Watch the specification file for changes and re-generate automatically.")]
    [DefaultValue(false)]
    public bool Watch { get; init; }

    [CommandOption("--report")]
    [Description("Generate a .generation-report.md file in the output directory.")]
    [DefaultValue(false)]
    public bool GenerateReport { get; init; }

    [CommandOption("--scaffold")]
    [Description("Generate package.json and tsconfig.json to make the output a ready-to-use npm package.")]
    [DefaultValue(false)]
    public bool Scaffold { get; init; }

    [CommandOption("--package-name <NAME>")]
    [Description("Package name for generated package.json (default: derived from OpenAPI info.title).")]
    public string? PackageName { get; init; }

    [CommandOption("--package-version <VERSION>")]
    [Description("Package version for generated package.json (default: 0.1.0).")]
    public string? PackageVersion { get; init; }

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

        // Validate hooks style if provided
        if (!string.IsNullOrWhiteSpace(HooksStyle) &&
            !Enum.TryParse<TypeScriptHooksStyle>(HooksStyle, ignoreCase: true, out _))
        {
            return ValidationResult.Error($"Invalid hooks style: '{HooksStyle}'. Valid values: None, ReactQuery.");
        }

        // Validate client type if provided
        if (!string.IsNullOrWhiteSpace(ClientType) &&
            !Enum.TryParse<TypeScriptHttpClient>(ClientType, ignoreCase: true, out _))
        {
            return ValidationResult.Error($"Invalid client type: '{ClientType}'. Valid values: Fetch, Axios.");
        }

        // Validate naming strategy if provided
        if (!string.IsNullOrWhiteSpace(NamingStrategy) &&
            !Enum.TryParse<TypeScriptNamingStrategy>(NamingStrategy, ignoreCase: true, out _))
        {
            return ValidationResult.Error($"Invalid naming strategy: '{NamingStrategy}'. Valid values: CamelCase, Original, PascalCase.");
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