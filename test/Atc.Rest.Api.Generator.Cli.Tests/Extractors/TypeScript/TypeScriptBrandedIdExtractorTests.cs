namespace Atc.Rest.Api.Generator.Cli.Tests.Extractors.TypeScript;

public class TypeScriptBrandedIdExtractorTests
{
    [Fact]
    public void CollectBrandNames_NullDocument_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => TypeScriptBrandedIdExtractor.CollectBrandNames(openApiDoc: null));
    }

    [Fact]
    public void CollectBrandNames_PropertyEndingInId_ProducesEntityIdBrand()
    {
        // The `<entity>Id` shape is the dominant convention in test specs — it
        // qualifies regardless of schema name. Property `ownerId` → brand `OwnerId`.
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths: {}
                            components:
                              schemas:
                                Pet:
                                  type: object
                                  properties:
                                    ownerId: { type: string, format: uuid }
                            """;
        var doc = ParseYaml(yaml);

        var brands = TypeScriptBrandedIdExtractor.CollectBrandNames(doc);

        Assert.Contains("OwnerId", brands, StringComparer.Ordinal);
    }

    [Fact]
    public void CollectBrandNames_BarePropertyId_FallsBackToSchemaName()
    {
        // Bare `id` on schema `Pet` → brand `PetId`. Without the fallback the brand
        // would be the useless `Id`.
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths: {}
                            components:
                              schemas:
                                Pet:
                                  type: object
                                  properties:
                                    id: { type: string, format: uuid }
                            """;
        var doc = ParseYaml(yaml);

        var brands = TypeScriptBrandedIdExtractor.CollectBrandNames(doc);

        Assert.Contains("PetId", brands, StringComparer.Ordinal);
        Assert.DoesNotContain("Id", brands, StringComparer.Ordinal);
    }

    [Fact]
    public void CollectBrandNames_NonUuidFormat_NotBranded()
    {
        // Branding deliberately gates on `format: uuid` — integer / unformatted-string
        // IDs are too noisy to brand. A `string` with no format must not produce a brand.
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths: {}
                            components:
                              schemas:
                                Pet:
                                  type: object
                                  properties:
                                    petId: { type: string }
                                    age: { type: integer }
                            """;
        var doc = ParseYaml(yaml);

        var brands = TypeScriptBrandedIdExtractor.CollectBrandNames(doc);

        Assert.Empty(brands);
    }

    [Fact]
    public void CollectBrandNames_NameNotEndingInId_NotBranded()
    {
        // `name`, `slug`, `code` — meaningful string identifiers in some specs but
        // without the "Id" suffix signal there's no defensible name to brand them as.
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths: {}
                            components:
                              schemas:
                                Pet:
                                  type: object
                                  properties:
                                    name: { type: string, format: uuid }
                                    slug: { type: string, format: uuid }
                            """;
        var doc = ParseYaml(yaml);

        var brands = TypeScriptBrandedIdExtractor.CollectBrandNames(doc);

        Assert.Empty(brands);
    }

    [Fact]
    public void CollectBrandNames_PathParamEndingInId_Branded()
    {
        // Path param `{petId}` with format uuid → `PetId`, same rule as properties.
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths:
                              /pets/{petId}:
                                get:
                                  operationId: getPet
                                  parameters:
                                    - { name: petId, in: path, required: true, schema: { type: string, format: uuid } }
                                  responses:
                                    '200': { description: OK }
                            """;
        var doc = ParseYaml(yaml);

        var brands = TypeScriptBrandedIdExtractor.CollectBrandNames(doc);

        Assert.Contains("PetId", brands, StringComparer.Ordinal);
    }

    [Fact]
    public void CollectBrandNames_BarePathParamId_UsesParentSegment()
    {
        // The `/users/{id}` convention is common (Showcase + Demo scenarios use it).
        // Bare `{id}` + format uuid + parent segment `users` → strip trailing `s` →
        // `UserId`. Without this fallback the brand collapses to the useless `Id`.
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths:
                              /users/{id}:
                                get:
                                  operationId: getUser
                                  parameters:
                                    - { name: id, in: path, required: true, schema: { type: string, format: uuid } }
                                  responses:
                                    '200': { description: OK }
                            """;
        var doc = ParseYaml(yaml);

        var brands = TypeScriptBrandedIdExtractor.CollectBrandNames(doc);

        Assert.Contains("UserId", brands, StringComparer.Ordinal);
    }

    [Fact]
    public void CollectBrandNames_PropertyAndPathParam_DedupedAndSorted()
    {
        // Pet.id (schema fallback → PetId) + /pets/{petId} (param-name → PetId) =
        // one brand entry. Mixed with OwnerId, the result is sorted alphabetically.
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths:
                              /pets/{petId}:
                                get:
                                  operationId: getPet
                                  parameters:
                                    - { name: petId, in: path, required: true, schema: { type: string, format: uuid } }
                                  responses:
                                    '200': { description: OK }
                            components:
                              schemas:
                                Pet:
                                  type: object
                                  properties:
                                    id: { type: string, format: uuid }
                                    ownerId: { type: string, format: uuid }
                            """;
        var doc = ParseYaml(yaml);

        var brands = TypeScriptBrandedIdExtractor.CollectBrandNames(doc);

        Assert.Equal(new[] { "OwnerId", "PetId" }, brands);
    }

    [Fact]
    public void Generate_EmptyList_ReturnsNull()
    {
        // Callers use the null return as the "skip the write" signal.
        var content = TypeScriptBrandedIdExtractor.Generate([], headerContent: null);

        Assert.Null(content);
    }

    [Fact]
    public void Generate_EmitsTypeAliasPerBrand()
    {
        var content = TypeScriptBrandedIdExtractor.Generate(
            ["OwnerId", "PetId"],
            headerContent: null);

        Assert.NotNull(content);
        Assert.Contains("export type OwnerId = string & { readonly __brand: 'OwnerId' };", content, StringComparison.Ordinal);
        Assert.Contains("export type PetId = string & { readonly __brand: 'PetId' };", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_IncludesHeaderWhenProvided()
    {
        var content = TypeScriptBrandedIdExtractor.Generate(
            ["PetId"],
            headerContent: "// <auto-generated />\n");

        Assert.NotNull(content);
        Assert.StartsWith("// <auto-generated />", content, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Pet", "ownerId", "OwnerId")]
    [InlineData("Pet", "ownerID", "OwnerId")]
    [InlineData("Pet", "id", "PetId")]
    [InlineData("Pet", "Id", "PetId")]
    [InlineData("Pet", "name", null)]
    public void ResolvePropertyBrand_UuidProperty_BrandsByNameOrSchema(
        string schemaName,
        string propertyName,
        string? expected)
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" };

        Assert.Equal(expected, TypeScriptBrandedIdExtractor.ResolvePropertyBrand(schemaName, propertyName, schema));
    }

    [Fact]
    public void ResolvePropertyBrand_NotAUuidString_IsNull()
    {
        Assert.Null(TypeScriptBrandedIdExtractor.ResolvePropertyBrand("Pet", "ownerId", new OpenApiSchema { Type = JsonSchemaType.String }));
        Assert.Null(TypeScriptBrandedIdExtractor.ResolvePropertyBrand("Pet", "ownerId", new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "uuid" }));
        Assert.Null(TypeScriptBrandedIdExtractor.ResolvePropertyBrand("Pet", "ownerId", propertySchema: null));
    }

    [Theory]
    [InlineData("/users/{id}", "id", "UserId")]
    [InlineData("/users/{userId}/pets", "userId", "UserId")]
    [InlineData("/{id}", "id", null)]
    [InlineData("/users/{userId}/{id}", "id", null)]
    [InlineData("/users/{slug}", "slug", null)]
    public void ResolveParamBrand_UuidPathParameter_BrandsByNameOrParentSegment(
        string path,
        string parameterName,
        string? expected)
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" };

        Assert.Equal(expected, TypeScriptBrandedIdExtractor.ResolveParamBrand(path, parameterName, schema));
    }

    [Fact]
    public void ResolveParamBrand_NotAUuidString_IsNull()
        => Assert.Null(TypeScriptBrandedIdExtractor.ResolveParamBrand("/users/{id}", "id", new OpenApiSchema { Type = JsonSchemaType.Integer }));

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, "test.yaml", out var document)
            ? document
            : null;
}