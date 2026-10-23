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
                assembly.GetTypes().SelectMany(t => t.GetMethods()).Single(m => m.Name == "AddErrorBodiesEndpoints").Invoke(null, [services, null]);
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
        await using var host = await XmlTestHost.StartAsync(Yaml, HandlerSource, "ErrorBodies.RuntimeTest.XmlRequestRegistration");

        var rows = new List<string>
        {
            await host.PostAsync("/pets", "application/json", """{"id":1,"name":"Rex"}""", accept: null),
            await host.PostAsync("/pets", "application/xml", """<pet id="1"><name>Rex</name></pet>""", accept: "application/xml"),

            // Not a declared media type: the Accepts metadata makes routing answer 415
            await host.PostAsync("/pets", "text/xml; charset=utf-8", """<pet id="2"><name>Fido</name></pet>""", accept: null),
            await host.PostAsync("/pets", "text/plain", "Rex", accept: null),
            await host.PostAsync("/pets", "application/xml", """<pet id="1"><name>unclosed</pet>""", accept: null),
            await host.PostAsync("/pets", "application/xml", """<!DOCTYPE pet [<!ENTITY x "y">]><pet id="1"><name>&x;</name></pet>""", accept: null),

            // The validation filter runs on the bound model, keyed by the name the client sent
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
                "400 application/problem+json errors=name",
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

        // The validation filter keys an XML request's errors by these names; a name the
        // XML shares with JSON (id as an attribute, zip) needs none
        Assert.Contains("[property: global::System.Xml.Serialization.XmlElement(\"surname\")] string? Name", person, StringComparison.Ordinal);
        Assert.Contains("[property: global::System.Xml.Serialization.XmlElement(\"town\")] string? City", address, StringComparison.Ordinal);
        Assert.DoesNotContain("XmlAttribute", person, StringComparison.Ordinal);
        Assert.DoesNotContain("XmlElement(\"zip\")", address, StringComparison.Ordinal);
        Assert.Empty(CompilationVerificationHarness.CompileGeneratedSources(sources, includeMinimalApi: true));
    }

    /// <summary>
    /// A message names a field by its XML name for an XML request, as the key does (nested models too): for a model
    /// only an XML request carries the generator writes the XML name into the message; for a model a JSON request
    /// carries as well, Atc.Rest.MinimalApi renames the field in the message per request (after 3.1.0). A property whose
    /// only rule has arguments (a length here) is named by its wire name too; it used to keep its C# name.
    /// </summary>
    [Fact]
    public async Task Server_ValidationMessages_OfAModelOnlyXmlCarries_NameTheXmlNames()
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
                    '204':
                      description: Stored
            components:
              schemas:
                Person:
                  type: object
                  properties:
                    givenName:
                      type: string
                      maxLength: 5
                      xml:
                        name: surname
                    address:
                      $ref: '#/components/schemas/Address'
                  xml:
                    name: person
                Address:
                  type: object
                  properties:
                    city:
                      type: string
                      maxLength: 3
                      xml:
                        name: town
                Pet:
                  type: object
                  properties:
                    nickName:
                      type: string
                      maxLength: 3
                      xml:
                        name: alias
                  xml:
                    name: pet
            """;
        const string handlerSource = """"
            using Microsoft.Extensions.DependencyInjection;

            namespace ErrorBodies.RuntimeTest;

            public sealed class AddPersonHandlerImpl : IAddPersonHandler
            {
                public System.Threading.Tasks.Task<AddPersonResult> ExecuteAsync(AddPersonParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                    => System.Threading.Tasks.Task.FromResult(AddPersonResult.NoContent());
            }

            public sealed class AddPetHandlerImpl : IAddPetHandler
            {
                public System.Threading.Tasks.Task<AddPetResult> ExecuteAsync(AddPetParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                    => System.Threading.Tasks.Task.FromResult(AddPetResult.NoContent());
            }

            public static class PersonRegistration
            {
                public static void Register(IServiceCollection services)
                    => services
                        .AddScoped<IAddPersonHandler, AddPersonHandlerImpl>()
                        .AddScoped<IAddPetHandler, AddPetHandlerImpl>();
            }
            """";
        await using var host = await XmlTestHost.StartAsync(yaml, handlerSource, "ErrorBodies.RuntimeTest.PersonRegistration");

        var rows = new List<string>
        {
            await host.PostAsync("/people", "application/xml", "<person><surname>Lovelace</surname></person>", accept: null, withMessages: true),
            await host.PostAsync("/people", "application/xml", "<person><address><town>London</town></address></person>", accept: null, withMessages: true),
            await host.PostAsync("/pets", "application/xml", "<pet><alias>Rexy</alias></pet>", accept: null, withMessages: true),
            await host.PostAsync("/pets", "application/json", """{"nickName":"Rexy"}""", accept: null, withMessages: true),
        };

        Assert.Equal(
            """
            400 application/problem+json errors=surname: The field surname must be a string or array type with a maximum length of '5'.
            400 application/problem+json errors=address.town: The field town must be a string or array type with a maximum length of '3'.
            400 application/problem+json errors=alias: The field alias must be a string or array type with a maximum length of '3'.
            400 application/problem+json errors=nickName: The field nickName must be a string or array type with a maximum length of '3'.
            """,
            string.Join("\n", rows),
            ignoreLineEndingDifferences: true);
    }

    /// <summary>
    /// A validation error names the field as the client sent it (Atc.Rest.MinimalApi 3.0 treats a body bound by
    /// <c>BindAsync</c> as the body): an XML request by its XML names, nested ones too; a JSON request to the same
    /// operation by its JSON names; a missing body under the empty key.
    /// </summary>
    [Fact]
    public async Task Server_ValidationErrors_AreKeyedByTheNamesTheClientSent()
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
                      application/json:
                        schema:
                          $ref: '#/components/schemas/Person'
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
                    name:
                      type: string
                      maxLength: 5
                      xml:
                        name: surname
                    address:
                      $ref: '#/components/schemas/Address'
                  xml:
                    name: person
                Address:
                  type: object
                  properties:
                    city:
                      type: string
                      maxLength: 3
                      xml:
                        name: town
            """;
        const string handlerSource = """"
            using Microsoft.Extensions.DependencyInjection;

            namespace ErrorBodies.RuntimeTest;

            public sealed class AddPersonHandlerImpl : IAddPersonHandler
            {
                public System.Threading.Tasks.Task<AddPersonResult> ExecuteAsync(AddPersonParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                    => System.Threading.Tasks.Task.FromResult(AddPersonResult.NoContent());
            }

            public static class PersonRegistration
            {
                public static void Register(IServiceCollection services)
                    => services.AddScoped<IAddPersonHandler, AddPersonHandlerImpl>();
            }
            """";
        await using var host = await XmlTestHost.StartAsync(yaml, handlerSource, "ErrorBodies.RuntimeTest.PersonRegistration");

        var rows = new List<string>
        {
            await host.PostAsync("/people", "application/xml", "<person><surname>Ada</surname></person>", accept: null),
            await host.PostAsync("/people", "application/xml", "<person><surname>Lovelace</surname></person>", accept: null),
            await host.PostAsync("/people", "application/json", """{"name":"Lovelace"}""", accept: null),
            await host.PostAsync("/people", "application/xml", "<person><address><town>London</town></address></person>", accept: null),
            await host.PostAsync("/people", "application/json", """{"address":{"city":"London"}}""", accept: null),
            await host.PostAsync("/people", "application/json", string.Empty, accept: null),
        };

        Assert.Equal(
            [
                "204  ",
                "400 application/problem+json errors=surname",
                "400 application/problem+json errors=name",
                "400 application/problem+json errors=address.town",
                "400 application/problem+json errors=address.city",
                "400 application/problem+json errors=",
            ],
            rows,
            StringComparer.Ordinal);
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
        await using var host = await XmlTestHost.StartAsync(Yaml, HandlerSource, "ErrorBodies.RuntimeTest.XmlRequestRegistration");
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
}