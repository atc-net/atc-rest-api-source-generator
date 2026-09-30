namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// <c>application/problem+xml</c> (RFC 9457 Appendix B) against a live generated server: every error source - a
/// handler's problem, a thrown exception (the error middleware), an unmatched route (status-code pages), the validation
/// filter and a malformed body - answers XML when <c>Accept</c> prefers <c>application/problem+xml</c>, and JSON
/// otherwise (also for a client that asks only for <c>application/xml</c>). The <c>errors</c> member uses the RFC's
/// generic mapping. The EndpointPerOperation client reads a problem+xml body into its typed ProblemDetails.
/// </summary>
public sealed class XmlProblemDetailsTests
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
                  maxLength: 5
              xml:
                name: pet
        """;

    private const string PreferXmlAccept = "application/xml, application/problem+xml;q=0.9, application/problem+json;q=0.8";

    private const string HandlerSource = """"
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public sealed class GetPetHandlerImpl : IGetPetHandler
        {
            public System.Threading.Tasks.Task<GetPetResult> ExecuteAsync(GetPetParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => parameters.Id switch
                {
                    404 => System.Threading.Tasks.Task.FromResult(GetPetResult.NotFound("No pet 404")),
                    500 => throw new System.InvalidOperationException("boom"),
                    _ => System.Threading.Tasks.Task.FromResult(GetPetResult.Ok(new Pet(parameters.Id, "Rex"))),
                };
        }

        public sealed class AddPetHandlerImpl : IAddPetHandler
        {
            public System.Threading.Tasks.Task<AddPetResult> ExecuteAsync(AddPetParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(AddPetResult.Ok(parameters.Request));
        }

        public static class ProblemRegistration
        {
            public static void Register(IServiceCollection services)
            {
                services.AddScoped<IGetPetHandler, GetPetHandlerImpl>();
                services.AddScoped<IAddPetHandler, AddPetHandlerImpl>();
            }
        }
        """";

    /// <summary>
    /// Calls the live host through the generated EndpointPerOperation client and describes the typed problem.
    /// </summary>
    private const string ClientProbeSource = """"
        using Atc.Rest.Client;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public static class ProblemClientProbe
        {
            public static async Task<List<string>> RunAsync(Uri baseAddress)
            {
                var assembly = typeof(ProblemClientProbe).Assembly;
                var httpClientName = (string)assembly.GetTypes().Single(t => t.Name == "Constants").GetField("HttpClientName")!.GetValue(null)!;
                var services = new ServiceCollection();
                services.AddHttpClient(httpClientName, c => c.BaseAddress = baseAddress);
                assembly.GetTypes().SelectMany(t => t.GetMethods()).Single(m => m.Name == "AddErrorBodiesEndpoints").Invoke(null, [services]);
                using var provider = services.BuildServiceProvider();
                var pet = assembly.GetTypes().Single(t => t.Name == "Pet");

                var rows = new List<string>();
                foreach (var (operation, argument, accessor) in new (string, object, string)[]
                {
                    ("GetPet", 404, "NotFoundContent"),
                    ("GetPet", 500, "InternalServerErrorContent"),
                    ("AddPet", Activator.CreateInstance(pet, 1L, "far-too-long")!, "BadRequestContent"),
                })
                {
                    var endpointType = assembly.GetTypes().Single(t => t.Name == $"I{operation}Endpoint");
                    var execute = endpointType.GetMethod("ExecuteAsync")!;
                    var parameters = Activator.CreateInstance(execute.GetParameters()[0].ParameterType, argument);
                    var task = (Task)execute.Invoke(provider.GetRequiredService(endpointType), [parameters, httpClientName, CancellationToken.None])!;
                    await task;
                    var result = (EndpointResponse)task.GetType().GetProperty("Result")!.GetValue(task)!;
                    var problem = result.GetType().GetProperty(accessor)!.GetValue(result)!;
                    var type = problem.GetType();
                    var errors = type.GetProperty("Errors")?.GetValue(problem) as Dictionary<string, string[]>;
                    rows.Add($"{operation}({(argument is int ? argument : "far-too-long")}): {(int)result.StatusCode} {type.Name} status={type.GetProperty("Status")!.GetValue(problem)} title={type.GetProperty("Title")!.GetValue(problem)} detail={type.GetProperty("Detail")!.GetValue(problem)}{(errors is null ? string.Empty : " errors=" + string.Join(",", errors.Select(e => $"{e.Key}:{e.Value.Length}")))}");
                }

                return rows;
            }
        }
        """";

    [Fact]
    public async Task Server_AnswersProblemXml_WhenAcceptPrefersIt_FromEveryErrorSource()
    {
        await using var host = await XmlTestHost.StartAsync(Yaml, HandlerSource, "ErrorBodies.RuntimeTest.ProblemRegistration");

        var rows = new List<string>
        {
            Describe("handler 404, problem+xml", await host.SendRawAsync(HttpMethod.Get, "/pets/404", "application/problem+xml")),
            Describe("handler 404, preferXml", await host.SendRawAsync(HttpMethod.Get, "/pets/404", PreferXmlAccept)),
            Describe("handler 404, no Accept", await host.SendRawAsync(HttpMethod.Get, "/pets/404", accept: null)),
            Describe("handler 404, application/xml", await host.SendRawAsync(HttpMethod.Get, "/pets/404", "application/xml")),
            Describe("handler 404, problem+json first", await host.SendRawAsync(HttpMethod.Get, "/pets/404", "application/problem+json, application/problem+xml;q=0.5")),
            Describe("thrown 500", await host.SendRawAsync(HttpMethod.Get, "/pets/500", "application/problem+xml")),
            Describe("unmatched route", await host.SendRawAsync(HttpMethod.Get, "/nothing/here", "application/problem+xml")),
            Describe("validation 400", await host.SendRawAsync(HttpMethod.Post, "/pets", "application/problem+xml", "application/json", """{"id":1,"name":"far-too-long"}""")),
            Describe("malformed body", await host.SendRawAsync(HttpMethod.Post, "/pets", "application/problem+xml", "application/xml", "<pet><name>x</pet>")),
        };

        Assert.Equal(
            [
                "handler 404, problem+xml: 404 application/problem+xml; charset=utf-8 status=404 title=Not Found detail=No pet 404",
                "handler 404, preferXml: 404 application/problem+xml; charset=utf-8 status=404 title=Not Found detail=No pet 404",
                "handler 404, no Accept: 404 application/problem+json json",
                "handler 404, application/xml: 404 application/problem+json json",
                "handler 404, problem+json first: 404 application/problem+json json",
                "thrown 500: 500 application/problem+xml; charset=utf-8 status=500 title=An error occurred while processing your request. detail=",
                "unmatched route: 404 application/problem+xml; charset=utf-8 status=404 title=Not Found detail=",
                "validation 400: 400 application/problem+xml; charset=utf-8 status=400 title=One or more validation errors occurred. detail= errors=Request.name:1",
                "malformed body: 400 application/problem+xml; charset=utf-8 status=400 title=Bad Request detail=The XML request body is invalid: The 'name' start tag on line 1 position 7 does not match the end tag of 'pet'. Line 1, position 15.",
            ],
            rows,
            StringComparer.Ordinal);
    }

    [Fact]
    public async Task Server_ProblemXml_IsTheRfc9457AppendixBDocument()
    {
        await using var host = await XmlTestHost.StartAsync(Yaml, HandlerSource, "ErrorBodies.RuntimeTest.ProblemRegistration");
        var (_, _, body) = await host.SendRawAsync(HttpMethod.Post, "/pets", "application/problem+xml", "application/json", """{"id":1,"name":"far-too-long"}""");

        // The RFC 9457 namespace on every element; an object is an element with children, an array is <i> children
        var root = XDocument.Parse(body).Root!;
        XNamespace rfc = "urn:ietf:rfc:7807";
        Assert.Equal(rfc + "problem", root.Name);
        Assert.All(root.Descendants(), e => Assert.Equal(rfc, e.Name.Namespace));
        var errors = root.Element(rfc + "errors")!;
        var field = Assert.Single(errors.Elements());
        var message = Assert.Single(field.Elements());
        Assert.Equal(rfc + "i", message.Name);
        Assert.False(string.IsNullOrWhiteSpace(message.Value));
        Assert.NotNull(root.Element(rfc + "traceId"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EndpointPerOperationClient_ReadsProblemXml_IntoItsTypedProblem(
        bool preferXml)
    {
        await using var host = await XmlTestHost.StartAsync(Yaml, HandlerSource, "ErrorBodies.RuntimeTest.ProblemRegistration");
        var prefer = preferXml ? ", \"preferXml\": true" : string.Empty;
        var marker = $$"""{ "generationMode": "EndpointPerOperation", "errorResponseFormat": "ProblemDetails"{{prefer}} }""";
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", marker, includeMinimalApi: false, Yaml);
        sources.Add(("ProblemClientProbe.cs", ClientProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);
        var rows = await (Task<List<string>>)assembly.GetType("ErrorBodies.RuntimeTest.ProblemClientProbe", throwOnError: true)!.GetMethod("RunAsync")!.Invoke(null, [host.BaseAddress])!;

        // The same typed problem whether the server answered JSON (default Accept) or XML (preferXml)
        Assert.Equal(
            [
                "GetPet(404): 404 ProblemDetails status=404 title=Not Found detail=No pet 404",
                "GetPet(500): 500 ProblemDetails status=500 title=An error occurred while processing your request. detail=",
                "AddPet(far-too-long): 400 ValidationProblemDetails status=400 title=One or more validation errors occurred. detail= errors=Request.name:1",
            ],
            rows,
            StringComparer.Ordinal);
    }

    private static string Describe(
        string name,
        (int Status, string? MediaType, string Body) response)
    {
        if (response.MediaType?.StartsWith("application/problem+xml", StringComparison.Ordinal) != true)
        {
            return $"{name}: {response.Status} {response.MediaType} {(response.Body.TrimStart().StartsWith('{') ? "json" : response.Body)}";
        }

        var root = XDocument.Parse(response.Body).Root!;
        string? Value(string element)
            => root.Elements().FirstOrDefault(e => e.Name.LocalName == element)?.Value;
        var errors = root.Elements().FirstOrDefault(e => e.Name.LocalName == "errors");
        return $"{name}: {response.Status} {response.MediaType} status={Value("status")} title={Value("title")} detail={Value("detail")}"
               + (errors is null ? string.Empty : " errors=" + string.Join(",", errors.Elements().Select(e => $"{XmlConvert.DecodeName(e.Name.LocalName)}:{e.Elements().Count()}")));
    }
}