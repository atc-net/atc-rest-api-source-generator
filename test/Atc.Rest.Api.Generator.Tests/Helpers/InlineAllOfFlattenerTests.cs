namespace Atc.Rest.Api.Generator.Tests.Helpers;

public class InlineAllOfFlattenerTests
{
    private const string Components = """
                                      components:
                                        schemas:
                                          Item:
                                            type: object
                                            properties:
                                              id:
                                                type: string
                                          Envelope:
                                            type: object
                                            required: [totalCount]
                                            properties:
                                              continuationToken:
                                                type: string
                                              totalCount:
                                                type: integer
                                          PaginatedResult:
                                            type: object
                                            properties:
                                              continuation:
                                                type: string
                                              results:
                                                type: array
                                                items: {}
                                          Shape:
                                            oneOf:
                                              - $ref: '#/components/schemas/Item'
                                              - $ref: '#/components/schemas/Envelope'
                                      """;

    [Fact]
    public void Flatten_BaseThatIsNotAGenericPage_BecomesOneInlineObject()
    {
        var schema = FlattenResponse("""
                                     allOf:
                                       - $ref: '#/components/schemas/Envelope'
                                       - type: object
                                         required: [items]
                                         properties:
                                           items:
                                             type: array
                                             items:
                                               $ref: '#/components/schemas/Item'
                                     """);

        var flattened = Assert.IsType<OpenApiSchema>(schema);
        Assert.Null(flattened.AllOf);
        Assert.True(InlineSchemaExtractor.IsInlineObjectSchema(flattened));
        Assert.Equal(["continuationToken", "totalCount", "items"], flattened.Properties!.Keys);
        Assert.Equal(["items", "totalCount"], flattened.Required!.OrderBy(r => r, StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact]
    public void Flatten_LaterPartRedefinesAProperty_TheLaterDefinitionWins()
    {
        var schema = FlattenResponse("""
                                     allOf:
                                       - $ref: '#/components/schemas/Envelope'
                                       - type: object
                                         properties:
                                           totalCount:
                                             type: string
                                     """);

        var flattened = Assert.IsType<OpenApiSchema>(schema);
        Assert.Equal(["continuationToken", "totalCount"], flattened.Properties!.Keys);
        Assert.True(flattened.Properties["totalCount"].Type!.Value.HasFlag(JsonSchemaType.String));
    }

    [Fact]
    public void Flatten_TwoReferences_BecomesOneInlineObject()
    {
        var schema = FlattenResponse("""
                                     allOf:
                                       - $ref: '#/components/schemas/Envelope'
                                       - $ref: '#/components/schemas/Item'
                                     """);

        var flattened = Assert.IsType<OpenApiSchema>(schema);
        Assert.Equal(["continuationToken", "totalCount", "id"], flattened.Properties!.Keys);
    }

    [Fact]
    public void Flatten_GenericPageWithItems_IsLeftAlone()
    {
        var schema = FlattenResponse("""
                                     allOf:
                                       - $ref: '#/components/schemas/PaginatedResult'
                                       - type: object
                                         properties:
                                           results:
                                             type: array
                                             items:
                                               $ref: '#/components/schemas/Item'
                                     """);

        Assert.Equal(2, Assert.IsType<OpenApiSchema>(schema).AllOf!.Count);
    }

    [Fact]
    public void Flatten_SingleReference_IsLeftAlone()
    {
        var schema = FlattenResponse("""
                                     description: An item
                                     allOf:
                                       - $ref: '#/components/schemas/Item'
                                     """);

        Assert.Single(Assert.IsType<OpenApiSchema>(schema).AllOf!);
    }

    [Fact]
    public void Flatten_ReferenceToAUnion_IsLeftAlone()
    {
        var schema = FlattenResponse("""
                                     allOf:
                                       - $ref: '#/components/schemas/Shape'
                                       - type: object
                                         properties:
                                           note:
                                             type: string
                                     """);

        Assert.Equal(2, Assert.IsType<OpenApiSchema>(schema).AllOf!.Count);
    }

    [Fact]
    public void Flatten_ArrayItems_AreFlattened()
    {
        var schema = FlattenResponse("""
                                     type: array
                                     items:
                                       allOf:
                                         - $ref: '#/components/schemas/Envelope'
                                         - type: object
                                           properties:
                                             note:
                                               type: string
                                     """);

        var items = Assert.IsType<OpenApiSchema>(Assert.IsType<OpenApiSchema>(schema).Items);
        Assert.Null(items.AllOf);
        Assert.Equal(["continuationToken", "totalCount", "note"], items.Properties!.Keys);
    }

    [Fact]
    public void Flatten_RequestBody_IsFlattened()
    {
        var document = Parse("""
                             type: object
                             properties:
                               ok:
                                 type: boolean
                             """);

        InlineAllOfFlattener.Flatten(document);

        var body = document.Paths["/things"].Operations![HttpMethod.Post].RequestBody!.Content!["application/json"].Schema;
        var flattened = Assert.IsType<OpenApiSchema>(body);
        Assert.Null(flattened.AllOf);
        Assert.Equal(["continuationToken", "totalCount", "name"], flattened.Properties!.Keys);
    }

    [Fact]
    public void Flatten_Twice_GivesTheSameResult()
    {
        var document = Parse("""
                             allOf:
                               - $ref: '#/components/schemas/Envelope'
                               - type: object
                                 properties:
                                   note:
                                     type: string
                             """);

        InlineAllOfFlattener.Flatten(document);
        var first = ResponseSchema(document);

        InlineAllOfFlattener.Flatten(document);

        Assert.Same(first, ResponseSchema(document));
    }

    private static IOpenApiSchema? FlattenResponse(string responseSchemaYaml)
    {
        var document = Parse(responseSchemaYaml);

        InlineAllOfFlattener.Flatten(document);

        return ResponseSchema(document);
    }

    private static IOpenApiSchema? ResponseSchema(OpenApiDocument document)
        => document.Paths["/things"].Operations![HttpMethod.Post].Responses!["200"].Content!["application/json"].Schema;

    private static OpenApiDocument Parse(string responseSchemaYaml)
    {
        var indented = string.Join(
            "\n",
            responseSchemaYaml
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n')
                .Select(line => "                " + line));

        var yaml = $"""
                    openapi: 3.0.0
                    info:
                      title: Test API
                      version: 1.0.0
                    paths:
                      /things:
                        post:
                          operationId: createThing
                          requestBody:
                            content:
                              application/json:
                                schema:
                                  allOf:
                                    - $ref: '#/components/schemas/Envelope'
                                    - type: object
                                      properties:
                                        name:
                                          type: string
                          responses:
                            '200':
                              description: OK
                              content:
                                application/json:
                                  schema:
                    {indented}
                    {Components}
                    """;

        return OpenApiDocumentHelper.ParseYaml(yaml);
    }
}