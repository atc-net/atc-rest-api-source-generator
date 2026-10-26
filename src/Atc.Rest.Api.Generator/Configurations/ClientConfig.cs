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
    /// Controls how many typed HTTP clients are generated. Default: PerArea.
    /// PerArea: one client per API area (first path segment) - the historical behaviour.
    /// Single: one client covering all operations across all path segments.
    /// </summary>
    [JsonConverter(typeof(ClientGranularityTypeConverter))]
    public ClientGranularityType ClientGranularity { get; set; } = ClientGranularityType.PerArea;

    /// <summary>
    /// Explicit name for the generated client type. Default: null (derived from the namespace).
    /// Only meaningful when <see cref="ClientGranularity"/> is Single, in which case the value is
    /// used verbatim and no suffix is appended. Ignored with a warning under PerArea.
    /// </summary>
    public string? ClientName { get; set; }

    /// <summary>
    /// Controls how generated typed client operations report non-success HTTP responses. Default: Throw.
    /// Throw: non-success status codes throw an HttpRequestException carrying the response body.
    /// Result: operations return EndpointResponse/EndpointResponse&lt;T&gt; so callers can inspect the
    /// status without exception handling. Transport-level failures still throw.
    /// Only applies to TypedClient generation mode, and requires a reference to Atc.Rest.Client.
    /// </summary>
    [JsonConverter(typeof(TypedClientResultStyleTypeConverter))]
    public TypedClientResultStyleType TypedClientResultStyle { get; set; } = TypedClientResultStyleType.Throw;

    /// <summary>
    /// How an EndpointPerOperation operation answers a request that got no answer from the server (the connection
    /// failed, a timeout the caller did not ask for, a resilience pipeline that rejected it). Default: Throw - the
    /// exception is thrown. Result: the operation's result is returned instead, with IsTransportFailure, a 503 whose
    /// problem has no text of its own (for the caller to word), and the exception in TransportException; the caller's own
    /// cancellation still throws. Only applies to EndpointPerOperation generation mode.
    /// </summary>
    [JsonConverter(typeof(TransportFailureTypeConverter))]
    public TransportFailureType TransportFailure { get; set; } = TransportFailureType.Throw;

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
    /// Enforce the specification's <c>required</c> properties when reading JSON. Default: false.
    /// When true, the required properties of the generated models get <c>[JsonRequired]</c>, so a response that
    /// leaves one out fails with a <c>JsonException</c> naming it, instead of a <c>null</c> (or <c>0</c>) inside a
    /// non-nullable property. Off by default: the specifications of third-party APIs often mark properties required
    /// that the API sometimes omits.
    /// </summary>
    public bool StrictRequiredProperties { get; set; }

    /// <summary>
    /// Ask for XML first for an operation that offers its response as JSON and XML. Default: false.
    /// By default the generated clients send <c>Accept: application/json, application/problem+json</c> for such an
    /// operation; when true they send the XML media types first (and <c>application/problem+xml</c> before
    /// <c>application/problem+json</c>), for third-party APIs that answer XML better. Either way the client reads
    /// whatever <c>Content-Type</c> comes back.
    /// </summary>
    public bool PreferXml { get; set; }

    /// <summary>
    /// Generate string enums as extensible enums. Default: true.
    /// When true, a string enum is a <c>readonly record struct</c> over the value on the wire, with the specification's
    /// values as named members (<c>DeviceStatus.Online</c>), their constants in <c>Names</c> for a <c>switch</c>, and
    /// <c>KnownValues</c>; a value the specification does not list is kept with <c>IsKnown</c> false instead of failing
    /// the response, so a server can add a value without breaking clients built before it. When false, string enums
    /// are C# enums, and an unknown value fails the response with a <c>JsonException</c>. Integer enums are C# enums
    /// either way (they already hold any number); a server keeps strict C# enums unless its marker sets extensibleEnums.
    /// </summary>
    public bool ExtensibleEnums { get; set; } = true;

    /// <summary>
    /// Whether a string enum is also read by its C# member name. Default: false - only the specification's value
    /// (<c>rtsp</c>) is read. When true, a response may also carry the member name (<c>Rtsp</c>), in another casing
    /// when only one member matches; the value is still what is written. For a rollout: a server built on 2.x wrote
    /// the member name for a value that is no member name (2.x wrote <c>Rtsp</c> for <c>rtsp</c>). An extensible enum
    /// already takes a listed value in any casing; the key adds the member name (<c>InProgress</c> for
    /// <c>in-progress</c>).
    /// </summary>
    public bool ReadEnumMemberNames { get; set; }

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