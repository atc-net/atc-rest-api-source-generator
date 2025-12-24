namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// Helper methods for detecting operation features from OpenAPI operations.
/// </summary>
public static class OperationFeaturesHelper
{
    /// <summary>
    /// Detects features of an OpenAPI operation for auto-apply and validation rules.
    /// </summary>
    /// <param name="operation">The OpenAPI operation.</param>
    /// <param name="pathItem">The path item containing the operation.</param>
    /// <param name="document">The OpenAPI document.</param>
    /// <param name="httpMethod">The HTTP method (GET, POST, PUT, DELETE, PATCH).</param>
    /// <returns>An OperationFeatures object with detected features.</returns>
    public static Models.OperationFeatures DetectOperationFeatures(
        OpenApiOperation operation,
        OpenApiPathItem pathItem,
        OpenApiDocument document,
        string httpMethod)
    {
        // Check for parameters (query, path, header, cookie) or request body
        var hasParameters = operation.HasParameters() || operation.HasRequestBody();

        // Check for path parameters specifically
        var hasPathParameters = operation.GetPathParameters().Any();

        // Check for security requirements
        var securityConfig = operation.ExtractUnifiedSecurityConfiguration(pathItem, document);
        var hasSecurity = securityConfig is { AuthenticationRequired: true };
        var hasRolesOrPolicies = securityConfig != null &&
            (securityConfig.Roles.Count > 0 || securityConfig.Policies.Count > 0 || securityConfig.Scopes.Count > 0);

        // Check for rate limiting
        var rateLimitConfig = operation.ExtractRateLimitConfiguration(pathItem, document);
        var hasRateLimiting = rateLimitConfig != null;

        return new Models.OperationFeatures
        {
            HasParameters = hasParameters,
            HasPathParameters = hasPathParameters,
            HasSecurity = hasSecurity,
            HasRolesOrPolicies = hasRolesOrPolicies,
            HasRateLimiting = hasRateLimiting,
            HttpMethod = httpMethod,
        };
    }
}