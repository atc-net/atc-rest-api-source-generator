namespace Atc.Rest.Api.Generator.Tests.Extractors;

public class OperationExtensionMetadataExtractorTests
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
              x-audit: false
              x-owner:
                team: billing
              x-tags: [a, b]
              x-weight: 3
              x-label: Orders
              x-ratelimit-enabled: false
              x-retry-enabled: false
              x-atc-suppress:
                - rule: ATC_API_OPR001
                  reason: legacy
              responses:
                '200':
                  description: OK
            post:
              operationId: createOrder
              responses:
                '201':
                  description: Created
        """;

    [Fact]
    public void Collect_TheExtensionsTheGeneratorDoesNotRead_InNameOrder_AsText()
    {
        // Arrange
        var operation = OpenApiDocumentHelper.ParseYaml(Yaml).GetAllOperations().Single(o => o.Method == "GET").Operation;

        // Act
        var extensions = OperationExtensionMetadataExtractor.Collect(operation);

        // Assert
        Assert.Equal(
            [
                ("x-audit", "false"),
                ("x-label", "Orders"),
                ("x-owner", """{"team":"billing"}"""),
                ("x-tags", """["a","b"]"""),
                ("x-weight", "3"),
            ],
            extensions);
    }

    [Fact]
    public void Collect_AnOperationWithoutExtensions_IsEmpty()
    {
        var operation = OpenApiDocumentHelper.ParseYaml(Yaml).GetAllOperations().Single(o => o.Method == "POST").Operation;

        Assert.Empty(OperationExtensionMetadataExtractor.Collect(operation));
    }

    [Theory]
    [InlineData("x-retry-max-attempts", true)]
    [InlineData("x-ratelimit-policy", true)]
    [InlineData("x-cache-tags", true)]
    [InlineData("x-authorize-roles", true)]
    [InlineData("x-authentication-required", true)]
    [InlineData("x-atc-suppress", true)]
    [InlineData("x-return-async-enumerable", true)]
    [InlineData("x-sse-item-events", true)]
    [InlineData("x-pagination", true)]
    [InlineData("x-multipart", true)]
    [InlineData("x-audit", false)]
    [InlineData("x-cached", false)]
    public void IsReadByTheGenerator_TheGeneratorsOwnExtensions(
        string name,
        bool expected)
        => Assert.Equal(expected, OperationExtensionMetadataExtractor.IsReadByTheGenerator(name));

    [Fact]
    public void Metadata_WritesOneWithMetadataCallPerExtension()
    {
        // Arrange
        var operation = OpenApiDocumentHelper.ParseYaml(Yaml).GetAllOperations().Single(o => o.Method == "GET").Operation;

        // Act
        var calls = OperationExtensionMetadataExtractor.MetadataCalls(operation, "Contoso");

        // Assert
        Assert.Equal(5, calls.Count);
        Assert.Equal(""".WithMetadata(new global::Contoso.Generated.ApiOperationExtension("x-audit", "false"))""", calls[0]);
        Assert.Equal(""".WithMetadata(new global::Contoso.Generated.ApiOperationExtension("x-owner", "{\"team\":\"billing\"}"))""", calls[2]);
    }

    [Fact]
    public void IsNeeded_OnlyWhenAnOperationHasOne()
    {
        const string none = """
            openapi: 3.1.0
            info:
              title: Contoso
              version: 1.0.0
            paths:
              /orders:
                get:
                  operationId: listOrders
                  x-retry-enabled: false
                  responses:
                    '200':
                      description: OK
            """;

        Assert.True(OperationExtensionMetadataExtractor.IsNeeded(OpenApiDocumentHelper.ParseYaml(Yaml), includeDeprecated: false));
        Assert.False(OperationExtensionMetadataExtractor.IsNeeded(OpenApiDocumentHelper.ParseYaml(none), includeDeprecated: false));
    }
}