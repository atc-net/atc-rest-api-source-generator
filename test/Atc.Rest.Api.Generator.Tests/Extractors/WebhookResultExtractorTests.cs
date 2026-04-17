namespace Atc.Rest.Api.Generator.Tests.Extractors;

public class WebhookResultExtractorTests
{
    [Theory]
    [InlineData("200", "TypedResults.Ok()")]
    [InlineData("201", "TypedResults.StatusCode(201)")]
    [InlineData("202", "TypedResults.StatusCode(202)")]
    [InlineData("204", "TypedResults.NoContent()")]
    [InlineData("400", "TypedResults.BadRequest()")]
    [InlineData("401", "TypedResults.Unauthorized()")]
    [InlineData("403", "TypedResults.StatusCode(403)")]
    [InlineData("404", "TypedResults.NotFound()")]
    [InlineData("409", "TypedResults.Conflict()")]
    [InlineData("500", "TypedResults.StatusCode(500)")]
    [InlineData("418", "TypedResults.StatusCode(418)")]
    public void Extract_EmitsExpectedTypedResultExpression_ForStatusCode(
        string statusCode,
        string expectedExpression)
    {
        var yaml = $$"""
            openapi: 3.1.1
            info:
              title: Test API
              version: 1.0.0
            paths: {}
            webhooks:
              notify:
                post:
                  operationId: onNotify
                  responses:
                    '{{statusCode}}':
                      description: Response for {{statusCode}}
            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        var result = WebhookResultExtractor.Extract(document, "TestApi");

        Assert.NotNull(result);
        var (_, content) = Assert.Single(result);
        Assert.Contains($"=> new({expectedExpression});", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_DefaultStatusCode_MapsToStatusCode500()
    {
        var yaml = """
            openapi: 3.1.1
            info:
              title: Test API
              version: 1.0.0
            paths: {}
            webhooks:
              notify:
                post:
                  operationId: onNotify
                  responses:
                    'default':
                      description: Default fallback
            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        var result = WebhookResultExtractor.Extract(document, "TestApi");

        Assert.NotNull(result);
        var (_, content) = Assert.Single(result);
        Assert.Contains("=> new(TypedResults.StatusCode(500));", content, StringComparison.Ordinal);
    }
}