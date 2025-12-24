namespace Atc.Rest.Api.CliGenerator.Commands.Settings;

/// <summary>
/// Settings for the options create and validate commands.
/// </summary>
public sealed class OptionsCommandSettings : CommandSettings
{
    [CommandOption("-o|--output <PATH>")]
    [Description("Path to the directory or file for the options file.")]
    public string OutputPath { get; init; } = string.Empty;

    [CommandOption("-f|--force")]
    [Description("Overwrite existing file if it exists.")]
    [DefaultValue(false)]
    public bool Force { get; init; }

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            return ValidationResult.Error("Output path is required. Use -o or --output.");
        }

        return ValidationResult.Success();
    }
}