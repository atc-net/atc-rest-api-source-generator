namespace Atc.Rest.Api.Generator.Tests.Extractors;

public class OAuthConfigExtractorTests
{
    // An oauth2 scheme with both token-managed flows, required by an operation.
    private const string ClientCredentialsAndAuthCodeYaml = """
                                                           openapi: 3.0.0
                                                           info:
                                                             title: Test
                                                             version: 1.0.0
                                                           paths:
                                                             /pets:
                                                               get:
                                                                 operationId: listPets
                                                                 security:
                                                                   - petstore_auth:
                                                                       - pets.read
                                                                 responses:
                                                                   '200':
                                                                     description: OK
                                                           components:
                                                             securitySchemes:
                                                               petstore_auth:
                                                                 type: oauth2
                                                                 flows:
                                                                   clientCredentials:
                                                                     tokenUrl: https://auth.example.com/token
                                                                     scopes:
                                                                       pets.read: Read pets
                                                                   authorizationCode:
                                                                     authorizationUrl: https://auth.example.com/authorize
                                                                     tokenUrl: https://auth.example.com/code-token
                                                                     refreshUrl: https://auth.example.com/refresh
                                                                     scopes:
                                                                       pets.write: Write pets
                                                           """;

    // The same scheme, but no operation and no document-level requirement uses it.
    private const string UnusedSchemeYaml = """
                                            openapi: 3.0.0
                                            info:
                                              title: Test
                                              version: 1.0.0
                                            paths:
                                              /pets:
                                                get:
                                                  operationId: listPets
                                                  responses:
                                                    '200':
                                                      description: OK
                                            components:
                                              securitySchemes:
                                                petstore_auth:
                                                  type: oauth2
                                                  flows:
                                                    clientCredentials:
                                                      tokenUrl: https://auth.example.com/token
                                                      scopes:
                                                        pets.read: Read pets
                                            """;

    [Fact]
    public void Extract_ClientCredentialsAndAuthorizationCodeFlows_PopulatesBothFlowsAndScopes()
    {
        var document = OpenApiDocumentHelper.ParseYaml(ClientCredentialsAndAuthCodeYaml);

        var result = OAuthConfigExtractor.Extract(document);

        Assert.NotNull(result);
        Assert.Equal("petstore_auth", result.SchemeName);

        Assert.True(result.HasClientCredentials);
        Assert.Equal("https://auth.example.com/token", result.ClientCredentialsTokenUrl);

        Assert.True(result.HasAuthorizationCode);
        Assert.Equal("https://auth.example.com/code-token", result.AuthorizationCodeTokenUrl);
        Assert.Equal("https://auth.example.com/refresh", result.AuthorizationCodeRefreshUrl);
        Assert.True(result.SupportsRefreshTokens);

        // Client Credentials wins as the effective token endpoint.
        Assert.Equal("https://auth.example.com/token", result.EffectiveTokenUrl);

        // Scopes of both flows are collected once each, with their descriptions.
        Assert.Equal(2, result.DefaultScopes.Count);
        Assert.Equal("Read pets", result.DefaultScopes["pets.read"]);
        Assert.Equal("Write pets", result.DefaultScopes["pets.write"]);
        Assert.Equal(2, result.AllAvailableScopes.Count);
        Assert.Contains("pets.read", result.DefaultScopeString, StringComparison.Ordinal);
        Assert.Contains("pets.write", result.DefaultScopeString, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_SchemeNotRequiredByAnyOperation_ReturnsNull()
    {
        var document = OpenApiDocumentHelper.ParseYaml(UnusedSchemeYaml);

        Assert.Null(OAuthConfigExtractor.Extract(document));
    }

    [Fact]
    public void Extract_ImplicitFlowOnly_ReturnsNullBecauseNoTokenManagementIsNeeded()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            security:
                              - browser_auth:
                                  - pets.read
                            paths:
                              /pets:
                                get:
                                  operationId: listPets
                                  responses:
                                    '200':
                                      description: OK
                            components:
                              securitySchemes:
                                browser_auth:
                                  type: oauth2
                                  flows:
                                    implicit:
                                      authorizationUrl: https://auth.example.com/authorize
                                      scopes:
                                        pets.read: Read pets
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        Assert.Null(OAuthConfigExtractor.Extract(document));
        Assert.False(OAuthConfigExtractor.HasOAuth2Security(document));
    }

    [Fact]
    public void HasOAuth2Security_TokenManagedFlowRequiredByOperation_ReturnsTrue()
    {
        var document = OpenApiDocumentHelper.ParseYaml(ClientCredentialsAndAuthCodeYaml);

        Assert.True(OAuthConfigExtractor.HasOAuth2Security(document));
    }

    [Fact]
    public void HasOAuth2Security_SchemeDefinedButUnused_ReturnsFalse()
    {
        var document = OpenApiDocumentHelper.ParseYaml(UnusedSchemeYaml);

        Assert.False(OAuthConfigExtractor.HasOAuth2Security(document));
    }
}