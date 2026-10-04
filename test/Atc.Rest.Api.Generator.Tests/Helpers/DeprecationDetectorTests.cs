namespace Atc.Rest.Api.Generator.Tests.Helpers;

/// <summary>
/// Whether a specification marks anything deprecated - a schema (also nested in a property, items or a composition),
/// a property, an operation or a parameter - which the generated code then marks <c>[Obsolete]</c>.
/// </summary>
public sealed class DeprecationDetectorTests
{
    [Theory]
    [InlineData("Schema", "components:\n  schemas:\n    Pet:\n      type: object\n      deprecated: true\n")]
    [InlineData("Property", "components:\n  schemas:\n    Pet:\n      type: object\n      properties:\n        tag:\n          type: string\n          deprecated: true\n")]
    [InlineData("NestedItems", "components:\n  schemas:\n    Pets:\n      type: array\n      items:\n        type: object\n        properties:\n          tag:\n            type: string\n            deprecated: true\n")]
    [InlineData("Composition", "components:\n  schemas:\n    Pet:\n      allOf:\n        - type: object\n          properties:\n            tag:\n              type: string\n              deprecated: true\n")]
    [InlineData("ComponentParameter", "components:\n  parameters:\n    Tag:\n      name: tag\n      in: query\n      deprecated: true\n      schema:\n        type: string\n")]
    [InlineData("Operation", "paths:\n  /pets:\n    get:\n      operationId: listPets\n      deprecated: true\n      responses:\n        '200':\n          description: OK\n")]
    [InlineData("OperationParameter", "paths:\n  /pets:\n    get:\n      operationId: listPets\n      parameters:\n        - name: tag\n          in: query\n          deprecated: true\n          schema:\n            type: string\n      responses:\n        '200':\n          description: OK\n")]
    [InlineData("PathParameter", "paths:\n  /pets/{id}:\n    parameters:\n      - name: id\n        in: path\n        required: true\n        deprecated: true\n        schema:\n          type: string\n    get:\n      operationId: getPet\n      responses:\n        '200':\n          description: OK\n")]
    [InlineData("InlineBody", "paths:\n  /pets:\n    post:\n      operationId: createPet\n      requestBody:\n        content:\n          application/json:\n            schema:\n              type: object\n              properties:\n                tag:\n                  type: string\n                  deprecated: true\n      responses:\n        '201':\n          description: Created\n")]
    public void HasDeprecatedMembers_FindsTheDeprecatedMember(
        string where,
        string yaml)
    {
        var document = Parse(yaml);

        Assert.True(DeprecationDetector.HasDeprecatedMembers(document), where);
    }

    [Fact]
    public void HasDeprecatedMembers_WithoutAnyDeprecation_IsFalse()
    {
        var document = Parse("paths:\n  /pets:\n    get:\n      operationId: listPets\n      responses:\n        '200':\n          description: OK\n          content:\n            application/json:\n              schema:\n                $ref: '#/components/schemas/Pet'\ncomponents:\n  schemas:\n    Pet:\n      type: object\n      properties:\n        next:\n          $ref: '#/components/schemas/Pet'\n");

        Assert.False(DeprecationDetector.HasDeprecatedMembers(document));
    }

    [Fact]
    public void WithoutDeprecatedOperations_DropsThem_AndAPathLeftWithoutOperations_AndLeavesTheOriginal()
    {
        var document = Parse("paths:\n  /pets:\n    get:\n      operationId: listPets\n      responses:\n        '200':\n          description: OK\n    delete:\n      operationId: clearPets\n      deprecated: true\n      responses:\n        '204':\n          description: Cleared\n  /legacy:\n    get:\n      operationId: listLegacy\n      deprecated: true\n      responses:\n        '200':\n          description: OK\n");

        var filtered = DeprecationDetector.WithoutDeprecatedOperations(document);

        Assert.Equal("/pets", Assert.Single(filtered.Paths.Keys));
        Assert.Equal([HttpMethod.Get], filtered.Paths["/pets"].Operations!.Keys);
        Assert.Equal(2, document.Paths.Count);
        Assert.Equal(2, document.Paths["/pets"].Operations!.Count);
    }

    [Fact]
    public void WithoutDeprecatedOperations_WithoutAny_IsTheDocumentItself()
    {
        var document = Parse("paths:\n  /pets:\n    get:\n      operationId: listPets\n      responses:\n        '200':\n          description: OK\n");

        Assert.Same(document, DeprecationDetector.WithoutDeprecatedOperations(document));
    }

    private static OpenApiDocument Parse(string body)
        => OpenApiDocumentHelper.ParseYaml("openapi: 3.0.1\ninfo:\n  title: Contoso\n  version: 1.0.0\n" + (body.StartsWith("paths:", StringComparison.Ordinal) ? body : "paths: {}\n" + body));
}