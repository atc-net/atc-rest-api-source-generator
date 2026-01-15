namespace Atc.Rest.Api.Generator;

/// <summary>
/// Shared constants for the REST API generator.
/// </summary>
[SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Intentional grouping of related constants.")]
public static class Constants
{
    /// <summary>
    /// Marker file names used to trigger code generation.
    /// </summary>
    public static class MarkerFile
    {
        /// <summary>
        /// Server contracts marker file name (.atc-rest-api-server).
        /// Triggers generation of models, endpoints, handler interfaces, and DI setup.
        /// </summary>
        public const string Server = ".atc-rest-api-server";

        /// <summary>
        /// Server contracts marker file name with JSON extension (.atc-rest-api-server.json).
        /// </summary>
        public const string ServerJson = Server + ".json";

        /// <summary>
        /// Server domain handlers marker file name (.atc-rest-api-server-handlers).
        /// Triggers generation of handler implementation scaffolds.
        /// </summary>
        public const string ServerHandlers = ".atc-rest-api-server-handlers";

        /// <summary>
        /// Server domain handlers marker file name with JSON extension (.atc-rest-api-server-handlers.json).
        /// </summary>
        public const string ServerHandlersJson = ServerHandlers + ".json";

        /// <summary>
        /// Client marker file name (.atc-rest-api-client).
        /// Triggers generation of HTTP client code.
        /// </summary>
        public const string Client = ".atc-rest-api-client";

        /// <summary>
        /// Client marker file name with JSON extension (.atc-rest-api-client.json).
        /// </summary>
        public const string ClientJson = Client + ".json";
    }

    /// <summary>
    /// File extension constants for specification and configuration files.
    /// </summary>
    public static class FileExtensions
    {
        /// <summary>
        /// YAML file extension (.yaml).
        /// </summary>
        public const string Yaml = ".yaml";

        /// <summary>
        /// Short YAML file extension (.yml).
        /// </summary>
        public const string Yml = ".yml";

        /// <summary>
        /// JSON file extension (.json).
        /// </summary>
        public const string Json = ".json";
    }

    /// <summary>
    /// Standard directory names used in project scaffolding.
    /// </summary>
    public static class Directories
    {
        /// <summary>
        /// Source directory name.
        /// </summary>
        public const string Source = "src";

        /// <summary>
        /// Test directory name.
        /// </summary>
        public const string Test = "test";

        /// <summary>
        /// Scripts and specifications directory name.
        /// </summary>
        public const string ScriptsAndSpecifications = "ScriptsAndSpecifications";
    }
}