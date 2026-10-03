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
}