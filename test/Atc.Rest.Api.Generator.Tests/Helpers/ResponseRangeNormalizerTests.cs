namespace Atc.Rest.Api.Generator.Tests.Helpers;

/// <summary>
/// Before any generator reads an operation's responses, a status range (<c>2XX</c>) is rewritten to its first status
/// (<c>200</c>) - or dropped when the operation declares that status itself - so every generator sees statuses only
/// (the validator has warned, <c>ATC_API_OPR034</c>).
/// </summary>
public class ResponseRangeNormalizerTests
{
    [Theory]
    [InlineData("2XX", "200")]
    [InlineData("4XX", "400")]
    [InlineData("5XX", "500")]
    [InlineData("3xx", "300")]
    public void WithExplicitStatuses_ARange_BecomesItsFirstStatus(
        string range,
        string status)
    {
        var document = Parse($"'{range}': {{ description: Ranged }}");

        ResponseRangeNormalizer.WithExplicitStatuses(document);

        var responses = document.Paths["/things"].Operations![HttpMethod.Get].Responses!;
        Assert.False(responses.ContainsKey(range));
        Assert.Equal("Ranged", responses[status].Description);
    }

    [Fact]
    public void WithExplicitStatuses_ARangeNextToItsFirstStatus_IsDropped()
    {
        var document = Parse("'200': { description: OK }\n        '2XX': { description: Ranged }");

        ResponseRangeNormalizer.WithExplicitStatuses(document);

        var responses = document.Paths["/things"].Operations![HttpMethod.Get].Responses!;
        var only = Assert.Single(responses);
        Assert.Equal("200", only.Key);
        Assert.Equal("OK", only.Value.Description);
    }

    [Fact]
    public void WithExplicitStatuses_AWebhooksRange_BecomesItsFirstStatus()
    {
        var yaml = """
            openapi: 3.1.0
            info: { title: Contoso, version: 1.0.0 }
            paths: {}
            webhooks:
              thingCreated:
                post:
                  operationId: onThingCreated
                  responses:
                    '2XX': { description: Received }
            """;
        var (document, _) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(yaml, "hooks.yaml");
        Assert.NotNull(document);

        ResponseRangeNormalizer.WithExplicitStatuses(document);

        var responses = document.Webhooks!["thingCreated"].Operations![HttpMethod.Post].Responses!;
        Assert.Equal("Received", Assert.Single(responses, r => r.Key == "200").Value.Description);
    }

    private static OpenApiDocument Parse(string responses)
    {
        var yaml = $$"""
            openapi: 3.0.3
            info: { title: Contoso, version: 1.0.0 }
            paths:
              /things:
                get:
                  operationId: listThings
                  responses:
                    {{responses}}
            """;
        var (document, _) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(yaml, "ranges.yaml");
        Assert.NotNull(document);
        return document;
    }
}