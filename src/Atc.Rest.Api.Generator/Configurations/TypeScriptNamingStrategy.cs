namespace Atc.Rest.Api.Generator.Configurations;

/// <summary>
/// Controls how OpenAPI property and parameter names are cased in generated TypeScript code.
/// </summary>
public enum TypeScriptNamingStrategy
{
    /// <summary>
    /// Convert names to camelCase (default). Example: user_name → userName.
    /// </summary>
    CamelCase,

    /// <summary>
    /// Keep names exactly as defined in the OpenAPI specification.
    /// </summary>
    Original,

    /// <summary>
    /// Convert names to PascalCase. Example: user_name → UserName.
    /// </summary>
    PascalCase,
}