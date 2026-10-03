namespace Atc.Rest.Api.Generator.Cli.Commands.Settings;

/// <summary>
/// Settings for the options create and validate commands.
/// </summary>
public sealed class OptionsCommandSettings : CommandSettings
{
    /// <summary>
    /// Gets or sets the options file path, or the directory that holds (or will hold) ApiGeneratorOptions.json.
    /// </summary>
    [CommandOption("-o|--output <PATH>")]
    [Description("Path to the directory or file for the options file.")]
    public string OutputPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether <c>options create</c> may overwrite an existing options file.
    /// </summary>
    [CommandOption("-f|--force")]
    [Description("Overwrite existing file if it exists.")]
    [DefaultValue(false)]
    public bool Force { get; init; }

    /// <summary>
    /// Requires an output path and resolves it to an absolute path.
    /// </summary>
    /// <returns>A success result, or an error when the output path is missing.</returns>
    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            return ValidationResult.Error("Output path is required. Use -o or --output.");
        }

        OutputPath = PathHelper.ResolveRelativePath(OutputPath);

        return ValidationResult.Success();
    }
}