namespace Atc.Rest.Api.Generator.Cli.Options;

/// <summary>
/// Root configuration model for ApiGeneratorOptions.json.
/// Contains all settings for server, client, and domain code generation.
/// </summary>
public sealed class ApiGeneratorOptions
{
    /// <summary>
    /// The default options file name.
    /// </summary>
    public const string FileName = "ApiGeneratorOptions.json";

    /// <summary>
    /// General settings shared across all generation modes.
    /// </summary>
    public GeneralOptions General { get; set; } = new();

    /// <summary>
    /// Repository scaffolding settings (init-repo, coding rules).
    /// </summary>
    public ScaffoldingOptions Scaffolding { get; set; } = new();

    /// <summary>
    /// Server-side code generation settings (contracts, endpoints, handlers).
    /// </summary>
    public ServerOptions Server { get; set; } = new();

    /// <summary>
    /// Client-side code generation settings (HTTP client, models).
    /// </summary>
    public ClientOptions Client { get; set; } = new();

    /// <summary>
    /// Creates a ClientConfig from these options for marker file generation.
    /// </summary>
    /// <returns>A ClientConfig populated from ApiGeneratorOptions.</returns>
    public ClientConfig ToClientConfig()
        => new()
        {
            Generate = true,
            ValidateSpecificationStrategy = General.ValidateSpecificationStrategy,
            IncludeDeprecated = General.IncludeDeprecated,
            Namespace = Client.Namespace,
            GenerationMode = Client.GenerationMode,
            ClientSuffix = Client.ClientSuffix,
            GenerateOAuthTokenManagement = Client.GenerateOAuthTokenManagement,
        };

    /// <summary>
    /// Creates a ServerConfig from these options for marker file generation.
    /// </summary>
    /// <returns>A ServerConfig populated from ApiGeneratorOptions.</returns>
    public ServerConfig ToServerConfig()
        => new()
        {
            Generate = true,
            ValidateSpecificationStrategy = General.ValidateSpecificationStrategy,
            IncludeDeprecated = General.IncludeDeprecated,
            Namespace = Server.Namespace,
            SubFolderStrategy = Server.SubFolderStrategy,
            UseMinimalApiPackage = Server.UseMinimalApiPackage,
            UseValidationFilter = Server.UseValidationFilter,
            UseGlobalErrorHandler = Server.UseGlobalErrorHandler,
            VersioningStrategy = Server.VersioningStrategy,
            DefaultApiVersion = Server.DefaultApiVersion,
            VersionQueryParameterName = Server.VersionQueryParameterName,
            VersionHeaderName = Server.VersionHeaderName,
            VersionRouteSegmentTemplate = Server.VersionRouteSegmentTemplate,
            ReportApiVersions = Server.ReportApiVersions,
            AssumeDefaultVersionWhenUnspecified = Server.AssumeDefaultVersionWhenUnspecified,
        };

    /// <summary>
    /// Creates a ServerDomainConfig from these options for marker file generation.
    /// </summary>
    /// <returns>A ServerDomainConfig populated from ApiGeneratorOptions.</returns>
    public ServerDomainConfig ToServerDomainConfig()
        => new()
        {
            Generate = true,
            ValidateSpecificationStrategy = General.ValidateSpecificationStrategy,
            IncludeDeprecated = General.IncludeDeprecated,
            GenerateHandlersOutput = Server.Domain.GenerateHandlersOutput,
            SubFolderStrategy = Server.Domain.SubFolderStrategy,
            HandlerSuffix = Server.Domain.HandlerSuffix,
            StubImplementation = Server.Domain.StubImplementation,
        };
}