namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A declared XML error model (a third-party API that answers its errors as XML): the generated server writes the
/// declared model as XML with the declared media type, the metadata names it, and the EndpointPerOperation client
/// reads the error body into the model. Before, an XML error model was ignored: the server wrote a problem+json and
/// the client had only the raw text.
/// </summary>
public sealed class XmlErrorModelTests
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
                  description: OK
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Report'
                '404':
                  description: Not found
                  content:
                    application/xml:
                      schema:
                        $ref: '#/components/schemas/ErrorInfo'
          /audits/{id}:
            get:
              operationId: getAudit
              parameters:
                - name: id
                  in: path
                  required: true
                  schema:
                    type: integer
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Report'
                default:
                  description: Any error
                  content:
                    application/xml:
                      schema:
                        $ref: '#/components/schemas/ErrorInfo'
        components:
          schemas:
            Report:
              type: object
              required: [title]
              properties:
                title:
                  type: string
            ErrorInfo:
              type: object
              required: [code, message]
              properties:
                code:
                  type: string
                  xml:
                    attribute: true
                message:
                  type: string
              xml:
                name: error
        """;

    private const string ServerProbeSource = """"
        using Microsoft.AspNetCore.Http;
        using Microsoft.Extensions.DependencyInjection;
        using ErrorBodies.Generated.Models;
        using ErrorBodies.Generated.Reports.Results;

        namespace ErrorBodies.RuntimeTest;

        public static class ErrorModelServerProbe
        {
            public static async Task<List<string>> RunAsync()
                => [
                    await WireAsync("GET /reports/404", GetReportResult.NotFound(new ErrorInfo(Code: "R404", Message: "No report & more"))),
                    await WireAsync("GET /reports/405", GetReportResult.NotFound()),
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
                return $"{route}\t{context.Response.StatusCode}\t{context.Response.ContentType}\t{System.Text.Encoding.UTF8.GetString(body.ToArray())}";
            }
        }
        """";

    private const string ClientProbeSource = """"
        using System.Net;
        using System.Text;
        using System.Text.Json;
        using Atc.Rest.Client;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public static class ErrorModelClientProbe
        {
            private static string[] wire = [];

            public static async Task<string> RunAsync(string line)
            {
                wire = line.Split('\t');
                var assembly = typeof(ErrorModelClientProbe).Assembly;
                var httpClientName = (string)assembly.GetTypes().Single(t => t.Name == "Constants").GetField("HttpClientName")!.GetValue(null)!;
                var services = new ServiceCollection();
                services
                    .AddHttpClient(httpClientName, c => c.BaseAddress = new Uri("http://stub/"))
                    .ConfigurePrimaryHttpMessageHandler(() => new StubHandler());
                assembly.GetTypes().SelectMany(t => t.GetMethods()).Single(m => m.Name == "AddErrorBodiesEndpoints").Invoke(null, [services]);
                using var provider = services.BuildServiceProvider();
                var endpointType = assembly.GetTypes().Single(t => t.Name == "IGetReportEndpoint");
                var execute = endpointType.GetMethod("ExecuteAsync")!;
                var parameters = Activator.CreateInstance(execute.GetParameters()[0].ParameterType, 404);
                var task = (Task)execute.Invoke(provider.GetRequiredService(endpointType), [parameters, httpClientName, CancellationToken.None])!;
                await task;
                var result = (EndpointResponse)task.GetType().GetProperty("Result")!.GetValue(task)!;
                var content = result.GetType().GetProperty("NotFoundContent")!.GetValue(result)!;
                return $"{(int)result.StatusCode} IsNotFound={result.GetType().GetProperty("IsNotFound")!.GetValue(result)} {content.GetType().Name} {JsonSerializer.Serialize(content, content.GetType())}";
            }

            private sealed class StubHandler : HttpMessageHandler
            {
                protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    var response = new HttpResponseMessage((HttpStatusCode)int.Parse(wire[1], System.Globalization.CultureInfo.InvariantCulture))
                    {
                        Content = new ByteArrayContent(Encoding.UTF8.GetBytes(wire[3])),
                    };
                    response.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(wire[2]);
                    return Task.FromResult(response);
                }
            }
        }
        """";

    private const string DefaultErrorProbeSource = """"
        using System.Net;
        using System.Text;
        using System.Text.Json;
        using Atc.Rest.Client;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public static class DefaultErrorProbe
        {
            public static async Task<List<string>> RunAsync()
            {
                var assembly = typeof(DefaultErrorProbe).Assembly;
                var httpClientName = (string)assembly.GetTypes().Single(t => t.Name == "Constants").GetField("HttpClientName")!.GetValue(null)!;
                var services = new ServiceCollection();
                services
                    .AddHttpClient(httpClientName, c => c.BaseAddress = new Uri("http://stub/"))
                    .ConfigurePrimaryHttpMessageHandler(() => new StubHandler());
                assembly.GetTypes().SelectMany(t => t.GetMethods()).Single(m => m.Name == "AddErrorBodiesEndpoints").Invoke(null, [services]);
                using var provider = services.BuildServiceProvider();
                var endpointType = assembly.GetTypes().Single(t => t.Name == "IGetAuditEndpoint");
                var execute = endpointType.GetMethod("ExecuteAsync")!;
                var rows = new List<string>();
                foreach (var id in new[] { 503, 502 })
                {
                    var parameters = Activator.CreateInstance(execute.GetParameters()[0].ParameterType, id);
                    var task = (Task)execute.Invoke(provider.GetRequiredService(endpointType), [parameters, httpClientName, CancellationToken.None])!;
                    await task;
                    var result = (EndpointResponse)task.GetType().GetProperty("Result")!.GetValue(task)!;
                    var isDefault = (bool)result.GetType().GetProperty("IsDefaultError")!.GetValue(result)!;
                    var content = isDefault ? result.GetType().GetProperty("DefaultErrorContent")!.GetValue(result)! : null;
                    rows.Add(isDefault
                        ? $"{(int)result.StatusCode} IsDefaultError=True {content!.GetType().Name} {JsonSerializer.Serialize(content, content.GetType())}"
                        : $"{(int)result.StatusCode} IsDefaultError=False Content={result.Content}");
                }

                return rows;
            }

            private sealed class StubHandler : HttpMessageHandler
            {
                protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    var xml = request.RequestUri!.AbsolutePath.EndsWith("/503", StringComparison.Ordinal);
                    var response = new HttpResponseMessage(xml ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.BadGateway)
                    {
                        Content = new StringContent(xml ? "<error code=\"A503\"><message>Down</message></error>" : "<html>Bad Gateway</html>", Encoding.UTF8, xml ? "application/xml" : "text/html"),
                    };
                    return Task.FromResult(response);
                }
            }
        }
        """";

    [Fact]
    public async Task Server_WritesTheDeclaredXmlErrorModel()
    {
        var wire = await RunServerAsync();

        Assert.Equal(
            [
                "GET /reports/404\t404\tapplication/xml; charset=utf-8\t<error code=\"R404\"><message>No report &amp; more</message></error>",
                "GET /reports/405\t404\t\t",
            ],
            wire,
            StringComparer.Ordinal);
    }

    [Fact]
    public void Server_EndpointMetadata_NamesTheXmlErrorModel()
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", ErrorResponseBodyRuntimeTests.ServerMarker, includeMinimalApi: true, Yaml);
        var endpoints = string.Join(Environment.NewLine, sources.Where(s => s.HintName.Contains("EndpointDefinition", StringComparison.Ordinal)).Select(s => s.Source));

        Assert.Contains(".Produces<ErrorInfo>(StatusCodes.Status404NotFound, contentType: \"application/xml\")", endpoints, StringComparison.Ordinal);
    }

    /// <summary>
    /// A <c>default</c> XML error model: any undeclared error status is read into the model, and a body that is not
    /// the model (a proxy's HTML page) is not the default error - its text stays in <c>Content</c>.
    /// </summary>
    [Fact]
    public async Task EndpointPerOperationClient_ReadsADefaultXmlErrorModel_AndNotAProxysPage()
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", ErrorResponseBodyRuntimeTests.ClientOperationMarker("ProblemDetails"), includeMinimalApi: false, Yaml);
        sources.Add(("DefaultErrorProbe.cs", DefaultErrorProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);
        var rows = await (Task<List<string>>)assembly.GetType("ErrorBodies.RuntimeTest.DefaultErrorProbe", throwOnError: true)!.GetMethod("RunAsync")!.Invoke(null, [])!;

        Assert.Equal(
            [
                "503 IsDefaultError=True ErrorInfo {\"Code\":\"A503\",\"Message\":\"Down\"}",
                "502 IsDefaultError=False Content=<html>Bad Gateway</html>",
            ],
            rows,
            StringComparer.Ordinal);
    }

    [Fact]
    public async Task EndpointPerOperationClient_ReadsTheXmlErrorModel()
    {
        var wire = await RunServerAsync();
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", ErrorResponseBodyRuntimeTests.ClientOperationMarker("ProblemDetails"), includeMinimalApi: false, Yaml);
        sources.Add(("ErrorModelClientProbe.cs", ClientProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);
        var row = await (Task<string>)assembly.GetType("ErrorBodies.RuntimeTest.ErrorModelClientProbe", throwOnError: true)!.GetMethod("RunAsync")!.Invoke(null, [wire[0]])!;

        Assert.Equal("404 IsNotFound=True ErrorInfo {\"Code\":\"R404\",\"Message\":\"No report \\u0026 more\"}", row);
    }

    private static Task<List<string>> RunServerAsync()
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", ErrorResponseBodyRuntimeTests.ServerMarker, includeMinimalApi: true, Yaml);
        sources.Add(("ErrorModelServerProbe.cs", ServerProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources, includeMinimalApi: true);
        return (Task<List<string>>)assembly.GetType("ErrorBodies.RuntimeTest.ErrorModelServerProbe", throwOnError: true)!.GetMethod("RunAsync")!.Invoke(null, [])!;
    }
}