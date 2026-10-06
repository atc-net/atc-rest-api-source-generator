namespace Atc.Rest.Api.Generator.Tests.Helpers;

public class SchemaNameNormalizerTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: Names
          version: 1.0.0
        paths:
          /things:
            get:
              operationId: listThings
              responses:
                '200':
                  description: ok
                  content:
                    application/json:
                      schema:
                        type: array
                        items:
                          $ref: '#/components/schemas/Note-Size'
        components:
          schemas:
            Plain:
              type: object
              properties:
                size:
                  $ref: '#/components/schemas/Note-Size'
                model:
                  $ref: '#/components/schemas/3DModel'
                dotted:
                  $ref: '#/components/schemas/Company.Api.Thing'
                shape:
                  $ref: '#/components/schemas/Shape'
            Note-Size:
              type: object
              properties:
                width:
                  type: integer
            3DModel:
              type: object
              properties:
                name:
                  type: string
            Company.Api.Thing:
              type: object
              properties:
                id:
                  type: string
            Shape:
              oneOf:
                - $ref: '#/components/schemas/Note-Size'
                - $ref: '#/components/schemas/3DModel'
              discriminator:
                propertyName: kind
                mapping:
                  size: '#/components/schemas/Note-Size'
                  model: '#/components/schemas/3DModel'
        """;

    [Fact]
    public void WithIdentifierSchemaNames_RenamesTheSchemasAndEveryReferenceToThem()
    {
        var document = OpenApiDocumentHelper.ParseYaml(Yaml);

        var normalized = SchemaNameNormalizer.WithIdentifierSchemaNames(document);

        var schemas = normalized.Components!.Schemas!;
        Assert.Equal(
            ["Company_Api_Thing", "Note_Size", "Plain", "Shape", "_3DModel"],
            schemas.Keys.OrderBy(k => k, StringComparer.Ordinal),
            StringComparer.Ordinal);

        var plain = (OpenApiSchema)schemas["Plain"];
        Assert.Equal("Note_Size", ((OpenApiSchemaReference)plain.Properties!["size"]).Reference.Id);
        Assert.Equal("_3DModel", ((OpenApiSchemaReference)plain.Properties["model"]).Reference.Id);
        Assert.Equal("Company_Api_Thing", ((OpenApiSchemaReference)plain.Properties["dotted"]).Reference.Id);
        Assert.NotNull(((OpenApiSchemaReference)plain.Properties["size"]).Target);

        var items = ((OpenApiSchema)normalized.Paths["/things"].Operations![HttpMethod.Get].Responses!["200"].Content!["application/json"].Schema!).Items;
        Assert.Equal("Note_Size", ((OpenApiSchemaReference)items!).Reference.Id);

        var shape = (OpenApiSchema)schemas["Shape"];
        Assert.Equal(
            ["Note_Size", "_3DModel"],
            shape.OneOf!.Select(s => ((OpenApiSchemaReference)s).Reference.Id),
            StringComparer.Ordinal);
        Assert.Equal(
            ["Note_Size", "_3DModel"],
            shape.Discriminator!.Mapping!.Values.Select(v => v.Reference.Id),
            StringComparer.Ordinal);
    }

    [Fact]
    public void WithSchemaNamesOutside_RenamesAReservedNameWithTheSuffix_AndEveryReferenceToIt()
    {
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.0.3
            info: { title: Names, version: 1.0.0 }
            paths:
              /things:
                get:
                  operationId: listThings
                  responses:
                    '200':
                      description: ok
                      content:
                        application/json:
                          schema: { $ref: '#/components/schemas/Response' }
            components:
              schemas:
                Response:
                  type: object
                  properties:
                    error: { $ref: '#/components/schemas/Error' }
                Error:
                  type: object
                  properties:
                    code: { type: string }
                ErrorModel:
                  type: object
                  properties:
                    taken: { type: boolean }
                Thing:
                  type: object
                  properties:
                    name: { type: string }
            """);

        var normalized = SchemaNameNormalizer.WithSchemaNamesOutside(document, ["Response", "Error", "Promise"], "Model");

        var schemas = normalized.Components!.Schemas!;
        Assert.Equal(
            ["ErrorModel", "ErrorModel2", "ResponseModel", "Thing"],
            schemas.Keys.OrderBy(k => k, StringComparer.Ordinal),
            StringComparer.Ordinal);
        var response = (OpenApiSchema)schemas["ResponseModel"];
        Assert.Equal("ErrorModel2", ((OpenApiSchemaReference)response.Properties!["error"]).Reference.Id);
        var body = normalized.Paths["/things"].Operations![HttpMethod.Get].Responses!["200"].Content!["application/json"].Schema;
        Assert.Equal("ResponseModel", ((OpenApiSchemaReference)body!).Reference.Id);
        Assert.Same(document, SchemaNameNormalizer.WithSchemaNamesOutside(document, ["Promise"], "Model"));
    }

    [Fact]
    public void WithIdentifierSchemaNames_NothingToRename_ReturnsTheSameDocument()
    {
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.0.3
            info:
              title: Names
              version: 1.0.0
            paths: {}
            components:
              schemas:
                Plain:
                  type: object
            """);

        Assert.Same(document, SchemaNameNormalizer.WithIdentifierSchemaNames(document));
    }

    [Fact]
    public void WithIdentifierSchemaNames_ASanitizedNameThatIsTaken_GetsASuffix()
    {
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.0.3
            info:
              title: Names
              version: 1.0.0
            paths: {}
            components:
              schemas:
                Note_Size:
                  type: object
                Note-Size:
                  type: object
            """);

        var normalized = SchemaNameNormalizer.WithIdentifierSchemaNames(document);

        Assert.Equal(
            ["Note_Size", "Note_Size2"],
            normalized.Components!.Schemas!.Keys.OrderBy(k => k, StringComparer.Ordinal),
            StringComparer.Ordinal);
    }
}