namespace Atc.Rest.Api.Generator.Tests.Helpers;

/// <summary>
/// Every generator looks a body up by its media type, so the document's media types are made canonical first:
/// parameters dropped (<c>application/json; charset=utf-8</c>), lower case (<c>Application/JSON</c>), and a wildcard
/// that carries a model (<c>*/*</c>, <c>application/*</c> - springdoc's default) read as JSON. A wildcard with a binary
/// schema stays what it is, and two declarations that become one media type keep the first.
/// </summary>
public class MediaTypeNormalizerTests
{
    private const string TwiceYaml = """
        openapi: 3.0.3
        info: { title: Contoso, version: 1.0.0 }
        paths:
          /pets:
            post:
              operationId: createPet
              requestBody:
                content:
                  application/json:
                    schema: { $ref: '#/components/schemas/Pet' }
                  Application/JSON; charset=utf-8:
                    schema: { $ref: '#/components/schemas/Cat' }
              responses:
                '201':
                  description: Created
                  content:
                    application/json:
                      schema: { $ref: '#/components/schemas/Pet' }
                    application/json; charset=utf-8:
                      schema: { $ref: '#/components/schemas/Pet' }
                '400': { $ref: '#/components/responses/BadRequest' }
        components:
          responses:
            BadRequest:
              description: Bad
              content:
                application/json:
                  schema: { $ref: '#/components/schemas/Pet' }
                '*/*':
                  schema: { type: object, properties: { code: { type: string } } }
          schemas:
            Pet: { type: object, properties: { name: { type: string } } }
            Cat: { type: object, properties: { lives: { type: integer } } }
        """;

    /// <summary>
    /// Two declarations that become one media type keep the first - and the one that is dropped is named when it
    /// says something else (another schema), before the rewrite and after it; the same schema twice is nobody's loss.
    /// </summary>
    [Fact]
    public void DroppedDeclarations_NamesWhatBecomesAMediaTypeAlreadyDeclared_WithAnotherSchema()
    {
        var (document, _) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(TwiceYaml, $"twice-{Guid.NewGuid():N}.yaml");
        Assert.NotNull(document);

        var before = MediaTypeNormalizer.DroppedDeclarations(document);
        MediaTypeNormalizer.WithCanonicalMediaTypes(document);
        MediaTypeNormalizer.WithCanonicalMediaTypes(document);
        var after = MediaTypeNormalizer.DroppedDeclarations(document);

        Assert.Equal(
            [
                ("the request body of operation 'createPet'", "application/json", "Application/JSON; charset=utf-8"),
                ("the response 'BadRequest' (components)", "application/json", "*/*"),
            ],
            before);
        Assert.Equal(before, after);
        Assert.Equal("Pet", ((OpenApiSchemaReference)document.Paths["/pets"].Operations![HttpMethod.Post].RequestBody!.Content!["application/json"].Schema!).Reference.Id);
    }

    private const string Yaml = """
        openapi: 3.0.3
        info: { title: Contoso, version: 1.0.0 }
        paths:
          /pets:
            post:
              operationId: createPet
              requestBody:
                content:
                  Application/JSON:
                    schema:
                      $ref: '#/components/schemas/Pet'
              responses:
                '201':
                  description: Created
                  content:
                    application/json; charset=utf-8:
                      schema:
                        $ref: '#/components/schemas/Pet'
                '400':
                  $ref: '#/components/responses/BadRequest'
          /pets/{id}:
            get:
              operationId: getPet
              responses:
                '200':
                  description: OK
                  content:
                    '*/*':
                      schema:
                        $ref: '#/components/schemas/Pet'
          /pets/{id}/photo:
            get:
              operationId: getPhoto
              responses:
                '200':
                  description: OK
                  content:
                    '*/*':
                      schema:
                        type: string
                        format: binary
          /pets/{id}/summary:
            get:
              operationId: getSummary
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Pet'
                    'application/json; charset=utf-8':
                      schema:
                        type: string
        components:
          responses:
            BadRequest:
              description: Bad
              content:
                'application/problem+json; charset=utf-8':
                  schema:
                    $ref: '#/components/schemas/Pet'
          schemas:
            Pet:
              type: object
              properties:
                name: { type: string }
        """;

    [Theory]
    [InlineData("createPet", "requestBody", "application/json")]
    [InlineData("createPet", "201", "application/json")]
    [InlineData("getPet", "200", "application/json")]
    [InlineData("getPhoto", "200", "*/*")]
    public void WithCanonicalMediaTypes_RewritesTheKeys(
        string operationId,
        string where,
        string expectedKey)
    {
        var document = MediaTypeNormalizer.WithCanonicalMediaTypes(Parse());

        var operation = document.Paths.Values.SelectMany(p => p.Operations!.Values).Single(o => o.OperationId == operationId);
        var content = where == "requestBody" ? operation.RequestBody!.Content : operation.Responses![where].Content;

        Assert.Equal([expectedKey], content!.Keys);
    }

    [Fact]
    public void WithCanonicalMediaTypes_TwoDeclarationsThatBecomeOne_KeepTheFirst()
    {
        var document = MediaTypeNormalizer.WithCanonicalMediaTypes(Parse());

        var content = document.Paths["/pets/{id}/summary"].Operations![HttpMethod.Get].Responses!["200"].Content!;

        var media = Assert.Single(content);
        Assert.Equal("application/json", media.Key);
        Assert.IsType<OpenApiSchemaReference>(media.Value.Schema);
    }

    [Fact]
    public void WithCanonicalMediaTypes_ComponentResponses_AreRewrittenToo()
    {
        var document = MediaTypeNormalizer.WithCanonicalMediaTypes(Parse());

        Assert.Equal(["application/problem+json"], document.Components!.Responses!["BadRequest"].Content!.Keys);
    }

    [Fact]
    public void WithCanonicalMediaTypes_IsIdempotent()
    {
        var once = MediaTypeNormalizer.WithCanonicalMediaTypes(Parse());
        var keysOnce = AllKeys(once);

        var twice = MediaTypeNormalizer.WithCanonicalMediaTypes(once);

        Assert.Equal(keysOnce, AllKeys(twice));
    }

    private static List<string> AllKeys(OpenApiDocument document)
        => document.Paths.Values
            .SelectMany(p => p.Operations!.Values)
            .SelectMany(o => (o.RequestBody?.Content?.Keys ?? []).Concat(o.Responses!.Values.SelectMany(r => r.Content?.Keys ?? [])))
            .ToList();

    private static OpenApiDocument Parse()
    {
        var (document, _) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(Yaml, "media-types.yaml");
        Assert.NotNull(document);
        return document;
    }
}