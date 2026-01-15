namespace Atc.Rest.Api.Generator.Configurations;

/// <summary>
/// Configuration for client-side code generation (HTTP client, models).
/// This model is deserialized from the .atc-rest-api-client marker file.
/// </summary>
public class ClientConfig : BaseConfig
{
    /// <summary>
    /// Client generation mode. Default: TypedClient.
    /// TypedClient: Single HTTP client class with methods for each operation.
    /// EndpointPerOperation: Separate endpoint classes per operation (requires Atc.Rest.Client).
    /// </summary>
    [JsonConverter(typeof(GenerationModeTypeConverter))]
    public GenerationModeType GenerationMode { get; set; } = GenerationModeType.TypedClient;

    /// <summary>
    /// Custom HTTP client class name suffix. Default: "Client".
    /// </summary>
    public string ClientSuffix { get; set; } = "Client";

    /// <summary>
    /// The HTTP client name to use in IHttpClientFactory. Default: "{ProjectName}-ApiClient".
    /// </summary>
    public string? HttpClientName { get; set; }

    /// <summary>
    /// Enable OAuth2 token management generation. Default: true.
    /// When enabled and OpenAPI spec contains OAuth2 security with Client Credentials
    /// or Authorization Code flow, generates token provider, handler, and DI extensions.
    /// </summary>
    public bool GenerateOAuthTokenManagement { get; set; } = true;

    /// <summary>
    /// Generate models as partial records. Default: false.
    /// When true, models are generated as partial records allowing extension in separate files.
    /// Useful for adding interfaces (via x-implements extension) or additional properties.
    /// </summary>
    public bool GeneratePartialModels { get; set; }

    /// <summary>
    /// Error response format expected from the API. Default: ProblemDetails.
    /// Only applies to EndpointPerOperation generation mode.
    /// </summary>
    [JsonConverter(typeof(ErrorResponseFormatTypeConverter))]
    public ErrorResponseFormatType ErrorResponseFormat { get; set; } = ErrorResponseFormatType.ProblemDetails;

    /// <summary>
    /// Custom error response model to use instead of ProblemDetails. Default: null (uses ProblemDetails).
    /// When specified and ErrorResponseFormat is Custom, generates a custom error response type with the defined schema.
    /// </summary>
    public CustomErrorResponseModelConfig? CustomErrorResponseModel { get; set; }

    /// <summary>
    /// Configuration for multi-part OpenAPI specification support.
    /// Controls how multiple YAML files are merged (e.g., Showcase.yaml + Showcase_Accounts.yaml).
    /// When null, uses MultiPartConfiguration.Default with auto-discovery and ErrorOnDuplicate strategy.
    /// </summary>
    public MultiPartConfiguration? MultiPartConfiguration { get; set; }
}