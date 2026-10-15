namespace Atc.Rest.Api.Generator.Configurations;

/// <summary>
/// Configuration for server-side code generation (models, parameters, results, handlers, endpoints, DI).
/// This model is deserialized from the .atc-rest-api-server marker file.
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

    /// <summary>
    /// Configuration for auto-generated health check endpoints.
    /// When enabled, generates /health, /health/live, and /health/ready with optional API key security.
    /// </summary>
    public HealthCheckConfig? HealthChecks { get; set; }

    /// <summary>
    /// How to generate an inline <c>enum</c> declared directly on a path/query/header
    /// parameter. Default: <see cref="InlineParameterEnumMode.Enum"/> (a named C# enum type).
    /// Set to <see cref="InlineParameterEnumMode.String"/> to keep the pre-1.0.252 behavior
    /// where such parameters are generated as <c>string</c>, avoiding a source-breaking change
    /// for handlers/services written against the old shape.
    /// </summary>
    [JsonConverter(typeof(InlineParameterEnumModeConverter))]
    public InlineParameterEnumMode InlineParameterEnums { get; set; } = InlineParameterEnumMode.Enum;

    /// <summary>
    /// Whether the endpoints that bind a form (a file upload, a URL-encoded form) validate an antiforgery token.
    /// Default: false - they call <c>DisableAntiforgery()</c>: an API's callers authenticate with a token in a header,
    /// which a cross-site form cannot send. Set it for a host whose browser clients authenticate with a cookie; the
    /// host then calls <c>AddAntiforgery()</c> and <c>UseAntiforgery()</c>, or a form post fails at request time.
    /// </summary>
    public bool UseAntiforgery { get; set; }

    /// <summary>
    /// Whether a string enum is also read by its C# member name. Default: false - only the specification's value
    /// (<c>rtsp</c>) is read. When true, a JSON body may also carry the member name (<c>Rtsp</c>), in another casing
    /// when only one member matches; the value is still what is written. For a rollout: a client built on 2.x wrote
    /// the member name for a value that is no member name (2.x wrote <c>Rtsp</c> for <c>rtsp</c>).
    /// </summary>
    public bool ReadEnumMemberNames { get; set; }

    /// <summary>
    /// The API's name in the generated host methods: <c>Add{ApiName}Api()</c>, <c>Map{ApiName}Api()</c>,
    /// <c>Map{ApiName}Webhooks()</c>, <c>Add{ApiName}OpenIdConnectAuthentication()</c>. Default: null - the namespace's last segment
    /// that names the API (a trailing <c>Contracts</c>, <c>Api</c> or <c>Server</c> is skipped:
    /// <c>Contoso.IoT.Nexus.Api.Contracts</c> gives <c>AddNexusApi()</c>).
    /// </summary>
    public string? ApiName { get; set; }
}