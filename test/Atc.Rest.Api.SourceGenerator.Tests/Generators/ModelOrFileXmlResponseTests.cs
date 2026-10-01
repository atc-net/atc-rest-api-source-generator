namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A model or a file where the model is XML: an XML-only model or a PDF (<c>getNote</c>), and a JSON model offered as
/// XML too, or a PDF (<c>getMemo</c>). Both C# clients read the model by the generated XML mapping when the answer is
/// XML, the JSON model when it is JSON, and the file otherwise - and ask for the model's and the file's media types
/// (<c>preferXml</c> asks for XML first).
/// </summary>
public sealed class ModelOrFileXmlResponseTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /notes/{id}:
            get:
              operationId: getNote
              parameters:
                - name: id
                  in: path
                  required: true
                  schema:
                    type: integer
              responses:
                '200':
                  description: The note as XML, or its PDF
                  content:
                    application/xml:
                      schema:
                        $ref: '#/components/schemas/Note'
                    application/pdf:
                      schema:
                        type: string
                        format: binary
          /memos/{id}:
            get:
              operationId: getMemo
              parameters:
                - name: id
                  in: path
                  required: true
                  schema:
                    type: integer
              responses:
                '200':
                  description: The memo as JSON or XML, or its PDF
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Note'
                    application/xml:
                      schema:
                        $ref: '#/components/schemas/Note'
                    application/pdf:
                      schema:
                        type: string
                        format: binary
        components:
          schemas:
            Note:
              type: object
              required: [text]
              properties:
                text:
                  type: string
              xml:
                name: note
        """;

    /// <summary>
    /// Answers <c>/{notes|memos}/1</c> with XML, <c>/2</c> with a PDF and <c>/memos/3</c> with JSON, and describes what each
    /// client read with the Accept it sent.
    /// </summary>
    private const string ClientProbeSource = """"
        using System.Net;
        using System.Text;
        using System.Text.Json;
        using Atc.Rest.Client;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public static class ModelOrFileXmlProbe
        {
            private static string accept = string.Empty;

            public static async Task<List<string>> RunAsync(bool typedClient)
            {
                var rows = new List<string>();
                foreach (var (operation, id) in new[] { ("GetNote", 1), ("GetNote", 2), ("GetMemo", 1), ("GetMemo", 2), ("GetMemo", 3) })
                {
                    try
                    {
                        rows.Add($"{operation}({id}): {(typedClient ? await CallTypedAsync(operation, id) : await CallEndpointAsync(operation, id))} accept={accept}");
                    }
                    catch (Exception ex)
                    {
                        var inner = ex is System.Reflection.TargetInvocationException { InnerException: { } e } ? e : ex;
                        rows.Add($"{operation}({id}): THROWS {inner.GetType().Name} {inner.Message}");
                    }
                }

                return rows;
            }

            private static async Task<string> CallEndpointAsync(string operation, int id)
            {
                var assembly = typeof(ModelOrFileXmlProbe).Assembly;
                var httpClientName = (string)assembly.GetTypes().Single(t => t.Name == "Constants").GetField("HttpClientName")!.GetValue(null)!;
                var services = new ServiceCollection();
                services
                    .AddHttpClient(httpClientName, c => c.BaseAddress = new Uri("http://stub/"))
                    .ConfigurePrimaryHttpMessageHandler(() => new StubHandler());
                foreach (var register in assembly.GetTypes().SelectMany(t => t.GetMethods()).Where(m => m.Name.StartsWith("AddErrorBodies", StringComparison.Ordinal) && m.Name.EndsWith("Endpoints", StringComparison.Ordinal)))
                {
                    register.Invoke(null, [services]);
                }

                using var provider = services.BuildServiceProvider();
                var endpointType = assembly.GetTypes().Single(t => t.Name == $"I{operation}Endpoint");
                var execute = endpointType.GetMethod("ExecuteAsync")!;
                var parameters = Activator.CreateInstance(execute.GetParameters()[0].ParameterType, id);
                var task = (Task)execute.Invoke(provider.GetRequiredService(endpointType), [parameters, httpClientName, CancellationToken.None])!;
                await task;
                var result = (EndpointResponse)task.GetType().GetProperty("Result")!.GetValue(task)!;
                var type = result.GetType();
                return (bool)type.GetProperty("IsOkFile")!.GetValue(result)!
                    ? $"file {File(type.GetProperty("OkFile")!.GetValue(result)!)}"
                    : $"model {Model(type.GetProperty("OkContent")!.GetValue(result)!)}";
            }

            private static async Task<string> CallTypedAsync(string operation, int id)
            {
                var assembly = typeof(ModelOrFileXmlProbe).Assembly;
                var clientType = assembly.GetTypes().Single(t => t.IsClass && !t.IsAbstract && t.GetMethod($"{operation}Async") is not null);
                var client = Activator.CreateInstance(clientType, new HttpClient(new StubHandler()) { BaseAddress = new Uri("http://stub/") })!;
                var method = clientType.GetMethod($"{operation}Async")!;
                var task = (Task)method.Invoke(client, [Activator.CreateInstance(method.GetParameters()[0].ParameterType, id), CancellationToken.None])!;
                await task;
                var value = task.GetType().GetProperty("Result")!.GetValue(task)!;
                if (value is EndpointResponse)
                {
                    value = value.GetType().GetProperty("SuccessContent")!.GetValue(value)!;
                }

                var file = value.GetType().GetProperty("File")!.GetValue(value);
                return file is not null
                    ? $"file {File(file)}"
                    : $"model {Model(value.GetType().GetProperty("Content")!.GetValue(value)!)}";
            }

            private static string Model(object value)
                => $"{value.GetType().Name} {JsonSerializer.Serialize(value, value.GetType())}";

            private static string File(object file)
            {
                var type = file.GetType();
                return $"{Convert.ToHexString((byte[])type.GetProperty("Content")!.GetValue(file)!)} {type.GetProperty("ContentType")!.GetValue(file)}";
            }

            private sealed class StubHandler : HttpMessageHandler
            {
                protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    accept = string.Join(", ", request.Headers.Accept.Select(a => a.ToString()));
                    var path = request.RequestUri!.AbsolutePath;
                    var (mediaType, body) = path.EndsWith("/1", StringComparison.Ordinal)
                        ? ("application/xml", Encoding.UTF8.GetBytes("<note><text>Hi &amp; bye</text></note>"))
                        : path.EndsWith("/3", StringComparison.Ordinal)
                            ? ("application/json", Encoding.UTF8.GetBytes("{\"text\":\"json\"}"))
                            : ("application/pdf", new byte[] { 0x25, 0x50, 0x44, 0x46, 0xFF });
                    var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) };
                    response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mediaType);
                    return Task.FromResult(response);
                }
            }
        }
        """";

    [Theory]
    [InlineData("EndpointPerOperation", false)]
    [InlineData("EndpointPerOperation", true)]
    [InlineData("Throw", false)]
    [InlineData("Throw", true)]
    [InlineData("Result", false)]
    [InlineData("Result", true)]
    public async Task Clients_ReadTheXmlModelOrTheFile_ByContentType(
        string client,
        bool preferXml)
    {
        var prefer = preferXml ? ", \"preferXml\": true" : string.Empty;
        var marker = client == "EndpointPerOperation"
            ? $$"""{ "generationMode": "EndpointPerOperation", "errorResponseFormat": "ProblemDetails"{{prefer}} }"""
            : $$"""{ "generationMode": "TypedClient", "typedClientResultStyle": "{{client}}"{{prefer}} }""";
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", marker, includeMinimalApi: false, Yaml);
        sources.Add(("ModelOrFileXmlProbe.cs", ClientProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);
        var rows = await (Task<List<string>>)assembly.GetType("ErrorBodies.RuntimeTest.ModelOrFileXmlProbe", throwOnError: true)!.GetMethod("RunAsync")!.Invoke(null, [client != "EndpointPerOperation"])!;

        const string noteAccept = "application/xml, application/pdf, application/problem+json";
        var memoAccept = preferXml
            ? "application/xml, application/pdf, application/problem+xml; q=0.9, application/problem+json; q=0.8"
            : "application/json, application/pdf, application/problem+json";
        Assert.Equal(
            [
                $"GetNote(1): model Note {{\"Text\":\"Hi \\u0026 bye\"}} accept={noteAccept}",
                $"GetNote(2): file 25504446FF application/pdf accept={noteAccept}",
                $"GetMemo(1): model Note {{\"Text\":\"Hi \\u0026 bye\"}} accept={memoAccept}",
                $"GetMemo(2): file 25504446FF application/pdf accept={memoAccept}",
                $"GetMemo(3): model Note {{\"Text\":\"json\"}} accept={memoAccept}",
            ],
            rows,
            StringComparer.Ordinal);
    }
}