namespace Atc.Rest.Api.Generator.Tests.Helpers;

/// <summary>
/// A HEAD answer has no body (RFC 9110 §9.3.2), whatever its operation declares - a specification often gives its
/// HEAD the responses of its GET. Before any generator reads the responses, the content of every response of a HEAD
/// operation is removed, so no generator types a value that is never there (the validator has warned,
/// <c>ATC_API_OPR039</c>). The description and the headers stay; no other operation is touched.
/// </summary>
public class HeadResponseNormalizerTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info: { title: Contoso, version: 1.0.0 }
        paths:
          /things/{id}:
            parameters:
              - { name: id, in: path, required: true, schema: { type: string } }
            get:
              operationId: getThing
              responses:
                '200':
                  description: The thing
                  content:
                    application/json:
                      schema: { $ref: '#/components/schemas/Thing' }
                '404': { $ref: '#/components/responses/NotFound' }
            head:
              operationId: headThing
              responses:
                '200':
                  description: The thing exists
                  headers:
                    ETag: { schema: { type: string } }
                  content:
                    application/json:
                      schema: { $ref: '#/components/schemas/Thing' }
                '404': { $ref: '#/components/responses/NotFound' }
        components:
          responses:
            NotFound:
              description: No such thing
              headers:
                X-Reason: { schema: { type: string } }
              content:
                application/problem+json:
                  schema: { type: object }
          schemas:
            Thing:
              type: object
              properties:
                id: { type: string }
        """;

    [Fact]
    public void WithoutResponseBodies_AHeadResponse_LosesItsContent_AndKeepsItsDescriptionAndHeaders()
    {
        var document = Parse(Yaml);

        HeadResponseNormalizer.WithoutResponseBodies(document);

        var ok = document.Paths["/things/{id}"].Operations![HttpMethod.Head].Responses!["200"];
        Assert.True(ok.Content is null || ok.Content.Count == 0);
        Assert.Equal("The thing exists", ok.Description);
        Assert.True(ok.Headers!.ContainsKey("ETag"));
    }

    [Fact]
    public void WithoutResponseBodies_AHeadResponseThatIsAReference_LosesItsContent_AndTheComponentKeepsIts()
    {
        var document = Parse(Yaml);

        HeadResponseNormalizer.WithoutResponseBodies(document);

        var notFound = document.Paths["/things/{id}"].Operations![HttpMethod.Head].Responses!["404"];
        Assert.True(notFound.Content is null || notFound.Content.Count == 0);
        Assert.Equal("No such thing", notFound.Description);
        Assert.True(notFound.Headers!.ContainsKey("X-Reason"));

        // The GET shares the component: its 404 still has a body
        Assert.True(document.Components!.Responses!["NotFound"].Content!.ContainsKey("application/problem+json"));
        Assert.True(document.Paths["/things/{id}"].Operations![HttpMethod.Get].Responses!["404"].Content!.ContainsKey("application/problem+json"));
    }

    [Fact]
    public void WithoutResponseBodies_OtherOperations_KeepTheirContent()
    {
        var document = Parse(Yaml);

        HeadResponseNormalizer.WithoutResponseBodies(document);

        Assert.True(document.Paths["/things/{id}"].Operations![HttpMethod.Get].Responses!["200"].Content!.ContainsKey("application/json"));
    }

    [Fact]
    public void ResponsesWithContent_NamesTheStatusesOfAHeadOperationThatDeclareContent()
    {
        var document = Parse(Yaml);
        var operations = document.Paths["/things/{id}"].Operations!;

        Assert.Equal(["200", "404"], HeadResponseNormalizer.ResponsesWithContent(HttpMethod.Head, operations[HttpMethod.Head]));
        Assert.Empty(HeadResponseNormalizer.ResponsesWithContent(HttpMethod.Get, operations[HttpMethod.Get]));
    }

    private static OpenApiDocument Parse(string yaml)
    {
        // A path of its own: the parser caches a document per path, and these tests rewrite theirs
        var (document, _) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(yaml, $"head-{Guid.NewGuid():N}.yaml");
        Assert.NotNull(document);
        return document;
    }
}