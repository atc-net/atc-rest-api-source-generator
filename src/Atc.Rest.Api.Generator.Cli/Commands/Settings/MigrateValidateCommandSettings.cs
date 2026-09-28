namespace Atc.Rest.Api.Generator.Cli.Commands.Settings;

/// <summary>
/// Settings for the migrate validate command.
/// </summary>
public sealed class MigrateValidateCommandSettings : CommandSettings
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

    [CommandOption("--verbose")]
    [Description("Show detailed validation output including all detected files and configurations.")]
    [DefaultValue(false)]
    public bool Verbose { get; init; }

    [CommandOption("--output-report <PATH>")]
    [Description("Save the validation report to a JSON file.")]
    public string? OutputReportPath { get; init; }

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