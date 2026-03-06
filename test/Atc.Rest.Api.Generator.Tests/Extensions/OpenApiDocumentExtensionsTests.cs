namespace Atc.Rest.Api.Generator.Tests.Extensions;

public class OpenApiDocumentExtensionsTests
{
    // ========== GetAllOperations Tests ==========
    [Fact]
    public void GetAllOperations_NoPaths_ReturnsEmpty()
    {
        var doc = new OpenApiDocument();

        var result = doc.GetAllOperations().ToList();

        Assert.Empty(result);
    }

    [Fact]
    public void GetAllOperations_WithPaths_ReturnsAllOperations()
    {
        var doc = CreateDocWithOperations(("/pets", HttpMethod.Get), ("/pets", HttpMethod.Post));

        var result = doc.GetAllOperations().ToList();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void GetAllOperations_MethodIsUpperCase()
    {
        var doc = CreateDocWithOperations(("/pets", HttpMethod.Get));

        var result = doc.GetAllOperations().First();

        Assert.Equal("GET", result.Method);
    }

    [Fact]
    public void GetAllOperations_IncludesPath()
    {
        var doc = CreateDocWithOperations(("/pets/{petId}", HttpMethod.Get));

        var result = doc.GetAllOperations().First();

        Assert.Equal("/pets/{petId}", result.Path);
    }

    // ========== HasWebhooks Tests ==========
    [Fact]
    public void HasWebhooks_NoWebhooks_ReturnsFalse()
    {
        var doc = new OpenApiDocument();

        Assert.False(doc.HasWebhooks());
    }

    [Fact]
    public void HasWebhooks_EmptyWebhooks_ReturnsFalse()
    {
        var doc = new OpenApiDocument
        {
            Webhooks = new Dictionary<string, IOpenApiPathItem>(StringComparer.Ordinal),
        };

        Assert.False(doc.HasWebhooks());
    }

    [Fact]
    public void HasWebhooks_WithWebhooks_ReturnsTrue()
    {
        var doc = CreateDocWithWebhook("petCreated");

        Assert.True(doc.HasWebhooks());
    }

    // ========== GetWebhooksCount Tests ==========
    [Fact]
    public void GetWebhooksCount_NoWebhooks_ReturnsZero()
    {
        var doc = new OpenApiDocument();

        Assert.Equal(0, doc.GetWebhooksCount());
    }

    [Fact]
    public void GetWebhooksCount_WithWebhooks_ReturnsCount()
    {
        var doc = CreateDocWithWebhook("petCreated");

        Assert.Equal(1, doc.GetWebhooksCount());
    }

    // ========== GetAllWebhookOperations Tests ==========
    [Fact]
    public void GetAllWebhookOperations_NoWebhooks_ReturnsEmpty()
    {
        var doc = new OpenApiDocument();

        var result = doc.GetAllWebhookOperations().ToList();

        Assert.Empty(result);
    }

    [Fact]
    public void GetAllWebhookOperations_WithWebhooks_ReturnsOperations()
    {
        var doc = CreateDocWithWebhook("petCreated");

        var result = doc.GetAllWebhookOperations().ToList();

        Assert.Single(result);
        Assert.Equal("petCreated", result[0].WebhookName);
        Assert.Equal("POST", result[0].Method);
    }

    // ========== Helper Methods ==========
    private static OpenApiDocument CreateDocWithOperations(
        params (string Path, HttpMethod Method)[] operations)
    {
        var doc = new OpenApiDocument { Paths = new OpenApiPaths() };

        foreach (var group in operations.GroupBy(
            o => o.Path,
            StringComparer.Ordinal))
        {
            var pathItem = new OpenApiPathItem
            {
                Operations = group.ToDictionary(
                    o => o.Method,
                    o => new OpenApiOperation { OperationId = $"{o.Method}{o.Path}" }),
            };
            doc.Paths.Add(group.Key, pathItem);
        }

        return doc;
    }

    private static OpenApiDocument CreateDocWithWebhook(string name)
    {
        var doc = new OpenApiDocument
        {
            Webhooks = new Dictionary<string, IOpenApiPathItem>(StringComparer.Ordinal),
        };

        doc.Webhooks[name] = new OpenApiPathItem
        {
            Operations = new Dictionary<HttpMethod, OpenApiOperation>
            {
                [HttpMethod.Post] = new OpenApiOperation { OperationId = name },
            },
        };

        return doc;
    }
}