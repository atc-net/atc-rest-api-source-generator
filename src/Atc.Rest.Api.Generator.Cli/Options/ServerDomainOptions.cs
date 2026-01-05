namespace Atc.Rest.Api.Generator.Cli.Options;

/// <summary>
/// Server domain handler scaffolding configuration options.
/// </summary>
public sealed class ServerDomainOptions
{
    /// <summary>
    /// Output folder for generated handler files, relative to the project root.
    /// Default: "ApiHandlers".
    /// </summary>
    public string GenerateHandlersOutput { get; set; } = "ApiHandlers";

    /// <summary>
    /// Sub-folder organization strategy for handlers.
    /// Default: None.
    /// </summary>
    public SubFolderStrategyType SubFolderStrategy { get; set; } = SubFolderStrategyType.None;

    /// <summary>
    /// Handler class name suffix.
    /// Default: "Handler".
    /// </summary>
    public string HandlerSuffix { get; set; } = "Handler";

    /// <summary>
    /// Stub implementation type for generated handlers.
    /// Options: "throw-not-implemented", "error-501", "default-value".
    /// Default: "throw-not-implemented".
    /// </summary>
    public string StubImplementation { get; set; } = "throw-not-implemented";
}