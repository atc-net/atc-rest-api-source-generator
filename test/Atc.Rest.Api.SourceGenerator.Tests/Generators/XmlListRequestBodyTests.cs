namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// XML <b>list</b> request bodies over HTTP against a live generated server. The generated clients send an array body
/// offered as XML as an XML document, so the server binds it by its <c>Content-Type</c> too: a generated
/// <c>RequestBodyList&lt;T&gt;</c> (a <c>List&lt;T&gt;</c> with a <c>BindAsync</c>) reads XML by the item model's mapping and
/// JSON by the host's JSON options. The echo handlers append the request's <c>Content-Type</c> to each name.
/// </summary>
public sealed class XmlListRequestBodyTests
{
    internal const string Yaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /pets/batch:
            post:
              operationId: addPets
              requestBody:
                required: true
                content:
                  application/json:
                    schema:
                      type: array
                      items:
                        $ref: '#/components/schemas/Pet'
                  application/xml:
                    schema:
                      type: array
                      xml:
                        name: pets
                      items:
                        $ref: '#/components/schemas/Pet'
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        type: array
                        items:
                          $ref: '#/components/schemas/Pet'
                '400':
                  description: Invalid
          /notes/batch:
            post:
              operationId: addNotes
              requestBody:
                required: true
                content:
                  application/xml:
                    schema:
                      type: array
                      items:
                        $ref: '#/components/schemas/Note'
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        type: array
                        items:
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
    /// Echo handlers: the bound items back, each name suffixed with the request's Content-Type.
    /// </summary>
    private const string HandlerSource = """"
        using Microsoft.AspNetCore.Http;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public sealed class AddPetsHandlerImpl(IHttpContextAccessor accessor) : IAddPetsHandler
        {
            public System.Threading.Tasks.Task<AddPetsResult> ExecuteAsync(AddPetsParameters parameters, System.Threading.CancellationToken cancellationToken = default)
            {
                List<Pet> pets = parameters.Request;
                return System.Threading.Tasks.Task.FromResult(AddPetsResult.Ok(pets.Select(p => p with { Name = $"{p.Name} via {accessor.HttpContext!.Request.ContentType}" }).ToList()));
            }
        }

        public sealed class AddNotesHandlerImpl(IHttpContextAccessor accessor) : IAddNotesHandler
        {
            public System.Threading.Tasks.Task<AddNotesResult> ExecuteAsync(AddNotesParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(AddNotesResult.Ok(parameters.Request.Select(n => n with { Text = $"{n.Text} via {accessor.HttpContext!.Request.ContentType}" }).ToList()));
        }

        public static class XmlListRequestRegistration
        {
            public static void Register(IServiceCollection services)
            {
                services.AddHttpContextAccessor();
                services.AddScoped<IAddPetsHandler, AddPetsHandlerImpl>();
                services.AddScoped<IAddNotesHandler, AddNotesHandlerImpl>();
            }
        }
        """";

    /// <summary>
    /// Calls the live host through a generated client (either mode) with a list body and describes what came back.
    /// </summary>
    private const string ClientProbeSource = """"
        using System.Collections;
        using System.Text.Json;
        using Atc.Rest.Client;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public static class XmlListRequestClientProbe
        {
            public static async Task<List<string>> RunAsync(Uri baseAddress, bool typedClient)
            {
                var assembly = typeof(XmlListRequestClientProbe).Assembly;
                var pets = List(assembly, "Pet", Activator.CreateInstance(assembly.GetTypes().Single(t => t.Name == "Pet"), 7L, "Rex")!, Activator.CreateInstance(assembly.GetTypes().Single(t => t.Name == "Pet"), 8L, "Fido")!);
                var notes = List(assembly, "Note", Activator.CreateInstance(assembly.GetTypes().Single(t => t.Name == "Note"), "Hi & <bye>")!);
                var rows = new List<string>();
                foreach (var (operation, body) in new (string, object)[] { ("AddPets", pets), ("AddNotes", notes) })
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

            private static object List(System.Reflection.Assembly assembly, string model, params object[] items)
            {
                var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(assembly.GetTypes().Single(t => t.Name == model)))!;
                foreach (var item in items)
                {
                    list.Add(item);
                }

                return list;
            }

            private static async Task<object> CallEndpointAsync(Uri baseAddress, string operation, object body)
            {
                var assembly = typeof(XmlListRequestClientProbe).Assembly;
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
                var assembly = typeof(XmlListRequestClientProbe).Assembly;
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
                    var accessor = value.GetType().GetProperties().FirstOrDefault(p => p.Name is "OkContent" or "SuccessContent");
                    return $"{(int)response.StatusCode} {Model(response.IsSuccess ? accessor?.GetValue(value) : response.Content)}";
                }

                return Model(value);
            }

            private static string Model(object? value)
                => value is null ? "null" : JsonSerializer.Serialize(value, value.GetType());
        }
        """";

    [Fact]
    public async Task Server_BindsAnXmlOrJsonList_ByContentType_AndRejectsWhatItCannotRead()
    {
        await using var host = await XmlTestHost.StartAsync(Yaml, HandlerSource, "ErrorBodies.RuntimeTest.XmlListRequestRegistration");

        var rows = new List<string>
        {
            await host.PostAsync("/pets/batch", "application/json", """[{"id":1,"name":"Rex"},{"id":2,"name":"Fido"}]""", accept: null),
            await host.PostAsync("/pets/batch", "application/xml", """<pets><pet id="1"><name>Rex</name></pet><pet id="2"><name>Fido</name></pet></pets>""", accept: null),

            // Another producer's root name: the items are read by the model's element name
            await host.PostAsync("/pets/batch", "application/xml", """<ArrayOfPet><pet id="3"><name>Bo</name></pet></ArrayOfPet>""", accept: null),
            await host.PostAsync("/pets/batch", "application/xml", """<pets/>""", accept: null),

            // Not a declared media type, a malformed document, a DTD, an item the mapping cannot read
            await host.PostAsync("/pets/batch", "text/plain", "Rex", accept: null),
            await host.PostAsync("/pets/batch", "application/xml", """<pets><pet id="1"><name>unclosed</pet></pets>""", accept: null),
            await host.PostAsync("/pets/batch", "application/xml", """<!DOCTYPE pets [<!ENTITY x "y">]><pets><pet id="1"><name>&x;</name></pet></pets>""", accept: null),
            await host.PostAsync("/pets/batch", "application/xml", """<pets><pet id="1"></pet></pets>""", accept: null),

            // The XML-only list: XML is bound, JSON is a 415
            await host.PostAsync("/notes/batch", "application/xml", """<ArrayOfNote><note><text>Hi</text></note><note><text>Yo</text></note></ArrayOfNote>""", accept: null),
            await host.PostAsync("/notes/batch", "application/json", """[{"text":"Hi"}]""", accept: null),
        };

        Assert.Equal(
            [
                "200 application/json; charset=utf-8 [{\"id\":1,\"name\":\"Rex via application/json\"},{\"id\":2,\"name\":\"Fido via application/json\"}]",
                "200 application/json; charset=utf-8 [{\"id\":1,\"name\":\"Rex via application/xml\"},{\"id\":2,\"name\":\"Fido via application/xml\"}]",
                "200 application/json; charset=utf-8 [{\"id\":3,\"name\":\"Bo via application/xml\"}]",
                "200 application/json; charset=utf-8 []",
                "415 application/problem+json title=Unsupported Media Type",
                "400 application/problem+json title=Bad Request",
                "400 application/problem+json title=Bad Request",
                "400 application/problem+json title=Bad Request",
                "200 application/json; charset=utf-8 [{\"text\":\"Hi via application/xml\"},{\"text\":\"Yo via application/xml\"}]",
                "415 application/problem+json title=Unsupported Media Type",
            ],
            rows,
            StringComparer.Ordinal);
    }

    [Fact]
    public async Task Server_ValidatesTheItemsOfAnXmlList()
    {
        await using var host = await XmlTestHost.StartAsync(Yaml, HandlerSource, "ErrorBodies.RuntimeTest.XmlListRequestRegistration");

        var xml = await host.PostAsync("/pets/batch", "application/xml", """<pets><pet id="1"><name>Rex</name></pet><pet id="2"><name>a name that is much too long for the limit</name></pet></pets>""", accept: null);
        var json = await host.PostAsync("/pets/batch", "application/json", """[{"id":1,"name":"Rex"},{"id":2,"name":"a name that is much too long for the limit"}]""", accept: null);

        // The validation filter runs on every item, whichever format the list arrived in
        Assert.StartsWith("400 application/problem+json errors=", xml, StringComparison.Ordinal);
        Assert.StartsWith("400 application/problem+json errors=", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Server_BindsTheListByContentType_AndNamesTheAcceptedMediaTypes()
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", ErrorResponseBodyRuntimeTests.ServerMarker, includeMinimalApi: true, Yaml);
        var endpoints = string.Join(Environment.NewLine, sources.Where(s => s.HintName.Contains("EndpointDefinition", StringComparison.Ordinal)).Select(s => s.Source));
        var parameters = string.Join(Environment.NewLine, sources.Where(s => s.HintName.Contains("Parameters", StringComparison.Ordinal)).Select(s => s.Source));

        Assert.Contains(".Accepts<List<Pet>>(\"application/json\", \"application/xml\")", endpoints, StringComparison.Ordinal);
        Assert.Contains(".Accepts<List<Note>>(\"application/xml\")", endpoints, StringComparison.Ordinal);
        Assert.Contains("[property: Required] RequestBodyList<Pet> Request", parameters, StringComparison.Ordinal);
        Assert.DoesNotContain("FromBody", parameters, StringComparison.Ordinal);
        Assert.Empty(CompilationVerificationHarness.CompileGeneratedSources(sources, includeMinimalApi: true));
    }

    [Theory]
    [InlineData("EndpointPerOperation", false)]
    [InlineData("EndpointPerOperation", true)]
    [InlineData("Throw", false)]
    [InlineData("Throw", true)]
    [InlineData("Result", false)]
    [InlineData("Result", true)]
    public async Task Clients_SendAnXmlList_TheServerBindsIt(
        string client,
        bool preferXml)
    {
        await using var host = await XmlTestHost.StartAsync(Yaml, HandlerSource, "ErrorBodies.RuntimeTest.XmlListRequestRegistration");
        var prefer = preferXml ? ", \"preferXml\": true" : string.Empty;
        var marker = client == "EndpointPerOperation"
            ? $$"""{ "generationMode": "EndpointPerOperation", "errorResponseFormat": "ProblemDetails"{{prefer}} }"""
            : $$"""{ "generationMode": "TypedClient", "typedClientResultStyle": "{{client}}"{{prefer}} }""";
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", marker, includeMinimalApi: false, Yaml);
        sources.Add(("XmlListRequestClientProbe.cs", ClientProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);
        var rows = await (Task<List<string>>)assembly.GetType("ErrorBodies.RuntimeTest.XmlListRequestClientProbe", throwOnError: true)!.GetMethod("RunAsync")!.Invoke(null, [host.BaseAddress, client != "EndpointPerOperation"])!;

        var ok = client == "Throw" ? string.Empty : "200 ";

        // What the client sent: the XML list with charset=utf-8; JSON as Atc.Rest.Client (no charset) or JsonContent does
        var petsVia = preferXml
            ? "application/xml; charset=utf-8"
            : client == "EndpointPerOperation" ? "application/json" : "application/json; charset=utf-8";
        Assert.Equal(
            [
                $"AddPets: {ok}[{{\"Id\":7,\"Name\":\"Rex via {petsVia}\"}},{{\"Id\":8,\"Name\":\"Fido via {petsVia}\"}}]",
                $"AddNotes: {ok}[{{\"Text\":\"Hi \\u0026 \\u003Cbye\\u003E via application/xml; charset=utf-8\"}}]",
            ],
            rows,
            StringComparer.Ordinal);
    }
}