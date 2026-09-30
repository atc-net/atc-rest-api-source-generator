namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A response offered as JSON and XML: the generated server negotiates by <c>Accept</c> (JSON unless XML is preferred -
/// no 406), and the generated C# clients ask for JSON first (<c>preferXml</c> asks for XML) and read whatever
/// <c>Content-Type</c> comes back. The server's wire is recorded per <c>Accept</c> and a client is answered with the
/// wire for exactly the <c>Accept</c> it sent, so the negotiation is checked end to end.
/// </summary>
public sealed class XmlNegotiationTests
{
    internal const string Yaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /pets:
            get:
              operationId: listPets
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        type: array
                        items:
                          $ref: '#/components/schemas/Pet'
                    application/xml:
                      schema:
                        type: array
                        items:
                          $ref: '#/components/schemas/Pet'
          /pets/{id}:
            get:
              operationId: getPet
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
                        $ref: '#/components/schemas/Pet'
                    application/xml:
                      schema:
                        $ref: '#/components/schemas/Pet'
                '404':
                  description: Not found
        components:
          schemas:
            Pet:
              type: object
              required: [id, name]
              properties:
                id:
                  type: integer
                  format: int64
                  xml:
                    attribute: true
                name:
                  type: string
              xml:
                name: pet
        """;

    private const string DefaultAccept = "application/json, application/problem+json";
    private const string PreferXmlAccept = "application/xml, application/problem+xml; q=0.9, application/problem+json; q=0.8";

    /// <summary>
    /// Runs the generated result factories on an <c>HttpContext</c> for each <c>Accept</c> and records the wire.
    /// </summary>
    private const string ServerProbeSource = """"
        using Microsoft.AspNetCore.Http;
        using Microsoft.Extensions.DependencyInjection;
        using ErrorBodies.Generated.Pets.Models;
        using ErrorBodies.Generated.Pets.Results;

        namespace ErrorBodies.RuntimeTest;

        public static class NegotiationServerProbe
        {
            public static async Task<List<string>> RunAsync(string[] accepts)
            {
                var rows = new List<string>();
                foreach (var accept in accepts)
                {
                    rows.Add(await WireAsync("GET /pets/1", accept, GetPetResult.Ok(new Pet(Id: 1, Name: "Rex"))));
                    rows.Add(await WireAsync("GET /pets", accept, ListPetsResult.Ok([new Pet(Id: 1, Name: "Rex"), new Pet(Id: 2, Name: "Fido")])));
                }

                return rows;
            }

            private static async Task<string> WireAsync(string route, string accept, IResult result)
            {
                var context = new DefaultHttpContext
                {
                    RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider(),
                };
                if (accept.Length > 0)
                {
                    context.Request.Headers.Accept = accept;
                }

                var body = new MemoryStream();
                context.Response.Body = body;
                await result.ExecuteAsync(context);
                return $"{route}\t{accept}\t{context.Response.StatusCode}\t{context.Response.ContentType}\t{System.Text.Encoding.UTF8.GetString(body.ToArray())}";
            }
        }
        """";

    /// <summary>
    /// Answers a generated client with the server's wire for the route and the <c>Accept</c> it sent (a canned XML
    /// answer for <c>/pets/9</c>, an API that answers XML to a JSON request), and describes what the client read.
    /// </summary>
    private const string ClientProbeSource = """"
        using System.Net;
        using System.Text;
        using System.Text.Json;
        using Atc.Rest.Client;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public static class NegotiationClientProbe
        {
            private static readonly Dictionary<string, string[]> Wire = new(StringComparer.Ordinal);

            public static async Task<List<string>> RunAsync(List<string> wire, bool typedClient)
            {
                foreach (var line in wire)
                {
                    var parts = line.Split('\t');
                    Wire[$"{parts[0]}\t{parts[1]}"] = parts;
                }

                var rows = new List<string>();
                foreach (var (operation, argument) in new (string, object?)[] { ("GetPet", 1), ("ListPets", null), ("GetPet", 9) })
                {
                    try
                    {
                        var value = typedClient ? await CallTypedAsync(operation, argument) : await CallEndpointAsync(operation, argument);
                        rows.Add($"{operation}({argument}): {Describe(value)}");
                    }
                    catch (Exception ex)
                    {
                        var inner = ex is System.Reflection.TargetInvocationException { InnerException: { } e } ? e : ex;
                        rows.Add($"{operation}({argument}): THROWS {inner.GetType().Name} {inner.Message}");
                    }
                }

                return rows;
            }

            private static async Task<object> CallEndpointAsync(string operation, object? argument)
            {
                var assembly = typeof(NegotiationClientProbe).Assembly;
                var httpClientName = (string)assembly.GetTypes().Single(t => t.Name == "Constants").GetField("HttpClientName")!.GetValue(null)!;
                var services = new ServiceCollection();
                services
                    .AddHttpClient(httpClientName, c => c.BaseAddress = new Uri("http://stub/"))
                    .ConfigurePrimaryHttpMessageHandler(() => new StubHandler());
                assembly.GetTypes().SelectMany(t => t.GetMethods()).Single(m => m.Name == "AddErrorBodiesEndpoints").Invoke(null, [services]);
                using var provider = services.BuildServiceProvider();
                var endpointType = assembly.GetTypes().Single(t => t.Name == $"I{operation}Endpoint");
                var execute = endpointType.GetMethod("ExecuteAsync")!;
                var parameters = execute.GetParameters();
                var arguments = parameters.Length == 3
                    ? new[] { Activator.CreateInstance(parameters[0].ParameterType, argument), httpClientName, CancellationToken.None }
                    : new object?[] { httpClientName, CancellationToken.None };
                var task = (Task)execute.Invoke(provider.GetRequiredService(endpointType), arguments)!;
                await task;
                return task.GetType().GetProperty("Result")!.GetValue(task)!;
            }

            private static async Task<object> CallTypedAsync(string operation, object? argument)
            {
                var assembly = typeof(NegotiationClientProbe).Assembly;
                var clientType = assembly.GetTypes().Single(t => t.IsClass && !t.IsAbstract && t.GetMethod(operation + "Async") is not null);
                var client = Activator.CreateInstance(clientType, new HttpClient(new StubHandler()) { BaseAddress = new Uri("http://stub/") })!;
                var method = clientType.GetMethod(operation + "Async")!;
                var parameters = method.GetParameters();
                var arguments = parameters.Length == 2
                    ? new[] { Activator.CreateInstance(parameters[0].ParameterType, argument), CancellationToken.None }
                    : new object?[] { CancellationToken.None };
                var task = (Task)method.Invoke(client, arguments)!;
                await task;
                return task.GetType().GetProperty("Result")!.GetValue(task)!;
            }

            private static string Describe(object value)
            {
                if (value is EndpointResponse response)
                {
                    var accessor = value.GetType().GetProperties().FirstOrDefault(p => p.Name is "OkContent" or "SuccessContent");
                    return $"{(int)response.StatusCode} {Model(accessor?.GetValue(value))}";
                }

                return Model(value);
            }

            private static string Model(object? value)
                => value is null ? "null" : $"{value.GetType().Name} {JsonSerializer.Serialize(value, value.GetType())}";

            private sealed class StubHandler : HttpMessageHandler
            {
                protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    var accept = string.Join(", ", request.Headers.Accept.Select(a => a.ToString()));
                    string[] parts;
                    if (request.RequestUri!.AbsolutePath == "/pets/9")
                    {
                        // An API that answers XML whatever was asked
                        parts = ["GET /pets/9", accept, "200", "application/xml", "<pet id=\"9\"><name>Nine</name></pet>"];
                    }
                    else if (!Wire.TryGetValue($"{request.Method} {request.RequestUri.AbsolutePath}\t{accept}", out parts!))
                    {
                        throw new InvalidOperationException($"No server wire for {request.Method} {request.RequestUri.AbsolutePath} with Accept '{accept}'");
                    }

                    var response = new HttpResponseMessage((HttpStatusCode)int.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture))
                    {
                        Content = new ByteArrayContent(Encoding.UTF8.GetBytes(parts[4])),
                    };
                    response.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(parts[3]);
                    return Task.FromResult(response);
                }
            }
        }
        """";

    private static readonly string[] ServerAccepts =
    [
        string.Empty,
        "application/xml",
        "application/json;q=0.5, application/xml",
        "*/*",
        "application/xml;q=0.5, application/json",
        "text/html",
        DefaultAccept,
        PreferXmlAccept,
    ];

    [Fact]
    public async Task Server_NegotiatesByAccept_JsonUnlessXmlIsPreferred()
    {
        var wire = await RunServerAsync();

        const string json = "application/json; charset=utf-8\t{\"id\":1,\"name\":\"Rex\"}";
        const string xml = "application/xml; charset=utf-8\t<pet id=\"1\"><name>Rex</name></pet>";
        Assert.Equal(
            [
                $"GET /pets/1\t\t200\t{json}",
                $"GET /pets/1\tapplication/xml\t200\t{xml}",
                $"GET /pets/1\tapplication/json;q=0.5, application/xml\t200\t{xml}",
                $"GET /pets/1\t*/*\t200\t{json}",
                $"GET /pets/1\tapplication/xml;q=0.5, application/json\t200\t{json}",
                $"GET /pets/1\ttext/html\t200\t{json}",
                $"GET /pets/1\t{DefaultAccept}\t200\t{json}",
                $"GET /pets/1\t{PreferXmlAccept}\t200\t{xml}",
            ],
            wire.Where(w => w.StartsWith("GET /pets/1\t", StringComparison.Ordinal)),
            StringComparer.Ordinal);
        Assert.Contains($"GET /pets\tapplication/xml\t200\tapplication/xml; charset=utf-8\t<ArrayOfPet><pet id=\"1\"><name>Rex</name></pet><pet id=\"2\"><name>Fido</name></pet></ArrayOfPet>", wire, StringComparer.Ordinal);
        Assert.Contains($"GET /pets\t\t200\tapplication/json; charset=utf-8\t[{{\"id\":1,\"name\":\"Rex\"}},{{\"id\":2,\"name\":\"Fido\"}}]", wire, StringComparer.Ordinal);
    }

    [Fact]
    public void Server_EndpointMetadata_NamesBothMediaTypes()
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", ErrorResponseBodyRuntimeTests.ServerMarker, includeMinimalApi: true, Yaml);
        var endpoints = string.Join(Environment.NewLine, sources.Where(s => s.HintName.Contains("EndpointDefinition", StringComparison.Ordinal)).Select(s => s.Source));

        Assert.Contains(".Produces<Pet>(StatusCodes.Status200OK, \"application/json\", \"application/xml\")", endpoints, StringComparison.Ordinal);
        Assert.Contains(".Produces<List<Pet>>(StatusCodes.Status200OK, \"application/json\", \"application/xml\")", endpoints, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("EndpointPerOperation", false)]
    [InlineData("EndpointPerOperation", true)]
    [InlineData("Throw", false)]
    [InlineData("Throw", true)]
    [InlineData("Result", false)]
    [InlineData("Result", true)]
    public async Task Clients_AskForJsonFirst_OrXmlWithPreferXml_AndReadWhatComesBack(
        string client,
        bool preferXml)
    {
        var prefer = preferXml ? ", \"preferXml\": true" : string.Empty;
        var marker = client == "EndpointPerOperation"
            ? $$"""{ "generationMode": "EndpointPerOperation", "errorResponseFormat": "ProblemDetails"{{prefer}} }"""
            : $$"""{ "generationMode": "TypedClient", "typedClientResultStyle": "{{client}}"{{prefer}} }""";
        var rows = await RunClientAsync(await RunServerAsync(), typedClient: client != "EndpointPerOperation", marker);

        var status = client == "Throw" ? string.Empty : "200 ";
        Assert.Equal(
            [
                $"GetPet(1): {status}Pet {{\"Id\":1,\"Name\":\"Rex\"}}",
                $"ListPets(): {status}List`1 [{{\"Id\":1,\"Name\":\"Rex\"}},{{\"Id\":2,\"Name\":\"Fido\"}}]",
                $"GetPet(9): {status}Pet {{\"Id\":9,\"Name\":\"Nine\"}}",
            ],
            rows,
            StringComparer.Ordinal);
    }

    private static Task<List<string>> RunServerAsync()
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", ErrorResponseBodyRuntimeTests.ServerMarker, includeMinimalApi: true, Yaml);

        // The probe imports the namespaces the models and results are generated into (one path segment collapses)
        string NamespaceOf(string typeDeclaration)
            => Regex.Match(sources.First(x => x.Source.Contains(typeDeclaration, StringComparison.Ordinal)).Source, @"^namespace (?<ns>[\w.]+);", RegexOptions.Multiline | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1)).Groups["ns"].Value;

        sources.Add(("NegotiationServerProbe.cs", ServerProbeSource
            .Replace("ErrorBodies.Generated.Pets.Models", NamespaceOf("public sealed record Pet("), StringComparison.Ordinal)
            .Replace("ErrorBodies.Generated.Pets.Results", NamespaceOf("class GetPetResult"), StringComparison.Ordinal)));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources, includeMinimalApi: true);
        return (Task<List<string>>)assembly.GetType("ErrorBodies.RuntimeTest.NegotiationServerProbe", throwOnError: true)!.GetMethod("RunAsync")!.Invoke(null, [ServerAccepts])!;
    }

    private static Task<List<string>> RunClientAsync(
        List<string> wire,
        bool typedClient,
        string marker)
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", marker, includeMinimalApi: false, Yaml);
        sources.Add(("NegotiationClientProbe.cs", ClientProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);
        return (Task<List<string>>)assembly.GetType("ErrorBodies.RuntimeTest.NegotiationClientProbe", throwOnError: true)!.GetMethod("RunAsync")!.Invoke(null, [wire, typedClient])!;
    }
}