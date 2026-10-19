namespace Atc.Rest.Api.Generator.Cli.Tests.Extractors.TypeScript;

public class TypeScriptResponseHeadersTests
{
    private static readonly TypeScriptResponseHeaderOptions Options = new(ConvertDates: false, TypeScriptNamingStrategy.CamelCase, BrandedIds: false);

    public static TheoryData<int> StatusCodes()
    {
        var codes = new TheoryData<int>();
        for (var code = 200; code < 600; code++)
        {
            codes.Add(code);
        }

        return codes;
    }

    [Theory]
    [MemberData(nameof(StatusCodes))]
    public void BuildPerOperationResultType_AHeaderOfAnyStatus_IsTypedOnTheArmTheStatusIsReadAs(
        int statusCode)
    {
        var operation = OperationOf($$"""
                                      '{{statusCode}}':
                                        description: The answer.
                                        headers:
                                          X-Probe:
                                            schema:
                                              type: integer
                                    """);

        var (declaration, _, _, readers) = TypeScriptOperationHelper.BuildPerOperationResultType(
            operation,
            "ProbeResult",
            isFileDownload: false,
            isTextDownload: false,
            TypeScriptHttpClient.Fetch,
            responseHeaders: Options);

        var arm = ArmOf(statusCode);
        if (arm is null)
        {
            // A status without an arm of its own: nothing reads its headers
            Assert.Null(readers);
            Assert.DoesNotContain("headers:", declaration, StringComparison.Ordinal);
            return;
        }

        var typeName = "Probe" + char.ToUpperInvariant(arm[0]) + arm[1..] + "Headers";
        Assert.Contains($"export interface {typeName} {{\n  'X-Probe'?: number;\n}}", declaration.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Contains($"status: '{arm}';", declaration, StringComparison.Ordinal);
        Assert.Single(declaration.Split('\n'), line => line.Contains("; headers: ", StringComparison.Ordinal));
        Assert.Single(declaration.Split('\n'), line => line.Contains($"status: '{arm}';", StringComparison.Ordinal) && line.Contains($"; headers: {typeName}; ", StringComparison.Ordinal));

        Assert.NotNull(readers);
        Assert.Equal(
            [
                $"{arm}: (header): {typeName} => ({{",
                "  'X-Probe': header.value('X-Probe', headerValue.integer),",
                "}),",
            ],
            readers);
    }

    [Fact]
    public void BuildPerOperationResultType_DefaultHeaders_AreTheHeadersOfTheArmsDefaultStandsFor()
    {
        var operation = OperationOf("""
                                      '200':
                                        description: The answer.
                                      '404':
                                        description: Not found, without headers.
                                      default:
                                        description: Any other error.
                                        headers:
                                          X-Trace:
                                            schema:
                                              type: string
                                    """);

        var (declaration, _, _, readers) = TypeScriptOperationHelper.BuildPerOperationResultType(
            operation,
            "ProbeResult",
            isFileDownload: false,
            isTextDownload: false,
            TypeScriptHttpClient.Fetch,
            responseHeaders: Options);

        var lines = declaration.Split('\n');
        foreach (var arm in (string[])["unauthorized", "forbidden", "conflict", "unprocessableEntity", "tooManyRequests", "clientError", "serverError"])
        {
            Assert.Single(lines, line => line.Contains($"status: '{arm}';", StringComparison.Ordinal) && line.Contains("; headers: ", StringComparison.Ordinal));
        }

        // A declared status keeps its own (no) headers, the validation 400 is the server's, and a success is no default
        foreach (var arm in (string[])["ok", "notFound", "badRequest", "parseError"])
        {
            Assert.Single(lines, line => line.Contains($"status: '{arm}';", StringComparison.Ordinal) && !line.Contains("; headers: ", StringComparison.Ordinal));
        }

        Assert.NotNull(readers);
        Assert.Contains("serverError: (header): ProbeServerErrorHeaders => ({", readers, StringComparer.Ordinal);
        Assert.Contains("  'X-Trace': header.text('X-Trace'),", readers, StringComparer.Ordinal);
    }

    [Fact]
    public void BuildPerOperationResultType_NoDeclaredHeaders_KeepsTheUnionAsItWas()
    {
        var operation = OperationOf("""
                                      '200':
                                        description: The answer.
                                      '503':
                                        description: Unavailable.
                                    """);

        var (withOptions, _, _, readers) = TypeScriptOperationHelper.BuildPerOperationResultType(operation, "ProbeResult", isFileDownload: false, isTextDownload: false, TypeScriptHttpClient.Fetch, responseHeaders: Options);
        var (without, _, _, _) = TypeScriptOperationHelper.BuildPerOperationResultType(operation, "ProbeResult", isFileDownload: false, isTextDownload: false, TypeScriptHttpClient.Fetch);

        Assert.Null(readers);
        Assert.Equal(without, withOptions);
    }

    // The arm the ApiClient returns for a status (handleResponse), when the union declares an arm for it
    private static string? ArmOf(int statusCode)
        => statusCode switch
        {
            200 or 203 or 206 or 207 or 208 or 226 => "ok",
            201 => "created",
            202 => "accepted",
            204 or 205 => "noContent",
            300 or 301 or 302 or 303 or 305 or 306 or 307 or 308 => "redirection",
            304 => "notModified",
            400 => "badRequest",
            401 => "unauthorized",
            403 => "forbidden",
            404 => "notFound",
            409 => "conflict",
            422 => "unprocessableEntity",
            429 => "tooManyRequests",
            >= 400 and < 500 => "clientError",
            >= 500 and < 600 => "serverError",
            _ => null,
        };

    private static OpenApiOperation OperationOf(string responses)
    {
        var yaml = """
                   openapi: 3.1.0
                   info:
                     title: Probe
                     version: 1.0.0
                   paths:
                     /probe:
                       get:
                         operationId: probe
                         responses:

                   """ + string.Join('\n', responses.Split('\n').Select(line => "          " + line));

        Assert.True(OpenApiDocumentHelper.TryParseYaml(yaml, "Probe.yaml", out var document));
        return ((OpenApiPathItem)document!.Paths["/probe"]).Operations!.Values.First();
    }
}