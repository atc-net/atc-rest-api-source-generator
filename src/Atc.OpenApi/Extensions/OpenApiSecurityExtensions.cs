namespace Atc.OpenApi.Extensions;

/// <summary>
/// Extension methods for extracting security configuration from OpenAPI extensions and standard security schemes.
/// Supports both ATC-specific extensions (x-authentication-required, x-authentication-schemes, x-authorize-roles)
/// and standard OpenAPI 3.0 security (components/securitySchemes + security arrays).
/// </summary>
[SuppressMessage("", "CA1034:Do not nest type", Justification = "OK - CLang14 - extension")]
[SuppressMessage("", "S3398:Move this method inside", Justification = "OK - CLang14 - extension")]
public static class OpenApiSecurityExtensions
{
    /// <param name="extensions">The OpenAPI extensions dictionary.</param>
    extension(IDictionary<string, IOpenApiExtension>? extensions)
    {
        /// <summary>
        /// Extracts the x-authentication-required value from extensions.
        /// </summary>
        /// <returns>True if required, false if explicitly disabled, null if not specified.</returns>
        public bool? ExtractAuthenticationRequired()
        {
            if (extensions is null)
            {
                return null;
            }

            if (!extensions.TryGetValue(SecurityExtensionNameConstants.AuthenticationRequired, out var extension) ||
                extension is null)
            {
                return null;
            }

            // Use reflection to access Node property (Microsoft.OpenApi v3.0.1 pattern)
            var extensionType = extension.GetType();
            var nodeProperty = extensionType.GetProperty("Node");
            if (nodeProperty == null)
            {
                return null;
            }

            var node = nodeProperty.GetValue(extension);
            if (node is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out var boolValue))
            {
                return boolValue;
            }

            return null;
        }

        /// <summary>
        /// Extracts the x-authorize-roles array from extensions.
        /// </summary>
        /// <returns>A list of role names, or empty list if not specified.</returns>
        public IReadOnlyList<string> ExtractAuthorizeRoles()
            => extensions is null ||
               !extensions.TryGetValue(SecurityExtensionNameConstants.AuthorizeRoles, out var extension) ||
               extension is null
                ? []
                : ExtractStringArray(extension);

        /// <summary>
        /// Extracts the x-authentication-schemes array from extensions.
        /// </summary>
        /// <returns>A list of scheme names, or empty list if not specified.</returns>
        public IReadOnlyList<string> ExtractAuthenticationSchemes()
            => extensions is null ||
               !extensions.TryGetValue(SecurityExtensionNameConstants.AuthenticationSchemes, out var extension) ||
               extension is null
                ? []
                : ExtractStringArray(extension);
    }

    /// <summary>
    /// Extracts the complete authorization configuration for an operation.
    /// Applies hierarchical inheritance: operation overrides path, path overrides document.
    /// </summary>
    /// <param name="operation">The OpenAPI operation.</param>
    /// <param name="pathItem">The path item containing the operation.</param>
    /// <param name="document">The OpenAPI document (for root-level settings).</param>
    /// <returns>A tuple with security configuration, or null if no security is configured.</returns>
    public static (bool AuthRequired, IReadOnlyList<string> Roles, IReadOnlyList<string> Schemes, bool AllowAnonymous)?
        ExtractSecurityConfiguration(
            this OpenApiOperation operation,
            OpenApiPathItem pathItem,
            OpenApiDocument document)
    {
        // Check for operation-level explicit AllowAnonymous override
        var operationAuthRequired = operation.Extensions.ExtractAuthenticationRequired();
        if (operationAuthRequired == false)
        {
            // Explicit AllowAnonymous at operation level
            return (false, [], [], true);
        }

        // Determine effective auth required (operation → path → document)
        var pathAuthRequired = pathItem.Extensions.ExtractAuthenticationRequired();
        var documentAuthRequired = document.Extensions.ExtractAuthenticationRequired();

        var effectiveAuthRequired = operationAuthRequired
                                    ?? pathAuthRequired
                                    ?? documentAuthRequired
                                    ?? false;

        // If no auth required and no roles/schemes, return null
        if (!effectiveAuthRequired)
        {
            // Check if roles or schemes imply auth required
            var hasOperationRoles = operation.Extensions.ExtractAuthorizeRoles().Count > 0;
            var hasPathRoles = pathItem.Extensions.ExtractAuthorizeRoles().Count > 0;
            var hasOperationSchemes = operation.Extensions.ExtractAuthenticationSchemes().Count > 0;
            var hasPathSchemes = pathItem.Extensions.ExtractAuthenticationSchemes().Count > 0;

            if (!hasOperationRoles && !hasPathRoles && !hasOperationSchemes && !hasPathSchemes)
            {
                return null;
            }

            // Roles or schemes imply auth required
            effectiveAuthRequired = true;
        }

        // Get roles (operation overrides path)
        var roles = operation.Extensions.ExtractAuthorizeRoles();
        if (roles.Count == 0)
        {
            roles = pathItem.Extensions.ExtractAuthorizeRoles();
        }

        // Get schemes (operation overrides path)
        var schemes = operation.Extensions.ExtractAuthenticationSchemes();
        if (schemes.Count == 0)
        {
            schemes = pathItem.Extensions.ExtractAuthenticationSchemes();
        }

        return (effectiveAuthRequired, roles, schemes, false);
    }

    /// <summary>
    /// Extracts a string array from an OpenAPI extension using reflection.
    /// </summary>
    private static IReadOnlyList<string> ExtractStringArray(IOpenApiExtension extension)
    {
        // Use reflection to access Node property (Microsoft.OpenApi v3.0.1 pattern)
        var extensionType = extension.GetType();
        var nodeProperty = extensionType.GetProperty("Node");
        if (nodeProperty == null)
        {
            return [];
        }

        var node = nodeProperty.GetValue(extension);
        if (node is not JsonArray jsonArray)
        {
            return [];
        }

        var result = new List<string>();
        foreach (var item in jsonArray)
        {
            if (item is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var value) && !string.IsNullOrEmpty(value))
            {
                result.Add(value);
            }
        }

        return result;
    }

    /// <summary>
    /// Extracts all security schemes from the OpenAPI document's components/securitySchemes.
    /// </summary>
    /// <param name="document">The OpenAPI document.</param>
    /// <returns>Dictionary of security scheme name to SecuritySchemeInfo.</returns>
    public static IReadOnlyDictionary<string, SecuritySchemeInfo> ExtractSecuritySchemes(
        this OpenApiDocument document)
    {
        var schemes = new Dictionary<string, SecuritySchemeInfo>(StringComparer.Ordinal);

        if (document.Components?.SecuritySchemes == null)
        {
            return schemes;
        }

        foreach (var kvp in document.Components.SecuritySchemes)
        {
            var name = kvp.Key;
            var scheme = kvp.Value;

            schemes[name] = new SecuritySchemeInfo
            {
                Name = name,
                Type = MapSecuritySchemeType(scheme.Type ?? Microsoft.OpenApi.SecuritySchemeType.Http),
                Scheme = scheme.Scheme,
                BearerFormat = scheme.BearerFormat,
                In = MapApiKeyLocation(scheme.In),
                ParameterName = scheme.Name,
                Flows = ExtractOAuthFlows(scheme.Flows),
                OpenIdConnectUrl = scheme.OpenIdConnectUrl?.ToString(),
                Description = scheme.Description,
            };
        }

        return schemes;
    }

    /// <summary>
    /// Extracts security requirements for an operation.
    /// Operation-level security overrides document-level security.
    /// </summary>
    /// <param name="operation">The OpenAPI operation.</param>
    /// <param name="document">The OpenAPI document (for document-level security).</param>
    /// <returns>
    /// List of security requirements, or null if no security specified.
    /// An empty list means explicitly public (security: []).
    /// </returns>
    public static IReadOnlyList<SecurityRequirement>? ExtractSecurityRequirements(
        this OpenApiOperation operation,
        OpenApiDocument document)
    {
        // Operation-level security overrides document-level
        var securityRequirements = operation.Security ?? document.Security;

        if (securityRequirements == null)
        {
            return null;
        }

        // Empty array means explicitly public (security: [])
        if (securityRequirements.Count == 0)
        {
            return [];
        }

        var requirements = new List<SecurityRequirement>();

        foreach (var requirement in securityRequirements)
        {
            // Each requirement is a dictionary of scheme name to scopes
            // Multiple entries in the same requirement means AND logic
            // Multiple requirements in the array means OR logic
            foreach (var kvp in requirement)
            {
                // The scheme name is stored in Reference.Id, not Name
                // Reference.Id is the key used in components/securitySchemes
                var schemeName = kvp.Key.Reference?.Id ?? kvp.Key.Name ?? string.Empty;
                var scopes = kvp.Value.ToList();

                requirements.Add(new SecurityRequirement
                {
                    SchemeName = schemeName,
                    Scopes = scopes,
                });
            }
        }

        return requirements;
    }

    /// <summary>
    /// Extracts unified security configuration for an operation, combining both ATC extensions
    /// and standard OpenAPI security into a single model.
    /// </summary>
    /// <param name="operation">The OpenAPI operation.</param>
    /// <param name="pathItem">The path item containing the operation.</param>
    /// <param name="document">The OpenAPI document.</param>
    /// <returns>UnifiedSecurityConfig with merged security configuration.</returns>
    public static UnifiedSecurityConfig ExtractUnifiedSecurityConfiguration(
        this OpenApiOperation operation,
        OpenApiPathItem pathItem,
        OpenApiDocument document)
    {
        // First, try ATC extensions
        var atcSecurity = operation.ExtractSecurityConfiguration(pathItem, document);

        // Then, extract standard OpenAPI security
        var standardRequirements = operation.ExtractSecurityRequirements(document);

        // Determine security source
        var hasAtcSecurity = atcSecurity.HasValue;
        var hasStandardSecurity = standardRequirements != null;

        // Build unified config
        var config = new UnifiedSecurityConfig();

        // Priority: ATC extensions take precedence, but we merge both if present
        if (hasAtcSecurity && hasStandardSecurity)
        {
            config.Source = SecuritySource.Both;

            // ATC AllowAnonymous override takes precedence
            if (atcSecurity!.Value.AllowAnonymous)
            {
                config.AllowAnonymous = true;
                config.AuthenticationRequired = false;
            }
            else
            {
                config.AuthenticationRequired = atcSecurity.Value.AuthRequired;
                config.Roles = atcSecurity.Value.Roles;
                config.Schemes = atcSecurity.Value.Schemes;
                config.Requirements = standardRequirements!;

                // Extract scopes from standard requirements
                config.Scopes = standardRequirements!
                    .SelectMany(r => r.Scopes)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
            }
        }
        else if (hasAtcSecurity)
        {
            config.Source = SecuritySource.AtcExtensions;
            config.AllowAnonymous = atcSecurity!.Value.AllowAnonymous;
            config.AuthenticationRequired = atcSecurity.Value.AuthRequired;
            config.Roles = atcSecurity.Value.Roles;
            config.Schemes = atcSecurity.Value.Schemes;
        }
        else if (hasStandardSecurity)
        {
            config.Source = SecuritySource.OpenApiSecuritySchemes;

            // Empty array means explicitly public (security: [])
            if (standardRequirements!.Count == 0)
            {
                config.AllowAnonymous = true;
                config.AuthenticationRequired = false;
            }
            else
            {
                config.AuthenticationRequired = true;
                config.Requirements = standardRequirements;

                // Extract scheme names and scopes from requirements
                var schemeNames = standardRequirements
                    .Select(r => r.SchemeName)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                config.Schemes = schemeNames;

                var scopes = standardRequirements
                    .SelectMany(r => r.Scopes)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                config.Scopes = scopes;
            }
        }
        else
        {
            config.Source = SecuritySource.None;
        }

        return config;
    }

    /// <summary>
    /// Checks if the document has any security schemes defined.
    /// </summary>
    /// <param name="document">The OpenAPI document.</param>
    /// <returns>True if security schemes are defined.</returns>
    public static bool HasSecuritySchemes(this OpenApiDocument document)
        => document.Components?.SecuritySchemes?.Count > 0;

    /// <summary>
    /// Checks if the document has document-level security requirements.
    /// </summary>
    /// <param name="document">The OpenAPI document.</param>
    /// <returns>True if document-level security is defined.</returns>
    public static bool HasDocumentSecurity(this OpenApiDocument document)
        => document.Security?.Count > 0;

    /// <summary>
    /// Checks if the document has JWT Bearer authentication configured.
    /// This includes HTTP type security schemes with 'bearer' scheme.
    /// </summary>
    /// <param name="document">The OpenAPI document.</param>
    /// <returns>True if JWT Bearer authentication is used.</returns>
    public static bool HasJwtBearerSecurity(this OpenApiDocument document)
    {
        if (document.Components?.SecuritySchemes == null)
        {
            return false;
        }

        foreach (var kvp in document.Components.SecuritySchemes)
        {
            var scheme = kvp.Value;

            // Check for HTTP type with 'bearer' scheme (JWT)
            if (scheme.Type == Microsoft.OpenApi.SecuritySchemeType.Http &&
                string.Equals(scheme.Scheme, "bearer", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Maps OpenAPI SecuritySchemeType enum to our SecuritySchemeType.
    /// </summary>
    private static Models.SecuritySchemeType MapSecuritySchemeType(Microsoft.OpenApi.SecuritySchemeType type)
        => type switch
        {
            Microsoft.OpenApi.SecuritySchemeType.Http => Models.SecuritySchemeType.Http,
            Microsoft.OpenApi.SecuritySchemeType.ApiKey => Models.SecuritySchemeType.ApiKey,
            Microsoft.OpenApi.SecuritySchemeType.OAuth2 => Models.SecuritySchemeType.OAuth2,
            Microsoft.OpenApi.SecuritySchemeType.OpenIdConnect => Models.SecuritySchemeType.OpenIdConnect,
            _ => Models.SecuritySchemeType.Http,
        };

    /// <summary>
    /// Maps OpenAPI ParameterLocation to our ApiKeyLocation.
    /// </summary>
    private static ApiKeyLocation? MapApiKeyLocation(ParameterLocation? location)
        => location switch
        {
            ParameterLocation.Header => ApiKeyLocation.Header,
            ParameterLocation.Query => ApiKeyLocation.Query,
            ParameterLocation.Cookie => ApiKeyLocation.Cookie,
            _ => null,
        };

    /// <summary>
    /// Extracts OAuth flows information from OpenApiOAuthFlows.
    /// </summary>
    private static OAuthFlowsInfo? ExtractOAuthFlows(OpenApiOAuthFlows? flows)
    {
        if (flows == null)
        {
            return null;
        }

        return new OAuthFlowsInfo
        {
            Implicit = ExtractOAuthFlow(flows.Implicit),
            Password = ExtractOAuthFlow(flows.Password),
            ClientCredentials = ExtractOAuthFlow(flows.ClientCredentials),
            AuthorizationCode = ExtractOAuthFlow(flows.AuthorizationCode),
        };
    }

    /// <summary>
    /// Extracts a single OAuth flow information.
    /// </summary>
    private static OAuthFlowInfo? ExtractOAuthFlow(OpenApiOAuthFlow? flow)
    {
        if (flow == null)
        {
            return null;
        }

        var scopes = new Dictionary<string, string>(StringComparer.Ordinal);
        if (flow.Scopes != null)
        {
            foreach (var kvp in flow.Scopes)
            {
                scopes[kvp.Key] = kvp.Value;
            }
        }

        return new OAuthFlowInfo
        {
            AuthorizationUrl = flow.AuthorizationUrl?.ToString(),
            TokenUrl = flow.TokenUrl?.ToString(),
            RefreshUrl = flow.RefreshUrl?.ToString(),
            Scopes = scopes,
        };
    }

    /// <summary>
    /// Checks if the document has OAuth2 security schemes with Client Credentials flow.
    /// </summary>
    /// <param name="document">The OpenAPI document.</param>
    /// <returns>True if OAuth2 Client Credentials flow is configured.</returns>
    public static bool HasOAuth2ClientCredentials(this OpenApiDocument document)
    {
        if (document.Components?.SecuritySchemes == null)
        {
            return false;
        }

        foreach (var kvp in document.Components.SecuritySchemes)
        {
            var scheme = kvp.Value;

            if (scheme.Type == Microsoft.OpenApi.SecuritySchemeType.OAuth2 &&
                scheme.Flows?.ClientCredentials != null &&
                !string.IsNullOrEmpty(scheme.Flows.ClientCredentials.TokenUrl?.ToString()))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if the document has OAuth2 security schemes with Authorization Code flow (for refresh token support).
    /// </summary>
    /// <param name="document">The OpenAPI document.</param>
    /// <returns>True if OAuth2 Authorization Code flow is configured.</returns>
    public static bool HasOAuth2AuthorizationCode(this OpenApiDocument document)
    {
        if (document.Components?.SecuritySchemes == null)
        {
            return false;
        }

        foreach (var kvp in document.Components.SecuritySchemes)
        {
            var scheme = kvp.Value;

            if (scheme.Type == Microsoft.OpenApi.SecuritySchemeType.OAuth2 &&
                scheme.Flows?.AuthorizationCode != null &&
                !string.IsNullOrEmpty(scheme.Flows.AuthorizationCode.TokenUrl?.ToString()))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if the document has any OAuth2 security that requires client-side token management.
    /// This includes Client Credentials flow or Authorization Code flow with refresh URL.
    /// </summary>
    /// <param name="document">The OpenAPI document.</param>
    /// <returns>True if OAuth2 token management is needed.</returns>
    public static bool HasOAuth2TokenManagement(this OpenApiDocument document)
        => document.HasOAuth2ClientCredentials() || document.HasOAuth2AuthorizationCode();

    /// <summary>
    /// Gets the OAuth2 Client Credentials flow configuration.
    /// </summary>
    /// <param name="document">The OpenAPI document.</param>
    /// <returns>OAuthFlowInfo for Client Credentials, or null if not configured.</returns>
    public static OAuthFlowInfo? GetOAuth2ClientCredentialsFlow(this OpenApiDocument document)
    {
        if (document.Components?.SecuritySchemes == null)
        {
            return null;
        }

        foreach (var kvp in document.Components.SecuritySchemes)
        {
            var scheme = kvp.Value;

            if (scheme.Type == Microsoft.OpenApi.SecuritySchemeType.OAuth2 &&
                scheme.Flows?.ClientCredentials != null)
            {
                return ExtractOAuthFlow(scheme.Flows.ClientCredentials);
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the OAuth2 Authorization Code flow configuration.
    /// </summary>
    /// <param name="document">The OpenAPI document.</param>
    /// <returns>OAuthFlowInfo for Authorization Code, or null if not configured.</returns>
    public static OAuthFlowInfo? GetOAuth2AuthorizationCodeFlow(this OpenApiDocument document)
    {
        if (document.Components?.SecuritySchemes == null)
        {
            return null;
        }

        foreach (var kvp in document.Components.SecuritySchemes)
        {
            var scheme = kvp.Value;

            if (scheme.Type == Microsoft.OpenApi.SecuritySchemeType.OAuth2 &&
                scheme.Flows?.AuthorizationCode != null)
            {
                return ExtractOAuthFlow(scheme.Flows.AuthorizationCode);
            }
        }

        return null;
    }

    /// <summary>
    /// Gets all OAuth2 scopes defined across all flows.
    /// </summary>
    /// <param name="document">The OpenAPI document.</param>
    /// <returns>Dictionary of scope name to description.</returns>
    public static IReadOnlyDictionary<string, string> GetAllOAuth2Scopes(this OpenApiDocument document)
    {
        var scopes = new Dictionary<string, string>(StringComparer.Ordinal);

        if (document.Components?.SecuritySchemes == null)
        {
            return scopes;
        }

        foreach (var kvp in document.Components.SecuritySchemes)
        {
            var scheme = kvp.Value;

            if (scheme.Type != Microsoft.OpenApi.SecuritySchemeType.OAuth2 || scheme.Flows == null)
            {
                continue;
            }

            // Collect scopes from all flows
            AddScopesFromFlow(scopes, scheme.Flows.ClientCredentials);
            AddScopesFromFlow(scopes, scheme.Flows.AuthorizationCode);
            AddScopesFromFlow(scopes, scheme.Flows.Implicit);
            AddScopesFromFlow(scopes, scheme.Flows.Password);
        }

        return scopes;
    }

    /// <summary>
    /// Checks if any operations in the document require OAuth2 authentication.
    /// </summary>
    /// <param name="document">The OpenAPI document.</param>
    /// <returns>True if any operations require OAuth2.</returns>
    public static bool HasOperationsRequiringOAuth2(this OpenApiDocument document)
    {
        // Check document-level security first
        if (document.Security != null)
        {
            foreach (var requirement in document.Security)
            {
                foreach (var kvp in requirement)
                {
                    var schemeName = kvp.Key.Reference?.Id ?? kvp.Key.Name ?? string.Empty;
                    if (IsOAuth2Scheme(document, schemeName))
                    {
                        return true;
                    }
                }
            }
        }

        // Check operation-level security
        if (document.Paths == null)
        {
            return false;
        }

        foreach (var pathPair in document.Paths)
        {
            if (pathPair.Value is not OpenApiPathItem pathItem || pathItem.Operations == null)
            {
                continue;
            }

            foreach (var operationPair in pathItem.Operations)
            {
                var operation = operationPair.Value;
                if (operation?.Security == null)
                {
                    continue;
                }

                foreach (var requirement in operation.Security)
                {
                    foreach (var kvp in requirement)
                    {
                        var schemeName = kvp.Key.Reference?.Id ?? kvp.Key.Name ?? string.Empty;
                        if (IsOAuth2Scheme(document, schemeName))
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the name of the first OAuth2 security scheme.
    /// </summary>
    /// <param name="document">The OpenAPI document.</param>
    /// <returns>The scheme name, or null if not found.</returns>
    public static string? GetOAuth2SchemeName(this OpenApiDocument document)
    {
        if (document.Components?.SecuritySchemes == null)
        {
            return null;
        }

        foreach (var kvp in document.Components.SecuritySchemes)
        {
            if (kvp.Value.Type == Microsoft.OpenApi.SecuritySchemeType.OAuth2)
            {
                return kvp.Key;
            }
        }

        return null;
    }

    /// <summary>
    /// Checks if the named security scheme is an OAuth2 scheme.
    /// </summary>
    private static bool IsOAuth2Scheme(
        OpenApiDocument document,
        string schemeName)
    {
        if (string.IsNullOrEmpty(schemeName) || document.Components?.SecuritySchemes == null)
        {
            return false;
        }

        return document.Components.SecuritySchemes.TryGetValue(schemeName, out var scheme) &&
               scheme.Type == Microsoft.OpenApi.SecuritySchemeType.OAuth2;
    }

    /// <summary>
    /// Checks if the document has OpenID Connect security schemes.
    /// </summary>
    /// <param name="document">The OpenAPI document.</param>
    /// <returns>True if OpenID Connect authentication is configured.</returns>
    public static bool HasOpenIdConnectSecurity(this OpenApiDocument document)
    {
        if (document.Components?.SecuritySchemes == null)
        {
            return false;
        }

        foreach (var kvp in document.Components.SecuritySchemes)
        {
            var scheme = kvp.Value;

            if (scheme.Type == Microsoft.OpenApi.SecuritySchemeType.OpenIdConnect)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the name of the first OpenID Connect security scheme.
    /// </summary>
    /// <param name="document">The OpenAPI document.</param>
    /// <returns>The scheme name, or null if not found.</returns>
    public static string? GetOpenIdConnectSchemeName(this OpenApiDocument document)
    {
        if (document.Components?.SecuritySchemes == null)
        {
            return null;
        }

        foreach (var kvp in document.Components.SecuritySchemes)
        {
            if (kvp.Value.Type == Microsoft.OpenApi.SecuritySchemeType.OpenIdConnect)
            {
                return kvp.Key;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the OpenID Connect discovery URL from the first OpenID Connect security scheme.
    /// </summary>
    /// <param name="document">The OpenAPI document.</param>
    /// <returns>The discovery URL, or null if not found.</returns>
    [SuppressMessage("Design", "CA1055:URI-like properties should not be strings", Justification = "Used for code generation")]
    public static string? GetOpenIdConnectUrl(this OpenApiDocument document)
    {
        if (document.Components?.SecuritySchemes == null)
        {
            return null;
        }

        foreach (var kvp in document.Components.SecuritySchemes)
        {
            if (kvp.Value.Type == Microsoft.OpenApi.SecuritySchemeType.OpenIdConnect &&
                kvp.Value.OpenIdConnectUrl != null)
            {
                return kvp.Value.OpenIdConnectUrl.ToString();
            }
        }

        return null;
    }

    /// <summary>
    /// Adds scopes from a flow to the scopes dictionary.
    /// </summary>
    private static void AddScopesFromFlow(
        Dictionary<string, string> scopes,
        OpenApiOAuthFlow? flow)
    {
        if (flow?.Scopes == null)
        {
            return;
        }

        foreach (var kvp in flow.Scopes)
        {
            // Use ContainsKey + Add instead of TryAdd (not available in netstandard2.0)
            if (!scopes.ContainsKey(kvp.Key))
            {
                scopes.Add(kvp.Key, kvp.Value);
            }
        }
    }
}