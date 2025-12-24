namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// Represents detected features of an OpenAPI operation for response code analysis.
/// Used to determine which response factory methods should be auto-applied
/// and which diagnostic warnings should be reported.
/// </summary>
public sealed class OperationFeatures
{
    /// <summary>
    /// Gets a value indicating whether the operation has any input parameters
    /// (query, path, header, cookie) or a request body.
    /// </summary>
    public bool HasParameters { get; init; }

    /// <summary>
    /// Gets a value indicating whether the operation has path parameters
    /// (e.g., /pets/{petId}).
    /// </summary>
    public bool HasPathParameters { get; init; }

    /// <summary>
    /// Gets a value indicating whether the operation has security requirements
    /// (OAuth2, JWT, ApiKey, etc.).
    /// </summary>
    public bool HasSecurity { get; init; }

    /// <summary>
    /// Gets a value indicating whether the operation has authorization requirements
    /// with roles, policies, or scopes defined.
    /// </summary>
    public bool HasRolesOrPolicies { get; init; }

    /// <summary>
    /// Gets a value indicating whether the operation has rate limiting configured
    /// via x-ratelimit-* extensions.
    /// </summary>
    public bool HasRateLimiting { get; init; }

    /// <summary>
    /// Gets the HTTP method of the operation (GET, POST, PUT, DELETE, PATCH).
    /// </summary>
    public string HttpMethod { get; init; } = string.Empty;
}