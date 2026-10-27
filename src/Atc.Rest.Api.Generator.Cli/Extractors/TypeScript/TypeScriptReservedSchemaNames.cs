namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// The names a component schema cannot carry into the generated TypeScript: the client's own exports, which the
/// package index re-exports next to the models (<c>ApiError</c>, <c>ApiResult</c>: TS2308), and the globals the
/// generated code reaches for unqualified (<c>Response</c>, <c>Headers</c>, <c>Error</c>, the utility types): a
/// model of that name shadows the global in every file that imports it. Such a schema is generated as
/// <c>{Name}Model</c> (<see cref="SchemaNameNormalizer.WithSchemaNamesOutside"/>); schema names are not on the wire.
/// </summary>
internal static class TypeScriptReservedSchemaNames
{
    /// <summary>
    /// The suffix a renamed schema gets.
    /// </summary>
    public const string Suffix = "Model";

    /// <summary>
    /// The reserved names.
    /// </summary>
    public static readonly IReadOnlyCollection<string> Names = new HashSet<string>(StringComparer.Ordinal)
    {
        // The client's own exports
        "ApiClient", "ApiClientOptions", "ApiError", "ApiResult", "ApiProvider", "ApiProviderProps", "ApiService",
        "ApiServiceContext", "RequestOptions", "RequestRetry", "RetryPolicy", "ServerName", "Servers",
        "StreamFraming", "ValidationError", "WireShape", "WireShapes", "XmlOptions", "XmlParser",
        "HubConnectionState", "FetchRequestInterceptor", "FetchResponseInterceptor", "AxiosRequestInterceptor",
        "AxiosResponseInterceptor",

        // The platform's globals the generated code uses unqualified (a global it never names, Event say, is a
        // fine model name)
        "Response", "Request", "Headers", "Error", "Blob", "File", "FormData", "URL", "URLSearchParams",
        "AbortSignal", "AbortController", "Promise", "Date", "Array", "Map", "JSON", "Object", "String",
        "Number", "TextDecoder",

        // TypeScript's utility types the generated code uses
        "Record", "Readonly", "Omit", "Extract", "ReturnType",
    };
}