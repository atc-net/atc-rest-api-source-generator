namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// <c>ApiOperations</c>: one constant per operation holding the name of its endpoint, generated into the server and both
/// C# clients. The server names its endpoints with the constants, so the two cannot drift apart.
/// </summary>
public sealed class OperationNamesTests
{
    private const string Yaml = """
        openapi: 3.1.0
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
                  content:
                    application/json:
                      schema:
                        type: array
                        items:
                          type: string
          /orders/{orderId}:
            delete:
              operationId: cancel-order
              parameters:
                - name: orderId
                  in: path
                  required: true
                  schema:
                    type: string
              responses:
                '204':
                  description: No Content
        webhooks:
          orderShipped:
            post:
              operationId: onOrderShipped
              requestBody:
                content:
                  application/json:
                    schema:
                      type: string
              responses:
                '200':
                  description: OK
        """;

    [Fact]
    public void Server_NamesEachEndpointWithItsConstant()
    {
        // Arrange
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", "{ }", includeMinimalApi: false, Yaml);

        // Act
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);
        var operations = assembly.GetTypes().Single(t => t.Name == "ApiOperations");
        var webhooks = operations.GetNestedType("Webhooks")!;
        var endpoints = string.Join(Environment.NewLine, sources.Select(s => s.Source));

        // Assert
        Assert.Equal("ListOrders", Constant(operations, "ListOrders"));
        Assert.Equal("CancelOrder", Constant(operations, "CancelOrder"));
        Assert.Equal("onOrderShipped", Constant(webhooks, "OnOrderShipped"));
        Assert.Contains(".WithName(global::Orders.Generated.ApiOperations.ListOrders)", endpoints, StringComparison.Ordinal);
        Assert.Contains(".WithName(global::Orders.Generated.ApiOperations.Webhooks.OnOrderShipped)", endpoints, StringComparison.Ordinal);
        Assert.DoesNotContain(".WithName(\"", endpoints, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("TypedClient")]
    [InlineData("EndpointPerOperation")]
    public void Client_HasTheSameConstants_WithoutWebhooks(string mode)
    {
        // Arrange
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", $$"""{ "generationMode": "{{mode}}" }""", includeMinimalApi: false, Yaml);

        // Act
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);
        var operations = assembly.GetTypes().Single(t => t.Name == "ApiOperations");

        // Assert
        Assert.Equal("ListOrders", Constant(operations, "ListOrders"));
        Assert.Equal("CancelOrder", Constant(operations, "CancelOrder"));
        Assert.Null(operations.GetNestedType("Webhooks"));
    }

    private static string? Constant(
        Type type,
        string name)
        => (string?)type.GetField(name, BindingFlags.Public | BindingFlags.Static)?.GetRawConstantValue();
}