namespace Atc.Rest.Api.CliGenerator.Options;

/// <summary>
/// Server-side code generation configuration options.
/// </summary>
public sealed class ServerOptions
{
    /// <summary>
    /// Custom namespace prefix (null = auto-detect from project name).
    /// Default: null.
    /// </summary>
    public string? Namespace { get; set; }

    /// <summary>
    /// Sub-folder organization strategy for generated types.
    /// Default: FirstPathSegment.
    /// </summary>
    public SubFolderStrategyType SubFolderStrategy { get; set; } = SubFolderStrategyType.FirstPathSegment;

    /// <summary>
    /// Controls Atc.Rest.MinimalApi package integration.
    /// Default: Auto (auto-detect package reference).
    /// </summary>
    public MinimalApiPackageMode UseMinimalApiPackage { get; set; } = MinimalApiPackageMode.Auto;

    /// <summary>
    /// Controls ValidationFilter generation for endpoints with parameters.
    /// Default: Auto.
    /// </summary>
    public MinimalApiPackageMode UseValidationFilter { get; set; } = MinimalApiPackageMode.Auto;

    /// <summary>
    /// Controls GlobalErrorHandlingMiddleware setup generation.
    /// Default: Auto.
    /// </summary>
    public MinimalApiPackageMode UseGlobalErrorHandler { get; set; } = MinimalApiPackageMode.Auto;

    /// <summary>
    /// API versioning strategy.
    /// Default: None (no versioning).
    /// </summary>
    public VersioningStrategyType VersioningStrategy { get; set; } = VersioningStrategyType.None;

    /// <summary>
    /// Default API version when versioning is enabled.
    /// Format: "major.minor" (e.g., "1.0", "2.0").
    /// Default: "1.0".
    /// </summary>
    public string DefaultApiVersion { get; set; } = "1.0";

    /// <summary>
    /// Query parameter name for QueryString versioning strategy.
    /// Default: "api-version".
    /// </summary>
    public string VersionQueryParameterName { get; set; } = "api-version";

    /// <summary>
    /// HTTP header name for Header versioning strategy.
    /// Default: "X-Api-Version".
    /// </summary>
    public string VersionHeaderName { get; set; } = "X-Api-Version";

    /// <summary>
    /// Route segment template for UrlSegment versioning strategy.
    /// Default: "v{version:apiVersion}".
    /// </summary>
    public string VersionRouteSegmentTemplate { get; set; } = "v{version:apiVersion}";

    /// <summary>
    /// Report supported API versions in response headers (api-supported-versions, api-deprecated-versions).
    /// Default: true.
    /// </summary>
    public bool ReportApiVersions { get; set; } = true;

    /// <summary>
    /// Assume default API version when client doesn't specify.
    /// Applies to QueryString and Header strategies only.
    /// Default: true.
    /// </summary>
    public bool AssumeDefaultVersionWhenUnspecified { get; set; } = true;

    /// <summary>
    /// Domain handler scaffolding configuration.
    /// </summary>
    public ServerDomainOptions Domain { get; set; } = new();
}