namespace Atc.Rest.Api.Generator.Tests.Extractors;

public class OpenIdConnectConfigExtractorTests
{
    [Fact]
    public void Extract_OpenIdConnectScheme_CollectsDiscoveryUrlDescriptionAndRequestedScopes()
    {
        // Scopes come from the security requirements (document level and operation level), not from the scheme.
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            security:
                              - oidc:
                                  - profile
                            paths:
                              /pets:
                                get:
                                  operationId: listPets
                                  security:
                                    - oidc:
                                        - email
                                        - profile
                                  responses:
                                    '200':
                                      description: OK
                            components:
                              securitySchemes:
                                oidc:
                                  type: openIdConnect
                                  description: Corporate login
                                  openIdConnectUrl: https://login.example.com/.well-known/openid-configuration
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        var result = OpenIdConnectConfigExtractor.Extract(document);

        Assert.NotNull(result);
        Assert.Equal("oidc", result.SchemeName);
        Assert.Equal("https://login.example.com/.well-known/openid-configuration", result.OpenIdConnectUrl);
        Assert.Equal("Corporate login", result.Description);
        Assert.Equal(2, result.DefaultScopes.Count);
        Assert.True(result.DefaultScopes.ContainsKey("profile"));
        Assert.True(result.DefaultScopes.ContainsKey("email"));
    }

    [Fact]
    public void Extract_SchemeWithoutRequirements_HasNoScopes()
    {
        const string yaml = """
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
                                oidc:
                                  type: openIdConnect
                                  openIdConnectUrl: https://login.example.com/.well-known/openid-configuration
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        var result = OpenIdConnectConfigExtractor.Extract(document);

        Assert.NotNull(result);
        Assert.Equal("oidc", result.SchemeName);
        Assert.Null(result.Description);
        Assert.Empty(result.DefaultScopes);
    }

    [Fact]
    public void Extract_NoOpenIdConnectScheme_ReturnsNull()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths:
                              /pets:
                                get:
                                  operationId: listPets
                                  security:
                                    - bearer: []
                                  responses:
                                    '200':
                                      description: OK
                            components:
                              securitySchemes:
                                bearer:
                                  type: http
                                  scheme: bearer
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        Assert.Null(OpenIdConnectConfigExtractor.Extract(document));
    }
}