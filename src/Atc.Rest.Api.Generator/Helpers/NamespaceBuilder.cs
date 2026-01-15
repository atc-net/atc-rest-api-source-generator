namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// Helper class for building consistent namespaces across the code generator.
/// </summary>
[SuppressMessage("Design", "CA1034:Do not nest types", Justification = "Categories is a simple constants container")]
public static class NamespaceBuilder
{
    /// <summary>
    /// Known namespace categories used in generated code.
    /// </summary>
    public static class Categories
    {
        public const string Models = "Models";
        public const string Parameters = "Parameters";
        public const string Results = "Results";
        public const string Handlers = "Handlers";
        public const string Endpoints = "Endpoints";
        public const string Client = "Client";
        public const string Interfaces = "Interfaces";
        public const string Webhooks = "Webhooks";
        public const string OAuth = "OAuth";
        public const string Caching = "Caching";
    }

    /// <summary>
    /// Builds a namespace for generated code.
    /// </summary>
    /// <param name="projectName">The project name (base namespace).</param>
    /// <param name="category">The category (Models, Parameters, Results, Handlers, Endpoints, Client).</param>
    /// <param name="pathSegment">Optional path segment for sub-folder organization.</param>
    /// <returns>The fully qualified namespace.</returns>
    public static string Build(
        string projectName,
        string category,
        string? pathSegment = null)
    {
        if (string.IsNullOrEmpty(pathSegment))
        {
            return $"{projectName}.Generated.{category}";
        }

        return $"{projectName}.Generated.{pathSegment}.{category}";
    }

    /// <summary>
    /// Builds a namespace with nested sub-category (e.g., Endpoints.Interfaces).
    /// </summary>
    /// <param name="projectName">The project name (base namespace).</param>
    /// <param name="category">The primary category.</param>
    /// <param name="subCategory">The sub-category.</param>
    /// <param name="pathSegment">Optional path segment for sub-folder organization.</param>
    /// <returns>The fully qualified namespace.</returns>
    public static string BuildNested(
        string projectName,
        string category,
        string subCategory,
        string? pathSegment = null)
    {
        if (string.IsNullOrEmpty(pathSegment))
        {
            return $"{projectName}.Generated.{category}.{subCategory}";
        }

        return $"{projectName}.Generated.{pathSegment}.{category}.{subCategory}";
    }

    /// <summary>
    /// Builds the base generated namespace without a category.
    /// </summary>
    /// <param name="projectName">The project name.</param>
    /// <returns>The base generated namespace.</returns>
    public static string BuildBase(string projectName)
        => $"{projectName}.Generated";

    /// <summary>
    /// Builds the Models namespace.
    /// </summary>
    public static string ForModels(
        string projectName,
        string? pathSegment = null)
        => Build(projectName, Categories.Models, pathSegment);

    /// <summary>
    /// Builds the Parameters namespace.
    /// </summary>
    public static string ForParameters(
        string projectName,
        string? pathSegment = null)
        => Build(projectName, Categories.Parameters, pathSegment);

    /// <summary>
    /// Builds the Results namespace.
    /// </summary>
    public static string ForResults(
        string projectName,
        string? pathSegment = null)
        => Build(projectName, Categories.Results, pathSegment);

    /// <summary>
    /// Builds the Handlers namespace.
    /// </summary>
    public static string ForHandlers(
        string projectName,
        string? pathSegment = null)
        => Build(projectName, Categories.Handlers, pathSegment);

    /// <summary>
    /// Builds the Endpoints namespace.
    /// </summary>
    public static string ForEndpoints(
        string projectName,
        string? pathSegment = null)
        => Build(projectName, Categories.Endpoints, pathSegment);

    /// <summary>
    /// Builds the Client namespace.
    /// </summary>
    public static string ForClient(
        string projectName,
        string? pathSegment = null)
        => Build(projectName, Categories.Client, pathSegment);

    /// <summary>
    /// Builds the Endpoints.Interfaces namespace.
    /// </summary>
    public static string ForEndpointInterfaces(
        string projectName,
        string? pathSegment = null)
        => BuildNested(projectName, Categories.Endpoints, Categories.Interfaces, pathSegment);

    /// <summary>
    /// Builds the Endpoints.Results namespace.
    /// </summary>
    public static string ForEndpointResults(
        string projectName,
        string? pathSegment = null)
        => BuildNested(projectName, Categories.Endpoints, Categories.Results, pathSegment);

    /// <summary>
    /// Builds the Webhooks namespace.
    /// </summary>
    public static string ForWebhooks(string projectName)
        => Build(projectName, Categories.Webhooks);

    /// <summary>
    /// Builds the Webhooks.Handlers namespace.
    /// </summary>
    public static string ForWebhookHandlers(string projectName)
        => BuildNested(projectName, Categories.Webhooks, Categories.Handlers);

    /// <summary>
    /// Builds the Webhooks.Parameters namespace.
    /// </summary>
    public static string ForWebhookParameters(string projectName)
        => BuildNested(projectName, Categories.Webhooks, Categories.Parameters);

    /// <summary>
    /// Builds the Webhooks.Results namespace.
    /// </summary>
    public static string ForWebhookResults(string projectName)
        => BuildNested(projectName, Categories.Webhooks, Categories.Results);

    /// <summary>
    /// Builds the Webhooks.Endpoints namespace.
    /// </summary>
    public static string ForWebhookEndpoints(string projectName)
        => BuildNested(projectName, Categories.Webhooks, Categories.Endpoints);
}