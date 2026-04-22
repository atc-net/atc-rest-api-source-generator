namespace Atc.Rest.Api.Generator.Cli.Tests.Extractors.TypeScript;

public class TypeScriptOperationHelperTests
{
    [Fact]
    public void GetReturnType_TextPlainResponseTypeString_ReturnsString()
    {
        // Text-only response (no application/json) — return type must fall back to the
        // textual schema so the generated client surfaces the body as a real string,
        // not Promise<ApiResult<void>>.
        var op = new OpenApiOperation
        {
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse
                {
                    Content = new Dictionary<string, IOpenApiMediaType>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["text/plain"] = new OpenApiMediaType
                        {
                            Schema = new OpenApiSchema { Type = JsonSchemaType.String },
                        },
                    },
                },
            },
        };

        var result = TypeScriptOperationHelper.GetReturnType(op, isStreaming: false, isFileDownload: false);

        Assert.Equal("string", result);
    }

    [Fact]
    public void GetReturnType_TextCsvResponseTypeString_ReturnsString()
    {
        var op = new OpenApiOperation
        {
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse
                {
                    Content = new Dictionary<string, IOpenApiMediaType>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["text/csv"] = new OpenApiMediaType
                        {
                            Schema = new OpenApiSchema { Type = JsonSchemaType.String },
                        },
                    },
                },
            },
        };

        var result = TypeScriptOperationHelper.GetReturnType(op, isStreaming: false, isFileDownload: false);

        Assert.Equal("string", result);
    }

    [Fact]
    public void GetReturnType_FileDownload_ReturnsBlobIgnoringTextFallback()
    {
        // Even if a textual response is somehow declared, the explicit isFileDownload signal
        // wins — file-download operations always surface as Blob in TS.
        var op = new OpenApiOperation();

        var result = TypeScriptOperationHelper.GetReturnType(op, isStreaming: false, isFileDownload: true);

        Assert.Equal("Blob", result);
    }

    [Fact]
    public void GetReturnType_NoResponse_ReturnsVoid()
    {
        var op = new OpenApiOperation();

        var result = TypeScriptOperationHelper.GetReturnType(op, isStreaming: false, isFileDownload: false);

        Assert.Equal("void", result);
    }

    [Fact]
    public void GetReturnType_NoResponseStreaming_ReturnsUnknown()
    {
        var op = new OpenApiOperation();

        var result = TypeScriptOperationHelper.GetReturnType(op, isStreaming: true, isFileDownload: false);

        Assert.Equal("unknown", result);
    }
}