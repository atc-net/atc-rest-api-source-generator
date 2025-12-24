namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts OpenID Connect configuration from OpenAPI document security schemes.
/// </summary>
public static class OpenIdConnectConfigExtractor
{
    /// <summary>
    /// Extracts OpenID Connect configuration from the OpenAPI document.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <returns>OpenID Connect configuration if found, null otherwise.</returns>
    public static OpenIdConnectConfig? Extract(OpenApiDocument openApiDoc)
    {
        if (openApiDoc == null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        // Check if document has OpenID Connect security
        if (!openApiDoc.HasOpenIdConnectSecurity())
        {
            return null;
        }

        var config = new OpenIdConnectConfig();

        // Get scheme name and discovery URL
        config.SchemeName = openApiDoc.GetOpenIdConnectSchemeName() ?? "openIdConnect";
        config.OpenIdConnectUrl = openApiDoc.GetOpenIdConnectUrl();

        // Get description from the security scheme
        if (openApiDoc.Components?.SecuritySchemes != null &&
            openApiDoc.Components.SecuritySchemes.TryGetValue(config.SchemeName, out var scheme))
        {
            config.Description = scheme.Description;
        }

        // Collect scopes from security requirements
        CollectScopesFromRequirements(openApiDoc, config);

        return config;
    }

    /// <summary>
    /// Collects scopes from security requirements that reference the OpenID Connect scheme.
    /// </summary>
    private static void CollectScopesFromRequirements(
        OpenApiDocument openApiDoc,
        OpenIdConnectConfig config)
    {
        // Check document-level security requirements
        if (openApiDoc.Security != null)
        {
            foreach (var requirement in openApiDoc.Security)
            {
                foreach (var kvp in requirement)
                {
                    var schemeName = kvp.Key.Reference?.Id ?? kvp.Key.Name ?? string.Empty;
                    if (string.Equals(schemeName, config.SchemeName, StringComparison.Ordinal))
                    {
                        foreach (var scope in kvp.Value)
                        {
                            if (!config.DefaultScopes.ContainsKey(scope))
                            {
                                config.DefaultScopes.Add(scope, string.Empty);
                            }
                        }
                    }
                }
            }
        }

        // Check operation-level security requirements
        if (openApiDoc.Paths == null)
        {
            return;
        }

        foreach (var pathPair in openApiDoc.Paths)
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
                        if (string.Equals(schemeName, config.SchemeName, StringComparison.Ordinal))
                        {
                            foreach (var scope in kvp.Value)
                            {
                                if (!config.DefaultScopes.ContainsKey(scope))
                                {
                                    config.DefaultScopes.Add(scope, string.Empty);
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Checks if the OpenAPI document has OpenID Connect security that requires code generation.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <returns>True if OpenID Connect code should be generated.</returns>
    public static bool HasOpenIdConnectSecurity(OpenApiDocument openApiDoc)
    {
        if (openApiDoc == null)
        {
            return false;
        }

        return openApiDoc.HasOpenIdConnectSecurity();
    }
}