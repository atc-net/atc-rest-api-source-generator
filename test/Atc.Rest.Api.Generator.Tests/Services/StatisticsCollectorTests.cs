namespace Atc.Rest.Api.Generator.Tests.Services;

public class StatisticsCollectorTests
{
    // ========== CollectFromOpenApiDocument Tests ==========
    [Fact]
    public void CollectFromOpenApiDocument_EmptyDocument_ReturnsZeroCounts()
    {
        var doc = new OpenApiDocument();

        var result = StatisticsCollector.CollectFromOpenApiDocument(
            doc, "test.yaml", "Server", []);

        Assert.Equal(0, result.OperationsCount);
        Assert.Equal(0, result.ModelsCount);
        Assert.Equal(0, result.EnumsCount);
        Assert.Equal(0, result.WebhooksCount);
    }

    [Fact]
    public void CollectFromOpenApiDocument_SetsSpecificationName()
    {
        var doc = new OpenApiDocument();

        var result = StatisticsCollector.CollectFromOpenApiDocument(
            doc, "petstore.yaml", "Server", []);

        Assert.Equal("petstore.yaml", result.SpecificationName);
    }

    [Fact]
    public void CollectFromOpenApiDocument_SetsGeneratorType()
    {
        var doc = new OpenApiDocument();

        var result = StatisticsCollector.CollectFromOpenApiDocument(
            doc, "test.yaml", "Client", []);

        Assert.Equal("Client", result.GeneratorType);
    }

    [Fact]
    public void CollectFromOpenApiDocument_WithApiInfo_SetsVersionAndTitle()
    {
        var doc = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "Pet Store", Version = "1.0.0" },
        };

        var result = StatisticsCollector.CollectFromOpenApiDocument(
            doc, "test.yaml", "Server", []);

        Assert.Equal("1.0.0", result.SpecificationVersion);
        Assert.Equal("Pet Store", result.ApiTitle);
    }

    [Fact]
    public void CollectFromOpenApiDocument_WithPaths_CountsOperations()
    {
        var doc = CreateDocWithPaths("/pets", "/users");

        var result = StatisticsCollector.CollectFromOpenApiDocument(
            doc, "test.yaml", "Server", []);

        Assert.Equal(2, result.OperationsCount);
    }

    [Fact]
    public void CollectFromOpenApiDocument_WithDiagnostics_CountsErrorsAndWarnings()
    {
        var doc = new OpenApiDocument();
        var diagnostics = new List<DiagnosticMessage>
        {
            new("ATC_001", "Error message", DiagnosticSeverity.Error),
            new("ATC_002", "Warning message", DiagnosticSeverity.Warning),
            new("ATC_003", "Another warning", DiagnosticSeverity.Warning),
        };

        var result = StatisticsCollector.CollectFromOpenApiDocument(
            doc, "test.yaml", "Server", diagnostics);

        Assert.Equal(1, result.ErrorCount);
        Assert.Equal(2, result.WarningCount);
    }

    [Fact]
    public void CollectFromOpenApiDocument_WithDiagnostics_CollectsDistinctRuleIds()
    {
        var doc = new OpenApiDocument();
        var diagnostics = new List<DiagnosticMessage>
        {
            new("ATC_001", "Error 1", DiagnosticSeverity.Error),
            new("ATC_001", "Error 2", DiagnosticSeverity.Error),
            new("ATC_002", "Warning 1", DiagnosticSeverity.Warning),
        };

        var result = StatisticsCollector.CollectFromOpenApiDocument(
            doc, "test.yaml", "Server", diagnostics);

        Assert.Single(result.ErrorRuleIds);
        Assert.Single(result.WarningRuleIds);
    }

    [Fact]
    public void CollectFromOpenApiDocument_ClientType_SetsClientMethodsCount()
    {
        var doc = CreateDocWithPaths("/pets", "/users");

        var result = StatisticsCollector.CollectFromOpenApiDocument(
            doc, "test.yaml", "Client", []);

        Assert.Equal(2, result.ClientMethodsCount);
    }

    [Fact]
    public void CollectFromOpenApiDocument_ServerType_ZeroClientMethods()
    {
        var doc = CreateDocWithPaths("/pets");

        var result = StatisticsCollector.CollectFromOpenApiDocument(
            doc, "test.yaml", "Server", []);

        Assert.Equal(0, result.ClientMethodsCount);
    }

    // ========== CollectFromGeneratedTypes Tests ==========
    [Fact]
    public void CollectFromGeneratedTypes_GroupsByCategory()
    {
        var types = new List<GeneratedType>
        {
            new("Pet", "Models", "MyApi.Models", "content", []),
            new("PetEnum", "Enums", "MyApi.Models", "content", []),
            new("GetPetsParameters", "Parameters", "MyApi.Parameters", "content", []),
            new("GetPetsHandler", "Handlers", "MyApi.Handlers", "content", []),
        };
        var doc = new OpenApiDocument();

        var result = StatisticsCollector.CollectFromGeneratedTypes(
            types, doc, "test.yaml", "Server", [], TimeSpan.Zero);

        Assert.Equal(1, result.ModelsCount);
        Assert.Equal(1, result.EnumsCount);
        Assert.Equal(1, result.ParametersCount);
        Assert.Equal(1, result.HandlersCount);
    }

    [Fact]
    public void CollectFromGeneratedTypes_EmptyTypes_ReturnsZeroCounts()
    {
        var doc = new OpenApiDocument();

        var result = StatisticsCollector.CollectFromGeneratedTypes(
            [], doc, "test.yaml", "Server", [], TimeSpan.FromSeconds(1));

        Assert.Equal(0, result.ModelsCount);
        Assert.Equal(0, result.TotalTypesGenerated);
        Assert.Equal(TimeSpan.FromSeconds(1), result.Duration);
    }

    // ========== CountPathSegments Tests (via CollectFromOpenApiDocument) ==========
    [Fact]
    public void CollectFromOpenApiDocument_CountsDistinctPathSegments()
    {
        var doc = CreateDocWithPaths("/pets", "/pets/{petId}", "/users");

        var result = StatisticsCollector.CollectFromOpenApiDocument(
            doc, "test.yaml", "Server", []);

        // /pets and /pets/{petId} share "Pets" segment, /users is "Users" => 2 segments
        Assert.Equal(2, result.EndpointsCount);
    }

    [Fact]
    public void CollectFromOpenApiDocument_SinglePath_MinimumOneEndpoint()
    {
        var doc = CreateDocWithPaths("/pets");

        var result = StatisticsCollector.CollectFromOpenApiDocument(
            doc, "test.yaml", "Server", []);

        Assert.True(result.EndpointsCount >= 1);
    }

    // ========== Helper Methods ==========
    private static OpenApiDocument CreateDocWithPaths(params string[] paths)
    {
        var doc = new OpenApiDocument
        {
            Paths = new OpenApiPaths(),
        };

        foreach (var path in paths)
        {
            var pathItem = new OpenApiPathItem
            {
                Operations = new Dictionary<HttpMethod, OpenApiOperation>
                {
                    [HttpMethod.Get] = new OpenApiOperation
                    {
                        OperationId = $"get{path.TrimStart('/').Replace("/", string.Empty, StringComparison.Ordinal)}",
                    },
                },
            };
            doc.Paths.Add(path, pathItem);
        }

        return doc;
    }
}