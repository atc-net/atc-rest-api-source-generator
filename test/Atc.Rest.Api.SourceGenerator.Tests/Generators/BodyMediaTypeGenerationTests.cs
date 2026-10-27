namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// Every generator decides "which body" with the same rule (<c>ResponseBodyClassifier</c>): a file download is a file
/// on the server and in both C# clients for every binary media type (PDF, Office documents, images, octet-stream,
/// any unknown type) - not only for the few each generator used to list - and a response that declares several
/// media types is read as its first one everywhere.
/// </summary>
public sealed class BodyMediaTypeGenerationTests
{
    internal const string Yaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /reports/{id}/pdf:
            get:
              operationId: getReportPdf
              parameters:
                - name: id
                  in: path
                  required: true
                  schema:
                    type: integer
              responses:
                '200':
                  description: A PDF
                  content:
                    application/pdf:
                      schema:
                        type: string
                        format: binary
                '404':
                  description: Not found
          /reports/{id}/excel:
            get:
              operationId: getReportExcel
              parameters:
                - name: id
                  in: path
                  required: true
                  schema:
                    type: integer
              responses:
                '200':
                  description: An Excel workbook
                  content:
                    application/vnd.openxmlformats-officedocument.spreadsheetml.sheet:
                      schema:
                        type: string
                        format: binary
                '404':
                  description: Not found
          /reports/{id}/custom:
            get:
              operationId: getReportCustom
              parameters:
                - name: id
                  in: path
                  required: true
                  schema:
                    type: integer
              responses:
                '200':
                  description: A vendor format nobody lists
                  content:
                    application/x-vendor-report: {}
          /reports/{id}/any:
            get:
              operationId: getReportAny
              parameters:
                - name: id
                  in: path
                  required: true
                  schema:
                    type: integer
              responses:
                '200':
                  description: JSON or PDF
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Report'
                    application/pdf:
                      schema:
                        type: string
                        format: binary
        components:
          schemas:
            Report:
              type: object
              properties:
                title:
                  type: string
        """;

    /// <summary>
    /// Runs the generated download results against an <c>HttpContext</c>: the content type defaults to the declared
    /// media type, no file name is an inline file, a file name an attachment, and a stream is written as-is.
    /// </summary>
    private const string DownloadProbeSource = """"
        using Microsoft.AspNetCore.Http;
        using Microsoft.Extensions.DependencyInjection;
        using ErrorBodies.Generated.Reports.Results;

        namespace ErrorBodies.RuntimeTest;

        public static class DownloadProbe
        {
            public static async Task<List<string>> RunAsync()
            {
                var rows = new List<string>();
                var pdf = new byte[] { 0x25, 0x50, 0x44, 0x46 };
                rows.Add(await DescribeAsync("pdf bytes", GetReportPdfResult.Ok(pdf)));
                rows.Add(await DescribeAsync("pdf bytes named", GetReportPdfResult.Ok(pdf, fileName: "report.pdf")));
                rows.Add(await DescribeAsync("pdf stream named", GetReportPdfResult.Ok(new MemoryStream(pdf), fileName: "report.pdf")));
                rows.Add(await DescribeAsync("excel bytes", GetReportExcelResult.Ok([1, 2, 3])));
                rows.Add(await DescribeAsync("custom bytes other type", GetReportCustomResult.Ok([9], contentType: "application/x-other")));
                return rows;
            }

            private static async Task<string> DescribeAsync(string name, IResult result)
            {
                var context = new DefaultHttpContext
                {
                    RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider(),
                };
                var body = new MemoryStream();
                context.Response.Body = body;
                await result.ExecuteAsync(context);
                return $"{name}: {context.Response.StatusCode} {context.Response.ContentType} | {context.Response.Headers.ContentDisposition} | {Convert.ToHexString(body.ToArray())}";
            }
        }
        """";

    /// <summary>
    /// Calls the EndpointPerOperation download of a PDF against a stub: a PDF with a non-ASCII file name, a 404 as
    /// application/problem+json, and a proxy's HTML 502. Found by reflection (no namespace assumptions).
    /// </summary>
    private const string EpoDownloadProbeSource = """"
        using System.Net;
        using System.Text;
        using Atc.Rest.Client;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public static class EpoDownloadProbe
        {
            public static async Task<List<string>> RunAsync()
            {
                var assembly = typeof(EpoDownloadProbe).Assembly;
                var constants = assembly.GetTypes().Single(t => t.Name == "Constants");
                var httpClientName = (string)constants.GetField("HttpClientName")!.GetValue(null)!;
                var services = new ServiceCollection();
                services
                    .AddHttpClient(httpClientName, c => c.BaseAddress = new Uri("http://stub/"))
                    .ConfigurePrimaryHttpMessageHandler(() => new StubHandler());
                var register = assembly.GetTypes().SelectMany(t => t.GetMethods()).Single(m => m.Name == "AddErrorBodiesEndpoints");
                register.Invoke(null, [services, null]);
                using var provider = services.BuildServiceProvider();

                var endpointType = assembly.GetTypes().Single(t => t.Name == "IGetReportPdfEndpoint");
                var parametersType = assembly.GetTypes().Single(t => t.Name == "GetReportPdfParameters");
                var endpoint = provider.GetRequiredService(endpointType);
                var execute = endpointType.GetMethod("ExecuteAsync")!;

                var rows = new List<string>();
                foreach (var id in new[] { 1, 404, 502 })
                {
                    var task = (Task)execute.Invoke(endpoint, [Activator.CreateInstance(parametersType, id), httpClientName, CancellationToken.None])!;
                    await task;
                    var result = (EndpointResponse)task.GetType().GetProperty("Result")!.GetValue(task)!;
                    rows.Add(Describe(id, result));
                }

                return rows;
            }

            private static string Describe(int id, EndpointResponse result)
            {
                var type = result.GetType();
                string Read(string property)
                {
                    try
                    {
                        var value = type.GetProperty(property)!.GetValue(result);
                        return value switch
                        {
                            null => "null",
                            _ when value.GetType().Name == "DownloadedFile" => $"file {Convert.ToHexString((byte[])value.GetType().GetProperty("Content")!.GetValue(value)!)} {value.GetType().GetProperty("ContentType")!.GetValue(value)} {value.GetType().GetProperty("FileName")!.GetValue(value)}",
                            _ => $"{value.GetType().Name} {System.Text.Json.JsonSerializer.Serialize(value, value.GetType())}",
                        };
                    }
                    catch (System.Reflection.TargetInvocationException ex)
                    {
                        return $"THROWS {ex.InnerException!.GetType().Name}";
                    }
                }

                return id switch
                {
                    1 => $"{(int)result.StatusCode} IsOk={type.GetProperty("IsOk")!.GetValue(result)} OkContent={Read("OkContent")}",
                    404 => $"{(int)result.StatusCode} IsNotFound={type.GetProperty("IsNotFound")!.GetValue(result)} NotFoundContent={Read("NotFoundContent")}",
                    _ => $"{(int)result.StatusCode} IsSuccess={result.IsSuccess} Content={result.Content}",
                };
            }

            private sealed class StubHandler : HttpMessageHandler
            {
                protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    var id = request.RequestUri!.AbsolutePath.Split('/')[2];
                    HttpResponseMessage response = id switch
                    {
                        "1" => new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new ByteArrayContent([0x25, 0x50, 0x44, 0x46]),
                        },
                        "404" => new HttpResponseMessage(HttpStatusCode.NotFound)
                        {
                            Content = new StringContent("""{"title":"Not Found","status":404,"detail":"No report 404"}""", Encoding.UTF8, "application/problem+json"),
                        },
                        _ => new HttpResponseMessage(HttpStatusCode.BadGateway)
                        {
                            Content = new StringContent("<html>502 Bad Gateway</html>", Encoding.UTF8, "text/html"),
                        },
                    };

                    if (id == "1")
                    {
                        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
                        response.Content.Headers.TryAddWithoutValidation("Content-Disposition", "attachment; filename=\"Rapport-_.pdf\"; filename*=UTF-8''Rapport-%C3%85.pdf");
                    }

                    return Task.FromResult(response);
                }
            }
        }
        """";

    /// <summary>
    /// A PDF download that opts into streaming (<c>x-return-stream: true</c>).
    /// </summary>
    private const string StreamedYaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /reports/{id}/pdf:
            get:
              operationId: getReportPdf
              x-return-stream: true
              parameters:
                - name: id
                  in: path
                  required: true
                  schema:
                    type: integer
              responses:
                '200':
                  description: A PDF
                  content:
                    application/pdf:
                      schema:
                        type: string
                        format: binary
                '404':
                  description: Not found
        """;

    /// <summary>
    /// Calls the streamed download against a stub and reads the stream only after the call returned (a disposed
    /// response would have closed it).
    /// </summary>
    private const string StreamedProbeSource = """"
        using System.Net;
        using System.Text;
        using Atc.Rest.Client;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public static class StreamedProbe
        {
            public static async Task<List<string>> RunAsync()
            {
                var assembly = typeof(StreamedProbe).Assembly;
                var httpClientName = (string)assembly.GetTypes().Single(t => t.Name == "Constants").GetField("HttpClientName")!.GetValue(null)!;
                var services = new ServiceCollection();
                services
                    .AddHttpClient(httpClientName, c => c.BaseAddress = new Uri("http://stub/"))
                    .ConfigurePrimaryHttpMessageHandler(() => new StubHandler());
                assembly.GetTypes().SelectMany(t => t.GetMethods()).Single(m => m.Name == "AddErrorBodiesEndpoints").Invoke(null, [services, null]);
                using var provider = services.BuildServiceProvider();

                var endpointType = assembly.GetTypes().Single(t => t.Name == "IGetReportPdfEndpoint");
                var parametersType = assembly.GetTypes().Single(t => t.Name == "GetReportPdfParameters");
                var endpoint = provider.GetRequiredService(endpointType);
                var execute = endpointType.GetMethod("ExecuteAsync")!;

                var rows = new List<string>();
                foreach (var id in new[] { 1, 404 })
                {
                    var task = (Task)execute.Invoke(endpoint, [Activator.CreateInstance(parametersType, id), httpClientName, CancellationToken.None])!;
                    await task;
                    using var result = (StreamBinaryEndpointResponse)task.GetType().GetProperty("Result")!.GetValue(task)!;
                    var bytes = new MemoryStream();
                    if (result.Content is not null)
                    {
                        await result.Content.CopyToAsync(bytes);
                    }

                    rows.Add(result.IsSuccess
                        ? $"{(int)result.StatusCode} IsSuccess=True {result.ContentType} {result.FileName} {Convert.ToHexString(bytes.ToArray())}"
                        : $"{(int)result.StatusCode} IsSuccess=False {result.ContentType} typed={result.ErrorContentObject?.GetType().Name}:{result.ErrorContentObject?.GetType().GetProperty("Title")?.GetValue(result.ErrorContentObject)} retry={string.Join(",", result.Headers.TryGetValue("Retry-After", out var retry) ? retry : [])} error={result.ErrorContent}");
                }

                return rows;
            }

            private sealed class StubHandler : HttpMessageHandler
            {
                protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    var id = request.RequestUri!.AbsolutePath.Split('/')[2];
                    if (id == "404")
                    {
                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
                        {
                            Content = new StringContent("""{"title":"Not Found","status":404}""", Encoding.UTF8, "application/problem+json"),
                            Headers = { { "Retry-After", "30" } },
                        });
                    }

                    var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new MemoryStream([0x25, 0x50, 0x44, 0x46])) };
                    response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
                    response.Content.Headers.TryAddWithoutValidation("Content-Disposition", "attachment; filename=\"report.pdf\"");
                    return Task.FromResult(response);
                }
            }
        }
        """";

    /// <summary>
    /// Calls the TypedClient download of a PDF (Throw or Result style) against a stub: a PDF with a non-ASCII file name,
    /// and a 404 as application/problem+json. The client is found by reflection (either granularity).
    /// </summary>
    private const string TypedDownloadProbeSource = """"
        using System.Net;
        using System.Text;

        namespace ErrorBodies.RuntimeTest;

        public static class TypedDownloadProbe
        {
            public static async Task<List<string>> RunAsync()
            {
                var assembly = typeof(TypedDownloadProbe).Assembly;
                var clientType = assembly.GetTypes().Single(t => t.IsClass && !t.IsAbstract && t.GetMethod("GetReportPdfAsync") is not null);
                var client = Activator.CreateInstance(clientType, new HttpClient(new StubHandler()) { BaseAddress = new Uri("http://stub/") })!;
                var method = clientType.GetMethod("GetReportPdfAsync")!;
                var parametersType = method.GetParameters()[0].ParameterType;

                var rows = new List<string>();
                foreach (var id in new[] { 1, 404 })
                {
                    try
                    {
                        var task = (Task)method.Invoke(client, [Activator.CreateInstance(parametersType, id), CancellationToken.None])!;
                        await task;
                        rows.Add(Describe(task.GetType().GetProperty("Result")!.GetValue(task)!));
                    }
                    catch (HttpRequestException ex)
                    {
                        rows.Add($"THROWS {(int?)ex.StatusCode}");
                    }
                }

                return rows;
            }

            private static string Describe(object value)
            {
                var type = value.GetType();
                if (type.Name == "DownloadedFile")
                {
                    return File(value);
                }

                // Result style: EndpointResponse<DownloadedFile>
                var isSuccess = (bool)type.GetProperty("IsSuccess")!.GetValue(value)!;
                return isSuccess
                    ? $"result {File(type.GetProperty("SuccessContent")!.GetValue(value)!)}"
                    : $"result IsSuccess=False {(int)(HttpStatusCode)type.GetProperty("StatusCode")!.GetValue(value)!} {type.GetProperty("Content")!.GetValue(value)}";
            }

            private static string File(object file)
            {
                var type = file.GetType();
                return $"file {Convert.ToHexString((byte[])type.GetProperty("Content")!.GetValue(file)!)} {type.GetProperty("ContentType")!.GetValue(file)} {type.GetProperty("FileName")!.GetValue(file)}";
            }

            private sealed class StubHandler : HttpMessageHandler
            {
                protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    var id = request.RequestUri!.AbsolutePath.Split('/')[2];
                    if (id == "404")
                    {
                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
                        {
                            Content = new StringContent("""{"title":"Not Found","status":404}""", Encoding.UTF8, "application/problem+json"),
                        });
                    }

                    var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([0x25, 0x50, 0x44, 0x46]) };
                    response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
                    response.Content.Headers.TryAddWithoutValidation("Content-Disposition", "attachment; filename=\"Rapport-_.pdf\"; filename*=UTF-8''Rapport-%C3%85.pdf");
                    return Task.FromResult(response);
                }
            }
        }
        """";

    private static readonly string[] BinaryOperations = ["GetReportPdf", "GetReportExcel", "GetReportCustom"];

    [Fact]
    public void Server_EveryBinaryMediaType_IsAFileDownload()
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", ErrorResponseBodyRuntimeTests.ServerMarker, includeMinimalApi: true, Yaml);
        var results = string.Join(Environment.NewLine, sources.Where(s => s.HintName.Contains("Results", StringComparison.Ordinal)).Select(s => s.Source));

        foreach (var operation in BinaryOperations)
        {
            Assert.Matches(new Regex($@"public static {operation}Result Ok\(\s*byte\[\]", RegexOptions.None, TimeSpan.FromSeconds(1)), results);
        }

        // JSON or PDF: the model or the file (ModelOrFileResponseTests runs both).
        Assert.Matches(new Regex(@"public static GetReportAnyResult Ok\(\s*Report ", RegexOptions.None, TimeSpan.FromSeconds(1)), results);
        Assert.Matches(new Regex(@"public static GetReportAnyResult Ok\(\s*byte\[\] bytes,\s*string\? contentType = null,\s*string\? fileName = null\)\s*=> new\(TypedResults\.File\(bytes, contentType \?\? ""application/pdf"", fileName\)\);", RegexOptions.None, TimeSpan.FromSeconds(1)), results);
        Assert.Empty(CompilationVerificationHarness.CompileGeneratedSources(sources, includeMinimalApi: true));
    }

    [Fact]
    public void EndpointPerOperationClient_EveryBinaryMediaType_IsAFileDownload()
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", ErrorResponseBodyRuntimeTests.ClientOperationMarker("ProblemDetails"), includeMinimalApi: false, Yaml);

        // Every binary media type is a file: a success without a JSON type, whose OkContent is the DownloadedFile.
        foreach (var operation in BinaryOperations)
        {
            var endpoint = sources.Single(s => s.HintName.EndsWith($".{operation}Endpoint.g.cs", StringComparison.Ordinal)).Source;
            Assert.Contains("responseBuilder.AddSuccessResponse(HttpStatusCode.OK);", endpoint, StringComparison.Ordinal);
            var result = sources.Single(s => s.HintName.EndsWith($".{operation}EndpointResult.g.cs", StringComparison.Ordinal) && !s.HintName.Contains(".Interfaces.", StringComparison.Ordinal)).Source;
            Assert.Contains("public DownloadedFile OkContent", result, StringComparison.Ordinal);
        }

        // JSON or PDF: the model, or the file by the answer's Content-Type
        var any = sources.Single(s => s.HintName.EndsWith(".GetReportAnyEndpoint.g.cs", StringComparison.Ordinal)).Source;
        Assert.Contains("AddSuccessResponse<Report>(HttpStatusCode.OK)", any, StringComparison.Ordinal);
        Assert.Contains("if (DownloadedFile.IsFileMediaType(response.Content.Headers.ContentType?.MediaType))", any, StringComparison.Ordinal);
        Assert.Empty(CompilationVerificationHarness.CompileGeneratedSources(sources));
    }

    [Fact]
    public void TypedClient_EveryBinaryMediaType_IsAFileDownload()
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", """{ "generationMode": "TypedClient" }""", includeMinimalApi: false, Yaml);
        var client = string.Join("\n", sources.Where(s => s.HintName.EndsWith("Client.g.cs", StringComparison.Ordinal)).Select(s => s.Source));

        foreach (var operation in BinaryOperations)
        {
            Assert.Contains($"Task<DownloadedFile> {operation}Async(", client, StringComparison.Ordinal);
        }

        Assert.Contains("Task<ContentOrFile<Report>> GetReportAnyAsync(", client, StringComparison.Ordinal);
        Assert.Empty(CompilationVerificationHarness.CompileGeneratedSources(sources));
    }

    [Fact]
    public async Task Server_DownloadFactories_DefaultTheDeclaredMediaType_AndStream()
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", ErrorResponseBodyRuntimeTests.ServerMarker, includeMinimalApi: true, Yaml);

        // The endpoint metadata names the media type (the runtime OpenAPI document matches the specification): as a
        // Stream, a binary body - without a type the endpoint metadata drops the media type.
        var endpoints = string.Join(Environment.NewLine, sources.Where(s => s.HintName.Contains("EndpointDefinition", StringComparison.Ordinal)).Select(s => s.Source));
        Assert.Contains(".Produces(StatusCodes.Status200OK, typeof(System.IO.Stream), \"application/pdf\")", endpoints, StringComparison.Ordinal);
        Assert.Contains(".Produces(StatusCodes.Status200OK, typeof(System.IO.Stream), \"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet\")", endpoints, StringComparison.Ordinal);

        // The probe imports the namespace the results are generated into.
        var resultsSource = sources.First(s => s.Source.Contains("class GetReportPdfResult", StringComparison.Ordinal)).Source;
        var resultsNamespace = Regex.Match(resultsSource, @"^namespace (?<ns>[\w.]+);", RegexOptions.Multiline | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1)).Groups["ns"].Value;
        sources.Add(("DownloadProbe.cs", DownloadProbeSource.Replace("ErrorBodies.Generated.Reports.Results", resultsNamespace, StringComparison.Ordinal)));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources, includeMinimalApi: true);
        var rows = await (Task<List<string>>)assembly.GetType("ErrorBodies.RuntimeTest.DownloadProbe", throwOnError: true)!.GetMethod("RunAsync")!.Invoke(null, [])!;

        Assert.Equal(
            [
                "pdf bytes: 200 application/pdf |  | 25504446",
                "pdf bytes named: 200 application/pdf | attachment; filename=report.pdf; filename*=UTF-8''report.pdf | 25504446",
                "pdf stream named: 200 application/pdf | attachment; filename=report.pdf; filename*=UTF-8''report.pdf | 25504446",
                "excel bytes: 200 application/vnd.openxmlformats-officedocument.spreadsheetml.sheet |  | 010203",
                "custom bytes other type: 200 application/x-other |  | 09",
            ],
            rows,
            StringComparer.Ordinal);
    }

    [Fact]
    public async Task EndpointPerOperationClient_Download_IsAResultWithTheFileAndTypedErrors()
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", ErrorResponseBodyRuntimeTests.ClientOperationMarker("ProblemDetails"), includeMinimalApi: false, Yaml);
        sources.Add(("EpoDownloadProbe.cs", EpoDownloadProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);
        var rows = await (Task<List<string>>)assembly.GetType("ErrorBodies.RuntimeTest.EpoDownloadProbe", throwOnError: true)!.GetMethod("RunAsync")!.Invoke(null, [])!;

        // The file: bytes, content type, and the RFC 5987 file name (not the ASCII fallback). A declared 404: a typed
        // ProblemDetails, as for a JSON operation. A foreign error page: no success, the page in Content.
        Assert.Equal(
            [
                "200 IsOk=True OkContent=file 25504446 application/pdf Rapport-Å.pdf",
                "404 IsNotFound=True NotFoundContent=ProblemDetails {\"Type\":null,\"Title\":\"Not Found\",\"Status\":404,\"Detail\":\"No report 404\",\"Instance\":null}",
                "502 IsSuccess=False Content=<html>502 Bad Gateway</html>",
            ],
            rows,
            StringComparer.Ordinal);
    }

    [Fact]
    public async Task EndpointPerOperationClient_StreamedDownload_ReturnsAnOpenStream()
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", ErrorResponseBodyRuntimeTests.ClientOperationMarker("ProblemDetails"), includeMinimalApi: false, StreamedYaml);
        sources.Add(("StreamedProbe.cs", StreamedProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);
        var rows = await (Task<List<string>>)assembly.GetType("ErrorBodies.RuntimeTest.StreamedProbe", throwOnError: true)!.GetMethod("RunAsync")!.Invoke(null, [])!;

        // The stream is still open after the call returned; an error keeps its body as text, typed as the declared
        // error (ProblemDetails), with its content type and headers.
        Assert.Equal(
            [
                "200 IsSuccess=True application/pdf report.pdf 25504446",
                "404 IsSuccess=False application/problem+json typed=ProblemDetails:Not Found retry=30 error={\"title\":\"Not Found\",\"status\":404}",
            ],
            rows,
            StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("Throw")]
    [InlineData("Result")]
    public async Task TypedClient_Download_IsAFileWithContentTypeAndFileName(
        string resultStyle)
    {
        var marker = $$"""{ "generationMode": "TypedClient", "typedClientResultStyle": "{{resultStyle}}" }""";
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", marker, includeMinimalApi: false, Yaml);
        sources.Add(("TypedDownloadProbe.cs", TypedDownloadProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);
        var rows = await (Task<List<string>>)assembly.GetType("ErrorBodies.RuntimeTest.TypedDownloadProbe", throwOnError: true)!.GetMethod("RunAsync")!.Invoke(null, [])!;

        // The file with its content type and the RFC 5987 file name; a 404 throws (Throw) or is IsSuccess = false with
        // the body in Content (Result).
        Assert.Equal(
            resultStyle == "Throw"
                ? ["file 25504446 application/pdf Rapport-Å.pdf", "THROWS 404"]
                : ["result file 25504446 application/pdf Rapport-Å.pdf", "result IsSuccess=False 404 {\"title\":\"Not Found\",\"status\":404}"],
            rows,
            StringComparer.Ordinal);
    }
}