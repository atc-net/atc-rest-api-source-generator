namespace Atc.Rest.Api.Generator.Cli.Commands.Settings;

/// <summary>
/// Settings for the migrate validate command.
/// </summary>
public sealed class MigrateValidateCommandSettings : CommandSettings
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
    /// Gets a value indicating whether every detected file and configuration is listed in the report output.
    /// </summary>
    [CommandOption("--verbose")]
    [Description("Show detailed validation output including all detected files and configurations.")]
    [DefaultValue(false)]
    public bool Verbose { get; init; }

    /// <summary>
    /// Gets the path of the JSON file the validation report is saved to; nothing is saved when not set.
    /// </summary>
    [CommandOption("--output-report <PATH>")]
    [Description("Save the validation report to a JSON file.")]
    public string? OutputReportPath { get; init; }

    /// <summary>
    /// Gets notes about how the specification and solution paths were resolved.
    /// </summary>
    internal List<string> PathNotes { get; } = [];

    /// <summary>
    /// Resolves the specification and solution paths through <see cref="MigrationPathResolver"/> and requires a .json report path when one is given.
    /// </summary>
    /// <returns>A success result, or an error when a path cannot be found or the report path is not .json.</returns>
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

        // Validate output report path if provided
        if (!string.IsNullOrWhiteSpace(OutputReportPath))
        {
            var reportExtension = Path.GetExtension(OutputReportPath).ToLowerInvariant();
            if (reportExtension is not ".json")
            {
                return ValidationResult.Error("Output report file must be a JSON (.json) file.");
            }
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