namespace Atc.Rest.Api.Generator.Tests.Extensions;

public class OpenApiOperationExtensionsTests
{
    [Fact]
    public void GetStreamingItemSchema_ItemSchemaPresent_ReturnsItem()
    {
        var op = CreateOperationWithItemSchema("text/event-stream");

        var item = op.GetStreamingItemSchema();

        Assert.NotNull(item);
        Assert.Equal("Event", item.Title);
    }

    [Fact]
    public void GetStreamingItemSchema_NoItemSchema_ReturnsNull()
    {
        var op = new OpenApiOperation
        {
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse
                {
                    Content = new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal)
                    {
                        ["application/json"] = new OpenApiMediaType { Schema = new OpenApiSchema() },
                    },
                },
            },
        };

        Assert.Null(op.GetStreamingItemSchema());
    }

    [Fact]
    public void IsStreamingResponse_ItemSchemaOnResponse_ReturnsTrue()
    {
        var op = CreateOperationWithItemSchema("application/jsonl");

        Assert.True(op.IsStreamingResponse());
    }

    [Fact]
    public void IsStreamingResponse_PlainJsonResponse_ReturnsFalse()
    {
        var op = new OpenApiOperation
        {
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse
                {
                    Content = new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal)
                    {
                        ["application/json"] = new OpenApiMediaType { Schema = new OpenApiSchema() },
                    },
                },
            },
        };

        Assert.False(op.IsStreamingResponse());
    }

    [Fact]
    public void IsStreamingResponse_AsyncEnumerableAnnotation_ReturnsTrue()
    {
        var op = new OpenApiOperation
        {
            Extensions = new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal)
            {
                ["x-return-async-enumerable"] = new JsonNodeExtension(JsonValue.Create(true)),
            },
        };

        Assert.True(op.IsStreamingResponse());
    }

    // ========== GetOperationId Tests ==========
    [Fact]
    public void GetOperationId_WithOperationId_ReturnsIt()
    {
        var op = new OpenApiOperation { OperationId = "listPets" };

        var result = op.GetOperationId("/pets", "GET");

        Assert.Equal("listPets", result);
    }

    [Fact]
    public void GetOperationId_WithoutOperationId_GeneratesFromPathAndMethod()
    {
        var op = new OpenApiOperation();

        var result = op.GetOperationId("/pets/{petId}", "GET");

        Assert.Equal("GET_pets_petId", result);
    }

    // ========== HasParameters Tests ==========
    [Fact]
    public void HasParameters_WithParameters_ReturnsTrue()
    {
        var op = new OpenApiOperation
        {
            Parameters = [new OpenApiParameter { Name = "id", In = ParameterLocation.Path }],
        };

        Assert.True(op.HasParameters());
    }

    [Fact]
    public void HasParameters_NoParameters_ReturnsFalse()
    {
        var op = new OpenApiOperation();

        Assert.False(op.HasParameters());
    }

    // ========== HasRequestBody Tests ==========
    [Fact]
    public void HasRequestBody_WithContent_ReturnsTrue()
    {
        var op = CreateOperationWithJsonBody();

        Assert.True(op.HasRequestBody());
    }

    [Fact]
    public void HasRequestBody_NoBody_ReturnsFalse()
    {
        var op = new OpenApiOperation();

        Assert.False(op.HasRequestBody());
    }

    // ========== GetRequestBodySchema Tests ==========
    [Fact]
    public void GetRequestBodySchema_JsonContent_ReturnsSchema()
    {
        var op = CreateOperationWithJsonBody();

        var result = op.GetRequestBodySchema();

        Assert.NotNull(result);
    }

    [Fact]
    public void GetRequestBodySchema_NoContent_ReturnsNull()
    {
        var op = new OpenApiOperation();

        var result = op.GetRequestBodySchema();

        Assert.Null(result);
    }

    // ========== GetResponseSchema Tests ==========
    [Fact]
    public void GetResponseSchema_With200_ReturnsSchema()
    {
        var op = CreateOperationWithResponse("200");

        var result = op.GetResponseSchema("200");

        Assert.NotNull(result);
    }

    [Fact]
    public void GetResponseSchema_MissingStatusCode_ReturnsNull()
    {
        var op = CreateOperationWithResponse("200");

        var result = op.GetResponseSchema("404");

        Assert.Null(result);
    }

    // ========== GetParametersByLocation Tests ==========
    [Fact]
    public void GetQueryParameters_ReturnsOnlyQueryParams()
    {
        var op = new OpenApiOperation
        {
            Parameters =
            [
                new OpenApiParameter { Name = "filter", In = ParameterLocation.Query },
                new OpenApiParameter { Name = "id", In = ParameterLocation.Path },
            ],
        };

        var result = op.GetQueryParameters().ToList();

        Assert.Single(result);
        Assert.Equal("filter", result[0].Name);
    }

    [Fact]
    public void GetPathParameters_ReturnsOnlyPathParams()
    {
        var op = new OpenApiOperation
        {
            Parameters =
            [
                new OpenApiParameter { Name = "filter", In = ParameterLocation.Query },
                new OpenApiParameter { Name = "id", In = ParameterLocation.Path },
            ],
        };

        var result = op.GetPathParameters().ToList();

        Assert.Single(result);
        Assert.Equal("id", result[0].Name);
    }

    // ========== HasFileUpload Tests ==========
    [Fact]
    public void HasFileUpload_OctetStream_ReturnsTrue()
    {
        var op = CreateOperationWithContentType("application/octet-stream");

        Assert.True(op.HasFileUpload());
    }

    [Fact]
    public void HasFileUpload_MultipartFormData_ReturnsTrue()
    {
        var op = CreateOperationWithContentType("multipart/form-data");

        Assert.True(op.HasFileUpload());
    }

    [Fact]
    public void HasFileUpload_JsonBody_ReturnsFalse()
    {
        var op = CreateOperationWithJsonBody();

        Assert.False(op.HasFileUpload());
    }

    // ========== HasFileDownload Tests ==========
    [Fact]
    public void HasFileDownload_OctetStreamResponse_ReturnsTrue()
    {
        var op = CreateOperationWithResponseContentType("200", "application/octet-stream");

        Assert.True(op.HasFileDownload());
    }

    [Fact]
    public void HasFileDownload_PdfResponse_ReturnsTrue()
    {
        var op = CreateOperationWithResponseContentType("200", "application/pdf");

        Assert.True(op.HasFileDownload());
    }

    [Fact]
    public void HasFileDownload_JsonResponse_ReturnsFalse()
    {
        var op = CreateOperationWithResponse("200");

        Assert.False(op.HasFileDownload());
    }

    [Fact]
    public void HasFileDownload_NoResponse_ReturnsFalse()
    {
        var op = new OpenApiOperation();

        Assert.False(op.HasFileDownload());
    }

    // ========== GetRequestBodySchemaWithContentType Tests ==========
    [Fact]
    public void GetRequestBodySchemaWithContentType_PrioritizesJson()
    {
        var op = new OpenApiOperation
        {
            RequestBody = new OpenApiRequestBody
            {
                Content = new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal)
                {
                    ["application/octet-stream"] = new OpenApiMediaType { Schema = new OpenApiSchema() },
                    ["application/json"] = new OpenApiMediaType { Schema = new OpenApiSchema() },
                },
            },
        };

        var (schema, contentType) = op.GetRequestBodySchemaWithContentType();

        Assert.NotNull(schema);
        Assert.Equal("application/json", contentType);
    }

    [Fact]
    public void GetRequestBodySchemaWithContentType_FallsBackToFileUpload()
    {
        var op = CreateOperationWithContentType("application/octet-stream");

        var (schema, contentType) = op.GetRequestBodySchemaWithContentType();

        Assert.NotNull(schema);
        Assert.Equal("application/octet-stream", contentType);
    }

    [Fact]
    public void GetRequestBodySchemaWithContentType_FallsBackToAUrlEncodedForm()
    {
        // A body that is only a URL-encoded form was (null, "") - every generator left the operation without a body
        var op = CreateOperationWithContentType("application/x-www-form-urlencoded");

        var (schema, contentType) = op.GetRequestBodySchemaWithContentType();

        Assert.NotNull(schema);
        Assert.Equal("application/x-www-form-urlencoded", contentType);
    }

    [Fact]
    public void GetRequestBodySchemaWithContentType_JsonWinsOverAForm()
    {
        var op = new OpenApiOperation
        {
            RequestBody = new OpenApiRequestBody
            {
                Content = new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal)
                {
                    ["application/x-www-form-urlencoded"] = new OpenApiMediaType { Schema = new OpenApiSchema() },
                    ["application/json"] = new OpenApiMediaType { Schema = new OpenApiSchema() },
                },
            },
        };

        var (_, contentType) = op.GetRequestBodySchemaWithContentType();

        Assert.Equal("application/json", contentType);
    }

    [Theory]
    [InlineData("multipart/form-data", true)]
    [InlineData("multipart/form-data; boundary=x", true)]
    [InlineData("application/x-www-form-urlencoded", true)]
    [InlineData("application/x-www-form-urlencoded; charset=utf-8", true)]
    [InlineData("application/json", false)]
    [InlineData("application/octet-stream", false)]
    [InlineData("multipart/mixed", false)]
    public void IsFormContentType_ReturnsExpected(
        string contentType,
        bool expected)
        => Assert.Equal(expected, OpenApiOperationExtensions.IsFormContentType(contentType));

    [Theory]
    [InlineData("multipart/form-data")]
    [InlineData("application/x-www-form-urlencoded")]
    public void GetFormBodySchemaInfo_AModelForm_ReturnsTheSchemaAndItsProperties(
        string contentType)
    {
        var document = OpenApiDocumentHelper.ParseYaml($$"""
            openapi: 3.0.0
            info: { title: T, version: 1.0.0 }
            paths:
              /login:
                post:
                  operationId: login
                  requestBody:
                    content:
                      {{contentType}}:
                        schema: { $ref: '#/components/schemas/Credentials' }
                  responses:
                    '204': { description: ok }
            components:
              schemas:
                Credentials:
                  type: object
                  required: [user]
                  properties:
                    user: { type: string }
                    remember: { type: boolean }
            """);
        var op = document.Paths["/login"].Operations![HttpMethod.Post];

        Assert.True(op.RequiresFormBindingFlattening());
        Assert.Equal(contentType, op.GetFormBodyMediaType());
        var (schemaName, properties, required) = op.GetFormBodySchemaInfo();
        Assert.Equal("Credentials", schemaName);
        Assert.Equal(["user", "remember"], properties!.Keys.ToList());
        Assert.Equal(["user"], required.ToList());
    }

    [Fact]
    public void GetFormBodyMediaType_PrefersMultipartWhenBothAreDeclared()
    {
        var op = new OpenApiOperation
        {
            RequestBody = new OpenApiRequestBody
            {
                Content = new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal)
                {
                    ["application/x-www-form-urlencoded"] = new OpenApiMediaType { Schema = new OpenApiSchema() },
                    ["multipart/form-data"] = new OpenApiMediaType { Schema = new OpenApiSchema() },
                },
            },
        };

        Assert.Equal("multipart/form-data", op.GetFormBodyMediaType());
        Assert.Equal(["multipart/form-data", "application/x-www-form-urlencoded"], op.GetFormBodyMediaTypes());
    }

    [Fact]
    public void GetRequestBodySchemaWithContentType_NoBody_ReturnsNullSchema()
    {
        var op = new OpenApiOperation();

        var (schema, contentType) = op.GetRequestBodySchemaWithContentType();

        Assert.Null(schema);
        Assert.Equal(string.Empty, contentType);
    }

    // ========== HasNotFoundResponse Tests ==========
    [Fact]
    public void HasNotFoundResponse_With404_ReturnsTrue()
    {
        var op = CreateOperationWithResponse("404");

        Assert.True(op.HasNotFoundResponse());
    }

    [Fact]
    public void HasNotFoundResponse_Without404_ReturnsFalse()
    {
        var op = CreateOperationWithResponse("200");

        Assert.False(op.HasNotFoundResponse());
    }

    // ========== IsFileUploadContentType / IsFileDownloadContentType (static) ==========
    [Theory]
    [InlineData("application/octet-stream", true)]
    [InlineData("multipart/form-data", true)]
    [InlineData("image/png", true)]
    [InlineData("application/json", false)]
    [InlineData("text/plain", false)]
    public void IsFileUploadContentType_ReturnsExpected(
        string contentType,
        bool expected)
    {
        Assert.Equal(expected, OpenApiOperationExtensions.IsFileUploadContentType(contentType));
    }

    [Theory]
    [InlineData("application/octet-stream", true)]
    [InlineData("image/png", true)]
    [InlineData("audio/mp3", true)]
    [InlineData("video/mp4", true)]
    [InlineData("application/pdf", true)]
    [InlineData("application/zip", true)]
    [InlineData("application/json", false)]
    [InlineData("text/html", false)]
    public void IsFileDownloadContentType_ReturnsExpected(
        string contentType,
        bool expected)
    {
        Assert.Equal(expected, OpenApiOperationExtensions.IsFileDownloadContentType(contentType));
    }

    // ========== HasTextResponse Tests ==========
    [Fact]
    public void HasTextResponse_TextPlainStringResponse_ReturnsTrue()
    {
        var op = new OpenApiOperation
        {
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse
                {
                    Content = new Dictionary<string, IOpenApiMediaType>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["text/plain"] = new OpenApiMediaType { Schema = new OpenApiSchema { Type = JsonSchemaType.String } },
                    },
                },
            },
        };

        Assert.True(op.HasTextResponse());
    }

    [Fact]
    public void HasTextResponse_JsonResponse_ReturnsFalse()
    {
        var op = CreateOperationWithResponse("200");

        Assert.False(op.HasTextResponse());
    }

    [Fact]
    public void HasTextResponse_BinaryResponse_ReturnsFalse()
    {
        var op = CreateOperationWithResponseContentType("200", "application/octet-stream");

        Assert.False(op.HasTextResponse());
    }

    [Fact]
    public void HasTextResponse_NoResponse_ReturnsFalse()
    {
        var op = new OpenApiOperation();

        Assert.False(op.HasTextResponse());
    }

    // ========== IsTextResponseMediaType (static) ==========
    [Theory]
    [InlineData("text/plain", true)]
    [InlineData("text/csv", true)]
    [InlineData("text/html", true)]
    [InlineData("text/markdown", true)]
    [InlineData("application/xml", true)]
    [InlineData("text/xml", true)]
    [InlineData("application/json", false)]
    [InlineData("application/octet-stream", false)]
    [InlineData("image/png", false)]
    [InlineData("multipart/form-data", false)]
    [InlineData("", false)]
    public void IsTextResponseMediaType_ReturnsExpected(
        string contentType,
        bool expected)
    {
        Assert.Equal(expected, OpenApiOperationExtensions.IsTextResponseMediaType(contentType));
    }

    // ========== TryGetTextResponseMediaType ==========
    [Fact]
    public void TryGetTextResponseMediaType_TextPlainTypeString_ReturnsTrue()
    {
        var response = new OpenApiResponse
        {
            Content = new Dictionary<string, IOpenApiMediaType>(StringComparer.OrdinalIgnoreCase)
            {
                ["text/plain"] = new OpenApiMediaType { Schema = new OpenApiSchema { Type = JsonSchemaType.String } },
            },
        };

        var result = response.TryGetTextResponseMediaType(out var mediaType, out var media);

        Assert.True(result);
        Assert.Equal("text/plain", mediaType);
        Assert.NotNull(media);
    }

    [Fact]
    public void TryGetTextResponseMediaType_TextCsvTypeString_ReturnsTrue()
    {
        var response = new OpenApiResponse
        {
            Content = new Dictionary<string, IOpenApiMediaType>(StringComparer.OrdinalIgnoreCase)
            {
                ["text/csv"] = new OpenApiMediaType { Schema = new OpenApiSchema { Type = JsonSchemaType.String } },
            },
        };

        var result = response.TryGetTextResponseMediaType(out var mediaType, out var media);

        Assert.True(result);
        Assert.Equal("text/csv", mediaType);
        Assert.NotNull(media);
    }

    [Fact]
    public void TryGetTextResponseMediaType_OnlyJson_ReturnsFalse()
    {
        var response = new OpenApiResponse
        {
            Content = new Dictionary<string, IOpenApiMediaType>(StringComparer.OrdinalIgnoreCase)
            {
                ["application/json"] = new OpenApiMediaType { Schema = new OpenApiSchema { Type = JsonSchemaType.Object } },
            },
        };

        var result = response.TryGetTextResponseMediaType(out var mediaType, out var media);

        Assert.False(result);
        Assert.Null(mediaType);
        Assert.Null(media);
    }

    [Fact]
    public void TryGetTextResponseMediaType_BinaryStringFormat_ReturnsFalse()
    {
        // type: string + format: binary is the OpenAPI convention for file downloads,
        // not a text response. Must NOT match.
        var response = new OpenApiResponse
        {
            Content = new Dictionary<string, IOpenApiMediaType>(StringComparer.OrdinalIgnoreCase)
            {
                ["application/octet-stream"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" },
                },
            },
        };

        var result = response.TryGetTextResponseMediaType(out var mediaType, out var media);

        Assert.False(result);
        Assert.Null(mediaType);
        Assert.Null(media);
    }

    [Fact]
    public void TryGetTextResponseMediaType_TextPlainNonStringSchema_ReturnsFalse()
    {
        // Spec edge case: text/plain declared with type: object would be malformed.
        // The helper should only match type: string.
        var response = new OpenApiResponse
        {
            Content = new Dictionary<string, IOpenApiMediaType>(StringComparer.OrdinalIgnoreCase)
            {
                ["text/plain"] = new OpenApiMediaType { Schema = new OpenApiSchema { Type = JsonSchemaType.Object } },
            },
        };

        var result = response.TryGetTextResponseMediaType(out var mediaType, out var media);

        Assert.False(result);
        Assert.Null(mediaType);
        Assert.Null(media);
    }

    [Fact]
    public void TryGetTextResponseMediaType_NoContent_ReturnsFalse()
    {
        var response = new OpenApiResponse();

        var result = response.TryGetTextResponseMediaType(out var mediaType, out var media);

        Assert.False(result);
        Assert.Null(mediaType);
        Assert.Null(media);
    }

    [Fact]
    public void TryGetTextResponseMediaType_PrefersTextPlainOverOtherTextMediaTypes()
    {
        // When a response declares multiple text media types (rare but legal),
        // prefer text/plain because that is the canonical case and matches what
        // consumers using HttpClient.GetStringAsync expect.
        var response = new OpenApiResponse
        {
            Content = new Dictionary<string, IOpenApiMediaType>(StringComparer.OrdinalIgnoreCase)
            {
                ["text/csv"] = new OpenApiMediaType { Schema = new OpenApiSchema { Type = JsonSchemaType.String } },
                ["text/plain"] = new OpenApiMediaType { Schema = new OpenApiSchema { Type = JsonSchemaType.String } },
            },
        };

        var result = response.TryGetTextResponseMediaType(out var mediaType, out var media);

        Assert.True(result);
        Assert.Equal("text/plain", mediaType);
        Assert.NotNull(media);
    }

    // ========== GetStreamingFraming Tests ==========
    [Theory]
    [InlineData("text/event-stream", StreamingFraming.ServerSentEvents)]
    [InlineData("application/jsonl", StreamingFraming.JsonLines)]
    [InlineData("application/x-ndjson", StreamingFraming.JsonLines)]
    [InlineData("application/x-jsonlines", StreamingFraming.JsonLines)]
    [InlineData("application/json-seq", StreamingFraming.JsonSequence)]
    [InlineData("multipart/mixed", StreamingFraming.MultipartMixed)]
    [InlineData("application/json", StreamingFraming.JsonArray)]
    [InlineData("text/event-stream; charset=utf-8", StreamingFraming.ServerSentEvents)]
    [InlineData("application/octet-stream", StreamingFraming.JsonArray)]
    [InlineData("text/html", StreamingFraming.JsonArray)]
    [InlineData("*/*", StreamingFraming.JsonArray)]
    public void GetStreamingFraming_ItemSchemaMediaType_ReturnsExpectedFraming(
        string mediaType,
        StreamingFraming expected)
    {
        var operation = BuildStreamingOperation(mediaType);

        Assert.Equal(expected, operation.GetStreamingFraming());
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Classify_EmptyOrNull_ReturnsJsonArray(string? mediaType)
    {
        Assert.Equal(StreamingFraming.JsonArray, StreamingMediaType.Classify(mediaType));
    }

    [Fact]
    public void GetStreamingFraming_LegacyAnnotationOnly_ReturnsJsonArray()
    {
        var operation = BuildAsyncEnumerableAnnotatedOperation();

        Assert.Equal(StreamingFraming.JsonArray, operation.GetStreamingFraming());
    }

    [Fact]
    public void GetStreamingResponse_ItemSchemaPresent_ReturnsMediaTypeAndSchema()
    {
        var operation = BuildStreamingOperation("application/jsonl");

        var result = operation.GetStreamingResponse();

        Assert.NotNull(result);
        Assert.Equal("application/jsonl", result.Value.MediaType);
        Assert.NotNull(result.Value.ItemSchema);
    }

    [Theory]
    [InlineData("text/event-stream", StreamingFraming.ServerSentEvents)]
    [InlineData("application/jsonl", StreamingFraming.JsonLines)]
    [InlineData("application/json-seq", StreamingFraming.JsonSequence)]
    public void GetStreamingResponse_AnnotationWithSchemaOnAStreamingMediaType_TheSchemaIsTheItem(
        string mediaType,
        StreamingFraming expected)
    {
        // Before 3.2 there is no itemSchema: the annotation plus a streaming media type says the same thing
        var schema = new OpenApiSchema { Title = "Event" };
        var operation = BuildAnnotatedOperation(mediaType, schema, annotated: true);

        var result = operation.GetStreamingResponse();

        Assert.NotNull(result);
        Assert.Equal(mediaType, result.Value.MediaType);
        Assert.Same(schema, result.Value.ItemSchema);
        Assert.Equal(expected, operation.GetStreamingFraming());
    }

    [Fact]
    public void GetStreamingResponse_AnnotationWithArraySchemaOnAStreamingMediaType_TheItemsAreTheItem()
    {
        var item = new OpenApiSchema { Title = "Event" };
        var operation = BuildAnnotatedOperation(
            "application/jsonl",
            new OpenApiSchema { Type = JsonSchemaType.Array, Items = item },
            annotated: true);

        Assert.Same(item, operation.GetStreamingItemSchema());
    }

    [Fact]
    public void GetStreamingResponse_SchemaOnAStreamingMediaTypeWithoutAnnotation_IsNotAStream()
    {
        var operation = BuildAnnotatedOperation("text/event-stream", new OpenApiSchema { Title = "Event" }, annotated: false);

        Assert.Null(operation.GetStreamingResponse());
        Assert.False(operation.IsStreamingResponse());
    }

    // ========== IsStringSchema Tests ==========
    [Fact]
    public void IsStringSchema_PlainString_ReturnsTrue()
        => Assert.True(OpenApiOperationExtensions.IsStringSchema(new OpenApiSchema { Type = JsonSchemaType.String }));

    [Fact]
    public void IsStringSchema_NullableString_ReturnsTrue()
        => Assert.True(OpenApiOperationExtensions.IsStringSchema(new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null }));

    [Fact]
    public void IsStringSchema_StringOrInteger_ReturnsFalse()
        => Assert.False(OpenApiOperationExtensions.IsStringSchema(new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Integer }));

    [Fact]
    public void IsStringSchema_IntegerOrNull_ReturnsFalse()
    {
        Assert.False(OpenApiOperationExtensions.IsStringSchema(new OpenApiSchema { Type = JsonSchemaType.Integer }));
        Assert.False(OpenApiOperationExtensions.IsStringSchema(new OpenApiSchema()));
        Assert.False(OpenApiOperationExtensions.IsStringSchema(schema: null));
    }

    // ========== GetJsonRequestBodyMediaType Tests ==========
    [Fact]
    public void GetJsonRequestBodyMediaType_ApplicationJson_ReturnsIt()
        => Assert.Equal("application/json", CreateOperationWithJsonBody().GetJsonRequestBodyMediaType());

    [Fact]
    public void GetJsonRequestBodyMediaType_OnlyJsonSuffixTypes_ReturnsFirstByName()
    {
        var op = new OpenApiOperation
        {
            RequestBody = new OpenApiRequestBody
            {
                Content = new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal)
                {
                    ["application/vnd.x+json; charset=utf-8"] = new OpenApiMediaType { Schema = new OpenApiSchema() },
                    ["application/merge-patch+json"] = new OpenApiMediaType { Schema = new OpenApiSchema() },
                },
            },
        };

        Assert.Equal("application/merge-patch+json", op.GetJsonRequestBodyMediaType());
    }

    [Fact]
    public void GetJsonRequestBodyMediaType_NoJson_ReturnsNull()
    {
        Assert.Null(CreateOperationWithContentType("application/xml").GetJsonRequestBodyMediaType());
        Assert.Null(new OpenApiOperation().GetJsonRequestBodyMediaType());
    }

    // ========== GetParametersByLocation Tests ==========
    [Fact]
    public void GetParametersByLocation_ParsedOperation_FiltersInlineAndReferencedParameters()
    {
        var operation = ParsePetsOperation(YamlWithMixedParameters);

        var header = operation.GetParametersByLocation(ParameterLocation.Header).Select(p => p.Name).ToList();
        var query = operation.GetParametersByLocation(ParameterLocation.Query).Select(p => p.Name).ToList();
        var cookie = operation.GetParametersByLocation(ParameterLocation.Cookie).Select(p => p.Name).ToList();
        var path = operation.GetParametersByLocation(ParameterLocation.Path).Select(p => p.Name).ToList();

        Assert.Equal(["X-Tenant"], header);
        Assert.Equal(["page", "size"], query);
        Assert.Equal(["session"], cookie);
        Assert.Empty(path);
    }

    [Fact]
    public void GetParametersByLocation_NoParameters_ReturnsEmpty()
        => Assert.Empty(new OpenApiOperation().GetParametersByLocation(ParameterLocation.Query));

    // ========== GetFileUploadContentType Tests ==========
    [Theory]
    [InlineData("multipart/form-data", "multipart/form-data")]
    [InlineData("multipart/form-data; boundary=x", "multipart/form-data; boundary=x")]
    [InlineData("application/octet-stream", "application/octet-stream")]
    [InlineData("image/png", "image/png")]
    [InlineData("application/json", null)]
    [InlineData("application/x-www-form-urlencoded", null)]
    public void GetFileUploadContentType_ReturnsTheUploadMediaTypeOrNull(
        string contentType,
        string? expected)
        => Assert.Equal(expected, CreateOperationWithContentType(contentType).GetFileUploadContentType());

    [Fact]
    public void GetFileUploadContentType_NoBody_ReturnsNull()
        => Assert.Null(new OpenApiOperation().GetFileUploadContentType());

    // ========== GetResponseCardinalityAnnotation Tests ==========
    [Theory]
    [InlineData("single", "single")]
    [InlineData("ARRAY", "array")]
    [InlineData("Single", "single")]
    [InlineData("many", null)]
    [InlineData("", null)]
    public void GetResponseCardinalityAnnotation_NormalizesRecognizedValues(
        string value,
        string? expected)
    {
        var op = new OpenApiOperation
        {
            Extensions = new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal)
            {
                ["x-operation-response-cardinality"] = new JsonNodeExtension(JsonValue.Create(value)),
            },
        };

        Assert.Equal(expected, op.GetResponseCardinalityAnnotation());
    }

    [Fact]
    public void GetResponseCardinalityAnnotation_Absent_ReturnsNull()
        => Assert.Null(new OpenApiOperation().GetResponseCardinalityAnnotation());

    [Fact]
    public void GetResponseCardinalityAnnotation_NonStringValue_ReturnsNull()
    {
        var op = new OpenApiOperation
        {
            Extensions = new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal)
            {
                ["x-operation-response-cardinality"] = new JsonNodeExtension(JsonValue.Create(true)),
            },
        };

        Assert.Null(op.GetResponseCardinalityAnnotation());
    }

    // ========== IsStreamedDownloadOperation Tests ==========
    [Fact]
    public void IsStreamedDownloadOperation_FileDownloadWithReturnStream_ReturnsTrue()
    {
        var op = CreateOperationWithResponseContentType("200", "application/pdf");
        op.Extensions = new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal)
        {
            ["x-return-stream"] = new JsonNodeExtension(JsonValue.Create(true)),
        };

        Assert.True(op.IsStreamedDownloadOperation());
    }

    [Fact]
    public void IsStreamedDownloadOperation_FileDownloadWithoutAnnotation_ReturnsFalse()
        => Assert.False(CreateOperationWithResponseContentType("200", "application/pdf").IsStreamedDownloadOperation());

    [Fact]
    public void IsStreamedDownloadOperation_JsonResponseWithAnnotation_ReturnsFalse()
    {
        var op = CreateOperationWithResponse("200");
        op.Extensions = new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal)
        {
            ["x-return-stream"] = new JsonNodeExtension(JsonValue.Create(true)),
        };

        Assert.False(op.IsStreamedDownloadOperation());
    }

    [Fact]
    public void IsStreamedDownloadOperation_AnnotationFalse_ReturnsFalse()
    {
        var op = CreateOperationWithResponseContentType("200", "application/pdf");
        op.Extensions = new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal)
        {
            ["x-return-stream"] = new JsonNodeExtension(JsonValue.Create(false)),
        };

        Assert.False(op.IsStreamedDownloadOperation());
    }

    // ========== IsAsyncEnumerableOperation Tests ==========
    [Fact]
    public void IsAsyncEnumerableOperation_AnnotationTrue_ReturnsTrue()
        => Assert.True(BuildAsyncEnumerableAnnotatedOperation().IsAsyncEnumerableOperation());

    [Fact]
    public void IsAsyncEnumerableOperation_AnnotationFalse_ReturnsFalse()
        => Assert.False(BuildAnnotatedOperation("application/json", new OpenApiSchema(), annotated: false).IsAsyncEnumerableOperation());

    [Fact]
    public void IsAsyncEnumerableOperation_NoExtensions_ReturnsFalse()
        => Assert.False(new OpenApiOperation().IsAsyncEnumerableOperation());

    [Fact]
    public void IsAsyncEnumerableOperation_ParsedAnnotation_ReturnsTrue()
    {
        var operation = ParsePetsOperation(YamlWithAsyncEnumerableAnnotation);

        Assert.True(operation.IsAsyncEnumerableOperation());
    }

    // ========== GetPaginationTokenProperty Tests ==========
    [Theory]
    [InlineData("continuationToken", "continuationToken")]
    [InlineData("NextPageToken", "NextPageToken")]
    [InlineData("cursor", "cursor")]
    [InlineData("offset", null)]
    public void GetPaginationTokenProperty_DirectProperty_ReturnsDeclaredName(
        string propertyName,
        string? expected)
    {
        var operation = ParsePetsOperation(YamlWithPagedResponseProperty(propertyName, "string"));

        Assert.Equal(expected, operation.GetPaginationTokenProperty());
    }

    [Fact]
    public void GetPaginationTokenProperty_NonStringToken_ReturnsNull()
    {
        var operation = ParsePetsOperation(YamlWithPagedResponseProperty("continuationToken", "integer"));

        Assert.Null(operation.GetPaginationTokenProperty());
    }

    [Fact]
    public void GetPaginationTokenProperty_TokenInAllOfPart_ReturnsIt()
    {
        var operation = ParsePetsOperation(YamlWithPaginationWrapper);

        Assert.Equal("continuationToken", operation.GetPaginationTokenProperty());
    }

    [Fact]
    public void GetPaginationTokenProperty_PrefersContinuationTokenOverCursor()
    {
        var operation = ParsePetsOperation(YamlWithTwoTokenCandidates);

        Assert.Equal("continuationToken", operation.GetPaginationTokenProperty());
    }

    [Fact]
    public void GetPaginationTokenProperty_StreamingResponse_ReturnsNull()
        => Assert.Null(BuildStreamingOperation("application/jsonl").GetPaginationTokenProperty());

    [Fact]
    public void GetPaginationTokenProperty_NoResponseSchema_ReturnsNull()
        => Assert.Null(new OpenApiOperation().GetPaginationTokenProperty());

    // ========== GetFirstTag Tests ==========
    [Fact]
    public void GetFirstTag_ParsedTags_ReturnsFirstInPascalCase()
    {
        var operation = ParsePetsOperation(YamlWithTags);

        Assert.Equal("PetStore", operation.GetFirstTag());
    }

    [Fact]
    public void GetFirstTag_NoTags_ReturnsEmpty()
        => Assert.Equal(string.Empty, new OpenApiOperation().GetFirstTag());

    // ========== GetModelOrFileMediaTypes Tests ==========
    [Fact]
    public void GetModelOrFileMediaTypes_JsonAndPdf_ReturnsThePdf()
    {
        var op = new OpenApiOperation
        {
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse
                {
                    Content = new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal)
                    {
                        ["application/json"] = new OpenApiMediaType { Schema = new OpenApiSchema { Type = JsonSchemaType.Object } },
                        ["application/pdf"] = new OpenApiMediaType { Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" } },
                    },
                },
            },
        };

        Assert.Equal(["application/pdf"], op.GetModelOrFileMediaTypes());
    }

    [Fact]
    public void GetModelOrFileMediaTypes_FileOnly_ReturnsEmpty()
        => Assert.Empty(CreateOperationWithResponseContentType("200", "application/pdf").GetModelOrFileMediaTypes());

    [Fact]
    public void GetModelOrFileMediaTypes_No200Response_ReturnsEmpty()
        => Assert.Empty(CreateOperationWithResponse("201").GetModelOrFileMediaTypes());

    // ========== GetFileDownloadContentType Tests ==========
    [Theory]
    [InlineData("application/pdf", "application/pdf")]
    [InlineData("application/octet-stream", "application/octet-stream")]
    [InlineData("image/png", "image/png")]
    [InlineData("application/json", null)]
    [InlineData("text/plain", null)]
    public void GetFileDownloadContentType_ReturnsTheBinaryMediaTypeOrNull(
        string contentType,
        string? expected)
        => Assert.Equal(expected, CreateOperationWithResponseContentType("200", contentType).GetFileDownloadContentType());

    [Fact]
    public void GetFileDownloadContentType_No200Response_ReturnsNull()
    {
        Assert.Null(CreateOperationWithResponseContentType("201", "application/pdf").GetFileDownloadContentType());
        Assert.Null(new OpenApiOperation().GetFileDownloadContentType());
    }

    // ========== IsRawFileUploadContentType Tests ==========
    [Theory]
    [InlineData("application/octet-stream", true)]
    [InlineData("application/pdf; version=1.7", true)]
    [InlineData("image/png", true)]
    [InlineData("multipart/form-data", false)]
    [InlineData("multipart/mixed", false)]
    [InlineData("application/x-www-form-urlencoded", false)]
    [InlineData("application/json", false)]
    [InlineData("text/plain", false)]
    [InlineData(null, false)]
    public void IsRawFileUploadContentType_ReturnsExpectedResult(
        string? contentType,
        bool expected)
        => Assert.Equal(expected, OpenApiOperationExtensions.IsRawFileUploadContentType(contentType));

    // ========== Streaming (OpenAPI 3.2 itemSchema) Tests ==========
    private static OpenApiOperation CreateOperationWithItemSchema(
        string contentType)
        => new()
        {
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse
                {
                    Content = new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal)
                    {
                        [contentType] = new OpenApiMediaType { ItemSchema = new OpenApiSchema { Title = "Event" } },
                    },
                },
            },
        };

    // ========== Helper Methods ==========
    private static OpenApiOperation ParsePetsOperation(string yaml)
        => OpenApiDocumentHelper.ParseYaml(yaml).Paths["/pets"].Operations!.Values.First();

    private const string YamlWithMixedParameters = """
        openapi: 3.0.0
        info:
          title: Test API
          version: 1.0.0
        paths:
          /pets:
            get:
              operationId: getPets
              parameters:
                - name: X-Tenant
                  in: header
                  schema:
                    type: string
                - name: page
                  in: query
                  schema:
                    type: integer
                - $ref: '#/components/parameters/Size'
                - name: session
                  in: cookie
                  schema:
                    type: string
              responses:
                '200':
                  description: OK
        components:
          parameters:
            Size:
              name: size
              in: query
              schema:
                type: integer
        """;

    private const string YamlWithAsyncEnumerableAnnotation = """
        openapi: 3.0.0
        info:
          title: Test API
          version: 1.0.0
        paths:
          /pets:
            get:
              operationId: getPets
              x-return-async-enumerable: true
              responses:
                '200':
                  description: OK
        """;

    private const string YamlWithTags = """
        openapi: 3.0.0
        info:
          title: Test API
          version: 1.0.0
        paths:
          /pets:
            get:
              operationId: getPets
              tags:
                - pet-store
                - admin
              responses:
                '200':
                  description: OK
        """;

    private static string YamlWithPagedResponseProperty(
        string propertyName,
        string propertyType)
        => $"""
        openapi: 3.0.0
        info:
          title: Test API
          version: 1.0.0
        paths:
          /pets:
            get:
              operationId: getPets
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        type: object
                        properties:
                          items:
                            type: array
                            items:
                              type: string
                          {propertyName}:
                            type: {propertyType}
        """;

    private const string YamlWithPaginationWrapper = """
        openapi: 3.0.0
        info:
          title: Test API
          version: 1.0.0
        paths:
          /pets:
            get:
              operationId: getPets
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        allOf:
                          - $ref: '#/components/schemas/PaginatedResult'
                          - type: object
                            properties:
                              items:
                                type: array
                                items:
                                  type: string
        components:
          schemas:
            PaginatedResult:
              type: object
              properties:
                continuationToken:
                  type: string
                  nullable: true
        """;

    private const string YamlWithTwoTokenCandidates = """
        openapi: 3.0.0
        info:
          title: Test API
          version: 1.0.0
        paths:
          /pets:
            get:
              operationId: getPets
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        type: object
                        properties:
                          cursor:
                            type: string
                          continuationToken:
                            type: string
        """;

    private static OpenApiOperation BuildAnnotatedOperation(
        string mediaType,
        OpenApiSchema schema,
        bool annotated)
        => new()
        {
            Extensions = new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal)
            {
                ["x-return-async-enumerable"] = new JsonNodeExtension(JsonValue.Create(annotated)),
            },
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse
                {
                    Content = new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal)
                    {
                        [mediaType] = new OpenApiMediaType { Schema = schema },
                    },
                },
            },
        };

    private static OpenApiOperation BuildStreamingOperation(string mediaType)
        => new()
        {
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse
                {
                    Content = new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal)
                    {
                        [mediaType] = new OpenApiMediaType { ItemSchema = new OpenApiSchema { Title = "Event" } },
                    },
                },
            },
        };

    private static OpenApiOperation BuildAsyncEnumerableAnnotatedOperation()
        => new()
        {
            Extensions = new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal)
            {
                ["x-return-async-enumerable"] = new JsonNodeExtension(JsonValue.Create(true)),
            },
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse
                {
                    Content = new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal)
                    {
                        ["application/json"] = new OpenApiMediaType { Schema = new OpenApiSchema() },
                    },
                },
            },
        };

    private static OpenApiOperation CreateOperationWithJsonBody()
        => new()
        {
            RequestBody = new OpenApiRequestBody
            {
                Content = new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal)
                {
                    ["application/json"] = new OpenApiMediaType { Schema = new OpenApiSchema() },
                },
            },
        };

    private static OpenApiOperation CreateOperationWithContentType(
        string contentType)
        => new()
        {
            RequestBody = new OpenApiRequestBody
            {
                Content = new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal)
                {
                    [contentType] = new OpenApiMediaType { Schema = new OpenApiSchema() },
                },
            },
        };

    private static OpenApiOperation CreateOperationWithResponse(
        string statusCode)
        => new()
        {
            Responses = new OpenApiResponses
            {
                [statusCode] = new OpenApiResponse
                {
                    Content = new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal)
                    {
                        ["application/json"] = new OpenApiMediaType { Schema = new OpenApiSchema() },
                    },
                },
            },
        };

    private static OpenApiOperation CreateOperationWithResponseContentType(
        string statusCode,
        string contentType)
        => new()
        {
            Responses = new OpenApiResponses
            {
                [statusCode] = new OpenApiResponse
                {
                    Content = new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal)
                    {
                        [contentType] = new OpenApiMediaType { Schema = new OpenApiSchema() },
                    },
                },
            },
        };
}