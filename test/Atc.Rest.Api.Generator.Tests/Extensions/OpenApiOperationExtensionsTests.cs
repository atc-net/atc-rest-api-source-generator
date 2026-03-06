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