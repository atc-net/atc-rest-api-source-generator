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

    /// <summary>
    /// Whether to use the base path from OpenAPI servers[0].url for endpoint routing and client requests.
    /// Default: true. When enabled, extracts the path portion from the first server URL (e.g., "/api/v1").
    /// </summary>
    public bool UseServersBasePath { get; set; } = true;

    /// <summary>
    /// API versioning strategy. Default: None.
    /// None = no versioning, QueryString = ?api-version=1.0, UrlSegment = /v1/path, Header = X-Api-Version header.
    /// When enabled, generates versioned endpoint groups and AddApiVersioning() DI registration.
    /// </summary>
    [JsonConverter(typeof(VersioningStrategyTypeConverter))]
    public VersioningStrategyType VersioningStrategy { get; set; } = VersioningStrategyType.None;

    /// <summary>
    /// Default API version to use when versioning is enabled. Default: "1.0".
    /// Format: "major.minor" (e.g., "1.0", "2.0").
    /// Used for AssumeDefaultVersionWhenUnspecified option.
    /// </summary>
    public string DefaultApiVersion { get; set; } = "1.0";

    /// <summary>
    /// Query string parameter name for version (when VersioningStrategy is QueryString). Default: "api-version".
    /// </summary>
    public string VersionQueryParameterName { get; set; } = "api-version";

    /// <summary>
    /// HTTP header name for version (when VersioningStrategy is Header). Default: "X-Api-Version".
    /// </summary>
    public string VersionHeaderName { get; set; } = "X-Api-Version";

    /// <summary>
    /// Route segment template for versioning (when VersioningStrategy is UrlSegment). Default: "v{version:apiVersion}".
    /// Used in route templates like "/api/v{version:apiVersion}/pets".
    /// </summary>
    public string VersionRouteSegmentTemplate { get; set; } = "v{version:apiVersion}";

    /// <summary>
    /// Remove blank lines between namespace groups in GlobalUsings.cs.
    /// Default: false (blank lines are preserved between groups).
    /// </summary>
    public bool RemoveNamespaceGroupSeparatorInGlobalUsings { get; set; }
}