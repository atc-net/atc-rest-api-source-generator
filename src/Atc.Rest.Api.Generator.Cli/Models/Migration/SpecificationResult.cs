namespace Atc.Rest.Api.Generator.Cli.Models.Migration;

/// <summary>
/// Result of OpenAPI specification validation.
/// </summary>
public sealed class SpecificationResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the specification file exists.
    /// </summary>
    public bool FileExists { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the specification is valid OpenAPI.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the OpenAPI version (e.g., "3.0.1", "3.1.0").
    /// </summary>
    public string? OpenApiVersion { get; set; }

    /// <summary>
    /// Gets or sets the API title from the specification.
    /// </summary>
    public string? ApiTitle { get; set; }

    /// <summary>
    /// Gets or sets the API version from the specification.
    /// </summary>
    public string? ApiVersion { get; set; }

    /// <summary>
    /// Gets or sets the number of operations/endpoints in the specification.
    /// </summary>
    public int OperationCount { get; set; }

    /// <summary>
    /// Gets or sets the number of schemas in the specification.
    /// </summary>
    public int SchemaCount { get; set; }

    /// <summary>
    /// Gets or sets any detected multi-part specification files.
    /// </summary>
    public List<string> MultiPartFiles { get; set; } = [];

    /// <summary>
    /// Gets or sets validation errors from parsing.
    /// </summary>
    public List<string> ValidationErrors { get; set; } = [];

    /// <summary>
    /// Gets a value indicating whether multi-part specifications were detected.
    /// </summary>
    public bool HasMultiPartFiles => MultiPartFiles.Count > 0;
}