namespace Atc.Rest.Api.Generator.Cli.Tests.Extractors.TypeScript;

public class TypeScriptOperationHelperOtherMethodsTests
{
    [Fact]
    public void GetParameterType_NoSchema_FallsBackToString()
    {
        var param = new OpenApiParameter { Name = "q", In = ParameterLocation.Query };

        Assert.Equal("string", TypeScriptOperationHelper.GetParameterType(param));
    }

    [Fact]
    public void GetParameterType_StripsTrailingPipeNullForUrlParameters()
    {
        // URL params are absent (undefined) or present (a value) — never the literal `null`.
        // Any "| null" tail must be stripped to keep the generated TS signature accurate.
        var param = new OpenApiParameter
        {
            Name = "q",
            In = ParameterLocation.Query,
            Schema = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
        };

        var result = TypeScriptOperationHelper.GetParameterType(param);

        Assert.DoesNotContain("| null", result, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildInterpolatedPath_SinglePathParam_EmitsTemplateLiteral()
    {
        var pathParams = new List<OpenApiParameter>
        {
            new() { Name = "petId", In = ParameterLocation.Path, Schema = new OpenApiSchema { Type = JsonSchemaType.String } },
        };

        var result = TypeScriptOperationHelper.BuildInterpolatedPath("/pets/{petId}", pathParams, TypeScriptNamingStrategy.CamelCase);

        Assert.Contains("`/pets/${", result, StringComparison.Ordinal);
        Assert.Contains("petId", result, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildInterpolatedPath_NoPathParams_ReturnsQuotedLiteral()
    {
        // No interpolation means a plain string literal — using a template literal would be noise.
        var result = TypeScriptOperationHelper.BuildInterpolatedPath("/pets", [], TypeScriptNamingStrategy.CamelCase);

        Assert.Equal("'/pets'", result);
    }

    [Fact]
    public void BuildInterpolatedPath_HonorsNamingStrategy()
    {
        // The OpenAPI param name is `pet_id` — with CamelCase the TS variable becomes `petId`.
        var pathParams = new List<OpenApiParameter>
        {
            new() { Name = "pet_id", In = ParameterLocation.Path, Schema = new OpenApiSchema { Type = JsonSchemaType.String } },
        };

        var camel = TypeScriptOperationHelper.BuildInterpolatedPath("/pets/{pet_id}", pathParams, TypeScriptNamingStrategy.CamelCase);
        Assert.Contains("petId", camel, StringComparison.Ordinal);

        var original = TypeScriptOperationHelper.BuildInterpolatedPath("/pets/{pet_id}", pathParams, TypeScriptNamingStrategy.Original);
        Assert.Contains("pet_id", original, StringComparison.Ordinal);
    }

    [Fact]
    public void GetMergedParameters_PathLevelAndOperationLevelMerged()
    {
        // OpenAPI lets parameters live on the path item AND the operation; both must be returned.
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: T
                              version: 1.0.0
                            paths:
                              /pets/{petId}:
                                parameters:
                                  - name: petId
                                    in: path
                                    required: true
                                    schema:
                                      type: string
                                get:
                                  operationId: getPet
                                  parameters:
                                    - name: include
                                      in: query
                                      schema:
                                        type: string
                                  responses:
                                    '200': { description: OK }
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);
        var (_, pathItem) = doc!.Paths!.First();
        var op = pathItem.Operations![HttpMethod.Get];

        var pathParams = TypeScriptOperationHelper.GetMergedParameters(op, doc!, "/pets/{petId}", ParameterLocation.Path);
        var queryParams = TypeScriptOperationHelper.GetMergedParameters(op, doc!, "/pets/{petId}", ParameterLocation.Query);

        Assert.Contains(pathParams, p => p.Name == "petId");
        Assert.Contains(queryParams, p => p.Name == "include");
    }

    [Fact]
    public void CollectImportTypes_ResponseRefSchema_AddsTypeName()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: T
                              version: 1.0.0
                            paths:
                              /pets:
                                get:
                                  operationId: listPets
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            $ref: '#/components/schemas/Pet'
                            components:
                              schemas:
                                Pet:
                                  type: object
                                  properties:
                                    id: { type: string }
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);
        var op = doc!.Paths!.Values.First().Operations![HttpMethod.Get];

        var imports = new HashSet<string>(StringComparer.Ordinal);
        TypeScriptOperationHelper.CollectImportTypes(op, imports);

        Assert.Contains("Pet", imports);
    }

    [Fact]
    public void GetStreamingItemType_ArraySchema_ReturnsItemType()
    {
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Array,
            Items = new OpenApiSchema { Type = JsonSchemaType.String },
        };

        var result = TypeScriptOperationHelper.GetStreamingItemType(schema);

        // Streaming endpoints unwrap T[] to T at the per-item yield boundary.
        Assert.Equal("string", result);
    }

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, "test.yaml", out var document)
            ? document
            : null;
}