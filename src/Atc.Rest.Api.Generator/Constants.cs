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

    /// <summary>
    /// Where a diagnostic sends the reader for the long explanation.
    /// </summary>
    /// <remarks>
    /// All prose documentation for this project lives in the GitHub wiki, so a help link points
    /// there rather than at a file in the repository.
    /// </remarks>
    [SuppressMessage("Design", "S1075:Refactor your code not to use hardcoded absolute paths or URIs", Justification = "The wiki location is fixed and is part of the published diagnostic contract.")]
    public static class Documentation
    {
        /// <summary>
        /// Root of the project wiki.
        /// </summary>
        public const string WikiUrl = "https://github.com/atc-net/atc-rest-api-source-generator/wiki";

        /// <summary>
        /// The page listing every analyzer rule.
        /// </summary>
        public const string AnalyzerRulesUrl = WikiUrl + "/Analyzer-Rules";

        /// <summary>
        /// Builds the help link for a rule.
        /// </summary>
        /// <remarks>
        /// GitHub derives a heading anchor by lower-casing and replacing spaces with hyphens; it does
        /// not touch underscores. The rule id is therefore only lower-cased, and the wiki gives each
        /// rule a heading that is exactly its id so the anchor resolves.
        /// </remarks>
        /// <param name="ruleId">The rule identifier, for example <c>ATC_API_VER001</c>.</param>
        /// <returns>An absolute URL to the rule's section.</returns>
        [SuppressMessage("Design", "CA1055:URI-like return values should not be strings", Justification = "DiagnosticMessage.DocumentationUrl is a string, and the value is passed straight through to it.")]
        public static string GetRuleUrl(string ruleId)
            => ruleId is null
                ? AnalyzerRulesUrl
                : $"{AnalyzerRulesUrl}#{ruleId.ToLowerInvariant()}";
    }
}