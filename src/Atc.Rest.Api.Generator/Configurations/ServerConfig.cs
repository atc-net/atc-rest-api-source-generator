namespace Atc.Rest.Api.Generator.Configurations;

/// <summary>
/// Configuration for server-side code generation (models, parameters, results, handlers, endpoints, DI).
/// This model is deserialized from the .atc-rest-api-server-contracts marker file.
/// </summary>
public class ServerConfig : BaseConfig
{
    /// <summary>
    /// Strategy for sub-folder organization. Default: FirstPathSegment.
    /// None = all types in root folder, FirstPathSegment = group by URL path segment, OpenApiTag = group by operation tag.
    /// </summary>
    [JsonConverter(typeof(SubFolderStrategyTypeConverter))]
    public SubFolderStrategyType SubFolderStrategy { get; set; } = SubFolderStrategyType.FirstPathSegment;

    /// <summary>
    /// Controls usage of Atc.Rest.MinimalApi package's IEndpointDefinition interface.
    /// "auto" (default) = auto-detect package reference, use package interface if referenced.
    /// true/"enabled" = force use of package interface (error if not referenced).
    /// false/"disabled" = always generate IEndpointDefinition interface (legacy behavior).
    /// </summary>
    [JsonConverter(typeof(MinimalApiPackageModeConverter))]
    public MinimalApiPackageMode UseMinimalApiPackage { get; set; } = MinimalApiPackageMode.Auto;

    /// <summary>
    /// Controls whether to add ValidationFilter&lt;T&gt; to endpoints with parameters.
    /// Requires Atc.Rest.MinimalApi package to be referenced.
    /// "auto" (default) = add ValidationFilter when Atc.Rest.MinimalApi is referenced.
    /// true/"enabled" = force ValidationFilter (error if package not referenced).
    /// false/"disabled" = never add ValidationFilter.
    /// </summary>
    [JsonConverter(typeof(MinimalApiPackageModeConverter))]
    public MinimalApiPackageMode UseValidationFilter { get; set; } = MinimalApiPackageMode.Auto;

    /// <summary>
    /// Controls whether to generate GlobalErrorHandlingMiddleware setup code.
    /// Requires Atc.Rest.MinimalApi package to be referenced.
    /// "auto" (default) = generate when Atc.Rest.MinimalApi is referenced.
    /// true/"enabled" = force generation (error if package not referenced).
    /// false/"disabled" = never generate.
    /// </summary>
    [JsonConverter(typeof(MinimalApiPackageModeConverter))]
    public MinimalApiPackageMode UseGlobalErrorHandler { get; set; } = MinimalApiPackageMode.Auto;

    /// <summary>
    /// Controls whether to generate AtcExceptionMapping middleware for custom exception-to-status-code mapping.
    /// TEMPORARY: Waiting for feature in Atc.Rest.MinimalApi (GitHub issue #22).
    /// "auto" (default) = generate when UseGlobalErrorHandler is enabled.
    /// true/"enabled" = force generation.
    /// false/"disabled" = never generate.
    /// See: https://github.com/atc-net/atc-rest-minimalapi/issues/22
    /// </summary>
    [JsonConverter(typeof(MinimalApiPackageModeConverter))]
    public MinimalApiPackageMode UseAtcExceptionMapping { get; set; } = MinimalApiPackageMode.Auto;

    /// <summary>
    /// Whether to report supported API versions in response headers. Default: true.
    /// When true, adds api-supported-versions and api-deprecated-versions headers to responses.
    /// </summary>
    public bool ReportApiVersions { get; set; } = true;

    /// <summary>
    /// Whether to assume default API version when client doesn't specify one. Default: true.
    /// Only applies to QueryString and Header strategies (UrlSegment always requires explicit version).
    /// </summary>
    public bool AssumeDefaultVersionWhenUnspecified { get; set; } = true;

    /// <summary>
    /// Generate models as partial records. Default: false.
    /// When true, models are generated as partial records allowing extension in separate files.
    /// Useful for adding interfaces (via x-implements extension) or additional properties.
    /// </summary>
    public bool GeneratePartialModels { get; set; }

    /// <summary>
    /// Generate webhook handlers, parameters, results, and endpoints. Default: true.
    /// When true and the OpenAPI spec contains webhooks, generates:
    /// - Handler interfaces (I{OperationId}WebhookHandler)
    /// - Parameter classes ({OperationId}WebhookParameters)
    /// - Result classes ({OperationId}WebhookResult)
    /// - Endpoint registration (Map{ProjectName}Webhooks)
    /// - DI extension (Add{ProjectName}WebhookHandlers)
    /// </summary>
    public bool GenerateWebhooks { get; set; } = true;

    /// <summary>
    /// Base path for webhook endpoints. Default: "/webhooks".
    /// The webhook endpoints are registered under this route group prefix.
    /// </summary>
    public string WebhookBasePath { get; set; } = "/webhooks";

    /// <summary>
    /// Configuration for multi-part OpenAPI specification support.
    /// Controls how multiple YAML files are merged (e.g., Showcase.yaml + Showcase_Accounts.yaml).
    /// When null, uses MultiPartConfiguration.Default with auto-discovery and ErrorOnDuplicate strategy.
    /// </summary>
    public MultiPartConfiguration? MultiPartConfiguration { get; set; }
}