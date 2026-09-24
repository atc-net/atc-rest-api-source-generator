namespace Atc.Rest.Api.Generator.Tests.Extractors;

/// <summary>
/// Tests for <see cref="SecurityDependencyInjectionExtractor"/>, and for its contract with
/// <see cref="UnifiedServiceCollectionExtractor"/>: whatever the unified <c>Add{Project}Api()</c>
/// references from the <c>Generated.Security</c> namespace has to actually be generated.
/// </summary>
public class SecurityDependencyInjectionExtractorTests
{
    private const string ProjectName = "TestApi";

    private const string BearerOnlyYaml = """
                                          openapi: 3.0.0
                                          info:
                                            title: Test
                                            version: 1.0.0
                                          paths:
                                            /whoami:
                                              get:
                                                operationId: getCurrentUser
                                                security:
                                                  - BearerAuth: []
                                                responses:
                                                  '200':
                                                    description: OK
                                                  '401':
                                                    description: Not authenticated.
                                                  '403':
                                                    description: Not a known user.
                                          components:
                                            securitySchemes:
                                              BearerAuth:
                                                type: http
                                                scheme: bearer
                                                bearerFormat: JWT
                                          """;

    private const string ScopesYaml = """
                                      openapi: 3.0.0
                                      info:
                                        title: Test
                                        version: 1.0.0
                                      paths:
                                        /orders:
                                          get:
                                            operationId: listOrders
                                            security:
                                              - oauth2:
                                                  - orders:read
                                            responses:
                                              '200':
                                                description: OK
                                      components:
                                        securitySchemes:
                                          oauth2:
                                            type: oauth2
                                            flows:
                                              clientCredentials:
                                                tokenUrl: https://auth.example.com/token
                                                scopes:
                                                  orders:read: Read orders
                                      """;

    [Theory]
    [InlineData(BearerOnlyYaml)]
    [InlineData(ScopesYaml)]
    public void UnifiedServiceCollection_OnlyReferencesSecurityMembersThatAreGenerated(
        string yaml)
    {
        // Arrange
        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        // Act
        var unified = UnifiedServiceCollectionExtractor.Extract(document, ProjectName, new ServerConfig());
        var securityDependencyInjection = SecurityDependencyInjectionExtractor.Extract(document, ProjectName);

        // Assert
        var referencesSecurity =
            unified.Contains($"using {ProjectName}.Generated.Security;", StringComparison.Ordinal) ||
            unified.Contains("AddApiSecurityPolicies()", StringComparison.Ordinal);

        if (referencesSecurity)
        {
            Assert.NotNull(securityDependencyInjection);
            Assert.Contains($"namespace {ProjectName}.Generated.Security;", securityDependencyInjection, StringComparison.Ordinal);
            Assert.Contains("public static IServiceCollection AddApiSecurityPolicies(", securityDependencyInjection, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Extract_BearerOnly_RegistersAuthorizationWithoutPolicies()
    {
        // Arrange
        var document = OpenApiDocumentHelper.ParseYaml(BearerOnlyYaml);

        // Act
        var result = SecurityDependencyInjectionExtractor.Extract(document, ProjectName);

        // Assert
        // The unified UseApi() calls app.UseAuthorization(), which throws at startup unless the
        // authorization services are registered - so the method must register them even when
        // there is no policy to add.
        Assert.NotNull(result);
        Assert.Contains("services.AddAuthorization();", result, StringComparison.Ordinal);
        Assert.DoesNotContain("AddPolicy(", result, StringComparison.Ordinal);
        Assert.DoesNotContain("SecurityPolicies.", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_WithScopes_RegistersPolicies()
    {
        // Arrange
        var document = OpenApiDocumentHelper.ParseYaml(ScopesYaml);

        // Act
        var result = SecurityDependencyInjectionExtractor.Extract(document, ProjectName);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("options.AddPolicy(SecurityPolicies.", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_WithoutSecuritySchemes_ReturnsNull()
    {
        // Arrange
        var document = OpenApiDocumentHelper.ParseYaml("""
                                                       openapi: 3.0.0
                                                       info:
                                                         title: Test
                                                         version: 1.0.0
                                                       paths:
                                                         /health:
                                                           get:
                                                             operationId: getHealth
                                                             responses:
                                                               '200':
                                                                 description: OK
                                                       """);

        // Act
        var result = SecurityDependencyInjectionExtractor.Extract(document, ProjectName);

        // Assert
        Assert.Null(result);
    }
}