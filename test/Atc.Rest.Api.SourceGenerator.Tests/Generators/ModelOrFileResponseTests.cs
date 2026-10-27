namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A response that is a model or a file (<c>200: application/json Report</c> or <c>application/pdf</c>): the generated
/// server writes either, the metadata names both, and both C# clients read what comes back by its Content-Type - the
/// EndpointPerOperation result through <c>OkContent</c> / <c>OkFile</c>, the TypedClient as a <c>ContentOrFile&lt;T&gt;</c>.
/// Before, only the first declared media type was generated and the other was lost.
/// </summary>
public sealed class ModelOrFileResponseTests
{
    internal const string Yaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /reports/{id}:
            get:
              operationId: getReport
              parameters:
                - name: id
                  in: path
                  required: true
                  schema:
                    type: integer
              responses:
                '200':
                  description: The report, or its PDF
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Report'
                    application/pdf:
                      schema:
                        type: string
                        format: binary
                '404':
                  description: Not found
        components:
          schemas:
            Report:
              type: object
              required: [title]
              properties:
                title:
                  type: string
        """;

    private const string ServerProbeSource = """"
        using Microsoft.AspNetCore.Http;
        using Microsoft.Extensions.DependencyInjection;
        using ErrorBodies.Generated.Models;
        using ErrorBodies.Generated.Results;

        namespace ErrorBodies.RuntimeTest;

        public static class ModelOrFileServerProbe
        {
            public static async Task<List<string>> RunAsync()
                => [
                    await WireAsync("GET /reports/1", GetReportResult.Ok(new Report(Title: "Q3"))),
                    await WireAsync("GET /reports/2", GetReportResult.Ok(new byte[] { 0x25, 0x50, 0x44, 0x46 }, fileName: "Rapport-Å.pdf")),
                ];

            private static async Task<string> WireAsync(string route, IResult result)
            {
                var context = new DefaultHttpContext
                {
                    RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider(),
                };
                var body = new MemoryStream();
                context.Response.Body = body;
                await result.ExecuteAsync(context);
                var text = context.Response.ContentType?.StartsWith("application/pdf", StringComparison.Ordinal) == true
                    ? Convert.ToHexString(body.ToArray())
                    : System.Text.Encoding.UTF8.GetString(body.ToArray());
                return $"{route}\t{context.Response.StatusCode}\t{context.Response.ContentType}\t{context.Response.Headers.ContentDisposition}\t{text}";
            }
        }
        """";

    private const string HandlerSource = """"
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public sealed class GetReportHandlerImpl : IGetReportHandler
        {
            public System.Threading.Tasks.Task<GetReportResult> ExecuteAsync(GetReportParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(parameters.Id == 1
                    ? GetReportResult.Ok(new Report(Title: "Q3"))
                    : GetReportResult.Ok(new byte[] { 0x25, 0x50, 0x44, 0x46 }, fileName: "Rapport-Å.pdf"));
        }

        public static class ModelOrFileRegistration
        {
            public static void Register(IServiceCollection services)
                => services.AddScoped<IGetReportHandler, GetReportHandlerImpl>();
        }
        """";

    /// <summary>
    /// Answers each route with the server's recorded wire (a PDF as its bytes) and describes what the client read;
    /// records the Accept it sent.
    /// </summary>
    private const string ClientProbeSource = """"
        using System.Net;
        using System.Text;
        using System.Text.Json;
        using Atc.Rest.Client;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public static class ModelOrFileClientProbe
        {
            private static readonly Dictionary<string, string[]> Wire = new(StringComparer.Ordinal);
            private static string accept = string.Empty;

            public static async Task<List<string>> RunAsync(List<string> wire, bool typedClient)
            {
                foreach (var line in wire)
                {
                    var parts = line.Split('\t');
                    Wire[parts[0]] = parts;
                }

                var rows = new List<string>();
                foreach (var id in new[] { 1, 2 })
                {
                    try
                    {
                        rows.Add($"{id}: {(typedClient ? await CallTypedAsync(id) : await CallEndpointAsync(id))} accept={accept}");
                    }
                    catch (Exception ex)
                    {
                        var inner = ex is System.Reflection.TargetInvocationException { InnerException: { } e } ? e : ex;
                        rows.Add($"{id}: THROWS {inner.GetType().Name} {inner.Message}");
                    }
                }

                return rows;
            }

            private static async Task<string> CallEndpointAsync(int id)
            {
                var assembly = typeof(ModelOrFileClientProbe).Assembly;
                var httpClientName = (string)assembly.GetTypes().Single(t => t.Name == "Constants").GetField("HttpClientName")!.GetValue(null)!;
                var services = new ServiceCollection();
                services
                    .AddHttpClient(httpClientName, c => c.BaseAddress = new Uri("http://stub/"))
                    .ConfigurePrimaryHttpMessageHandler(() => new StubHandler());
                assembly.GetTypes().SelectMany(t => t.GetMethods()).Single(m => m.Name == "AddErrorBodiesEndpoints").Invoke(null, [services, null]);
                using var provider = services.BuildServiceProvider();
                var endpointType = assembly.GetTypes().Single(t => t.Name == "IGetReportEndpoint");
                var execute = endpointType.GetMethod("ExecuteAsync")!;
                var parameters = Activator.CreateInstance(execute.GetParameters()[0].ParameterType, id);
                var task = (Task)execute.Invoke(provider.GetRequiredService(endpointType), [parameters, httpClientName, CancellationToken.None])!;
                await task;
                var result = (EndpointResponse)task.GetType().GetProperty("Result")!.GetValue(task)!;
                var type = result.GetType();
                var isFile = (bool)type.GetProperty("IsOkFile")!.GetValue(result)!;
                return isFile
                    ? $"IsOk={type.GetProperty("IsOk")!.GetValue(result)} IsOkFile=True {File(type.GetProperty("OkFile")!.GetValue(result)!)}"
                    : $"IsOk={type.GetProperty("IsOk")!.GetValue(result)} IsOkFile=False {Model(type.GetProperty("OkContent")!.GetValue(result)!)}";
            }

            private static async Task<string> CallTypedAsync(int id)
            {
                var assembly = typeof(ModelOrFileClientProbe).Assembly;
                var clientType = assembly.GetTypes().Single(t => t.IsClass && !t.IsAbstract && t.GetMethod("GetReportAsync") is not null);
                var client = Activator.CreateInstance(clientType, new HttpClient(new StubHandler()) { BaseAddress = new Uri("http://stub/") })!;
                var method = clientType.GetMethod("GetReportAsync")!;
                var task = (Task)method.Invoke(client, [Activator.CreateInstance(method.GetParameters()[0].ParameterType, id), CancellationToken.None])!;
                await task;
                var value = task.GetType().GetProperty("Result")!.GetValue(task)!;
                if (value is EndpointResponse response)
                {
                    value = value.GetType().GetProperty("SuccessContent")!.GetValue(value)!;
                }

                var type = value.GetType();
                var file = type.GetProperty("File")!.GetValue(value);
                return file is not null
                    ? $"{type.Name.Split('`')[0]} IsFile=True {File(file)}"
                    : $"{type.Name.Split('`')[0]} IsFile=False {Model(type.GetProperty("Content")!.GetValue(value)!)}";
            }

            private static string Model(object value)
                => $"{value.GetType().Name} {JsonSerializer.Serialize(value, value.GetType())}";

            private static string File(object file)
            {
                var type = file.GetType();
                return $"file {Convert.ToHexString((byte[])type.GetProperty("Content")!.GetValue(file)!)} {type.GetProperty("ContentType")!.GetValue(file)} {type.GetProperty("FileName")!.GetValue(file)}";
            }

            private sealed class StubHandler : HttpMessageHandler
            {
                protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    accept = string.Join(", ", request.Headers.Accept.Select(a => a.ToString()));
                    var parts = Wire[$"{request.Method} {request.RequestUri!.AbsolutePath}"];
                    var isPdf = parts[2].StartsWith("application/pdf", StringComparison.Ordinal);
                    var response = new HttpResponseMessage((HttpStatusCode)int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture))
                    {
                        Content = new ByteArrayContent(isPdf ? Convert.FromHexString(parts[4]) : Encoding.UTF8.GetBytes(parts[4])),
                    };
                    response.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(parts[2]);
                    if (parts[3].Length > 0)
                    {
                        response.Content.Headers.TryAddWithoutValidation("Content-Disposition", parts[3]);
                    }

                    return Task.FromResult(response);
                }
            }
        }
        """";

    [Fact]
    public async Task Server_WritesTheModelOrTheFile()
    {
        var wire = await RunServerAsync();

        Assert.Equal(
            "GET /reports/1\t200\tapplication/json; charset=utf-8\t\t{\"title\":\"Q3\"}\n" +
            "GET /reports/2\t200\tapplication/pdf\tattachment; filename=Rapport-_.pdf; filename*=UTF-8''Rapport-%C3%85.pdf\t25504446",
            string.Join('\n', wire));
    }

    /// <summary>
    /// The live host: the model and the file each come back as written, and the endpoint metadata - one type per status
    /// - describes the model with both media types (a second Produces for the file would replace the model's).
    /// </summary>
    [Fact]
    public async Task LiveHost_AnswersTheModelOrTheFile_AndDescribesBoth()
    {
        await using var host = await XmlTestHost.StartAsync(Yaml, HandlerSource, "ErrorBodies.RuntimeTest.ModelOrFileRegistration");

        var model = await host.SendRawAsync(HttpMethod.Get, "/reports/1", accept: "application/json, application/pdf");
        var file = await host.SendRawAsync(HttpMethod.Get, "/reports/2", accept: "application/json, application/pdf");

        Assert.Equal((200, "application/json; charset=utf-8", "{\"title\":\"Q3\"}"), model);
        Assert.Equal((200, "application/pdf", "%PDF"), file);
        Assert.Contains("GET reports/{id}: 200 Report [application/json, application/pdf]", host.DescribeResponses(), StringComparer.Ordinal);
    }

    [Fact]
    public void Server_EndpointMetadata_NamesTheModelAndTheFile()
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", ErrorResponseBodyRuntimeTests.ServerMarker, includeMinimalApi: true, Yaml);
        var endpoints = string.Join(Environment.NewLine, sources.Where(s => s.HintName.Contains("EndpointDefinition", StringComparison.Ordinal)).Select(s => s.Source));

        // One Produces for the status: the endpoint metadata keeps one type per status, so a second would replace the model's
        Assert.Contains(".Produces<Report>(StatusCodes.Status200OK, \"application/json\", \"application/pdf\")", endpoints, StringComparison.Ordinal);
        Assert.DoesNotContain("contentType: \"application/pdf\"", endpoints, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("EndpointPerOperation")]
    [InlineData("Throw")]
    [InlineData("Result")]
    public async Task Clients_ReadTheModelOrTheFile_ByContentType(string client)
    {
        var marker = client == "EndpointPerOperation"
            ? ErrorResponseBodyRuntimeTests.ClientOperationMarker("ProblemDetails")
            : $$"""{ "generationMode": "TypedClient", "typedClientResultStyle": "{{client}}" }""";
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", marker, includeMinimalApi: false, Yaml);
        sources.Add(("ModelOrFileClientProbe.cs", ClientProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);
        var rows = await (Task<List<string>>)assembly.GetType("ErrorBodies.RuntimeTest.ModelOrFileClientProbe", throwOnError: true)!.GetMethod("RunAsync")!.Invoke(null, [await RunServerAsync(), client != "EndpointPerOperation"])!;

        const string accept = "application/json, application/pdf, application/problem+json";
        Assert.Equal(
            client == "EndpointPerOperation"
                ?
                [
                    $"1: IsOk=True IsOkFile=False Report {{\"Title\":\"Q3\"}} accept={accept}",
                    $"2: IsOk=True IsOkFile=True file 25504446 application/pdf Rapport-Å.pdf accept={accept}",
                ]
                :
                [
                    $"1: ContentOrFile IsFile=False Report {{\"Title\":\"Q3\"}} accept={accept}",
                    $"2: ContentOrFile IsFile=True file 25504446 application/pdf Rapport-Å.pdf accept={accept}",
                ],
            rows,
            StringComparer.Ordinal);
    }

    /// <summary>
    /// The EndpointPerOperation result's test factories: <c>Ok(model)</c> is the model, <c>Ok(DownloadedFile)</c> the file
    /// (with its media type and file name), so a consumer's unit test builds either answer.
    /// </summary>
    [Fact]
    public void EndpointPerOperationResult_TestFactories_BuildTheModelOrTheFile()
    {
        const string probe = """"
            namespace ErrorBodies.RuntimeTest;

            public static class ModelOrFileFactoryProbe
            {
                public static string Run()
                {
                    var model = GetReportEndpointResult.Ok(new Report(Title: "Q3"));
                    var file = GetReportEndpointResult.Ok(new DownloadedFile(new byte[] { 0x25, 0x50 }, "application/pdf", "Rapport-Å.pdf", 2));
                    return $"{model.IsOk} {model.IsOkFile} {model.OkContent.Title} | {file.IsOk} {file.IsOkFile} {file.OkFile.ContentType} {file.OkFile.FileName} {file.OkFile.Content.Length}";
                }
            }
            """";
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", ErrorResponseBodyRuntimeTests.ClientOperationMarker("ProblemDetails"), includeMinimalApi: false, Yaml);
        var usings = sources
            .Select(s => Regex.Match(s.Source, @"^namespace (?<ns>[\w.]+);", RegexOptions.Multiline | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1)).Groups["ns"].Value)
            .Where(ns => ns.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Select(ns => $"global using {ns};");
        sources.Add(("ModelOrFileFactoryProbe.cs", string.Join(Environment.NewLine, usings) + Environment.NewLine + probe));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);
        var row = (string)assembly.GetType("ErrorBodies.RuntimeTest.ModelOrFileFactoryProbe", throwOnError: true)!.GetMethod("Run")!.Invoke(null, [])!;

        Assert.Equal("True False Q3 | True True application/pdf Rapport-Å.pdf 2", row);
    }

    /// <summary>
    /// A file next to a model the generator cannot type (<c>application/json</c> without a schema; the classifier tests cover
    /// an inline XML object) is no model or file: it stays a file download, so the file is never lost.
    /// </summary>
    [Fact]
    public void Server_AFileBesideAnUntypedModel_StaysAFileDownload()
    {
        const string yaml = """
            openapi: 3.0.3
            info:
              title: ErrorBodies
              version: 1.0.0
            paths:
              /reports/{id}:
                get:
                  operationId: getReport
                  parameters:
                    - name: id
                      in: path
                      required: true
                      schema:
                        type: integer
                  responses:
                    '200':
                      description: The PDF
                      content:
                        application/pdf:
                          schema:
                            type: string
                            format: binary
                        application/json: {}
            """;
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", ErrorResponseBodyRuntimeTests.ServerMarker, includeMinimalApi: true, yaml);
        var results = string.Join(Environment.NewLine, sources.Where(s => s.HintName.Contains("Results", StringComparison.Ordinal)).Select(s => s.Source));

        Assert.Matches(new Regex(@"public static GetReportResult Ok\(\s*byte\[\] bytes", RegexOptions.None, TimeSpan.FromSeconds(1)), results);
        var errors = CompilationVerificationHarness.CompileGeneratedSources(sources, includeMinimalApi: true);
        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
    }

    private static Task<List<string>> RunServerAsync()
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", ErrorResponseBodyRuntimeTests.ServerMarker, includeMinimalApi: true, Yaml);
        sources.Add(("ModelOrFileServerProbe.cs", ServerProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources, includeMinimalApi: true);
        return (Task<List<string>>)assembly.GetType("ErrorBodies.RuntimeTest.ModelOrFileServerProbe", throwOnError: true)!.GetMethod("RunAsync")!.Invoke(null, [])!;
    }
}