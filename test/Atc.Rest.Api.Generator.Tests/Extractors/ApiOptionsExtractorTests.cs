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
    public void ExtractServiceOptions_PlainDocumentWithoutVersioning_FeatureSwitchesAreNotGenerated()
    {
        var document = OpenApiDocumentHelper.ParseYaml(PlainYaml);
        var config = new ServerConfig { VersioningStrategy = VersioningStrategyType.None };

        var result = ApiOptionsExtractor.ExtractServiceOptions(document, "TestApi", config);

        Assert.Contains("namespace TestApi.Generated;", result, StringComparison.Ordinal);
        Assert.Contains("public class ApiServiceOptions", result, StringComparison.Ordinal);

        // A switch exists only when Add{Api}Api() reads it: a switch nothing reads is a lie
        Assert.DoesNotContain("UseRateLimiting", result, StringComparison.Ordinal);
        Assert.DoesNotContain("UseVersioning", result, StringComparison.Ordinal);
        Assert.DoesNotContain("UseSecurity", result, StringComparison.Ordinal);
        Assert.DoesNotContain("UseOutputCaching", result, StringComparison.Ordinal);

        // ProblemDetails is not decided by the specification; it is on unless the host opts out.
        Assert.Contains("public bool UseProblemDetails { get; set; } = true;", result, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractServiceOptions_RateLimitSecurityAndVersioning_FeatureSwitchesAreGeneratedAndOn()
    {
        var document = OpenApiDocumentHelper.ParseYaml(RateLimitedAndSecuredYaml);
        var config = new ServerConfig { VersioningStrategy = VersioningStrategyType.UrlSegment };

        var result = ApiOptionsExtractor.ExtractServiceOptions(document, "TestApi", config);

        Assert.Contains("public bool UseRateLimiting { get; set; } = true;", result, StringComparison.Ordinal);
        Assert.Contains("public bool UseVersioning { get; set; } = true;", result, StringComparison.Ordinal);
        Assert.Contains("public bool UseSecurity { get; set; } = true;", result, StringComparison.Ordinal);
        Assert.Contains("/// The options of the generated <c>Add…Api()</c>: a switch per registration it makes.", result, StringComparison.Ordinal);

        // The default is decided when the code is generated, not detected at run time: the comment says which
        Assert.DoesNotContain("Auto-detected", result, StringComparison.Ordinal);
        Assert.Contains("Generated because the specification declares x-ratelimit-* policies", result, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractMiddlewareOptions_PlainDocument_FeatureSwitchesAreNotGeneratedAndErrorHandlingHookIsPresent()
    {
        var document = OpenApiDocumentHelper.ParseYaml(PlainYaml);

        var result = ApiOptionsExtractor.ExtractMiddlewareOptions(document, "TestApi");

        Assert.Contains("namespace TestApi.Generated;", result, StringComparison.Ordinal);
        Assert.Contains("public class ApiMiddlewareOptions", result, StringComparison.Ordinal);

        // A switch exists only when Use{Api}Api() reads it
        Assert.DoesNotContain("UseRateLimiter", result, StringComparison.Ordinal);
        Assert.DoesNotContain("UseAuthentication", result, StringComparison.Ordinal);
        Assert.DoesNotContain("UseAuthorization", result, StringComparison.Ordinal);
        Assert.DoesNotContain("UseOutputCache", result, StringComparison.Ordinal);
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
        Assert.DoesNotContain("Auto-detected", result, StringComparison.Ordinal);
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
    public void ExtractMiddlewareOptions_HasTheAfterErrorHandlingHook_AndNoHostOwnsPipeline(
        bool useGlobalErrorHandler)
    {
        var document = OpenApiDocumentHelper.ParseYaml(PlainYaml);

        var result = ApiOptionsExtractor.ExtractMiddlewareOptions(document, "TestApi", useGlobalErrorHandler);

        // The host's own middleware (CORS, HSTS, ...) goes in at the generator's spot; a host that orders its whole
        // pipeline does not call Use{Api}Api() at all, so there is no switch for that any more
        Assert.Contains("public Action<IApplicationBuilder>? AfterErrorHandling { get; set; }", result, StringComparison.Ordinal);
        Assert.Contains("/// The options of the generated <c>Use…Api()</c>:", result, StringComparison.Ordinal);
        Assert.Contains("using Microsoft.AspNetCore.Builder;", result, StringComparison.Ordinal);
        Assert.DoesNotContain("HostOwnsPipeline", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// The generated output cache stands aside only for a host's own UseOutputCache() that runs before it: the switch
    /// tells a host that adds its own after Use…Api() to turn the generated one off (two output caches throw).
    /// </summary>
    [Fact]
    public void ExtractMiddlewareOptions_OutputCache_TellsAHostWithItsOwnToTurnItOff()
    {
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.0.0
            info: { title: T, version: 1.0.0 }
            paths:
              /products:
                x-cache-type: output
                x-cache-policy: products
                get:
                  operationId: listProducts
                  responses:
                    '200': { description: OK }
            """);

        var result = ApiOptionsExtractor.ExtractMiddlewareOptions(document, "TestApi");

        Assert.Contains("public bool UseOutputCache { get; set; } = true;", result, StringComparison.Ordinal);
        Assert.Contains("A host that adds <c>UseOutputCache()</c> itself after <c>Use…Api()</c>", result, StringComparison.Ordinal);
        Assert.Contains("turns this off - a second output cache throws on every request", result, StringComparison.Ordinal);
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