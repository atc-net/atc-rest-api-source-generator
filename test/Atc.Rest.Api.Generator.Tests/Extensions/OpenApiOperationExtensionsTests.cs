namespace Atc.Rest.Api.Generator.Tests.Extensions;

public class OpenApiOperationExtensionsTests
{
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

    // ========== Helper Methods ==========
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