namespace Atc.Rest.Api.Generator.Tests.Extractors;

/// <summary>
/// Tests for the DI registration extractors.
/// Covers the key extractors that read OpenAPI extensions and generate DI code.
/// </summary>
public class DependencyInjectionExtractorTests
{
    // ========== ResilienceDependencyInjectionExtractor ==========
    [Fact]
    public void ResilienceDI_WithRetryExtensions_ProducesOutput()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.1.1
                            info:
                              title: Test
                              version: 1.0.0
                            x-retry-policy: standard
                            x-retry-max-attempts: 3
                            x-retry-backoff: exponential
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
        var result = ResilienceDependencyInjectionExtractor.Extract(document, "TestApi");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("AddApiResilience", result, StringComparison.Ordinal);
    }

    [Fact]
    public void ResilienceDI_WithoutRetryExtensions_ReturnsNull()
    {
        // Arrange
        const string yaml = """
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
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        // Act
        var result = ResilienceDependencyInjectionExtractor.Extract(document, "TestApi");

        // Assert
        Assert.Null(result);
    }

    // ========== ResiliencePoliciesExtractor ==========
    [Fact]
    public void ResiliencePolicies_WithRetryExtensions_ProducesNamedPolicies()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.1.1
                            info:
                              title: Test
                              version: 1.0.0
                            x-retry-policy: standard
                            x-retry-max-attempts: 5
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
        var result = ResiliencePoliciesExtractor.Extract(document, "TestApi");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("ResiliencePolicies", result, StringComparison.Ordinal);
        Assert.Contains("Standard", result, StringComparison.Ordinal);
    }

    // ========== RateLimitDependencyInjectionExtractor ==========
    [Fact]
    public void RateLimitDI_WithoutExtensions_ReturnsNull()
    {
        // Arrange
        const string yaml = """
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
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        // Act
        var result = RateLimitDependencyInjectionExtractor.Extract(document, "TestApi");

        // Assert
        Assert.Null(result);
    }

    // ========== SecurityDependencyInjectionExtractor ==========
    [Fact]
    public void SecurityDI_WithoutSecuritySchemes_ReturnsNull()
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
        var result = SecurityDependencyInjectionExtractor.Extract(document, "TestApi");

        // Assert
        Assert.Null(result);
    }

    // ========== DependencyRegistrationExtractor ==========
    [Fact]
    public void DependencyRegistration_WithHandlers_ProducesClass()
    {
        // Arrange
        var handlers = new List<(string OperationId, string HandlerName, string HandlerNamespace)>
        {
            ("listPets", "ListPetsHandler", "TestApi.Handlers.Pets"),
            ("createPet", "CreatePetHandler", "TestApi.Handlers.Pets"),
        };

        // Act
        var result = DependencyRegistrationExtractor.Extract(
            "TestApi",
            "TestApi",
            handlers,
            "Handler");

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Methods);
        Assert.True(result.Methods.Count > 0);
    }

    [Fact]
    public void DependencyRegistration_EmptyHandlers_ReturnsNull()
    {
        // Arrange
        var handlers = new List<(string OperationId, string HandlerName, string HandlerNamespace)>();

        // Act
        var result = DependencyRegistrationExtractor.Extract(
            "TestApi",
            "TestApi",
            handlers,
            "Handler");

        // Assert
        Assert.Null(result);
    }

    // ========== HybridCacheDependencyInjectionExtractor ==========
    [Fact]
    public void HybridCacheDI_WithCacheExtensions_ProducesOutput()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.1.1
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
        var result = HybridCacheDependencyInjectionExtractor.Extract(document, "TestApi");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("AddApiCaching", result, StringComparison.Ordinal);
    }

    [Fact]
    public void HybridCacheDI_WithoutCacheExtensions_ReturnsNull()
    {
        // Arrange
        const string yaml = """
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
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        // Act
        var result = HybridCacheDependencyInjectionExtractor.Extract(document, "TestApi");

        // Assert
        Assert.Null(result);
    }
}