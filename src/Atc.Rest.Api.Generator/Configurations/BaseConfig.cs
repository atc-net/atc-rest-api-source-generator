namespace Atc.Rest.Api.Generator.Configurations;

/// <summary>
/// Base configuration class for all generator configurations.
/// Contains common properties shared by server, client, and domain configurations.
/// </summary>
public abstract class BaseConfig
{
    /// <summary>
    /// Enable/disable code generation. Default: true.
    /// </summary>
    public bool Generate { get; set; } = true;

    /// <summary>
    /// OpenAPI specification validation strategy. Default: Strict.
    /// None = skip validation, Standard = Microsoft.OpenApi errors only, Strict = standard + custom ATC rules.
    /// </summary>
    [JsonConverter(typeof(ValidateSpecificationStrategyTypeConverter))]
    public ValidateSpecificationStrategy ValidateSpecificationStrategy { get; set; } = ValidateSpecificationStrategy.Strict;

    /// <summary>
    /// Include deprecated operations and schemas in generated code. Default: false.
    /// When false, operations and schemas marked as deprecated in the OpenAPI spec are excluded.
    /// </summary>
    public bool IncludeDeprecated { get; set; }

    /// <summary>
    /// Custom namespace prefix (null = auto-detect from project name). Default: null.
    /// </summary>
    public string? Namespace { get; set; }
}