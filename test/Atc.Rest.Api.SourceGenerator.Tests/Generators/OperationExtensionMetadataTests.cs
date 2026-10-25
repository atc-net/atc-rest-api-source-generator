namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// An operation's own <c>x-</c> extensions reach its endpoint as <c>ApiOperationExtension</c> metadata, which a
/// middleware reads; the generator's own extensions (<c>x-retry-enabled</c>) do not.
/// </summary>
public sealed class OperationExtensionMetadataTests
{
    private const string Yaml = """
        openapi: 3.1.0
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /orders:
            get:
              operationId: listOrders
              x-audit: false
              x-owner:
                team: billing
              x-retry-enabled: false
              responses:
                '204':
                  description: No Content
            post:
              operationId: createOrder
              responses:
                '204':
                  description: No Content
        """;

    private const string HandlerSource = """"
        using Microsoft.AspNetCore.Builder;
        using Microsoft.AspNetCore.Http;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public sealed class ListOrdersHandlerImpl : IListOrdersHandler
        {
            public System.Threading.Tasks.Task<ListOrdersResult> ExecuteAsync(System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(ListOrdersResult.NoContent());
        }

        public sealed class CreateOrderHandlerImpl : ICreateOrderHandler
        {
            public System.Threading.Tasks.Task<CreateOrderResult> ExecuteAsync(System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(CreateOrderResult.NoContent());
        }

        public static class OrdersRegistration
        {
            public static void Register(IServiceCollection services)
            {
                services.AddScoped<IListOrdersHandler, ListOrdersHandlerImpl>();
                services.AddScoped<ICreateOrderHandler, CreateOrderHandlerImpl>();
            }
        }

        // Answers with the extensions of the endpoint the request was routed to, instead of running it
        public static class ExtensionProbe
        {
            public static void Use(WebApplication app)
                => app.Use(async (HttpContext context, RequestDelegate next) =>
                {
                    var endpoint = context.GetEndpoint();
                    var all = endpoint?.Metadata.GetOrderedMetadata<ErrorBodies.Generated.ApiOperationExtension>() ?? [];
                    await context.Response.WriteAsync(
                        $"{ErrorBodies.Generated.ApiOperationExtension.Find(endpoint, "x-audit") ?? "-"}|" +
                        $"{ErrorBodies.Generated.ApiOperationExtension.Find(endpoint, "x-owner") ?? "-"}|" +
                        $"{ErrorBodies.Generated.ApiOperationExtension.Find(endpoint, "x-retry-enabled") ?? "-"}|" +
                        $"{all.Count}");
                });
        }
        """";

    [Theory]
    [InlineData("GET", """false|{"team":"billing"}|-|2""")]
    [InlineData("POST", "-|-|-|0")]
    public async Task AMiddleware_ReadsTheOperationsOwnExtensions(
        string method,
        string expected)
    {
        // Arrange
        await using var host = await XmlTestHost.StartAsync(
            Yaml,
            HandlerSource,
            "ErrorBodies.RuntimeTest.OrdersRegistration",
            configureWithAssembly: (app, assembly) => assembly.GetType("ErrorBodies.RuntimeTest.ExtensionProbe", throwOnError: true)!.GetMethod("Use")!.Invoke(null, [app]));

        // Act
        var (_, _, body) = await host.SendRawAsync(new HttpMethod(method), "/orders", accept: null);

        // Assert
        Assert.Equal(expected, body);
    }

    [Fact]
    public async Task TheMetadata_KeepsTheGeneratedCodeTrimAndAotSafe()
    {
        // Arrange
        var server = ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", "{ }", includeMinimalApi: false, Yaml);

        // Act
        var problems = await AotAnalyzerHarness.AnalyzeAsync(server, includeMinimalApi: false);

        // Assert
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }
}