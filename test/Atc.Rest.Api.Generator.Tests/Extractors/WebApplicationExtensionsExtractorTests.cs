namespace Atc.Rest.Api.Generator.Tests.Extractors;

/// <summary>
/// Tests for the generated host wiring: <c>Use{Api}Api()</c> adds the middleware (in the order these tests pin),
/// <c>Map{Api}Api()</c> maps the endpoints and nothing else.
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
    /// The methods are named after the API, not after a trailing <c>Contracts</c> / <c>Api</c> segment - or as the
    /// marker's <c>apiName</c> says. <c>Map{Api}Endpoints()</c> would clash with a path segment's own
    /// <c>Map{Segment}Endpoints()</c> (an API <c>Pets</c> with a <c>/pets</c> path), so the endpoints go through
    /// <c>Map{Api}Api()</c>.
    /// </summary>
    [Theory]
    [InlineData("Contoso.IoT.Nexus.Api.Contracts", null, "Nexus")]
    [InlineData("Contoso.IoT.Nexus.Api.Contracts", "Fleet", "Fleet")]
    [InlineData("Showcase", null, "Showcase")]
    public void ExtractUnified_TheMethodsAreNamedAfterTheApi(
        string projectName,
        string? apiName,
        string expected)
    {
        // Arrange
        var document = OpenApiDocumentHelper.ParseYaml(SecuredAndLimitedYaml.Replace("{partition}", "ip", StringComparison.Ordinal));

        // Act
        var result = WebApplicationExtensionsExtractor.ExtractUnified(document, projectName, new ServerConfig { ApiName = apiName });

        // Assert
        Assert.Contains($"public static WebApplication Use{expected}Api(", result, StringComparison.Ordinal);
        Assert.Contains($"public static WebApplication Map{expected}Api(", result, StringComparison.Ordinal);
        Assert.DoesNotContain($"Map{expected}Endpoints(", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>Use{Api}Api()</c> adds middleware and maps nothing; <c>Map{Api}Api()</c> maps the endpoints and adds no
    /// middleware - the split ASP.NET's own <c>Use…</c> / <c>Map…</c> methods follow.
    /// </summary>
    [Fact]
    public void ExtractUnified_UseAddsOnlyMiddleware_MapOnlyMaps()
    {
        // Arrange
        var document = OpenApiDocumentHelper.ParseYaml(SecuredAndLimitedYaml.Replace("{partition}", "user", StringComparison.Ordinal));

        // Act
        var result = WebApplicationExtensionsExtractor.ExtractUnified(document, ProjectName, new ServerConfig());

        // Assert
        var use = MethodBody(result, "public static WebApplication UseTestApi(");
        var map = MethodBody(result, "public static WebApplication MapTestApi(");
        Assert.DoesNotContain("MapEndpoints()", use, StringComparison.Ordinal);
        Assert.Contains("app.UseAuthentication();", use, StringComparison.Ordinal);
        Assert.Contains("app.MapEndpoints();", map, StringComparison.Ordinal);
        Assert.DoesNotContain(map.Split('\n'), line => line.TrimStart().StartsWith("app.Use", StringComparison.Ordinal));
    }

    /// <summary>
    /// Until 3.1 <c>Map{Api}Api()</c> also added the middleware. A host that still calls only it would lose its error
    /// handling and status code pages without a compile error, so <c>Use{Api}Api()</c> leaves a mark and
    /// <c>Map{Api}Api()</c> refuses to start without it - unless the host says it orders the pipeline itself
    /// (<c>requireUse: false</c>).
    /// </summary>
    [Fact]
    public void ExtractUnified_MapRequiresUseUnlessTheHostOrdersItsOwnPipeline()
    {
        // Arrange
        var document = OpenApiDocumentHelper.ParseYaml(SecuredAndLimitedYaml.Replace("{partition}", "ip", StringComparison.Ordinal));

        // Act
        var result = WebApplicationExtensionsExtractor.ExtractUnified(document, ProjectName, new ServerConfig());

        // Assert
        var normalized = result.Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.Contains("    public static WebApplication MapTestApi(\n        this WebApplication app,\n        bool requireUse = true)", normalized, StringComparison.Ordinal);
        var use = MethodBody(normalized, "public static WebApplication UseTestApi(");
        var map = MethodBody(normalized, "public static WebApplication MapTestApi(");
        Assert.Contains("Properties[UseApiMark] = true;", use, StringComparison.Ordinal);
        Assert.Contains("if (requireUse && !((global::Microsoft.AspNetCore.Builder.IApplicationBuilder)app).Properties.ContainsKey(UseApiMark))", map, StringComparison.Ordinal);
        Assert.Contains("throw new InvalidOperationException(", map, StringComparison.Ordinal);
        Assert.Contains("app.UseTestApi()", map, StringComparison.Ordinal);
        Assert.Contains("requireUse: false", map, StringComparison.Ordinal);
    }

    /// <summary>
    /// The host's own middleware (CORS, HSTS, HTTPS redirection) goes in through <c>AfterErrorHandling</c>: inside the
    /// error handler and the status code pages, so what it throws or answers empty is a ProblemDetails, and ahead of
    /// the rate limiter, authentication and the output cache, where CORS has to be.
    /// </summary>
    [Fact]
    public void ExtractUnified_TheAfterErrorHandlingHook_RunsAfterErrorHandlingAndBeforeTheRest()
    {
        // Arrange
        var document = OpenApiDocumentHelper.ParseYaml(SecuredAndLimitedYaml.Replace("{partition}", "ip", StringComparison.Ordinal));

        // Act
        var result = WebApplicationExtensionsExtractor.ExtractUnified(document, ProjectName, new ServerConfig());

        // Assert
        var use = MethodBody(result, "public static WebApplication UseTestApi(");
        var hook = use.IndexOf("options.AfterErrorHandling?.Invoke(app);", StringComparison.Ordinal);
        Assert.True(hook >= 0, use);
        foreach (var before in new[] { "app.UseGlobalErrorHandler(", "app.UseStatusCodePages();" })
        {
            Assert.True(use.IndexOf(before, StringComparison.Ordinal) < hook, $"{before} after the hook:\n{use}");
        }

        foreach (var after in new[] { "app.UseRateLimiter();", "app.UseAuthentication();", "app.UseAuthorization();" })
        {
            Assert.True(use.IndexOf(after, StringComparison.Ordinal) > hook, $"{after} before the hook:\n{use}");
        }
    }

    /// <summary>
    /// A middleware catches only what is thrown after it: the error handler comes first, so an exception from
    /// authentication (an unreachable token authority) or the rate limiter is a ProblemDetails too.
    /// </summary>
    [Theory]
    [InlineData(true, "app.UseGlobalErrorHandler(")]
    [InlineData(false, "app.UseExceptionHandler();")]
    public void ExtractUnified_TheErrorHandlerIsTheFirstMiddleware(
        bool useGlobalErrorHandler,
        string errorHandler)
    {
        // Arrange
        var document = OpenApiDocumentHelper.ParseYaml(SecuredAndLimitedYaml.Replace("{partition}", "user", StringComparison.Ordinal));

        // Act
        var result = WebApplicationExtensionsExtractor.ExtractUnified(document, ProjectName, new ServerConfig(), useGlobalErrorHandler);

        // Assert
        var handler = result.IndexOf(errorHandler, StringComparison.Ordinal);
        Assert.True(handler >= 0, result);
        foreach (var middleware in new[] { "app.UseStatusCodePages();", "app.UseAuthentication();", "app.UseRateLimiter();", "app.MapEndpoints();" })
        {
            var index = result.IndexOf(middleware, StringComparison.Ordinal);
            Assert.True(index > handler, $"{middleware} before {errorHandler}:\n{result}");
        }
    }

    /// <summary>
    /// Each middleware <c>Use{Api}Api()</c> adds is behind its own switch, so a host turns off what it adds itself.
    /// There is no all-or-nothing <c>HostOwnsPipeline</c> any more: a host that orders its whole pipeline does not call
    /// <c>Use{Api}Api()</c>.
    /// </summary>
    [Fact]
    public void ExtractUnified_EveryMiddlewareIsBehindItsOwnSwitch()
    {
        // Arrange
        var document = OpenApiDocumentHelper.ParseYaml(SecuredAndLimitedYaml.Replace("{partition}", "user", StringComparison.Ordinal));

        // Act
        var result = WebApplicationExtensionsExtractor.ExtractUnified(document, ProjectName, new ServerConfig());

        // Assert
        var normalized = result.Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.DoesNotContain("HostOwnsPipeline", normalized, StringComparison.Ordinal);
        Assert.DoesNotContain("hostOwnsPipeline", normalized, StringComparison.Ordinal);
        Assert.Contains("if (options.UseGlobalErrorHandler)", normalized, StringComparison.Ordinal);
        Assert.Contains("if (options.UseStatusCodePages &&", normalized, StringComparison.Ordinal);
        Assert.Contains("if (options.UseAuthentication)", normalized, StringComparison.Ordinal);
        Assert.Contains("else if (options.UseAuthorization)", normalized, StringComparison.Ordinal);
        Assert.Contains("if (options.UseRateLimiter)", normalized, StringComparison.Ordinal);
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

    /// <summary>
    /// The XML docs are what Visual Studio shows on mouse-over: lists as real bullets (a <c>/// - </c> line is joined
    /// into one long line), the API by its name (not its namespace), and the companion method as a link.
    /// </summary>
    [Fact]
    public void ExtractUnified_TheToolTips_ListTheMiddlewareAndLinkTheCompanionMethod()
    {
        // Arrange
        var document = OpenApiDocumentHelper.ParseYaml(SecuredAndLimitedYaml.Replace("{partition}", "ip", StringComparison.Ordinal));

        // Act
        var result = WebApplicationExtensionsExtractor.ExtractUnified(document, ProjectName, new ServerConfig());

        // Assert
        var lines = result.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        Assert.DoesNotContain(lines, line => line.TrimStart().StartsWith("/// - ", StringComparison.Ordinal));
        Assert.DoesNotContain("TestApi API", result, StringComparison.Ordinal);
        Assert.Contains("/// Adds the middleware the Test API needs, in the recommended order. Call it before <see cref=\"MapTestApi\"/>.", result, StringComparison.Ordinal);
        Assert.Contains("/// Maps all endpoints of the Test API. Adds no middleware: call <see cref=\"UseTestApi\"/> first.", result, StringComparison.Ordinal);

        // One paragraph per item with a typed bullet: Visual Studio's tooltip renders a <list> as one run-on line
        Assert.DoesNotContain("<list", result, StringComparison.Ordinal);
        foreach (var item in new[] { "Global error handler", "Status code pages", "AfterErrorHandling", "Rate limiter", "Authentication and authorization" })
        {
            Assert.Contains(lines, line => line.Contains("/// <para>• ", StringComparison.Ordinal) && line.Contains(item, StringComparison.Ordinal) && line.TrimEnd().EndsWith("</para>", StringComparison.Ordinal));
        }

        // A paragraph is one <para>…</para>: no text outside it (a tooltip would run it into the next line)
        var normalized = result.Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.Contains("calls <c>MapTestApi(requireUse: false)</c>.\n    /// </para>", normalized, StringComparison.Ordinal);
        Assert.DoesNotContain(lines, line => line.Contains("<para>", StringComparison.Ordinal) && !line.Contains("</para>", StringComparison.Ordinal) && !line.TrimEnd().EndsWith("<para>", StringComparison.Ordinal));
    }

    private static string MethodBody(
        string source,
        string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"{signature} not found:\n{source}");
        var open = source.IndexOf("\n    {", start, StringComparison.Ordinal);
        var close = source.IndexOf("\n    }", open, StringComparison.Ordinal);
        return source[open..close];
    }
}