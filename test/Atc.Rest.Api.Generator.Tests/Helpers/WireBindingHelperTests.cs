namespace Atc.Rest.Api.Generator.Tests.Helpers;

public class WireBindingHelperTests
{
    private const string Yaml = """
        openapi: 3.0.0
        info: { title: T, version: 1.0.0 }
        paths:
          /pets/{petId}:
            parameters:
              - name: petId
                in: path
                required: true
                schema: { type: string }
            get:
              operationId: getPet
              responses:
                '200': { description: OK }
          /pets:
            get:
              operationId: listPets
              parameters:
                - name: tags
                  in: query
                  style: form
                  explode: false
                  schema: { type: array, items: { type: string } }
                - name: ids
                  in: query
                  style: pipeDelimited
                  schema: { type: array, items: { type: integer } }
                - name: kinds
                  in: query
                  style: spaceDelimited
                  schema: { type: array, items: { type: string } }
                - name: names
                  in: query
                  schema: { type: array, items: { type: string } }
                - name: limit
                  in: query
                  schema: { type: integer }
              responses:
                '200': { description: OK }
          /health:
            get:
              operationId: getHealth
              responses:
                '200': { description: OK }
          /uploads:
            post:
              operationId: upload
              requestBody:
                content:
                  multipart/form-data:
                    schema:
                      $ref: '#/components/schemas/UploadForm'
              responses:
                '201': { description: Created }
        components:
          schemas:
            Color:
              type: string
              enum: [red, dark-blue]
            UploadForm:
              type: object
              required: [name, file]
              properties:
                name: { type: string }
                count: { type: integer, format: int32 }
                color: { $ref: '#/components/schemas/Color' }
                tags: { type: array, items: { type: string } }
                file: { type: string, format: binary }
        """;

    private const string NoParametersYaml = """
        openapi: 3.0.0
        info: { title: T, version: 1.0.0 }
        paths:
          /health:
            get:
              operationId: getHealth
              responses:
                '200': { description: OK }
          /notes:
            post:
              operationId: createNote
              requestBody:
                content:
                  application/json:
                    schema:
                      type: object
                      properties:
                        text: { type: string }
              responses:
                '201': { description: Created }
        """;

    [Fact]
    public void IsNeeded_Operation_PathLevelParameter_CountsForTheOperation()
    {
        var document = OpenApiDocumentHelper.ParseYaml(Yaml);
        var pathItem = document.Paths["/pets/{petId}"];
        var operation = pathItem.Operations![HttpMethod.Get];

        Assert.True(WireBindingHelper.IsNeeded(operation, pathItem));

        // Without the path item the operation declares nothing of its own
        Assert.False(WireBindingHelper.IsNeeded(operation, pathItem: null));
    }

    [Fact]
    public void IsNeeded_Operation_QueryParameters_ReturnsTrue()
    {
        var document = OpenApiDocumentHelper.ParseYaml(Yaml);
        var pathItem = document.Paths["/pets"];

        Assert.True(WireBindingHelper.IsNeeded(pathItem.Operations![HttpMethod.Get], pathItem));
    }

    [Fact]
    public void IsNeeded_Operation_NoParameters_ReturnsFalse()
    {
        var document = OpenApiDocumentHelper.ParseYaml(Yaml);
        var pathItem = document.Paths["/health"];

        Assert.False(WireBindingHelper.IsNeeded(pathItem.Operations![HttpMethod.Get], pathItem));
    }

    [Fact]
    public void IsNeeded_Document_AnyBoundParameter_ReturnsTrue()
    {
        var document = OpenApiDocumentHelper.ParseYaml(Yaml);

        Assert.True(WireBindingHelper.IsNeeded(document));
    }

    [Fact]
    public void IsNeeded_Document_OnlyJsonBodies_ReturnsFalse()
    {
        var document = OpenApiDocumentHelper.ParseYaml(NoParametersYaml);

        Assert.False(WireBindingHelper.IsNeeded(document));
    }

    [Fact]
    public void IsNeeded_Document_FormBodyWithoutParameters_ReturnsTrue()
    {
        // A form bound field by field needs the form step even when no parameter is bound
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.0.0
            info: { title: T, version: 1.0.0 }
            paths:
              /uploads:
                post:
                  operationId: upload
                  requestBody:
                    content:
                      multipart/form-data:
                        schema:
                          type: object
                          properties:
                            name: { type: string }
                  responses:
                    '201': { description: Created }
            """);

        Assert.True(WireBindingHelper.IsNeeded(document));
    }

    [Fact]
    public void FormFieldArguments_WritesNameTypeRequiredAndListPerField()
    {
        var document = OpenApiDocumentHelper.ParseYaml(Yaml);
        var operation = document.Paths["/uploads"].Operations![HttpMethod.Post];

        var result = WireBindingHelper.FormFieldArguments(operation);

        Assert.Equal(
            [
                "(\"name\", typeof(string), true, false)",
                "(\"count\", typeof(int), false, false)",
                "(\"color\", typeof(Color), false, false)",
                "(\"tags\", typeof(string), false, true)",
                "(\"file\", typeof(global::Microsoft.AspNetCore.Http.IFormFile), true, false)",
            ],
            result);
    }

    [Fact]
    public void FormFieldArguments_NoFormBody_ReturnsEmpty()
    {
        var document = OpenApiDocumentHelper.ParseYaml(Yaml);
        var operation = document.Paths["/health"].Operations![HttpMethod.Get];

        Assert.Empty(WireBindingHelper.FormFieldArguments(operation));
    }

    [Fact]
    public void DelimitedArrayArguments_OnlyDelimitedArrayQueryParameters()
    {
        // form/explode:false is comma, pipeDelimited is pipe, spaceDelimited is space; an exploded array (names)
        // and a scalar (limit) are not delimited
        var document = OpenApiDocumentHelper.ParseYaml(Yaml);
        var pathItem = document.Paths["/pets"];

        var result = WireBindingHelper.DelimitedArrayArguments(pathItem.Operations![HttpMethod.Get], pathItem);

        Assert.Equal(
            [
                "(\"tags\", ',')",
                "(\"ids\", '|')",
                "(\"kinds\", ' ')",
            ],
            result);
    }

    [Fact]
    public void DelimitedArrayArguments_PathParameterOnly_ReturnsEmpty()
    {
        var document = OpenApiDocumentHelper.ParseYaml(Yaml);
        var pathItem = document.Paths["/pets/{petId}"];

        Assert.Empty(WireBindingHelper.DelimitedArrayArguments(pathItem.Operations![HttpMethod.Get], pathItem));
    }

    [Fact]
    public void GenerateContent_EmitsTheExtensionsClassInTheProjectNamespace()
    {
        var code = WireBindingHelper.GenerateContent("TestApi");

        Assert.StartsWith("// <auto-generated />", code, StringComparison.Ordinal);
        Assert.Contains("namespace TestApi.Generated;", code, StringComparison.Ordinal);
        Assert.Contains("public static class WireBindingExtensions", code, StringComparison.Ordinal);
        Assert.Contains("private const string CookieHeaderPrefix = \"x-cookie-\";", code, StringComparison.Ordinal);
        Assert.Contains("public static TBuilder WithWireBinding<TBuilder, TParameters>(this TBuilder builder, params (string Name, char Delimiter)[] delimitedArrays)", code, StringComparison.Ordinal);
        Assert.Contains("public static TBuilder WithWireFormBinding<TBuilder>(this TBuilder builder, params (string Name, Type Type, bool Required, bool IsArray)[] fields)", code, StringComparison.Ordinal);
        Assert.Contains($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]", code, StringComparison.Ordinal);
    }
}