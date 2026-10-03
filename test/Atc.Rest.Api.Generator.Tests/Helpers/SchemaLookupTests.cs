namespace Atc.Rest.Api.Generator.Tests.Helpers;

public class SchemaLookupTests
{
    private const string SchemasYaml = """
        openapi: 3.0.0
        info: { title: T, version: 1.0.0 }
        paths: {}
        components:
          schemas:
            Pet:
              type: object
              title: Pet
              properties:
                name: { type: string }
            Owner:
              type: object
              title: Pet owner
              properties:
                name: { type: string }
            Untitled:
              type: object
              properties:
                id: { type: integer }
        """;

    [Fact]
    public void FindSchemaNameByReference_SameInstance_ReturnsItsName()
    {
        var document = OpenApiDocumentHelper.ParseYaml(SchemasYaml);
        var schema = (OpenApiSchema)document.Components!.Schemas!["Untitled"];

        var result = SchemaLookup.FindSchemaNameByReference(document, schema);

        Assert.Equal("Untitled", result);
    }

    [Fact]
    public void FindSchemaNameByReference_DetachedCopyWithTheSameTitle_ReturnsTheTitledComponent()
    {
        var document = OpenApiDocumentHelper.ParseYaml(SchemasYaml);
        var copy = new OpenApiSchema { Type = JsonSchemaType.Object, Title = "Pet owner" };

        var result = SchemaLookup.FindSchemaNameByReference(document, copy);

        Assert.Equal("Owner", result);
    }

    [Fact]
    public void FindSchemaNameByReference_NoInstanceAndNoTitleMatch_ReturnsNull()
    {
        var document = OpenApiDocumentHelper.ParseYaml(SchemasYaml);

        Assert.Null(SchemaLookup.FindSchemaNameByReference(document, new OpenApiSchema { Title = "Nobody" }));

        // An untitled stranger matches nothing, not even the untitled component
        Assert.Null(SchemaLookup.FindSchemaNameByReference(document, new OpenApiSchema { Type = JsonSchemaType.Object }));
    }

    [Fact]
    public void FindSchemaNameByReference_NoComponents_ReturnsNull()
    {
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.0.0
            info: { title: T, version: 1.0.0 }
            paths: {}
            """);

        Assert.Null(SchemaLookup.FindSchemaNameByReference(document, new OpenApiSchema { Title = "Pet" }));
    }

    [Fact]
    public void FindSchemaNameByReference_NullArguments_Throw()
    {
        var document = OpenApiDocumentHelper.ParseYaml(SchemasYaml);

        Assert.Throws<ArgumentNullException>(() => SchemaLookup.FindSchemaNameByReference(null!, new OpenApiSchema()));
        Assert.Throws<ArgumentNullException>(() => SchemaLookup.FindSchemaNameByReference(document, null!));
    }
}