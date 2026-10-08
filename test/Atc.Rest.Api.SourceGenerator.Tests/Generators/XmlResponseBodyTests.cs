namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// An XML-only response body (<c>application/xml</c>, <c>text/xml</c>, <c>*+xml</c> with a model schema) end to end:
/// the generated server writes it with the declared media type, and both generated C# clients read exactly those bytes
/// back into the model - the probes replay the wire the server wrote. Before, the body type came from
/// <c>application/json</c> only, so an XML-only body was dropped on every side.
/// </summary>
public sealed class XmlResponseBodyTests
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
                    application/xml:
                      schema:
                        type: array
                        items:
                          $ref: '#/components/schemas/Pet'
            post:
              operationId: createPet
              requestBody:
                required: true
                content:
                  application/json:
                    schema:
                      $ref: '#/components/schemas/NewPet'
              responses:
                '201':
                  description: Created
                  content:
                    text/xml:
                      schema:
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
                    application/xml:
                      schema:
                        $ref: '#/components/schemas/Pet'
                '404':
                  description: Not found
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
                    application/vnd.acme.report+xml:
                      schema:
                        $ref: '#/components/schemas/Report'
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
                tags:
                  type: array
                  xml:
                    wrapped: true
                  items:
                    type: string
                    xml:
                      name: tag
              xml:
                name: pet
            NewPet:
              type: object
              required: [name]
              properties:
                name:
                  type: string
            Report:
              type: object
              required: [title]
              properties:
                title:
                  type: string
                pages:
                  type: integer
              xml:
                name: report
                namespace: 'urn:acme:report'
        """;

    /// <summary>
    /// Runs the generated result factories on an <c>HttpContext</c> and records the wire: route, status, content
    /// type and body.
    /// </summary>
    private const string ServerProbeSource = """"
        using Microsoft.AspNetCore.Http;
        using Microsoft.Extensions.DependencyInjection;
        using ErrorBodies.Generated.Pets.Models;
        using ErrorBodies.Generated.Pets.Results;
        using ErrorBodies.Generated.Reports.Models;
        using ErrorBodies.Generated.Reports.Results;

        namespace ErrorBodies.RuntimeTest;

        public static class XmlServerProbe
        {
            public static async Task<List<string>> RunAsync()
            {
                var pet = new Pet(Id: 1, Name: "Rex", Tags: ["good", "boy"]);
                return
                [
                    await WireAsync("GET /pets/1", GetPetResult.Ok(pet)),
                    await WireAsync("GET /pets/404", GetPetResult.NotFound("No pet 404"), accept: "application/xml"),
                    await WireAsync("GET /pets", ListPetsResult.Ok([pet, new Pet(Id: 2, Name: "Fido")])),
                    await WireAsync("POST /pets", CreatePetResult.Created(new Pet(Id: 3, Name: "New & <shiny>"))),
                    await WireAsync("GET /reports/7", GetReportResult.Ok(new Report(Title: "Q3", Pages: 12))),
                ];
            }

            private static async Task<string> WireAsync(string route, IResult result, string? accept = null)
            {
                var context = new DefaultHttpContext
                {
                    RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider(),
                };
                if (accept is not null)
                {
                    context.Request.Headers.Accept = accept;
                }

                var body = new MemoryStream();
                context.Response.Body = body;
                await result.ExecuteAsync(context);
                return $"{route}\t{context.Response.StatusCode}\t{context.Response.ContentType}\t{System.Text.Encoding.UTF8.GetString(body.ToArray())}";
            }
        }
        """";

    /// <summary>
    /// Replays the server's wire to a generated client: a stub answers each route with the recorded status, content
    /// type and body, and records the <c>Accept</c> header the client sent. The clients and their operations are found
    /// by reflection (either mode, no namespace assumptions).
    /// </summary>
    private const string ClientProbeSource = """"
        using System.Net;
        using System.Text;
        using System.Text.Json;
        using Atc.Rest.Client;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public static class XmlClientProbe
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

                var calls = new (string Operation, object?[] Arguments)[]
                {
                    ("GetPet", [1]),
                    ("GetPet", [404]),
                    ("ListPets", []),
                    ("CreatePet", ["NewPet"]),
                    ("GetReport", [7]),
                    ("GetReport", [8]),
                };

                var rows = new List<string>();
                foreach (var (operation, arguments) in calls)
                {
                    accept = string.Empty;
                    var label = $"{operation}({string.Join(",", arguments)})";
                    try
                    {
                        var value = typedClient ? await CallTypedAsync(operation, arguments) : await CallEndpointAsync(operation, arguments);
                        rows.Add($"{label}: {Describe(value)} accept={accept}");
                    }
                    catch (Exception ex) when (ex is HttpRequestException or System.Reflection.TargetInvocationException)
                    {
                        var inner = ex is System.Reflection.TargetInvocationException { InnerException: { } e } ? e : ex;
                        rows.Add($"{label}: THROWS {inner.GetType().Name} {(inner as HttpRequestException)?.StatusCode} accept={accept}");
                    }
                }

                return rows;
            }

            private static object? Argument(Type parametersType, object? argument)
            {
                if (argument is "NewPet")
                {
                    var newPet = parametersType.Assembly.GetTypes().Single(t => t.Name == "NewPet");
                    return Activator.CreateInstance(parametersType, Activator.CreateInstance(newPet, "Neo"));
                }

                return Activator.CreateInstance(parametersType, argument);
            }

            private static async Task<object> CallEndpointAsync(string operation, object?[] arguments)
            {
                var assembly = typeof(XmlClientProbe).Assembly;
                var httpClientName = (string)assembly.GetTypes().Single(t => t.Name == "Constants").GetField("HttpClientName")!.GetValue(null)!;
                var services = new ServiceCollection();
                services
                    .AddHttpClient(httpClientName, c => c.BaseAddress = new Uri("http://stub/"))
                    .ConfigurePrimaryHttpMessageHandler(() => new StubHandler());
                assembly.GetTypes().SelectMany(t => t.GetMethods()).Single(m => m.Name == "AddErrorBodiesEndpoints").Invoke(null, [services, null]);
                using var provider = services.BuildServiceProvider();
                var endpointType = assembly.GetTypes().Single(t => t.Name == $"I{operation}Endpoint");
                var endpoint = provider.GetRequiredService(endpointType);
                var execute = endpointType.GetMethod("ExecuteAsync")!;
                var parameters = execute.GetParameters();
                var callArguments = parameters.Length == 3
                    ? new[] { Argument(parameters[0].ParameterType, arguments[0]), httpClientName, CancellationToken.None }
                    : new object?[] { httpClientName, CancellationToken.None };
                var task = (Task)execute.Invoke(endpoint, callArguments)!;
                await task;
                return task.GetType().GetProperty("Result")!.GetValue(task)!;
            }

            private static async Task<object> CallTypedAsync(string operation, object?[] arguments)
            {
                var assembly = typeof(XmlClientProbe).Assembly;
                var clientType = assembly.GetTypes().Single(t => t.IsClass && !t.IsAbstract && t.GetMethod(operation + "Async") is not null);
                var client = Activator.CreateInstance(clientType, new HttpClient(new StubHandler()) { BaseAddress = new Uri("http://stub/") })!;
                var method = clientType.GetMethod(operation + "Async")!;
                var parameters = method.GetParameters();
                var callArguments = parameters.Length == 2
                    ? new[] { Argument(parameters[0].ParameterType, arguments[0]), CancellationToken.None }
                    : new object?[] { CancellationToken.None };
                var task = (Task)method.Invoke(client, callArguments)!;
                await task;
                return task.GetType().GetProperty("Result")!.GetValue(task)!;
            }

            private static string Describe(object value)
            {
                var type = value.GetType();
                if (value is EndpointResponse response)
                {
                    // EndpointPerOperation result class: the typed accessor of the status; TypedClient Result style: SuccessContent
                    var accessor = response.IsSuccess
                        ? type.GetProperties().FirstOrDefault(p => p.Name is "OkContent" or "CreatedContent" or "SuccessContent")
                        : type.GetProperties().FirstOrDefault(p => p.Name == "NotFoundContent");
                    var content = accessor?.GetValue(value);
                    return $"{(int)response.StatusCode} {accessor?.Name}={Model(content)}";
                }

                return Model(value);
            }

            private static string Model(object? value)
                => value switch
                {
                    null => "null",
                    _ when value.GetType().Name is "ProblemDetails" => $"ProblemDetails detail={value.GetType().GetProperty("Detail")!.GetValue(value)}",
                    _ => $"{value.GetType().Name} {JsonSerializer.Serialize(value, value.GetType())}",
                };

            private sealed class StubHandler : HttpMessageHandler
            {
                protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    accept = string.Join(" | ", request.Headers.Accept.Select(a => a.ToString()));
                    if (request.RequestUri!.AbsolutePath == "/reports/8")
                    {
                        // Another producer: ISO-8859-1 bytes, declared in the document only (no charset parameter)
                        var latin1 = new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new ByteArrayContent(Encoding.Latin1.GetBytes("<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?><report xmlns=\"urn:acme:report\"><title>Årsrapport</title></report>")),
                        };
                        latin1.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/vnd.acme.report+xml");
                        return Task.FromResult(latin1);
                    }

                    var parts = Wire[$"{request.Method} {request.RequestUri!.AbsolutePath}"];
                    var response = new HttpResponseMessage((HttpStatusCode)int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture))
                    {
                        Content = new ByteArrayContent(Encoding.UTF8.GetBytes(parts[3])),
                    };
                    response.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(parts[2]);
                    return Task.FromResult(response);
                }
            }
        }
        """";

    [Fact]
    public async Task Server_WritesAnXmlOnlyBody_WithItsDeclaredMediaType()
    {
        var wire = await RunServerAsync();

        Assert.Equal("GET /pets/1\t200\tapplication/xml; charset=utf-8\t<pet id=\"1\"><name>Rex</name><tags><tag>good</tag><tag>boy</tag></tags></pet>", wire[0]);
        Assert.StartsWith("GET /pets/404\t404\tapplication/problem+json\t{", wire[1], StringComparison.Ordinal);
        Assert.Equal("GET /pets\t200\tapplication/xml; charset=utf-8\t<ArrayOfPet><pet id=\"1\"><name>Rex</name><tags><tag>good</tag><tag>boy</tag></tags></pet><pet id=\"2\"><name>Fido</name></pet></ArrayOfPet>", wire[2]);
        Assert.Equal("POST /pets\t201\ttext/xml; charset=utf-8\t<pet id=\"3\"><name>New &amp; &lt;shiny&gt;</name></pet>", wire[3]);
        Assert.Equal("GET /reports/7\t200\tapplication/vnd.acme.report+xml; charset=utf-8\t<report xmlns=\"urn:acme:report\"><title>Q3</title><pages>12</pages></report>", wire[4]);
    }

    [Fact]
    public void Server_EndpointMetadata_NamesTheXmlMediaType()
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", ErrorResponseBodyRuntimeTests.ServerMarker, includeMinimalApi: true, Yaml);
        var endpoints = string.Join(Environment.NewLine, sources.Where(s => s.HintName.Contains("EndpointDefinition", StringComparison.Ordinal)).Select(s => s.Source));

        Assert.Contains(".Produces<Pet>(StatusCodes.Status200OK, contentType: \"application/xml\")", endpoints, StringComparison.Ordinal);
        Assert.Contains(".Produces<List<Pet>>(StatusCodes.Status200OK, contentType: \"application/xml\")", endpoints, StringComparison.Ordinal);
        Assert.Contains(".Produces<Pet>(StatusCodes.Status201Created, contentType: \"text/xml\")", endpoints, StringComparison.Ordinal);
        Assert.Contains(".Produces<Report>(StatusCodes.Status200OK, contentType: \"application/vnd.acme.report+xml\")", endpoints, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EndpointPerOperationClient_ReadsTheXmlTheServerWrote()
    {
        var rows = await RunClientAsync(await RunServerAsync(), typedClient: false, ErrorResponseBodyRuntimeTests.ClientOperationMarker("ProblemDetails"));

        Assert.Equal(
            [
                "GetPet(1): 200 OkContent=Pet {\"Id\":1,\"Name\":\"Rex\",\"Tags\":[\"good\",\"boy\"]} accept=application/xml | application/problem+json",
                "GetPet(404): 404 NotFoundContent=ProblemDetails detail=No pet 404 accept=application/xml | application/problem+json",
                "ListPets(): 200 OkContent=List`1 [{\"Id\":1,\"Name\":\"Rex\",\"Tags\":[\"good\",\"boy\"]},{\"Id\":2,\"Name\":\"Fido\",\"Tags\":null}] accept=application/xml | application/problem+json",
                "CreatePet(NewPet): 201 CreatedContent=Pet {\"Id\":3,\"Name\":\"New \\u0026 \\u003Cshiny\\u003E\",\"Tags\":null} accept=text/xml | application/problem+json",
                "GetReport(7): 200 OkContent=Report {\"Title\":\"Q3\",\"Pages\":12} accept=application/vnd.acme.report+xml | application/problem+json",
                "GetReport(8): 200 OkContent=Report {\"Title\":\"\\u00C5rsrapport\",\"Pages\":null} accept=application/vnd.acme.report+xml | application/problem+json",
            ],
            rows,
            StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("Throw")]
    [InlineData("Result")]
    public async Task TypedClient_ReadsTheXmlTheServerWrote(string resultStyle)
    {
        var marker = $$"""{ "generationMode": "TypedClient", "typedClientResultStyle": "{{resultStyle}}" }""";
        var rows = await RunClientAsync(await RunServerAsync(), typedClient: true, marker);

        var pet = "Pet {\"Id\":1,\"Name\":\"Rex\",\"Tags\":[\"good\",\"boy\"]}";
        var pets = "List`1 [{\"Id\":1,\"Name\":\"Rex\",\"Tags\":[\"good\",\"boy\"]},{\"Id\":2,\"Name\":\"Fido\",\"Tags\":null}]";
        var created = "Pet {\"Id\":3,\"Name\":\"New \\u0026 \\u003Cshiny\\u003E\",\"Tags\":null}";
        var report = "Report {\"Title\":\"Q3\",\"Pages\":12}";
        var latin1 = "Report {\"Title\":\"\\u00C5rsrapport\",\"Pages\":null}";
        Assert.Equal(
            resultStyle == "Throw"
                ?
                [
                    $"GetPet(1): {pet} accept=application/xml | application/problem+json",
                    "GetPet(404): THROWS HttpRequestException NotFound accept=application/xml | application/problem+json",
                    $"ListPets(): {pets} accept=application/xml | application/problem+json",
                    $"CreatePet(NewPet): {created} accept=text/xml | application/problem+json",
                    $"GetReport(7): {report} accept=application/vnd.acme.report+xml | application/problem+json",
                    $"GetReport(8): {latin1} accept=application/vnd.acme.report+xml | application/problem+json",
                ]
                :
                [
                    $"GetPet(1): 200 SuccessContent={pet} accept=application/xml | application/problem+json",
                    "GetPet(404): 404 =null accept=application/xml | application/problem+json",
                    $"ListPets(): 200 SuccessContent={pets} accept=application/xml | application/problem+json",
                    $"CreatePet(NewPet): 201 SuccessContent={created} accept=text/xml | application/problem+json",
                    $"GetReport(7): 200 SuccessContent={report} accept=application/vnd.acme.report+xml | application/problem+json",
                    $"GetReport(8): 200 SuccessContent={latin1} accept=application/vnd.acme.report+xml | application/problem+json",
                ],
            rows,
            StringComparer.Ordinal);
    }

    private static Task<List<string>> RunServerAsync()
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", ErrorResponseBodyRuntimeTests.ServerMarker, includeMinimalApi: true, Yaml);
        sources.Add(("XmlServerProbe.cs", ServerProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources, includeMinimalApi: true);
        return (Task<List<string>>)assembly.GetType("ErrorBodies.RuntimeTest.XmlServerProbe", throwOnError: true)!.GetMethod("RunAsync")!.Invoke(null, [])!;
    }

    private static Task<List<string>> RunClientAsync(
        List<string> wire,
        bool typedClient,
        string marker)
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", marker, includeMinimalApi: false, Yaml);
        sources.Add(("XmlClientProbe.cs", ClientProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);
        return (Task<List<string>>)assembly.GetType("ErrorBodies.RuntimeTest.XmlClientProbe", throwOnError: true)!.GetMethod("RunAsync")!.Invoke(null, [wire, typedClient])!;
    }
}