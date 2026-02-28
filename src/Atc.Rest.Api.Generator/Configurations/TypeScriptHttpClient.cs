namespace Atc.Rest.Api.Generator.Configurations;

/// <summary>
/// Defines which HTTP client library the generated TypeScript client should use.
/// </summary>
public enum TypeScriptHttpClient
{
    /// <summary>
    /// Use the native fetch API (default). No additional dependencies required.
    /// </summary>
    Fetch,

    /// <summary>
    /// Use Axios for HTTP requests. Provides interceptor support, automatic JSON parsing,
    /// and broader browser compatibility.
    /// </summary>
    Axios,
}