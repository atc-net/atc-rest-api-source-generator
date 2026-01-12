namespace Atc.Rest.Api.Generator.Configurations;

/// <summary>
/// Configuration for server domain handler scaffold generation.
/// This model is deserialized from the .atc-rest-api-server-handlers marker file.
/// </summary>
public class ServerDomainConfig : BaseConfig
{
    /// <summary>
    /// Explicit namespace of the contracts project that contains generated handlers, parameters, and results.
    /// When specified, GlobalUsings will import from this namespace instead of auto-discovering.
    /// Example: "Contoso.IoT.Nexus.Api.Contracts" generates "global using Contoso.IoT.Nexus.Api.Contracts.Generated.*.Handlers".
    /// Default: null (auto-detect from sibling project's .atc-rest-api-server marker).
    /// </summary>
    public string? ContractsNamespace { get; set; }

    /// <summary>
    /// Output folder for generated handler files, relative to the marker file location.
    /// Example: "ApiHandlers" will output handlers to {MarkerFileDir}/ApiHandlers/.
    /// Default: "ApiHandlers".
    /// </summary>
    public string GenerateHandlersOutput { get; set; } = "ApiHandlers";

    /// <summary>
    /// Strategy for sub-folder organization. Default: None.
    /// None = all handlers in root folder, FirstPathSegment = group by URL path, OpenApiTag = group by operation tag.
    /// </summary>
    [JsonConverter(typeof(SubFolderStrategyTypeConverter))]
    public SubFolderStrategyType SubFolderStrategy { get; set; } = SubFolderStrategyType.None;

    /// <summary>
    /// Suffix for handler class names. Default: "Handler".
    /// </summary>
    public string HandlerSuffix { get; set; } = "Handler";

    /// <summary>
    /// Type of stub implementation to generate. Default: "throw-not-implemented".
    /// Options: "throw-not-implemented", "error-501", "default-value".
    /// </summary>
    public string StubImplementation { get; set; } = "throw-not-implemented";
}