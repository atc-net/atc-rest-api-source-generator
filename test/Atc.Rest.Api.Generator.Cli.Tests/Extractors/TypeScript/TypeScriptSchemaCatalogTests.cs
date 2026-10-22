namespace Atc.Rest.Api.Generator.Cli.Tests.Extractors.TypeScript;

/// <summary>
/// The one choice of which components the TypeScript client emits and as what - the plain models and the Zod schemas
/// both follow it, so every model has its schema.
/// </summary>
public sealed class TypeScriptSchemaCatalogTests
{
    private const string Yaml = """
        openapi: 3.1.0
        info: { title: T, version: 1.0.0 }
        paths: {}
        components:
          schemas:
            Pet:
              type: object
              properties:
                name: { type: string }
            Dog:
              allOf:
                - $ref: '#/components/schemas/Pet'
                - properties:
                    breed: { type: string }
            OldPet:
              type: object
              deprecated: true
              properties:
                name: { type: string }
            PetOrError:
              oneOf:
                - $ref: '#/components/schemas/Pet'
                - type: string
            Pets: { type: array, items: { $ref: '#/components/schemas/Pet' } }
            Coordinate:
              type: array
              prefixItems: [{ type: number }, { type: number }]
            Labels: { type: object, additionalProperties: { type: string } }
            PetId: { type: string, format: uuid }
            Status: { type: string, enum: [active, active, null] }
            NoValues: { type: string, enum: [null] }
        """;

    [Theory]
    [InlineData(false, "Pet,Dog")]
    [InlineData(true, "Pet,Dog,OldPet")]
    public void ObjectModels_AreTheObjectsAndTheComposedObjects(
        bool includeDeprecated,
        string expected)
        => Assert.Equal(
            expected,
            string.Join(",", TypeScriptSchemaCatalog.ObjectModels(Parse(), includeDeprecated).Select(m => m.Name)));

    [Fact]
    public void Aliases_AreTheArraysTheTuplesTheDictionariesAndTheNamedPrimitives()
        => Assert.Equal(
            "Pets:Array,Coordinate:Array,Labels:Record,PetId:Scalar",
            string.Join(",", TypeScriptSchemaCatalog.Aliases(Parse(), includeDeprecated: false).Select(a => $"{a.Name}:{a.Kind}")));

    [Fact]
    public void StringEnums_HaveEachValueOnce_AndAnEnumWithoutAValueIsLeftOut()
    {
        var (name, _, values) = Assert.Single(TypeScriptSchemaCatalog.StringEnums(Parse(), includeDeprecated: false));

        Assert.Equal("Status", name);
        Assert.Equal(["active"], values, StringComparer.Ordinal);
    }

    private static OpenApiDocument Parse()
    {
        var (document, _) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(Yaml, "schema-catalog.yaml");
        Assert.NotNull(document);
        return document;
    }
}