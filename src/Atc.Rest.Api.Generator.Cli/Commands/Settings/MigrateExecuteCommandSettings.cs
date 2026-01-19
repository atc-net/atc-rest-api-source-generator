namespace Atc.Rest.Api.Generator.Cli.Commands.Settings;

/// <summary>
/// Settings for the migrate execute command.
/// </summary>
public sealed class MigrateExecuteCommandSettings : CommandSettings
{
    [CommandOption("-s|--solution <PATH>")]
    [Description("Path to the solution file (.sln/.slnx) or root directory of the project to migrate.")]
    public string SolutionPath { get; set; } = string.Empty;

    [CommandOption("-p|--spec <PATH>")]
    [Description("Path to the OpenAPI specification file (.yaml/.yml/.json) used to generate the API.")]
    public string SpecificationPath { get; set; } = string.Empty;

    [CommandOption("--dry-run")]
    [Description("Preview changes without executing. Shows what would be modified, created, or deleted.")]
    [DefaultValue(false)]
    public bool DryRun { get; init; }

    [CommandOption("--force")]
    [Description("Skip confirmation prompts (git status check, upgrade confirmations).")]
    [DefaultValue(false)]
    public bool Force { get; init; }

    [CommandOption("--verbose")]
    [Description("Show detailed output during migration.")]
    [DefaultValue(false)]
    public bool Verbose { get; init; }

    [CommandOption("--client-project-suffix <SUFFIX>")]
    [Description("Override the client project suffix. Default: 'ApiClient'. Use 'Api.Client' for dot-separated naming.")]
    public string? ClientProjectSuffix { get; init; }

    [CommandOption("--http-client-name <NAME>")]
    [Description("Set the httpClientName in the client marker file for HttpClientFactory registration.")]
    public string? HttpClientName { get; init; }

    public override ValidationResult Validate()
    {
        // Validate solution path
        if (string.IsNullOrWhiteSpace(SolutionPath))
        {
            return ValidationResult.Error("Solution path is required. Use -s or --solution.");
        }

        SolutionPath = PathHelper.ResolveRelativePath(SolutionPath);

        // Check if it's a file or directory
        if (File.Exists(SolutionPath))
        {
            var extension = Path.GetExtension(SolutionPath).ToLowerInvariant();
            if (extension is not ".sln" and not ".slnx")
            {
                return ValidationResult.Error("Solution file must be a .sln or .slnx file.");
            }
        }
        else if (!Directory.Exists(SolutionPath))
        {
            return ValidationResult.Error($"Solution path not found: {SolutionPath}");
        }

        // Validate specification path
        if (string.IsNullOrWhiteSpace(SpecificationPath))
        {
            return ValidationResult.Error("Specification path is required. Use -p or --spec.");
        }

        SpecificationPath = PathHelper.ResolveRelativePath(SpecificationPath);

        if (!File.Exists(SpecificationPath))
        {
            return ValidationResult.Error($"Specification file not found: {SpecificationPath}");
        }

        var specExtension = Path.GetExtension(SpecificationPath).ToLowerInvariant();
        if (specExtension is not ".yaml" and not ".yml" and not ".json")
        {
            return ValidationResult.Error("Specification file must be a YAML (.yaml, .yml) or JSON (.json) file.");
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Gets the root directory of the solution.
    /// </summary>
    public string GetSolutionDirectory()
    {
        if (Directory.Exists(SolutionPath))
        {
            return SolutionPath;
        }

        return Path.GetDirectoryName(SolutionPath) ?? SolutionPath;
    }
}