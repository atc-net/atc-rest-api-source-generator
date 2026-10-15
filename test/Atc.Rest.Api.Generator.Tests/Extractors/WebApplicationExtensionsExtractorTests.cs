namespace Atc.Rest.Api.Generator.Tests.Extractors;

/// <summary>
/// Tests for the middleware order of the unified <c>Map{Project}Api()</c>.
/// </summary>
public class WebApplicationExtensionsExtractorTests
{
    private const string ProjectName = "TestApi";

    private const string SecuredAndLimitedYaml = """
        openapi: 3.0.0
        info:
          title: Test
          version: 1.0.0
        security:
          - bearer_auth: []
        paths:
          /orders:
            x-ratelimit-policy: orders
            x-ratelimit-permit-limit: 10
            x-ratelimit-partition: {partition}
            get:
              operationId: listOrders
              responses:
                '200':
                  description: OK
        components:
          securitySchemes:
            bearer_auth:
              type: http
              scheme: bearer
        """;

    /// <summary>
    /// A limiter partitioned by user reads <c>HttpContext.User</c>, so authentication has to run before it; a
    /// limiter that does not needs no identity and stays in front of authentication, where it costs the least.
    /// </summary>
    [Theory]
    [InlineData("user", true)]
    [InlineData("ip", false)]
    public void ExtractUnified_AuthenticationRunsBeforeTheRateLimiter_OnlyWhenAPolicyPartitionsByUser(
        string partition,
        bool authenticationFirst)
    {
        // Arrange
        var document = OpenApiDocumentHelper.ParseYaml(SecuredAndLimitedYaml.Replace("{partition}", partition, StringComparison.Ordinal));

        // Act
        var result = WebApplicationExtensionsExtractor.ExtractUnified(document, ProjectName, new ServerConfig());

        // Assert
        var authentication = result.IndexOf("app.UseAuthentication();", StringComparison.Ordinal);
        var rateLimiter = result.IndexOf("app.UseRateLimiter();", StringComparison.Ordinal);
        Assert.True(authentication >= 0 && rateLimiter >= 0, result);
        Assert.Equal(authenticationFirst, authentication < rateLimiter);
    }

    /// <summary>
    /// A specification may declare its access with Atc's extensions alone, without a security scheme: it still needs
    /// authentication in the pipeline, and before a limiter that partitions by user.
    /// </summary>
    [Theory]
    [InlineData("x-authorize-roles: [reader]")]
    [InlineData("x-authentication-required: true")]
    public void ExtractUnified_AtcSecurityExtensionsWithoutASecurityScheme_AuthenticationRunsBeforeAUserPartitionedLimiter(
        string securityExtension)
    {
        // Arrange
        var document = OpenApiDocumentHelper.ParseYaml($$"""
            openapi: 3.0.0
            info:
              title: Test
              version: 1.0.0
            x-authorize-roles: [reader]
            paths:
              /orders:
                {{securityExtension}}
                x-ratelimit-policy: orders
                x-ratelimit-permit-limit: 10
                x-ratelimit-partition: user
                get:
                  operationId: listOrders
                  responses:
                    '200':
                      description: OK
            """);

        // Act
        var result = WebApplicationExtensionsExtractor.ExtractUnified(document, ProjectName, new ServerConfig());

        // Assert
        var authentication = result.IndexOf("app.UseAuthentication();", StringComparison.Ordinal);
        var rateLimiter = result.IndexOf("app.UseRateLimiter();", StringComparison.Ordinal);
        Assert.Contains("app.UseAuthorization();", result, StringComparison.Ordinal);
        Assert.True(authentication >= 0 && rateLimiter >= 0, result);
        Assert.True(authentication < rateLimiter, result);
    }

    [Fact]
    public void ExtractUnified_NoSecurityAtAll_HasNoAuthentication()
    {
        // Arrange
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.0.0
            info:
              title: Test
              version: 1.0.0
            paths:
              /orders:
                get:
                  operationId: listOrders
                  responses:
                    '200':
                      description: OK
            """);

        // Act
        var result = WebApplicationExtensionsExtractor.ExtractUnified(document, ProjectName, new ServerConfig());

        // Assert
        Assert.DoesNotContain("app.UseAuthentication();", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// The method is named after the API, not after a trailing <c>Contracts</c> / <c>Api</c> segment - or as the
    /// marker's <c>apiName</c> says.
    /// </summary>
    [Theory]
    [InlineData("Contoso.IoT.Nexus.Api.Contracts", null, "MapNexusApi(")]
    [InlineData("Contoso.IoT.Nexus.Api.Contracts", "Fleet", "MapFleetApi(")]
    [InlineData("Showcase", null, "MapShowcaseApi(")]
    public void ExtractUnified_TheMethodIsNamedAfterTheApi(
        string projectName,
        string? apiName,
        string expected)
    {
        // Arrange
        var document = OpenApiDocumentHelper.ParseYaml(SecuredAndLimitedYaml.Replace("{partition}", "ip", StringComparison.Ordinal));

        // Act
        var result = WebApplicationExtensionsExtractor.ExtractUnified(document, projectName, new ServerConfig { ApiName = apiName });

        // Assert
        Assert.Contains($"public static WebApplication {expected}", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// A host that orders its own pipeline already has an error handler: it turns the generated one off.
    /// </summary>
    [Fact]
    public void ExtractUnified_TheGlobalErrorHandler_IsBehindItsOption()
    {
        // Arrange
        var document = OpenApiDocumentHelper.ParseYaml(SecuredAndLimitedYaml.Replace("{partition}", "ip", StringComparison.Ordinal));

        // Act
        var result = WebApplicationExtensionsExtractor.ExtractUnified(document, ProjectName, new ServerConfig());

        // Assert
        var normalized = result.Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.Contains(
            "        if (options.UseGlobalErrorHandler)\n        {\n            app.UseGlobalErrorHandler(options.ConfigureErrorHandling);\n        }\n",
            normalized,
            StringComparison.Ordinal);
    }
}