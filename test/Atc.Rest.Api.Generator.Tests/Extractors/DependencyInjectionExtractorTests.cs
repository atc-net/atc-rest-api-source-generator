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

    [Fact]
    public void RateLimitDI_FixedAlgorithm_ProducesFixedWindowLimiterWithConfiguredValues()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths:
                              /orders:
                                get:
                                  operationId: listOrders
                                  x-ratelimit-policy: orders-standard
                                  x-ratelimit-algorithm: fixed
                                  x-ratelimit-permit-limit: 100
                                  x-ratelimit-window-seconds: 60
                                  x-ratelimit-queue-limit: 5
                                  responses:
                                    '200':
                                      description: OK
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        // Act
        var result = RateLimitDependencyInjectionExtractor.Extract(document, "TestApi");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("AddApiRateLimiting", result, StringComparison.Ordinal);
        Assert.Contains("options.AddFixedWindowLimiter(RateLimitPolicies.OrdersStandard, opt =>", result, StringComparison.Ordinal);
        Assert.Contains("opt.PermitLimit = 100;", result, StringComparison.Ordinal);
        Assert.Contains("opt.Window = TimeSpan.FromSeconds(60);", result, StringComparison.Ordinal);
        Assert.Contains("opt.QueueLimit = 5;", result, StringComparison.Ordinal);
    }

    [Fact]
    public void RateLimitDI_SlidingAlgorithm_ProducesSlidingWindowLimiterWithSegments()
    {
        // Arrange - WindowSeconds=60 => SegmentsPerWindow = max(1, 60/10) = 6
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths:
                              /reports:
                                get:
                                  operationId: listReports
                                  x-ratelimit-policy: reports-sliding
                                  x-ratelimit-algorithm: sliding
                                  x-ratelimit-permit-limit: 50
                                  x-ratelimit-window-seconds: 60
                                  responses:
                                    '200':
                                      description: OK
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        // Act
        var result = RateLimitDependencyInjectionExtractor.Extract(document, "TestApi");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("options.AddSlidingWindowLimiter(RateLimitPolicies.ReportsSliding, opt =>", result, StringComparison.Ordinal);
        Assert.Contains("opt.PermitLimit = 50;", result, StringComparison.Ordinal);
        Assert.Contains("opt.SegmentsPerWindow = 6;", result, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(5, 1)] // below the 10s-per-segment granularity clamps to the minimum of 1
    [InlineData(700, 60)] // above 600s clamps to the maximum of 60 segments
    public void RateLimitDI_SlidingAlgorithm_ClampsSegmentsPerWindow(
        int windowSeconds,
        int expectedSegments)
    {
        // Arrange
        var yaml = $"""
                    openapi: 3.0.0
                    info:
                      title: Test
                      version: 1.0.0
                    paths:
                      /reports:
                        get:
                          operationId: listReports
                          x-ratelimit-policy: reports-sliding
                          x-ratelimit-algorithm: sliding
                          x-ratelimit-permit-limit: 50
                          x-ratelimit-window-seconds: {windowSeconds}
                          responses:
                            '200':
                              description: OK
                    """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        // Act
        var result = RateLimitDependencyInjectionExtractor.Extract(document, "TestApi");

        // Assert
        Assert.NotNull(result);
        Assert.Contains($"opt.SegmentsPerWindow = {expectedSegments};", result, StringComparison.Ordinal);
    }

    [Fact]
    public void RateLimitDI_TokenBucketAlgorithm_ProducesTokenBucketLimiterWithConfiguredValues()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths:
                              /notifications:
                                post:
                                  operationId: sendNotification
                                  x-ratelimit-policy: notifications-burst
                                  x-ratelimit-algorithm: token-bucket
                                  x-ratelimit-permit-limit: 200
                                  x-ratelimit-window-seconds: 60
                                  x-ratelimit-queue-limit: 20
                                  responses:
                                    '202':
                                      description: Accepted
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        // Act
        var result = RateLimitDependencyInjectionExtractor.Extract(document, "TestApi");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("options.AddTokenBucketLimiter(RateLimitPolicies.NotificationsBurst, opt =>", result, StringComparison.Ordinal);
        Assert.Contains("opt.TokenLimit = 200;", result, StringComparison.Ordinal);
        Assert.Contains("opt.ReplenishmentPeriod = TimeSpan.FromSeconds(60);", result, StringComparison.Ordinal);
        Assert.Contains("opt.TokensPerPeriod = 200;", result, StringComparison.Ordinal);
        Assert.Contains("opt.QueueLimit = 20;", result, StringComparison.Ordinal);
    }

    [Fact]
    public void RateLimitDI_ConcurrencyAlgorithm_ProducesConcurrencyLimiterWithConfiguredValues()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths:
                              /exports:
                                post:
                                  operationId: createExport
                                  x-ratelimit-policy: exports-concurrent
                                  x-ratelimit-algorithm: concurrency
                                  x-ratelimit-permit-limit: 5
                                  responses:
                                    '202':
                                      description: Accepted
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        // Act
        var result = RateLimitDependencyInjectionExtractor.Extract(document, "TestApi");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("options.AddConcurrencyLimiter(RateLimitPolicies.ExportsConcurrent, opt =>", result, StringComparison.Ordinal);
        Assert.Contains("opt.PermitLimit = 5;", result, StringComparison.Ordinal);
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