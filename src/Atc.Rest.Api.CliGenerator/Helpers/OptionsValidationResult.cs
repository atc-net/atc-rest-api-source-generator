namespace Atc.Rest.Api.CliGenerator.Helpers;

/// <summary>
/// Result of validating an ApiGeneratorOptions.json file.
/// </summary>
public sealed class OptionsValidationResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the options file is valid.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the loaded options if valid.
    /// </summary>
    public ApiGeneratorOptions? Options { get; set; }

    /// <summary>
    /// Gets the list of validation errors.
    /// </summary>
    public IList<string> Errors { get; } = new List<string>();

    /// <summary>
    /// Gets the list of validation warnings.
    /// </summary>
    public IList<string> Warnings { get; } = new List<string>();
}