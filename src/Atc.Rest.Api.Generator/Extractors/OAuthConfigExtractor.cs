namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts OAuth2 configuration from OpenAPI document security schemes.
/// Detects OAuth2 Client Credentials and Authorization Code flows.
/// </summary>
public static class OAuthConfigExtractor
{
    /// <summary>
    /// Extracts OAuth2 configuration from the OpenAPI document.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <returns>OAuth2 configuration if found, null otherwise.</returns>
    public static OAuthConfig? Extract(OpenApiDocument openApiDoc)
    {
        if (openApiDoc == null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        // Check if document has OAuth2 security that needs token management
        if (!openApiDoc.HasOAuth2TokenManagement())
        {
            return null;
        }

        // Check if any operations actually use OAuth2
        if (!openApiDoc.HasOperationsRequiringOAuth2())
        {
            return null;
        }

        var config = new OAuthConfig();

        // Get scheme name
        config.SchemeName = openApiDoc.GetOAuth2SchemeName() ?? "oauth2";

        // Check for Client Credentials flow
        var clientCredentialsFlow = openApiDoc.GetOAuth2ClientCredentialsFlow();
        if (clientCredentialsFlow != null)
        {
            config.HasClientCredentials = true;
            config.ClientCredentialsTokenUrl = clientCredentialsFlow.TokenUrl;

            // Collect scopes from this flow
            if (clientCredentialsFlow.Scopes != null)
            {
                foreach (var scope in clientCredentialsFlow.Scopes)
                {
                    if (!config.DefaultScopes.ContainsKey(scope.Key))
                    {
                        config.DefaultScopes.Add(scope.Key, scope.Value);
                    }
                }
            }
        }

        // Check for Authorization Code flow (for refresh token support)
        var authCodeFlow = openApiDoc.GetOAuth2AuthorizationCodeFlow();
        if (authCodeFlow != null)
        {
            config.HasAuthorizationCode = true;
            config.AuthorizationCodeTokenUrl = authCodeFlow.TokenUrl;
            config.AuthorizationCodeRefreshUrl = authCodeFlow.RefreshUrl;

            // Collect scopes from this flow
            if (authCodeFlow.Scopes != null)
            {
                foreach (var scope in authCodeFlow.Scopes)
                {
                    if (!config.DefaultScopes.ContainsKey(scope.Key))
                    {
                        config.DefaultScopes.Add(scope.Key, scope.Value);
                    }
                }
            }
        }

        // Get all scopes across all flows for reference
        var allScopes = openApiDoc.GetAllOAuth2Scopes();
        foreach (var scope in allScopes)
        {
            if (!config.AllAvailableScopes.ContainsKey(scope.Key))
            {
                config.AllAvailableScopes.Add(scope.Key, scope.Value);
            }
        }

        return config;
    }

    /// <summary>
    /// Checks if the OpenAPI document has OAuth2 security that requires code generation.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <returns>True if OAuth2 code should be generated.</returns>
    public static bool HasOAuth2Security(OpenApiDocument openApiDoc)
    {
        if (openApiDoc == null)
        {
            return false;
        }

        return openApiDoc.HasOAuth2TokenManagement() && openApiDoc.HasOperationsRequiringOAuth2();
    }
}