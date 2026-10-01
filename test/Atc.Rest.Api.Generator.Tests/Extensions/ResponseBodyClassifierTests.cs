namespace Atc.Rest.Api.Generator.Tests.Extensions;

/// <summary>
/// The one rule every generator (server, C# clients, TypeScript) uses to decide how a body is read and written:
/// JSON, an XML model, text, or a binary file. A declared media type is never "no body".
/// </summary>
public class ResponseBodyClassifierTests
{
    private static readonly OpenApiSchema ObjectSchema = new() { Type = JsonSchemaType.Object };
    private static readonly OpenApiSchema StringSchema = new() { Type = JsonSchemaType.String };
    private static readonly OpenApiSchema BinarySchema = new() { Type = JsonSchemaType.String, Format = "binary" };
    private static readonly OpenApiSchema ArraySchema = new() { Type = JsonSchemaType.Array, Items = ObjectSchema };

    public static TheoryData<string, string, ResponseBodyKind> Cases()
        => new()
        {
            // JSON - any schema
            { "application/json", "object", ResponseBodyKind.Json },
            { "application/json; charset=utf-8", "object", ResponseBodyKind.Json },
            { "application/problem+json", "object", ResponseBodyKind.Json },
            { "application/vnd.api+json", "array", ResponseBodyKind.Json },
            { "APPLICATION/JSON", "string", ResponseBodyKind.Json },

            // XML - a model with an object / array schema, text with a string schema (or none)
            { "application/xml", "object", ResponseBodyKind.Xml },
            { "text/xml", "object", ResponseBodyKind.Xml },
            { "application/problem+xml", "object", ResponseBodyKind.Xml },
            { "application/atom+xml", "array", ResponseBodyKind.Xml },
            { "application/xml", "string", ResponseBodyKind.Text },
            { "application/xml", "none", ResponseBodyKind.Text },

            // Text - unless the schema says binary
            { "text/plain", "string", ResponseBodyKind.Text },
            { "text/csv", "string", ResponseBodyKind.Text },
            { "text/html", "none", ResponseBodyKind.Text },
            { "text/csv", "binary", ResponseBodyKind.Binary },

            // Stream - sequential media types (an item stream, not a file)
            { "text/event-stream", "none", ResponseBodyKind.Stream },
            { "application/jsonl", "none", ResponseBodyKind.Stream },
            { "application/x-ndjson", "object", ResponseBodyKind.Stream },
            { "application/json-seq", "none", ResponseBodyKind.Stream },

            // Binary - known file types, and any other media type (never "no body")
            { "application/octet-stream", "binary", ResponseBodyKind.Binary },
            { "application/octet-stream", "none", ResponseBodyKind.Binary },
            { "application/pdf", "binary", ResponseBodyKind.Binary },
            { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "binary", ResponseBodyKind.Binary },
            { "application/vnd.ms-excel", "binary", ResponseBodyKind.Binary },
            { "application/zip", "binary", ResponseBodyKind.Binary },
            { "image/png", "binary", ResponseBodyKind.Binary },
            { "image/jpeg", "none", ResponseBodyKind.Binary },
            { "audio/mpeg", "binary", ResponseBodyKind.Binary },
            { "video/mp4", "binary", ResponseBodyKind.Binary },
            { "application/x-custom", "object", ResponseBodyKind.Binary },
            { "*/*", "none", ResponseBodyKind.Binary },
        };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Classify_ReturnsTheBodyKind(
        string mediaType,
        string schema,
        ResponseBodyKind expected)
    {
        var openApiSchema = schema switch
        {
            "object" => ObjectSchema,
            "string" => StringSchema,
            "binary" => BinarySchema,
            "array" => ArraySchema,
            _ => null,
        };

        Assert.Equal(expected, ResponseBodyClassifier.Classify(mediaType, openApiSchema));
    }

    [Fact]
    public void Classify_ASchemaReferenceToAModel_UnderXml_IsAnXmlModel()
    {
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.0.3
            info:
              title: Test
              version: 1.0.0
            paths:
              /users:
                get:
                  responses:
                    '200':
                      description: OK
                      content:
                        application/json:
                          schema:
                            $ref: '#/components/schemas/User'
                        application/xml:
                          schema:
                            $ref: '#/components/schemas/User'
                        application/pdf:
                          schema:
                            type: string
                            format: binary
            components:
              schemas:
                User:
                  type: object
                  properties:
                    name:
                      type: string
            """);

        var content = document!.Paths["/users"].Operations![HttpMethod.Get].Responses!["200"].Content;

        // Every declared media type, in the specification's order, with its kind.
        var bodies = ResponseBodyClassifier.ClassifyContent(content);
        Assert.Equal(
            ["application/json:Json", "application/xml:Xml", "application/pdf:Binary"],
            bodies.Select(b => $"{b.MediaType}:{b.Kind}"),
            StringComparer.Ordinal);
    }

    [Fact]
    public void ClassifyContent_AnItemSchema_IsAStream_WhateverTheMediaType()
    {
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.2.0
            info:
              title: Test
              version: 1.0.0
            paths:
              /events:
                get:
                  responses:
                    '200':
                      description: OK
                      content:
                        multipart/mixed:
                          itemSchema:
                            $ref: '#/components/schemas/Event'
            components:
              schemas:
                Event:
                  type: object
                  properties:
                    id:
                      type: string
            """);

        var content = document!.Paths["/events"].Operations![HttpMethod.Get].Responses!["200"].Content;

        Assert.Equal(ResponseBodyKind.Stream, Assert.Single(ResponseBodyClassifier.ClassifyContent(content)).Kind);
    }

    /// <summary>
    /// A model or a file: a file beside a typed model (JSON with a schema, XML of a component model or a list of one) is
    /// a file alternative of the model, in either order; beside an untyped body (JSON without a schema, an inline XML
    /// object) the file stays the primary body.
    /// </summary>
    [Theory]
    [InlineData("application/pdf: { schema: { type: string, format: binary } }\n            application/json: { schema: { $ref: '#/components/schemas/Report' } }", "application/json", "application/pdf")]
    [InlineData("application/json: { schema: { $ref: '#/components/schemas/Report' } }\n            application/pdf: { schema: { type: string, format: binary } }", "application/json", "application/pdf")]
    [InlineData("application/pdf: { schema: { type: string, format: binary } }\n            application/xml: { schema: { type: array, items: { $ref: '#/components/schemas/Report' } } }", "application/xml", "application/pdf")]
    [InlineData("application/pdf: { schema: { type: string, format: binary } }\n            application/json: {}", "application/pdf", "")]
    [InlineData("application/pdf: { schema: { type: string, format: binary } }\n            application/xml: { schema: { type: object, properties: { name: { type: string } } } }", "application/pdf", "")]
    [InlineData("application/json: { schema: { $ref: '#/components/schemas/Report' } }", "application/json", "")]
    public void PrimaryBodyAndFileAlternatives_AModelOrAFile(
        string content,
        string primary,
        string fileAlternatives)
    {
        var document = OpenApiDocumentHelper.ParseYaml($$"""
            openapi: 3.0.3
            info:
              title: Test
              version: 1.0.0
            paths:
              /reports:
                get:
                  responses:
                    '200':
                      description: OK
                      content:
                        {{content}}
            components:
              schemas:
                Report:
                  type: object
                  properties:
                    title:
                      type: string
            """);

        var responseContent = document!.Paths["/reports"].Operations![HttpMethod.Get].Responses!["200"].Content;

        Assert.Equal(primary, ResponseBodyClassifier.PrimaryBody(responseContent)?.MediaType);
        Assert.Equal(fileAlternatives, string.Join(", ", ResponseBodyClassifier.FileAlternatives(responseContent)));
    }

    [Fact]
    public void ClassifyContent_NoContent_IsEmpty()
    {
        Assert.Empty(ResponseBodyClassifier.ClassifyContent(null));
        Assert.Empty(ResponseBodyClassifier.ClassifyContent(new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal)));
    }
}