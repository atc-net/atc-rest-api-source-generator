namespace Atc.Rest.Api.Generator.Tests.Extractors;

/// <summary>
/// The host methods say what they do: <c>Add{Api}ApiServices()</c> registers services,
/// <c>Use{Api}ApiMiddleware()</c> adds middleware, <c>Map{Api}ApiEndpoints()</c> maps endpoints. The earlier names
/// (<c>Add{Api}Api()</c>, <c>Use{Api}Api()</c>, <c>Map{Api}Api()</c>) are not generated: one name per method.
/// </summary>
public class HostMethodNamesTests
{
    private const string ProjectName = "Contoso.Orders.Api.Contracts";

    private const string Yaml = """
                                openapi: 3.0.0
                                info:
                                  title: Orders
                                  version: 1.0.0
                                paths:
                                  /orders:
                                    get:
                                      operationId: listOrders
                                      responses:
                                        '200':
                                          description: OK
                                """;

    [Fact]
    public void Extract_AddApiServices_IsTheMethod_AndAddApiIsGone()
    {
        // Arrange
        var document = OpenApiDocumentHelper.ParseYaml(Yaml);

        // Act
        var result = Normalize(UnifiedServiceCollectionExtractor.Extract(document, ProjectName, new ServerConfig()));

        // Assert
        Assert.Contains("public static IServiceCollection AddOrdersApiServices(\n        this IServiceCollection services,\n        Action<ApiServiceOptions>? configure = null)\n    {", result, StringComparison.Ordinal);
        Assert.DoesNotContain("AddOrdersApi(", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_AddApiServices_SaysWhatItRegisters_AndWhatNot()
    {
        // Arrange
        var document = OpenApiDocumentHelper.ParseYaml(Yaml);

        // Act
        var result = Normalize(UnifiedServiceCollectionExtractor.Extract(document, ProjectName, new ServerConfig()));

        // Assert
        Assert.Contains("/// Registers the services the generated endpoints of the Orders API rely on, chosen from the specification and\n    /// the marker file. It registers no handler and maps no endpoint.", result, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractUnified_UseApiMiddleware_IsTheMethod_AndUseApiIsGone()
    {
        // Arrange
        var document = OpenApiDocumentHelper.ParseYaml(Yaml);

        // Act
        var result = Normalize(WebApplicationExtensionsExtractor.ExtractUnified(document, ProjectName, new ServerConfig()));

        // Assert
        Assert.Contains("public static WebApplication UseOrdersApiMiddleware(\n        this WebApplication app,\n        Action<ApiMiddlewareOptions>? configure = null)\n    {", result, StringComparison.Ordinal);
        Assert.DoesNotContain("UseOrdersApi(", result, StringComparison.Ordinal);
        Assert.Contains("/// Adds the middleware the generated endpoints of the Orders API rely on, in the order listed below. It maps no\n    /// endpoint: call <see cref=\"MapOrdersApiEndpoints(WebApplication)\"/> after it.", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>Map{Api}ApiEndpoints</c> is two overloads, not one with an optional parameter: a path segment named like the
    /// API (<c>/orders-api</c> gives <c>MapOrdersApiEndpoints(this WebApplication)</c>) then makes a call with both
    /// namespaces imported ambiguous - a compile error - instead of quietly picking the segment's method, which maps
    /// one segment and skips the check that the middleware was added.
    /// </summary>
    [Fact]
    public void ExtractUnified_MapApiEndpoints_IsTwoOverloads_AndMapApiIsGone()
    {
        // Arrange
        var document = OpenApiDocumentHelper.ParseYaml(Yaml);

        // Act
        var result = Normalize(WebApplicationExtensionsExtractor.ExtractUnified(document, ProjectName, new ServerConfig()));

        // Assert
        Assert.Contains("public static WebApplication MapOrdersApiEndpoints(\n        this WebApplication app,\n        bool requireUse)\n    {", result, StringComparison.Ordinal);
        Assert.Contains("public static WebApplication MapOrdersApiEndpoints(\n        this WebApplication app)\n        => MapOrdersApiEndpoints(app, requireUse: true);", result, StringComparison.Ordinal);
        Assert.DoesNotContain("MapOrdersApi(", result, StringComparison.Ordinal);
        Assert.Contains("/// Maps every endpoint of the Orders API. It adds no middleware: call <see cref=\"UseOrdersApiMiddleware\"/> before\n    /// it.", result, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractUnified_TheStartupCheck_NamesTheNewMethods()
    {
        // Arrange
        var document = OpenApiDocumentHelper.ParseYaml(Yaml);

        // Act
        var result = WebApplicationExtensionsExtractor.ExtractUnified(document, ProjectName, new ServerConfig());

        // Assert
        Assert.Contains("\"Call app.UseOrdersApiMiddleware() before app.MapOrdersApiEndpoints(): it adds the error handling, the status code pages \" +", result, StringComparison.Ordinal);
        Assert.Contains("\"A host that adds that middleware itself calls app.MapOrdersApiEndpoints(requireUse: false).\");", result, StringComparison.Ordinal);
        Assert.DoesNotContain("app.UseOrdersApi()", result, StringComparison.Ordinal);
        Assert.DoesNotContain("app.MapOrdersApi(", result, StringComparison.Ordinal);
    }

    private static string Normalize(string source)
        => source.Replace("\r\n", "\n", StringComparison.Ordinal);
}