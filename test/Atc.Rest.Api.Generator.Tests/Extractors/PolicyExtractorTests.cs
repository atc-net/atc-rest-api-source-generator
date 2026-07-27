namespace Atc.Rest.Api.Generator.Tests.Extractors;

/// <summary>
/// Tests for policy and endpoint extractors that generate constants and route mappings.
/// Covers RateLimitPoliciesExtractor, SecurityPoliciesExtractor, OutputCachePoliciesExtractor,
/// EndpointRegistrationExtractor, WebhookEndpointExtractor, and WebhookParameterExtractor.
/// </summary>
public class PolicyExtractorTests
{
    // ========== RateLimitPoliciesExtractor ==========
    [Fact]
    public void RateLimitPoliciesExtractor_WithDocumentLevelPolicy_ProducesConstants()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            x-ratelimit-policy: global
                            x-ratelimit-permit-limit: 50
                            x-ratelimit-window-seconds: 30
                            paths:
                              /pets:
                                get:
                                  operationId: listPets
                                  responses:
                                    '200':
                                      description: OK
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        // Act
        var result = RateLimitPoliciesExtractor.Extract(document, "TestApi");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("RateLimitPolicies", result, StringComparison.Ordinal);
        Assert.Contains("public const string Global = \"global\";", result, StringComparison.Ordinal);
        Assert.Contains("namespace TestApi.Generated.RateLimiting;", result, StringComparison.Ordinal);
    }

    [Fact]
    public void RateLimitPoliciesExtractor_WithOperationLevelPolicy_ProducesConstants()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths:
                              /pets:
                                get:
                                  operationId: listPets
                                  x-ratelimit-policy: pets-read
                                  x-ratelimit-permit-limit: 200
                                  x-ratelimit-window-seconds: 60
                                  x-ratelimit-algorithm: sliding
                                  responses:
                                    '200':
                                      description: OK
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        // Act
        var result = RateLimitPoliciesExtractor.Extract(document, "TestApi");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("PetsRead", result, StringComparison.Ordinal);
        Assert.Contains("\"pets-read\"", result, StringComparison.Ordinal);
        Assert.Contains("Sliding", result, StringComparison.Ordinal);
    }

    [Fact]
    public void RateLimitPoliciesExtractor_WithPathLevelPolicy_ProducesConstants()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths:
                              /pets:
                                x-ratelimit-policy: pets-api
                                x-ratelimit-permit-limit: 100
                                x-ratelimit-window-seconds: 60
                                get:
                                  operationId: listPets
                                  responses:
                                    '200':
                                      description: OK
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        // Act
        var result = RateLimitPoliciesExtractor.Extract(document, "TestApi");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("PetsApi", result, StringComparison.Ordinal);
        Assert.Contains("\"pets-api\"", result, StringComparison.Ordinal);
    }

    [Fact]
    public void RateLimitPoliciesExtractor_WithoutExtensions_ReturnsNull()
    {
        // Arrange
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
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        // Act
        var result = RateLimitPoliciesExtractor.Extract(document, "TestApi");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void RateLimitPoliciesExtractor_GeneratesCorrectNamespace()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            x-ratelimit-policy: global
                            paths:
                              /health:
                                get:
                                  operationId: getHealth
                                  responses:
                                    '200':
                                      description: OK
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        // Act
        var result = RateLimitPoliciesExtractor.Extract(document, "MyProject.Api");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("namespace MyProject.Api.Generated.RateLimiting;", result, StringComparison.Ordinal);
    }

    [Fact]
    public void RateLimitPoliciesExtractor_DocComment_ReflectsActualPermitLimitAndWindow()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths:
                              /logs:
                                get:
                                  operationId: getLogs
                                  x-ratelimit-policy: logs-read
                                  x-ratelimit-algorithm: sliding
                                  x-ratelimit-permit-limit: 30
                                  x-ratelimit-window-seconds: 60
                                  responses:
                                    '200':
                                      description: OK
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        // Act
        var result = RateLimitPoliciesExtractor.Extract(document, "TestApi");

        // Assert - the doc comment interpolates the *actual* configured numbers, not the
        // fallback defaults (100/60), so this fails if permit-limit extraction regresses.
        Assert.NotNull(result);
        Assert.Contains("Policy: Sliding window, 30 requests/60s", result, StringComparison.Ordinal);
        Assert.Contains("public const string LogsRead = \"logs-read\";", result, StringComparison.Ordinal);
    }

    [Fact]
    public void RateLimitPoliciesExtractor_MultiplePolicies_AreSortedAlphabetically()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths:
                              /orders:
                                x-ratelimit-policy: orders-standard
                                x-ratelimit-permit-limit: 100
                                get:
                                  operationId: listOrders
                                  responses:
                                    '200':
                                      description: OK
                              /accounts:
                                x-ratelimit-policy: accounts-standard
                                x-ratelimit-permit-limit: 50
                                get:
                                  operationId: listAccounts
                                  responses:
                                    '200':
                                      description: OK
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        // Act
        var result = RateLimitPoliciesExtractor.Extract(document, "TestApi");

        // Assert - policies are ordered by policy name (Ordinal), so "accounts-standard"
        // (constant AccountsStandard) must appear before "orders-standard" even though
        // /orders is declared first in the document.
        Assert.NotNull(result);
        var accountsIndex = result.IndexOf("AccountsStandard", StringComparison.Ordinal);
        var ordersIndex = result.IndexOf("OrdersStandard", StringComparison.Ordinal);
        Assert.True(accountsIndex >= 0 && ordersIndex >= 0, "Both constants should be present");
        Assert.True(accountsIndex < ordersIndex, "AccountsStandard should be emitted before OrdersStandard");
    }

    [Fact]
    public void RateLimitPoliciesExtractor_SamePolicyNameReusedAcrossPaths_KeepsFirstOccurrenceConfig()
    {
        // Arrange - "global" is declared once at document level (permit-limit 1000) and then
        // reused verbatim (no override) on a second path. CollectPolicies dedups by policy
        // name and keeps whichever config it saw first.
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            x-ratelimit-policy: global
                            x-ratelimit-permit-limit: 1000
                            x-ratelimit-window-seconds: 60
                            paths:
                              /health:
                                get:
                                  operationId: getHealth
                                  responses:
                                    '200':
                                      description: OK
                              /webhooks:
                                x-ratelimit-policy: global
                                post:
                                  operationId: receiveWebhook
                                  responses:
                                    '200':
                                      description: OK
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        // Act
        var result = RateLimitPoliciesExtractor.Extract(document, "TestApi");

        // Assert - only one "Global" constant is emitted, with the document-level config.
        Assert.NotNull(result);
        var occurrences = result.Split("public const string Global", StringSplitOptions.None).Length - 1;
        Assert.Equal(1, occurrences);
        Assert.Contains("Policy: Fixed window, 1000 requests/60s", result, StringComparison.Ordinal);
    }

    // ========== SecurityPoliciesExtractor ==========
    [Fact]
    public void SecurityPoliciesExtractor_WithOAuth2Scopes_ProducesConstants()
    {
        // Arrange
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
                                    - oauth2:
                                        - "pets.read"
                                  responses:
                                    '200':
                                      description: OK
                            components:
                              securitySchemes:
                                oauth2:
                                  type: oauth2
                                  flows:
                                    authorizationCode:
                                      authorizationUrl: https://auth.example.com/authorize
                                      tokenUrl: https://auth.example.com/token
                                      scopes:
                                        "pets.read": Read access to pets
                                        "pets.write": Write access to pets
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        // Act
        var result = SecurityPoliciesExtractor.Extract(document, "TestApi");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("SecurityPolicies", result, StringComparison.Ordinal);
        Assert.Contains("namespace TestApi.Generated.Security;", result, StringComparison.Ordinal);
        Assert.Contains("pets.read", result, StringComparison.Ordinal);
    }

    [Fact]
    public void SecurityPoliciesExtractor_WithMultipleScopes_ProducesCompositePolicyAndIndividualPolicies()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths:
                              /pets:
                                post:
                                  operationId: createPet
                                  security:
                                    - oauth2:
                                        - "pets.read"
                                        - "pets.write"
                                  responses:
                                    '201':
                                      description: Created
                            components:
                              securitySchemes:
                                oauth2:
                                  type: oauth2
                                  flows:
                                    authorizationCode:
                                      authorizationUrl: https://auth.example.com/authorize
                                      tokenUrl: https://auth.example.com/token
                                      scopes:
                                        "pets.read": Read access to pets
                                        "pets.write": Write access to pets
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        // Act
        var result = SecurityPoliciesExtractor.Extract(document, "TestApi");

        // Assert
        Assert.NotNull(result);

        // The composite policy should contain both scopes joined with '+'
        Assert.Contains("pets.read+pets.write", result, StringComparison.Ordinal);

        // Individual scope policies should also be generated
        Assert.Contains("\"oauth2:pets.read\"", result, StringComparison.Ordinal);
        Assert.Contains("\"oauth2:pets.write\"", result, StringComparison.Ordinal);
    }

    [Fact]
    public void SecurityPoliciesExtractor_WithoutSecurityRequirements_ReturnsNull()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths:
                              /public:
                                get:
                                  operationId: getPublic
                                  responses:
                                    '200':
                                      description: OK
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        // Act
        var result = SecurityPoliciesExtractor.Extract(document, "TestApi");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void SecurityPoliciesExtractor_WithEmptyScopes_ReturnsNull()
    {
        // Arrange - security with no scopes should produce no policies
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

        // Act
        var result = SecurityPoliciesExtractor.Extract(document, "TestApi");

        // Assert - empty scopes means no policies to generate
        Assert.Null(result);
    }

    [Fact]
    public void SecurityPoliciesExtractor_WithDeprecatedScheme_EmitsObsoleteAttribute()
    {
        // Arrange - A8: deprecated: true on security scheme → [Obsolete] on policy constant
        const string yaml = """
                            openapi: 3.2.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths:
                              /pets:
                                get:
                                  operationId: listPets
                                  security:
                                    - legacyOAuth:
                                        - "pets.read"
                                  responses:
                                    '200':
                                      description: OK
                            components:
                              securitySchemes:
                                legacyOAuth:
                                  type: oauth2
                                  deprecated: true
                                  flows:
                                    authorizationCode:
                                      authorizationUrl: https://auth.example.com/authorize
                                      tokenUrl: https://auth.example.com/token
                                      scopes:
                                        "pets.read": Read access to pets
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        // Act
        var result = SecurityPoliciesExtractor.Extract(document, "TestApi");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("[Obsolete(", result, StringComparison.Ordinal);
        Assert.Contains("Security scheme marked deprecated", result, StringComparison.Ordinal);
        Assert.Contains("pets.read", result, StringComparison.Ordinal);
    }

    [Fact]
    public void SecurityPoliciesExtractor_WithMutualTlsScheme_ProducesConstantWithTlsDocComment()
    {
        // Arrange - A6: mutualTLS scheme type → policy constant with TLS doc comment (ATC_API_SEC011)
        const string yaml = """
                            openapi: 3.2.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths:
                              /devices:
                                get:
                                  operationId: listDevices
                                  security:
                                    - clientCert: []
                                  responses:
                                    '200':
                                      description: OK
                            components:
                              securitySchemes:
                                clientCert:
                                  type: mutualTLS
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        // Act
        var result = SecurityPoliciesExtractor.Extract(document, "TestApi");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("clientCert", result, StringComparison.Ordinal);
        Assert.Contains("TLS client certificate", result, StringComparison.Ordinal);
    }

    [Fact]
    public void SecurityPoliciesExtractor_WithNonDeprecatedScheme_DoesNotEmitObsoleteAttribute()
    {
        // Arrange - non-deprecated scheme should not get [Obsolete]
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
                                    - oauth2:
                                        - "pets.read"
                                  responses:
                                    '200':
                                      description: OK
                            components:
                              securitySchemes:
                                oauth2:
                                  type: oauth2
                                  flows:
                                    authorizationCode:
                                      authorizationUrl: https://auth.example.com/authorize
                                      tokenUrl: https://auth.example.com/token
                                      scopes:
                                        "pets.read": Read access to pets
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        // Act
        var result = SecurityPoliciesExtractor.Extract(document, "TestApi");

        // Assert
        Assert.NotNull(result);
        Assert.DoesNotContain("[Obsolete", result, StringComparison.Ordinal);
        Assert.Contains("pets.read", result, StringComparison.Ordinal);
    }

    // ========== OAuth2MetadataUrl + DeviceAuthorization flow ==========
    [Fact]
    public void SecurityPoliciesExtractor_WithOAuth2MetadataUrl_EmitsMetadataUrlConstant()
    {
        // Arrange — A9: oauth2MetadataUrl → named constant in SecurityPolicies.Metadata nested class
        const string yaml = """
                            openapi: "3.2.0"
                            info:
                              title: Test
                              version: 1.0.0
                            paths:
                              /pets:
                                get:
                                  operationId: listPets
                                  security:
                                    - deviceOAuth:
                                        - "pets.read"
                                  responses:
                                    '200':
                                      description: OK
                            components:
                              securitySchemes:
                                deviceOAuth:
                                  type: oauth2
                                  oauth2MetadataUrl: "https://auth.example.com/.well-known/oauth-authorization-server"
                                  flows:
                                    deviceAuthorization:
                                      tokenUrl: "https://auth.example.com/token"
                                      scopes:
                                        "pets.read": Read access to pets
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        // Act
        var result = SecurityPoliciesExtractor.Extract(document, "TestApi");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("DeviceOAuthMetadataUrl", result, StringComparison.Ordinal);
        Assert.Contains("https://auth.example.com/.well-known/oauth-authorization-server", result, StringComparison.Ordinal);
        Assert.Contains("RFC 8414", result, StringComparison.Ordinal);
    }

    [Fact]
    public void SecurityPoliciesExtractor_WithDeviceAuthorizationFlow_EmitsPolicyConstant()
    {
        // Arrange — A9: deviceAuthorization flow scopes generate policy constants just like other OAuth2 flows
        const string yaml = """
                            openapi: "3.2.0"
                            info:
                              title: Test
                              version: 1.0.0
                            paths:
                              /pets:
                                get:
                                  operationId: listPets
                                  security:
                                    - deviceOAuth:
                                        - "pets.read"
                                  responses:
                                    '200':
                                      description: OK
                            components:
                              securitySchemes:
                                deviceOAuth:
                                  type: oauth2
                                  flows:
                                    deviceAuthorization:
                                      tokenUrl: "https://auth.example.com/token"
                                      scopes:
                                        "pets.read": Read access to pets
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        // Act
        var result = SecurityPoliciesExtractor.Extract(document, "TestApi");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("DeviceOAuthPetsRead", result, StringComparison.Ordinal);
        Assert.Contains("deviceOAuth:pets.read", result, StringComparison.Ordinal);
    }

    [Fact]
    public void SecurityPoliciesExtractor_WithoutOAuth2MetadataUrl_DoesNotEmitMetadataClass()
    {
        // Arrange — no oauth2MetadataUrl → no Metadata nested class emitted
        const string yaml = """
                            openapi: "3.0.0"
                            info:
                              title: Test
                              version: 1.0.0
                            paths:
                              /pets:
                                get:
                                  operationId: listPets
                                  security:
                                    - oauth2:
                                        - "pets.read"
                                  responses:
                                    '200':
                                      description: OK
                            components:
                              securitySchemes:
                                oauth2:
                                  type: oauth2
                                  flows:
                                    clientCredentials:
                                      tokenUrl: "https://auth.example.com/token"
                                      scopes:
                                        "pets.read": Read access to pets
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        // Act
        var result = SecurityPoliciesExtractor.Extract(document, "TestApi");

        // Assert
        Assert.NotNull(result);
        Assert.DoesNotContain("MetadataUrl", result, StringComparison.Ordinal);
        Assert.DoesNotContain("RFC 8414", result, StringComparison.Ordinal);
    }

    // ========== OutputCachePoliciesExtractor ==========
    [Fact]
    public void OutputCachePoliciesExtractor_WithPathLevelCachePolicy_ProducesConstants()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths:
                              /products:
                                x-cache-type: output
                                x-cache-policy: products
                                x-cache-expiration-seconds: 600
                                get:
                                  operationId: listProducts
                                  responses:
                                    '200':
                                      description: OK
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        // Act
        var result = OutputCachePoliciesExtractor.Extract(document, "TestApi");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("OutputCachePolicies", result, StringComparison.Ordinal);
        Assert.Contains("namespace TestApi.Generated.Caching;", result, StringComparison.Ordinal);
        Assert.Contains("public const string Products = \"products\";", result, StringComparison.Ordinal);
        Assert.Contains("expiration", result, StringComparison.Ordinal);
    }

    [Fact]
    public void OutputCachePoliciesExtractor_WithDocumentLevelCachePolicy_ProducesConstants()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            x-cache-type: output
                            x-cache-policy: global-cache
                            x-cache-expiration-seconds: 120
                            paths:
                              /items:
                                get:
                                  operationId: listItems
                                  responses:
                                    '200':
                                      description: OK
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        // Act
        var result = OutputCachePoliciesExtractor.Extract(document, "TestApi");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("GlobalCache", result, StringComparison.Ordinal);
        Assert.Contains("\"global-cache\"", result, StringComparison.Ordinal);
    }

    [Fact]
    public void OutputCachePoliciesExtractor_WithHybridCacheType_ReturnsNull()
    {
        // Arrange - hybrid cache type is handled by a different extractor
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths:
                              /items:
                                x-cache-type: hybrid
                                x-cache-policy: items
                                x-cache-expiration-seconds: 300
                                get:
                                  operationId: listItems
                                  responses:
                                    '200':
                                      description: OK
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        // Act
        var result = OutputCachePoliciesExtractor.Extract(document, "TestApi");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void OutputCachePoliciesExtractor_WithoutCacheExtensions_ReturnsNull()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths:
                              /items:
                                get:
                                  operationId: listItems
                                  responses:
                                    '200':
                                      description: OK
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        // Act
        var result = OutputCachePoliciesExtractor.Extract(document, "TestApi");

        // Assert
        Assert.Null(result);
    }

    // ========== EndpointRegistrationExtractor ==========
    [Fact]
    public void EndpointRegistrationExtractor_WithPaths_ProducesClassParameters()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths:
                              /pets:
                                get:
                                  operationId: listPets
                                  summary: List all pets
                                  tags:
                                    - Pets
                                  responses:
                                    '200':
                                      description: OK
                              /pets/{petId}:
                                get:
                                  operationId: getPet
                                  summary: Get a pet by ID
                                  parameters:
                                    - name: petId
                                      in: path
                                      required: true
                                      schema:
                                        type: string
                                  responses:
                                    '200':
                                      description: OK
                                    '404':
                                      description: Not found
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        // Act
        var result = EndpointRegistrationExtractor.Extract(document, "TestApi");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("EndpointRegistration", result.ClassTypeName);
        Assert.NotNull(result.Methods);
        Assert.Single(result.Methods);

        var method = result.Methods[0];
        Assert.Equal("MapTestApiEndpoints", method.Name);
        Assert.Equal("IEndpointRouteBuilder", method.ReturnTypeName);
        Assert.NotNull(method.Content);
        Assert.Contains("MapGet", method.Content, StringComparison.Ordinal);
        Assert.Contains("/pets", method.Content, StringComparison.Ordinal);
        Assert.Contains("/pets/{petId}", method.Content, StringComparison.Ordinal);
        Assert.Contains("IListPetsHandler", method.Content, StringComparison.Ordinal);
        Assert.Contains("IGetPetHandler", method.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void EndpointRegistrationExtractor_WithPostOperation_GeneratesMapPost()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths:
                              /pets:
                                post:
                                  operationId: createPet
                                  requestBody:
                                    required: true
                                    content:
                                      application/json:
                                        schema:
                                          type: object
                                          properties:
                                            name:
                                              type: string
                                  responses:
                                    '201':
                                      description: Created
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        // Act
        var result = EndpointRegistrationExtractor.Extract(document, "TestApi");

        // Assert
        Assert.NotNull(result);
        var method = result.Methods[0];
        Assert.Contains("MapPost", method.Content, StringComparison.Ordinal);
        Assert.Contains("CreatePetParameters", method.Content, StringComparison.Ordinal);
        Assert.Contains("ICreatePetHandler", method.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void EndpointRegistrationExtractor_WithNoPaths_ReturnsNull()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths: {}
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        // Act
        var result = EndpointRegistrationExtractor.Extract(document, "TestApi");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void EndpointRegistrationExtractor_ExtractEndpointMappingExtension_WithClassNames_ProducesMapping()
    {
        // Arrange
        var classNames = new List<string>
        {
            "PetEndpointDefinition",
            "UserEndpointDefinition",
        };

        // Act
        var result = EndpointRegistrationExtractor.ExtractEndpointMappingExtension(
            "TestApi",
            classNames);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("EndpointDefinitionExtensions", result.ClassTypeName);
        Assert.NotNull(result.Methods);
        Assert.Single(result.Methods);

        var method = result.Methods[0];
        Assert.Equal("MapApiEndpoints", method.Name);
        Assert.Contains("PetEndpointDefinition", method.Content, StringComparison.Ordinal);
        Assert.Contains("UserEndpointDefinition", method.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void EndpointRegistrationExtractor_ExtractEndpointMappingExtension_EmptyList_ReturnsNull()
    {
        // Arrange
        var classNames = new List<string>();

        // Act
        var result = EndpointRegistrationExtractor.ExtractEndpointMappingExtension(
            "TestApi",
            classNames);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void EndpointRegistrationExtractor_ExtractEndpointMappingExtension_WithPathSegment_UsesSegmentInMethodName()
    {
        // Arrange
        var classNames = new List<string> { "ListPetsEndpointDefinition" };

        // Act
        var result = EndpointRegistrationExtractor.ExtractEndpointMappingExtension(
            "TestApi",
            "Pets",
            classNames);

        // Assert
        Assert.NotNull(result);
        var method = result.Methods[0];
        Assert.Equal("MapPetsEndpoints", method.Name);
    }

    // ========== WebhookEndpointExtractor ==========
    [Fact]
    public void WebhookEndpointExtractor_WithWebhooks_ProducesEndpointRegistration()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.1.1
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            webhooks:
                              newPet:
                                post:
                                  operationId: onNewPet
                                  summary: A new pet was added
                                  requestBody:
                                    required: true
                                    content:
                                      application/json:
                                        schema:
                                          $ref: '#/components/schemas/Pet'
                                  responses:
                                    '200':
                                      description: Acknowledged
                              orderPlaced:
                                post:
                                  operationId: onOrderPlaced
                                  summary: An order was placed
                                  requestBody:
                                    required: true
                                    content:
                                      application/json:
                                        schema:
                                          $ref: '#/components/schemas/Order'
                                  responses:
                                    '200':
                                      description: Acknowledged
                            components:
                              schemas:
                                Pet:
                                  type: object
                                  properties:
                                    id:
                                      type: integer
                                    name:
                                      type: string
                                Order:
                                  type: object
                                  properties:
                                    id:
                                      type: integer
                                    total:
                                      type: number
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var config = new ServerConfig();

        // Act
        var result = WebhookEndpointExtractor.Extract(document, "TestApi", config);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("WebhookEndpointExtensions", result.ClassTypeName);
        Assert.NotNull(result.Methods);
        Assert.Single(result.Methods);

        var method = result.Methods[0];
        Assert.Equal("MapTestApiWebhooks", method.Name);
        Assert.NotNull(method.Content);
        Assert.Contains("/webhooks", method.Content, StringComparison.Ordinal);
        Assert.Contains("MapPost", method.Content, StringComparison.Ordinal);
        Assert.Contains("IOnNewPetWebhookHandler", method.Content, StringComparison.Ordinal);
        Assert.Contains("IOnOrderPlacedWebhookHandler", method.Content, StringComparison.Ordinal);
        Assert.Contains("/new-pet", method.Content, StringComparison.Ordinal);
        Assert.Contains("/order-placed", method.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void WebhookEndpointExtractor_WithCustomBasePath_UsesConfiguredPath()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.1.1
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            webhooks:
                              newPet:
                                post:
                                  operationId: onNewPet
                                  requestBody:
                                    required: true
                                    content:
                                      application/json:
                                        schema:
                                          type: object
                                          properties:
                                            name:
                                              type: string
                                  responses:
                                    '200':
                                      description: OK
                            components:
                              schemas: {}
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var config = new ServerConfig { WebhookBasePath = "/api/hooks" };

        // Act
        var result = WebhookEndpointExtractor.Extract(document, "TestApi", config);

        // Assert
        Assert.NotNull(result);
        var method = result.Methods[0];
        Assert.Contains("/api/hooks", method.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void WebhookEndpointExtractor_WithoutWebhooks_ReturnsNull()
    {
        // Arrange
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
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var config = new ServerConfig();

        // Act
        var result = WebhookEndpointExtractor.Extract(document, "TestApi", config);

        // Assert
        Assert.Null(result);
    }

    // ========== WebhookParameterExtractor ==========
    [Fact]
    public void WebhookParameterExtractor_WithRequestBody_ProducesParameterClass()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.1.1
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            webhooks:
                              newPet:
                                post:
                                  operationId: onNewPet
                                  requestBody:
                                    required: true
                                    content:
                                      application/json:
                                        schema:
                                          $ref: '#/components/schemas/Pet'
                                  responses:
                                    '200':
                                      description: OK
                            components:
                              schemas:
                                Pet:
                                  type: object
                                  required:
                                    - id
                                    - name
                                  properties:
                                    id:
                                      type: integer
                                      format: int64
                                    name:
                                      type: string
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var resolver = new SystemTypeConflictResolver([]);

        // Act
        var result = WebhookParameterExtractor.Extract(document, "TestApi", resolver);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        var (className, content) = result[0];
        Assert.Equal("OnNewPetWebhookParameters", className);
        Assert.Contains("Pet Payload", content, StringComparison.Ordinal);
        Assert.Contains("[FromBody]", content, StringComparison.Ordinal);
        Assert.Contains("[Required]", content, StringComparison.Ordinal);
        Assert.Contains("sealed class OnNewPetWebhookParameters", content, StringComparison.Ordinal);
    }

    [Fact]
    public void WebhookParameterExtractor_WithOptionalBody_DoesNotAddRequired()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.1.1
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            webhooks:
                              statusUpdate:
                                post:
                                  operationId: onStatusUpdate
                                  requestBody:
                                    required: false
                                    content:
                                      application/json:
                                        schema:
                                          $ref: '#/components/schemas/StatusPayload'
                                  responses:
                                    '200':
                                      description: OK
                            components:
                              schemas:
                                StatusPayload:
                                  type: object
                                  properties:
                                    status:
                                      type: string
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var resolver = new SystemTypeConflictResolver([]);

        // Act
        var result = WebhookParameterExtractor.Extract(document, "TestApi", resolver);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        var (className, content) = result[0];
        Assert.Equal("OnStatusUpdateWebhookParameters", className);
        Assert.DoesNotContain("[Required]", content, StringComparison.Ordinal);
        Assert.Contains("StatusPayload?", content, StringComparison.Ordinal);
    }

    [Fact]
    public void WebhookParameterExtractor_NoBodyWebhooks_ReturnsNull()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.1.1
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            webhooks:
                              heartbeat:
                                post:
                                  operationId: onHeartbeat
                                  responses:
                                    '200':
                                      description: OK
                            components:
                              schemas: {}
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var resolver = new SystemTypeConflictResolver([]);

        // Act
        var result = WebhookParameterExtractor.Extract(document, "TestApi", resolver);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void WebhookParameterExtractor_WithoutWebhooks_ReturnsNull()
    {
        // Arrange
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
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var resolver = new SystemTypeConflictResolver([]);

        // Act
        var result = WebhookParameterExtractor.Extract(document, "TestApi", resolver);

        // Assert
        Assert.Null(result);
    }
}