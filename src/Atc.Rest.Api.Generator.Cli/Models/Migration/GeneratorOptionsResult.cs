namespace Atc.Rest.Api.Generator.Cli.Models.Migration;

/// <summary>
/// Result of generator options file analysis.
/// </summary>
public sealed class GeneratorOptionsResult
{
    /// <summary>
    /// Gets or sets a value indicating whether an options file was found.
    /// </summary>
    public bool Found { get; set; }

    /// <summary>
    /// Gets or sets the path to the options file if found.
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the options file is valid.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the ASP.NET output type (MinimalApi, Mvc).
    /// </summary>
    public string? AspNetOutputType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether REST extended features are used.
    /// </summary>
    public bool? UseRestExtended { get; set; }

    /// <summary>
    /// Gets or sets the HTTP client name.
    /// </summary>
    public string? HttpClientName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether ProblemDetails is used for responses.
    /// </summary>
    public bool? UseProblemDetails { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether strict validation mode is enabled.
    /// </summary>
    public bool? StrictMode { get; set; }

    /// <summary>
    /// Gets or sets the operation ID casing style.
    /// </summary>
    public string? OperationIdCasingStyle { get; set; }

    /// <summary>
    /// Gets or sets the model name casing style.
    /// </summary>
    public string? ModelNameCasingStyle { get; set; }

    /// <summary>
    /// Gets or sets the model property name casing style.
    /// </summary>
    public string? ModelPropertyNameCasingStyle { get; set; }

    /// <summary>
    /// Gets or sets any parsing errors.
    /// </summary>
    public List<string> ParsingErrors { get; set; } = [];

    /// <summary>
    /// Gets a value indicating whether the project uses MinimalApi.
    /// </summary>
    public bool IsMinimalApi =>
        string.Equals(AspNetOutputType, "MinimalApi", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets a value indicating whether the project uses Controllers (MVC).
    /// </summary>
    public bool IsControllerBased =>
        string.Equals(AspNetOutputType, "Mvc", StringComparison.OrdinalIgnoreCase);
}