namespace Atc.Rest.Api.Generator.Cli.Commands.Settings;

/// <summary>
/// Settings for the migrate execute command.
/// </summary>
public sealed class MigrateExecuteCommandSettings : CommandSettings
{
    [CommandOption("-s|--specification <PATH>")]
    [Description("Path to the OpenAPI specification file (.yaml/.yml/.json) used to generate the API.")]
    public string SpecificationPath { get; set; } = string.Empty;

    [CommandOption("-p|--spec <PATH>", IsHidden = true)]
    [Description("Old spelling of -s|--specification.")]
    public string? LegacySpecificationPath { get; init; }

    [CommandOption("--solution <PATH>")]
    [Description("Path to the solution file (.sln/.slnx) or the directory that contains it. Default: the nearest folder with a .sln/.slnx, searched from the specification's folder upwards, then from the current directory upwards.")]
    public string SolutionPath { get; set; } = string.Empty;

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

    /// <summary>
    /// Gets notes about how the specification and solution paths were resolved.
    /// </summary>
    internal List<string> PathNotes { get; } = [];

    public override ValidationResult Validate()
    {
        var solutionPath = SolutionPath;
        var specificationPath = SpecificationPath;
        var error = MigrationPathResolver.Resolve(ref specificationPath, LegacySpecificationPath, ref solutionPath, PathNotes);
        SolutionPath = solutionPath;
        SpecificationPath = specificationPath;
        if (error is not null)
        {
            return ValidationResult.Error(error);
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