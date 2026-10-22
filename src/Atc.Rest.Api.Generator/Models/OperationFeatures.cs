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

    /// <summary>
    /// The error statuses the generated server answers by itself that the specification does not list - one rule for
    /// the server's endpoint metadata and every client: 400 with parameters (validation), 401 with security, 403 with
    /// roles or policies, 404 for GET/PUT/DELETE/PATCH with path parameters, 409 for POST/PUT, 429 with rate limiting,
    /// and 500 and 504 (the global error handler) unless a <c>default</c> response says what any other status carries.
    /// </summary>
    /// <param name="definedCodes">The status codes the specification lists for the operation.</param>
    /// <param name="hasDefaultResponse">Whether the operation has a <c>default</c> response.</param>
    /// <returns>The status codes, in that order.</returns>
    public IReadOnlyList<string> ImplicitErrorStatuses(
        ICollection<string> definedCodes,
        bool hasDefaultResponse)
    {
        if (definedCodes is null)
        {
            throw new ArgumentNullException(nameof(definedCodes));
        }

        var statuses = new List<string>();
        void AddWhen(
            bool condition,
            string statusCode)
        {
            if (condition && !definedCodes.Contains(statusCode))
            {
                statuses.Add(statusCode);
            }
        }

        AddWhen(HasParameters, "400");
        AddWhen(HasSecurity, "401");
        AddWhen(HasRolesOrPolicies, "403");
        AddWhen(HasPathParameters && HttpMethod is "GET" or "PUT" or "DELETE" or "PATCH", "404");
        AddWhen(HttpMethod is "POST" or "PUT", "409");
        AddWhen(HasRateLimiting, "429");
        AddWhen(!hasDefaultResponse, "500");
        AddWhen(!hasDefaultResponse, "504");
        return statuses;
    }
}