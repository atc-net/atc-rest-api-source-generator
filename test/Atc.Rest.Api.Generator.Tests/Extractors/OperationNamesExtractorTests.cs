namespace Atc.Rest.Api.Generator.Tests.Extractors;

public class OperationNamesExtractorTests
{
    private const string Yaml = """
        openapi: 3.1.0
        info:
          title: Contoso
          version: 1.0.0
        paths:
          /orders:
            get:
              operationId: listOrders
              summary: List the orders & their lines
              responses:
                '200':
                  description: OK
            post:
              operationId: create-order
              responses:
                '201':
                  description: Created
          /orders/{orderId}:
            parameters:
              - name: orderId
                in: path
                required: true
                schema:
                  type: string
            delete:
              operationId: cancelOrder
              deprecated: true
              responses:
                '204':
                  description: No Content
            put:
              responses:
                '204':
                  description: No Content
        webhooks:
          orderShipped:
            post:
              operationId: onOrderShipped
              responses:
                '200':
                  description: OK
        """;

    [Fact]
    public void Extract_EachOperation_IsAConstantHoldingItsEndpointName()
    {
        // Act
        var content = OperationNamesExtractor.Extract(OpenApiDocumentHelper.ParseYaml(Yaml), "Contoso.Orders", includeDeprecated: false, includeWebhooks: false);

        // Assert
        Assert.NotNull(content);
        Assert.Contains("namespace Contoso.Orders.Generated;", content, StringComparison.Ordinal);
        Assert.Contains("public static class ApiOperations", content, StringComparison.Ordinal);
        Assert.Contains("public const string ListOrders = \"ListOrders\";", content, StringComparison.Ordinal);
        Assert.Contains("public const string CreateOrder = \"CreateOrder\";", content, StringComparison.Ordinal);
        Assert.Contains("/// <c>GET /orders</c>: List the orders &amp; their lines", content, StringComparison.Ordinal);
        Assert.DoesNotContain("CancelOrder", content, StringComparison.Ordinal);
        Assert.DoesNotContain("Webhooks", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_AnOperationWithoutAnOperationId_GetsTheNameItsEndpointHas()
    {
        // Arrange
        var document = OpenApiDocumentHelper.ParseYaml(Yaml);
        var (path, method, put) = document.GetAllOperations().Single(o => o.Method == "PUT");
        var expected = OperationIdHelper.Effective(put, method, path).ToPascalCaseForDotNet();

        // Act
        var content = OperationNamesExtractor.Extract(document, "Contoso.Orders", includeDeprecated: false, includeWebhooks: false);

        // Assert
        Assert.Contains($"public const string {expected} = \"{expected}\";", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_IncludeDeprecated_ListsTheDeprecatedOperationToo()
    {
        // Act
        var content = OperationNamesExtractor.Extract(OpenApiDocumentHelper.ParseYaml(Yaml), "Contoso.Orders", includeDeprecated: true, includeWebhooks: false);

        // Assert
        Assert.Contains("public const string CancelOrder = \"CancelOrder\";", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_Webhooks_AreANestedClass_HoldingTheNameTheirEndpointHas()
    {
        // Act
        var content = OperationNamesExtractor.Extract(OpenApiDocumentHelper.ParseYaml(Yaml), "Contoso.Orders", includeDeprecated: false, includeWebhooks: true);

        // Assert
        Assert.Contains("public static class Webhooks", content, StringComparison.Ordinal);
        Assert.Contains("public const string OnOrderShipped = \"onOrderShipped\";", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_AnOperationNamedLikeTheClass_IsRenamed_ItsValueKept()
    {
        // A member may not have its class's name (CS0542)
        const string yaml = """
            openapi: 3.1.0
            info:
              title: Contoso
              version: 1.0.0
            paths:
              /a:
                get:
                  operationId: apiOperations
                  responses:
                    '200':
                      description: OK
              /b:
                get:
                  operationId: webhooks
                  responses:
                    '200':
                      description: OK
            """;

        // Act
        var content = OperationNamesExtractor.Extract(OpenApiDocumentHelper.ParseYaml(yaml), "Contoso.Orders", includeDeprecated: false, includeWebhooks: false);

        // Assert
        Assert.Contains("public const string ApiOperationsOperation = \"ApiOperations\";", content, StringComparison.Ordinal);
        Assert.Contains("public const string Webhooks = \"Webhooks\";", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_NoOperations_IsNull()
    {
        const string yaml = """
            openapi: 3.1.0
            info:
              title: Contoso
              version: 1.0.0
            paths: {}
            """;

        Assert.Null(OperationNamesExtractor.Extract(OpenApiDocumentHelper.ParseYaml(yaml), "Contoso.Orders", includeDeprecated: false, includeWebhooks: true));
    }
}