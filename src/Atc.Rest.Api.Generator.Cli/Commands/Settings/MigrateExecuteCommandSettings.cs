namespace Atc.Rest.Api.Generator.Cli.Commands.Settings;

/// <summary>
/// Settings for the migrate execute command.
/// </summary>
public sealed class MigrateExecuteCommandSettings : CommandSettings
{
    /// <summary>
    /// Gets or sets the path to the OpenAPI specification the old CLI generated the API from; resolved by <see cref="Validate"/>.
    /// </summary>
    [CommandOption("-s|--specification <PATH>")]
    [Description("Path to the OpenAPI specification file (.yaml/.yml/.json) used to generate the API.")]
    public string SpecificationPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets the specification path given with the old <c>-p|--spec</c> spelling; folded into <see cref="SpecificationPath"/> by <see cref="Validate"/>.
    /// </summary>
    [CommandOption("-p|--spec <PATH>", IsHidden = true)]
    [Description("Old spelling of -s|--specification.")]
    public string? LegacySpecificationPath { get; init; }

    /// <summary>
    /// Gets or sets the solution file or its directory; <see cref="Validate"/> finds the nearest .sln/.slnx when not set.
    /// </summary>
    [CommandOption("--solution <PATH>")]
    [Description("Path to the solution file (.sln/.slnx) or the directory that contains it. Default: the nearest folder with a .sln/.slnx, searched from the specification's folder upwards, then from the current directory upwards.")]
    public string SolutionPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the migration only reports what it would modify, create and delete.
    /// </summary>
    [CommandOption("--dry-run")]
    [Description("Preview changes without executing. Shows what would be modified, created, or deleted.")]
    [DefaultValue(false)]
    public bool DryRun { get; init; }

    /// <summary>
    /// Gets a value indicating whether the git-status and upgrade confirmation prompts are skipped.
    /// </summary>
    [CommandOption("--force")]
    [Description("Skip confirmation prompts (git status check, upgrade confirmations).")]
    [DefaultValue(false)]
    public bool Force { get; init; }

    /// <summary>
    /// Gets a value indicating whether detailed output is shown during the migration.
    /// </summary>
    [CommandOption("--verbose")]
    [Description("Show detailed output during migration.")]
    [DefaultValue(false)]
    public bool Verbose { get; init; }

    /// <summary>
    /// Gets the suffix of the renamed client project (default <c>ApiClient</c>; <c>Api.Client</c> for dotted naming).
    /// </summary>
    [CommandOption("--client-project-suffix <SUFFIX>")]
    [Description("Override the client project suffix. Default: 'ApiClient'. Use 'Api.Client' for dot-separated naming.")]
    public string? ClientProjectSuffix { get; init; }

    /// <summary>
    /// Gets the <c>httpClientName</c> written to the client marker file for the HttpClientFactory registration.
    /// </summary>
    [CommandOption("--http-client-name <NAME>")]
    [Description("Set the httpClientName in the client marker file for HttpClientFactory registration.")]
    public string? HttpClientName { get; init; }

    /// <summary>
    /// Gets notes about how the specification and solution paths were resolved.
    /// </summary>
    internal List<string> PathNotes { get; } = [];

    /// <summary>
    /// Resolves the specification and solution paths through <see cref="MigrationPathResolver"/>, recording how in <see cref="PathNotes"/>.
    /// </summary>
    /// <returns>A success result, or the resolver's error when a path cannot be found.</returns>
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
}