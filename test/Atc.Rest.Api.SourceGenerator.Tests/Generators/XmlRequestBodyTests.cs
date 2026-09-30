namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// XML request bodies over HTTP against a live generated server: a body offered as JSON and XML is bound by its
/// <c>Content-Type</c> (a generated <c>BindAsync</c> - <c>[FromBody]</c> reads JSON only and answered XML with 415), an
/// XML-only body accepts XML only (415 for JSON), a malformed or DTD-carrying document is a 400, and the validation
/// filter still runs on the bound model. Both generated C# clients send the XML (<c>preferXml</c> for a body offered
/// as JSON too) and read the echo. The echo handlers append the request's <c>Content-Type</c> to the name.
/// </summary>
public sealed class XmlRequestBodyTests
{
    internal const string Yaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /pets:
            post:
              operationId: addPet
              requestBody:
                required: true
                content:
                  application/json:
                    schema:
                      $ref: '#/components/schemas/Pet'
                  application/xml:
                    schema:
                      $ref: '#/components/schemas/Pet'
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
                '400':
                  description: Invalid
          /notes:
            post:
              operationId: addNote
              requestBody:
                required: true
                content:
                  application/xml:
                    schema:
                      $ref: '#/components/schemas/Note'
              responses:
                '201':
                  description: Created
                  content:
                    application/xml:
                      schema:
                        $ref: '#/components/schemas/Note'
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
                  maxLength: 40
              xml:
                name: pet
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
    /// Echo handlers: the bound model back, its name suffixed with the request's Content-Type.
    /// </summary>
    private const string HandlerSource = """"
        using Microsoft.AspNetCore.Http;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public sealed class AddPetHandlerImpl(IHttpContextAccessor accessor) : IAddPetHandler
        {
            public System.Threading.Tasks.Task<AddPetResult> ExecuteAsync(AddPetParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(AddPetResult.Ok(parameters.Request with { Name = $"{parameters.Request.Name} via {accessor.HttpContext!.Request.ContentType}" }));
        }

        public sealed class AddNoteHandlerImpl(IHttpContextAccessor accessor) : IAddNoteHandler
        {
            public System.Threading.Tasks.Task<AddNoteResult> ExecuteAsync(AddNoteParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(AddNoteResult.Created(parameters.Request with { Text = $"{parameters.Request.Text} via {accessor.HttpContext!.Request.ContentType}" }));
        }

        public static class XmlRequestRegistration
        {
            public static void Register(IServiceCollection services)
            {
                services.AddHttpContextAccessor();
                services.AddScoped<IAddPetHandler, AddPetHandlerImpl>();
                services.AddScoped<IAddNoteHandler, AddNoteHandlerImpl>();
            }
        }
        """";

    /// <summary>
    /// Calls the live host through a generated client (either mode) and describes what came back.
    /// </summary>
    private const string ClientProbeSource = """"
        using System.Text.Json;
        using Atc.Rest.Client;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public static class XmlRequestClientProbe
        {
            public static async Task<List<string>> RunAsync(Uri baseAddress, bool typedClient)
            {
                var assembly = typeof(XmlRequestClientProbe).Assembly;
                var pet = Activator.CreateInstance(assembly.GetTypes().Single(t => t.Name == "Pet"), 7L, "Rex")!;
                var note = Activator.CreateInstance(assembly.GetTypes().Single(t => t.Name == "Note"), "Hi & <bye>")!;
                var rows = new List<string>();
                foreach (var (operation, body) in new (string, object)[] { ("AddPet", pet), ("AddNote", note) })
                {
                    try
                    {
                        var value = typedClient ? await CallTypedAsync(baseAddress, operation, body) : await CallEndpointAsync(baseAddress, operation, body);
                        rows.Add($"{operation}: {Describe(value)}");
                    }
                    catch (Exception ex)
                    {
                        var inner = ex is System.Reflection.TargetInvocationException { InnerException: { } e } ? e : ex;
                        rows.Add($"{operation}: THROWS {inner.GetType().Name} {inner.Message}");
                    }
                }

                return rows;
            }

            private static async Task<object> CallEndpointAsync(Uri baseAddress, string operation, object body)
            {
                var assembly = typeof(XmlRequestClientProbe).Assembly;
                var httpClientName = (string)assembly.GetTypes().Single(t => t.Name == "Constants").GetField("HttpClientName")!.GetValue(null)!;
                var services = new ServiceCollection();
                services.AddHttpClient(httpClientName, c => c.BaseAddress = baseAddress);
                assembly.GetTypes().SelectMany(t => t.GetMethods()).Single(m => m.Name == "AddErrorBodiesEndpoints").Invoke(null, [services]);
                using var provider = services.BuildServiceProvider();
                var endpointType = assembly.GetTypes().Single(t => t.Name == $"I{operation}Endpoint");
                var execute = endpointType.GetMethod("ExecuteAsync")!;
                var parameters = Activator.CreateInstance(execute.GetParameters()[0].ParameterType, body);
                var task = (Task)execute.Invoke(provider.GetRequiredService(endpointType), [parameters, httpClientName, CancellationToken.None])!;
                await task;
                return task.GetType().GetProperty("Result")!.GetValue(task)!;
            }

            private static async Task<object> CallTypedAsync(Uri baseAddress, string operation, object body)
            {
                var assembly = typeof(XmlRequestClientProbe).Assembly;
                var clientType = assembly.GetTypes().Single(t => t.IsClass && !t.IsAbstract && t.GetMethod(operation + "Async") is not null);
                var client = Activator.CreateInstance(clientType, new HttpClient { BaseAddress = baseAddress })!;
                var method = clientType.GetMethod(operation + "Async")!;
                var parameters = Activator.CreateInstance(method.GetParameters()[0].ParameterType, body);
                var task = (Task)method.Invoke(client, [parameters, CancellationToken.None])!;
                await task;
                return task.GetType().GetProperty("Result")!.GetValue(task)!;
            }

            private static string Describe(object value)
            {
                if (value is EndpointResponse response)
                {
                    var accessor = value.GetType().GetProperties().FirstOrDefault(p => p.Name is "OkContent" or "CreatedContent" or "SuccessContent");
                    return $"{(int)response.StatusCode} {Model(response.IsSuccess ? accessor?.GetValue(value) : response.Content)}";
                }

                return Model(value);
            }

            private static string Model(object? value)
                => value is null ? "null" : $"{value.GetType().Name} {JsonSerializer.Serialize(value, value.GetType())}";
        }
        """";

    [Fact]
    public async Task Server_BindsXmlAndJsonByContentType_AndRejectsWhatItCannotRead()
    {
        await using var host = await XmlHost.StartAsync();

        var rows = new List<string>
        {
            await host.PostAsync("/pets", "application/json", """{"id":1,"name":"Rex"}""", accept: null),
            await host.PostAsync("/pets", "application/xml", """<pet id="1"><name>Rex</name></pet>""", accept: "application/xml"),

            // Not a declared media type: the Accepts metadata makes routing answer 415
            await host.PostAsync("/pets", "text/xml; charset=utf-8", """<pet id="2"><name>Fido</name></pet>""", accept: null),
            await host.PostAsync("/pets", "text/plain", "Rex", accept: null),
            await host.PostAsync("/pets", "application/xml", """<pet id="1"><name>unclosed</pet>""", accept: null),
            await host.PostAsync("/pets", "application/xml", """<!DOCTYPE pet [<!ENTITY x "y">]><pet id="1"><name>&x;</name></pet>""", accept: null),

            // The validation filter runs on the bound model. Its key is "Request.name" until atc-rest-minimalapi treats a
            // body bound by BindAsync like [FromBody] (atc-rest-minimalapi/issues/13-xml-request-body-validation-keys.md)
            await host.PostAsync("/pets", "application/xml", """<pet id="1"><name>a name that is much too long for the limit</name></pet>""", accept: null),
            await host.PostAsync("/pets", "application/xml", """<pet id="1"></pet>""", accept: null),
            await host.PostAsync("/notes", "application/xml", """<note><text>Hi</text></note>""", accept: null),
            await host.PostAsync("/notes", "application/json", """{"text":"Hi"}""", accept: null),
        };

        Assert.Equal(
            [
                "200 application/json; charset=utf-8 {\"id\":1,\"name\":\"Rex via application/json\"}",
                "200 application/xml; charset=utf-8 <pet id=\"1\"><name>Rex via application/xml</name></pet>",
                "415 application/problem+json title=Unsupported Media Type",
                "415 application/problem+json title=Unsupported Media Type",
                "400 application/problem+json title=Bad Request",
                "400 application/problem+json title=Bad Request",
                "400 application/problem+json errors=Request.name",
                "400 application/problem+json title=Bad Request",
                "201 application/xml; charset=utf-8 <note><text>Hi via application/xml</text></note>",
                "415 application/problem+json title=Unsupported Media Type",
            ],
            rows,
            StringComparer.Ordinal);
    }

    [Fact]
    public void Server_EndpointMetadata_NamesTheAcceptedMediaTypes()
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", ErrorResponseBodyRuntimeTests.ServerMarker, includeMinimalApi: true, Yaml);
        var endpoints = string.Join(Environment.NewLine, sources.Where(s => s.HintName.Contains("EndpointDefinition", StringComparison.Ordinal)).Select(s => s.Source));

        Assert.Contains(".Accepts<Pet>(\"application/json\", \"application/xml\")", endpoints, StringComparison.Ordinal);
        Assert.Contains(".Accepts<Note>(\"application/xml\")", endpoints, StringComparison.Ordinal);
    }

    [Fact]
    public void Server_ModelsOfAnXmlRequestBody_NameTheirXmlNames_WhereTheyDifferFromTheJsonNames()
    {
        const string yaml = """
            openapi: 3.0.3
            info:
              title: ErrorBodies
              version: 1.0.0
            paths:
              /people:
                post:
                  operationId: addPerson
                  requestBody:
                    required: true
                    content:
                      application/xml:
                        schema:
                          $ref: '#/components/schemas/Person'
                  responses:
                    '204':
                      description: Stored
            components:
              schemas:
                Person:
                  type: object
                  properties:
                    id:
                      type: integer
                      xml:
                        attribute: true
                    name:
                      type: string
                      xml:
                        name: surname
                    address:
                      $ref: '#/components/schemas/Address'
                Address:
                  type: object
                  properties:
                    city:
                      type: string
                      xml:
                        name: town
                    zip:
                      type: string
            """;
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", ErrorResponseBodyRuntimeTests.ServerMarker, includeMinimalApi: true, yaml);
        var person = sources.Single(s => s.Source.Contains("record Person(", StringComparison.Ordinal)).Source;
        var address = sources.Single(s => s.Source.Contains("record Address(", StringComparison.Ordinal)).Source;

        // The validation filter maps an XML request's keys through these (atc-rest-minimalapi/issues/13); a name the
        // XML shares with JSON (id as an attribute, zip) needs none
        Assert.Contains("[property: global::System.Xml.Serialization.XmlElement(\"surname\")] string? Name", person, StringComparison.Ordinal);
        Assert.Contains("[property: global::System.Xml.Serialization.XmlElement(\"town\")] string? City", address, StringComparison.Ordinal);
        Assert.DoesNotContain("XmlAttribute", person, StringComparison.Ordinal);
        Assert.DoesNotContain("XmlElement(\"zip\")", address, StringComparison.Ordinal);
        Assert.Empty(CompilationVerificationHarness.CompileGeneratedSources(sources, includeMinimalApi: true));
    }

    [Theory]
    [InlineData("EndpointPerOperation", false)]
    [InlineData("EndpointPerOperation", true)]
    [InlineData("Throw", false)]
    [InlineData("Throw", true)]
    [InlineData("Result", false)]
    [InlineData("Result", true)]
    public async Task Clients_SendXml_ForAnXmlBody_AndWithPreferXml(
        string client,
        bool preferXml)
    {
        await using var host = await XmlHost.StartAsync();
        var prefer = preferXml ? ", \"preferXml\": true" : string.Empty;
        var marker = client == "EndpointPerOperation"
            ? $$"""{ "generationMode": "EndpointPerOperation", "errorResponseFormat": "ProblemDetails"{{prefer}} }"""
            : $$"""{ "generationMode": "TypedClient", "typedClientResultStyle": "{{client}}"{{prefer}} }""";
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", marker, includeMinimalApi: false, Yaml);
        sources.Add(("XmlRequestClientProbe.cs", ClientProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);
        var rows = await (Task<List<string>>)assembly.GetType("ErrorBodies.RuntimeTest.XmlRequestClientProbe", throwOnError: true)!.GetMethod("RunAsync")!.Invoke(null, [host.BaseAddress, client != "EndpointPerOperation"])!;

        var ok = client == "Throw" ? string.Empty : "200 ";
        var created = client == "Throw" ? string.Empty : "201 ";

        // What the client sent: the XML with charset=utf-8; JSON as Atc.Rest.Client (no charset) or JsonContent does
        var petVia = preferXml
            ? "application/xml; charset=utf-8"
            : client == "EndpointPerOperation" ? "application/json" : "application/json; charset=utf-8";
        Assert.Equal(
            [
                $"AddPet: {ok}Pet {{\"Id\":7,\"Name\":\"Rex via {petVia}\"}}",
                $"AddNote: {created}Note {{\"Text\":\"Hi \\u0026 \\u003Cbye\\u003E via application/xml; charset=utf-8\"}}",
            ],
            rows,
            StringComparer.Ordinal);
    }

    /// <summary>
    /// The generated server on Kestrel (random port) with the echo handlers.
    /// </summary>
    private sealed class XmlHost : IAsyncDisposable
    {
        private readonly WebApplication app;
        private readonly HttpClient client;

        private XmlHost(WebApplication app)
        {
            this.app = app;
            BaseAddress = new Uri(app.Urls.First());
            client = new HttpClient { BaseAddress = BaseAddress };
        }

        public Uri BaseAddress { get; }

        public static async Task<XmlHost> StartAsync()
        {
            var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", ErrorResponseBodyRuntimeTests.ServerMarker, includeMinimalApi: true, Yaml);

            // The handlers see every generated namespace
            var usings = sources
                .Select(s => Regex.Match(s.Source, @"^namespace (?<ns>[\w.]+);", RegexOptions.Multiline | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1)).Groups["ns"].Value)
                .Where(ns => ns.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .Select(ns => $"global using {ns};");
            sources.Add(("XmlRequestHandlers.cs", string.Join(Environment.NewLine, usings) + Environment.NewLine + HandlerSource));

            var assembly = CompilationVerificationHarness.EmitAndLoad(sources, includeMinimalApi: true);
            var builder = WebApplication.CreateSlimBuilder();
            builder.Logging.ClearProviders();
            assembly.GetType("ErrorBodies.RuntimeTest.XmlRequestRegistration", throwOnError: true)!.GetMethod("Register")!.Invoke(null, [builder.Services]);
            assembly.GetType("ErrorBodies.Generated.Extensions.UnifiedServiceCollectionExtensions", throwOnError: true)!.GetMethod("AddErrorBodiesApi")!.Invoke(null, [builder.Services, null]);
            var webApp = builder.Build();
            webApp.Urls.Add("http://127.0.0.1:0");
            assembly.GetType("ErrorBodies.Generated.Extensions.UnifiedWebApplicationExtensions", throwOnError: true)!.GetMethod("MapErrorBodiesApi")!.Invoke(null, [webApp, null]);
            await webApp.StartAsync(TestContext.Current.CancellationToken);
            return new XmlHost(webApp);
        }

        public async Task<string> PostAsync(
            string path,
            string contentType,
            string body,
            string? accept)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = new ByteArrayContent(Encoding.UTF8.GetBytes(body)),
            };
            request.Content.Headers.TryAddWithoutValidation("Content-Type", contentType);
            if (accept is not null)
            {
                request.Headers.TryAddWithoutValidation("Accept", accept);
            }

            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            var mediaType = response.Content.Headers.ContentType?.ToString();
            if (mediaType?.StartsWith("application/problem+json", StringComparison.Ordinal) == true)
            {
                var problem = JsonNode.Parse(text)!.AsObject();
                return problem["errors"] is JsonObject errors
                    ? $"{(int)response.StatusCode} {mediaType} errors={string.Join(",", errors.Select(e => e.Key))}"
                    : $"{(int)response.StatusCode} {mediaType} title={problem["title"]}";
            }

            return $"{(int)response.StatusCode} {mediaType} {text}";
        }

        public ValueTask DisposeAsync()
        {
            client.Dispose();
            return app.DisposeAsync();
        }
    }
}