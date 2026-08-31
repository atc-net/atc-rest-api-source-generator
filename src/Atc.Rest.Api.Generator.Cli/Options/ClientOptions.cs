namespace Atc.Rest.Api.Generator.Cli.Options;

/// <summary>
/// Client-side code generation configuration options.
/// </summary>
public sealed class ClientOptions
{
    /// <summary>
    /// Custom namespace prefix (null = auto-detect from project name).
    /// Default: null.
    /// </summary>
    public string? Namespace { get; set; }

    /// <summary>
    /// Client generation mode.
    /// Default: TypedClient.
    /// </summary>
    public GenerationModeType GenerationMode { get; set; } = GenerationModeType.TypedClient;

    /// <summary>
    /// HTTP client class name suffix.
    /// Default: "Client".
    /// </summary>
    public string ClientSuffix { get; set; } = "Client";

    /// <summary>
    /// Client granularity: one client per API area, or a single client covering all operations.
    /// Default: PerArea.
    /// </summary>
    public ClientGranularityType ClientGranularity { get; set; } = ClientGranularityType.PerArea;

    /// <summary>
    /// Explicit client type name, used verbatim. Only meaningful when ClientGranularity is Single.
    /// Default: null (derived from the namespace).
    /// </summary>
    public string? ClientName { get; set; }

    /// <summary>
    /// Generate OAuth2 token provider, handler, and DI extensions
    /// when OAuth2 security is detected in OpenAPI spec.
    /// Default: true.
    /// </summary>
    public bool GenerateOAuthTokenManagement { get; set; } = true;
}