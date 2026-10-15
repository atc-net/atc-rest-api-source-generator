namespace Atc.Rest.Api.Generator.Tests.Extractors;

public class ApiOptionsExtractorTests
{
    private const string PlainYaml = """
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

    private const string RateLimitedAndSecuredYaml = """
                                                     openapi: 3.0.0
                                                     info:
                                                       title: Test
                                                       version: 1.0.0
                                                     x-ratelimit-policy: global
                                                     x-ratelimit-permit-limit: 100
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
                                                           bearerFormat: JWT
                                                     """;

    [Fact]
    public void ExtractServiceOptions_PlainDocumentWithoutVersioning_AutoDetectedFlagsAreOff()
    {
        var document = OpenApiDocumentHelper.ParseYaml(PlainYaml);
        var config = new ServerConfig { VersioningStrategy = VersioningStrategyType.None };

        var result = ApiOptionsExtractor.ExtractServiceOptions(document, "TestApi", config);

        Assert.Contains("namespace TestApi.Generated;", result, StringComparison.Ordinal);
        Assert.Contains("public class ApiServiceOptions", result, StringComparison.Ordinal);
        Assert.Contains("public bool UseRateLimiting { get; set; } = false;", result, StringComparison.Ordinal);
        Assert.Contains("public bool UseVersioning { get; set; } = false;", result, StringComparison.Ordinal);
        Assert.Contains("public bool UseSecurity { get; set; } = false;", result, StringComparison.Ordinal);

        // ProblemDetails is not auto-detected; it is on unless the host opts out.
        Assert.Contains("public bool UseProblemDetails { get; set; } = true;", result, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractServiceOptions_RateLimitSecurityAndVersioning_AutoDetectedFlagsAreOn()
    {
        var document = OpenApiDocumentHelper.ParseYaml(RateLimitedAndSecuredYaml);
        var config = new ServerConfig { VersioningStrategy = VersioningStrategyType.UrlSegment };

        var result = ApiOptionsExtractor.ExtractServiceOptions(document, "TestApi", config);

        Assert.Contains("public bool UseRateLimiting { get; set; } = true;", result, StringComparison.Ordinal);
        Assert.Contains("public bool UseVersioning { get; set; } = true;", result, StringComparison.Ordinal);
        Assert.Contains("public bool UseSecurity { get; set; } = true;", result, StringComparison.Ordinal);
        Assert.Contains("/// Options for configuring TestApi API services.", result, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractMiddlewareOptions_PlainDocument_MiddlewareFlagsAreOffAndErrorHandlingHookIsPresent()
    {
        var document = OpenApiDocumentHelper.ParseYaml(PlainYaml);

        var result = ApiOptionsExtractor.ExtractMiddlewareOptions(document, "TestApi");

        Assert.Contains("namespace TestApi.Generated;", result, StringComparison.Ordinal);
        Assert.Contains("public class ApiMiddlewareOptions", result, StringComparison.Ordinal);
        Assert.Contains("public bool UseRateLimiter { get; set; } = false;", result, StringComparison.Ordinal);
        Assert.Contains("public bool UseAuthentication { get; set; } = false;", result, StringComparison.Ordinal);
        Assert.Contains("public bool UseAuthorization { get; set; } = false;", result, StringComparison.Ordinal);
        Assert.Contains("public bool UseStatusCodePages { get; set; } = true;", result, StringComparison.Ordinal);

        // The hook references Atc.Rest.MinimalApi.Options and System.Action, so both usings are emitted.
        Assert.Contains("public Action<GlobalErrorHandlingOptions>? ConfigureErrorHandling { get; set; }", result, StringComparison.Ordinal);
        Assert.Contains("using Atc.Rest.MinimalApi.Options;", result, StringComparison.Ordinal);
        Assert.Contains("using System;", result, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractMiddlewareOptions_RateLimitedAndSecured_MiddlewareFlagsAreOn()
    {
        var document = OpenApiDocumentHelper.ParseYaml(RateLimitedAndSecuredYaml);

        var result = ApiOptionsExtractor.ExtractMiddlewareOptions(document, "TestApi");

        Assert.Contains("public bool UseRateLimiter { get; set; } = true;", result, StringComparison.Ordinal);
        Assert.Contains("public bool UseAuthentication { get; set; } = true;", result, StringComparison.Ordinal);
        Assert.Contains("public bool UseAuthorization { get; set; } = true;", result, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractMiddlewareOptions_AtcSecurityExtensionsWithoutASecurityScheme_AuthenticationFlagsAreOn()
    {
        var document = OpenApiDocumentHelper.ParseYaml("""
                                                        openapi: 3.0.0
                                                        info:
                                                          title: Test
                                                          version: 1.0.0
                                                        x-authorize-roles: [reader]
                                                        paths:
                                                          /pets:
                                                            x-authorize-roles: [reader]
                                                            get:
                                                              operationId: listPets
                                                              responses:
                                                                '200':
                                                                  description: OK
                                                        """);

        var result = ApiOptionsExtractor.ExtractMiddlewareOptions(document, "TestApi");

        Assert.Contains("public bool UseAuthentication { get; set; } = true;", result, StringComparison.Ordinal);
        Assert.Contains("public bool UseAuthorization { get; set; } = true;", result, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExtractMiddlewareOptions_UseGlobalErrorHandler_OnlyWithTheGlobalErrorHandler(
        bool useGlobalErrorHandler)
    {
        var document = OpenApiDocumentHelper.ParseYaml(PlainYaml);

        var result = ApiOptionsExtractor.ExtractMiddlewareOptions(document, "TestApi", useGlobalErrorHandler);

        Assert.Equal(
            useGlobalErrorHandler,
            result.Contains("public bool UseGlobalErrorHandler { get; set; } = true;", StringComparison.Ordinal));
    }
}