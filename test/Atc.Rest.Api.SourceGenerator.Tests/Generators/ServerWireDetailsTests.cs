namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// Smaller things the generated server owes the wire: a negotiated JSON/XML response says <c>Vary: Accept</c> and reads
/// the media types of <c>Accept</c> case-insensitively; a JSON Lines stream is one item per line even when the host
/// writes indented JSON; an OAS 3.2 <c>in: querystring</c> parameter is the raw query string (it was bound from a
/// query key of its C# name, so always null); a 201 or 202 with a body can say where the resource is (<c>Location</c>).
/// </summary>
[Collection(nameof(ServerWireDetailsTests))]
[CollectionDefinition(nameof(ServerWireDetailsTests), DisableParallelization = true)]
public sealed class ServerWireDetailsTests
{
    private const string Yaml = """
        openapi: 3.2.0
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /pets/{id}:
            get:
              operationId: getPet
              parameters:
                - name: id
                  in: path
                  required: true
                  schema: { type: integer }
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema: { $ref: '#/components/schemas/Pet' }
                    application/xml:
                      schema: { $ref: '#/components/schemas/Pet' }
          /lines:
            get:
              operationId: listLines
              x-return-async-enumerable: true
              responses:
                '200':
                  description: Pets
                  content:
                    application/jsonl:
                      schema: { $ref: '#/components/schemas/Pet' }
          /search:
            get:
              operationId: search
              parameters:
                - name: filter
                  in: querystring
                  content:
                    application/x-www-form-urlencoded:
                      schema: { type: string }
              responses:
                '200':
                  description: The query string the server bound
                  content:
                    application/json:
                      schema: { type: string }
        components:
          schemas:
            Pet:
              type: object
              required: [id, name]
              properties:
                id: { type: integer, format: int64 }
                name: { type: string }
              xml: { name: pet }
        """;

    private const string HandlerSource = """"
        using System.Runtime.CompilerServices;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public sealed class GetPetHandlerImpl : IGetPetHandler
        {
            public System.Threading.Tasks.Task<GetPetResult> ExecuteAsync(GetPetParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(GetPetResult.Ok(new Pet(parameters.Id, "Rex")));
        }

        public sealed class ListLinesHandlerImpl : IListLinesHandler
        {
            public System.Threading.Tasks.Task<ListLinesResult> ExecuteAsync(System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(ListLinesResult.Ok(Pets(cancellationToken)));

            private static async IAsyncEnumerable<Pet> Pets([EnumeratorCancellation] System.Threading.CancellationToken cancellationToken)
            {
                yield return new Pet(1, "Rex");
                await System.Threading.Tasks.Task.Yield();
                yield return new Pet(2, "Fido");
            }
        }

        public sealed class SearchHandlerImpl : ISearchHandler
        {
            public System.Threading.Tasks.Task<SearchResult> ExecuteAsync(SearchParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(SearchResult.Ok(parameters.Filter ?? "(null)"));
        }

        public static class DetailsRegistration
        {
            public static void Register(IServiceCollection services)
            {
                services.AddScoped<IGetPetHandler, GetPetHandlerImpl>();
                services.AddScoped<IListLinesHandler, ListLinesHandlerImpl>();
                services.AddScoped<ISearchHandler, SearchHandlerImpl>();
            }
        }
        """";

    private const string LocationYaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /pets:
            post:
              operationId: createPet
              requestBody:
                required: true
                content:
                  application/json:
                    schema: { $ref: '#/components/schemas/Pet' }
              responses:
                '201':
                  description: Created
                  content:
                    application/json:
                      schema: { $ref: '#/components/schemas/Pet' }
          /jobs:
            post:
              operationId: startJob
              responses:
                '202':
                  description: Accepted
                  content:
                    application/json:
                      schema: { $ref: '#/components/schemas/Pet' }
        components:
          schemas:
            Pet:
              type: object
              required: [id, name]
              properties:
                id: { type: integer, format: int64 }
                name: { type: string }
        """;

    private const string LocationHandlerSource = """"
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public sealed class CreatePetHandlerImpl : ICreatePetHandler
        {
            public System.Threading.Tasks.Task<CreatePetResult> ExecuteAsync(CreatePetParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(CreatePetResult.Created(parameters.Request, location: "/pets/" + parameters.Request.Id));
        }

        public sealed class StartJobHandlerImpl : IStartJobHandler
        {
            public System.Threading.Tasks.Task<StartJobResult> ExecuteAsync(System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(StartJobResult.Accepted(new Pet(7, "Job"), location: "/jobs/7"));
        }

        public static class LocationRegistration
        {
            public static void Register(IServiceCollection services)
            {
                services.AddScoped<ICreatePetHandler, CreatePetHandlerImpl>();
                services.AddScoped<IStartJobHandler, StartJobHandlerImpl>();
            }
        }
        """";

    [Theory]
    [InlineData("application/json", "application/json")]
    [InlineData("Application/JSON, application/xml;q=0.5", "application/json")]
    [InlineData("application/xml", "application/xml")]
    public async Task ANegotiatedResponse_VariesByAccept_AndReadsMediaTypesCaseInsensitively(
        string accept,
        string expectedMediaType)
    {
        await using var host = await XmlTestHost.StartAsync(Yaml, HandlerSource, "ErrorBodies.RuntimeTest.DetailsRegistration");
        using var client = new HttpClient { BaseAddress = host.BaseAddress };
        using var request = new HttpRequestMessage(HttpMethod.Get, "/pets/1");
        request.Headers.TryAddWithoutValidation("Accept", accept);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectedMediaType, response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Accept", response.Headers.Vary, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task JsonLines_AreOneItemPerLine_WhenTheHostWritesIndentedJson()
    {
        await using var host = await XmlTestHost.StartAsync(
            Yaml,
            HandlerSource,
            "ErrorBodies.RuntimeTest.DetailsRegistration",
            configureServices: services => services.ConfigureHttpJsonOptions(o => o.SerializerOptions.WriteIndented = true));

        var (status, _, body) = await host.SendRawAsync(HttpMethod.Get, "/lines", accept: "application/jsonl");

        Assert.Equal(200, status);
        var lines = body.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.Equal("Rex", (string?)JsonNode.Parse(lines[0])!["name"]);
        Assert.Equal("Fido", (string?)JsonNode.Parse(lines[1])!["name"]);
    }

    [Theory]
    [InlineData("/search?a=1&b=x%20y", "a=1&b=x%20y")]
    [InlineData("/search", "(null)")]
    public async Task AQueryStringParameter_IsTheRawQueryString(
        string pathAndQuery,
        string expected)
    {
        await using var host = await XmlTestHost.StartAsync(Yaml, HandlerSource, "ErrorBodies.RuntimeTest.DetailsRegistration");

        var (status, _, body) = await host.SendRawAsync(HttpMethod.Get, pathAndQuery, accept: "application/json");

        Assert.True(status == 200, $"{status}: {body}");
        Assert.Equal(expected, JsonNode.Parse(body)!.GetValue<string>());
    }

    /// <summary>
    /// Both C# clients put a querystring parameter on the URL as it is (the EndpointPerOperation client left it out).
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Clients_SendAQueryStringParameterAsTheQueryString(
        bool typedClient)
    {
        var marker = typedClient
            ? """{ "generationMode": "TypedClient" }"""
            : ErrorResponseBodyRuntimeTests.ClientOperationMarker("ProblemDetails");
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", marker, includeMinimalApi: false, Yaml);
        sources.Add(("SearchProbe.cs", SearchProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);

        var requested = await (Task<string>)assembly.GetType("ErrorBodies.RuntimeTest.SearchProbe", throwOnError: true)!.GetMethod("RunAsync")!.Invoke(null, [typedClient])!;

        Assert.Equal("/search?a=1&b=x%20y", requested);
    }

    private const string SearchProbeSource = """"
        using System.Net;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public static class SearchProbe
        {
            private static string? requested;

            public static async Task<string> RunAsync(bool typedClient)
            {
                var assembly = typeof(SearchProbe).Assembly;
                var parameters = Activator.CreateInstance(assembly.GetTypes().Single(t => t.Name == "SearchParameters"), "a=1&b=x%20y")!;
                if (typedClient)
                {
                    var clientType = assembly.GetTypes().Single(t => t.IsClass && !t.IsAbstract && t.GetMethod("SearchAsync") is not null);
                    var client = Activator.CreateInstance(clientType, new HttpClient(new StubHandler()) { BaseAddress = new Uri("http://stub/") })!;
                    await (Task)clientType.GetMethod("SearchAsync")!.Invoke(client, [parameters, CancellationToken.None])!;
                }
                else
                {
                    var httpClientName = (string)assembly.GetTypes().Single(t => t.Name == "Constants").GetField("HttpClientName")!.GetValue(null)!;
                    var services = new ServiceCollection();
                    services
                        .AddHttpClient(httpClientName, c => c.BaseAddress = new Uri("http://stub/"))
                        .ConfigurePrimaryHttpMessageHandler(() => new StubHandler());
                    assembly.GetTypes().SelectMany(t => t.GetMethods()).Single(m => m.Name == "AddErrorBodiesEndpoints").Invoke(null, [services, null]);
                    using var provider = services.BuildServiceProvider();
                    var endpointType = assembly.GetTypes().Single(t => t.Name == "ISearchEndpoint");
                    await (Task)endpointType.GetMethod("ExecuteAsync")!.Invoke(provider.GetRequiredService(endpointType), [parameters, httpClientName, CancellationToken.None])!;
                }

                return requested!;
            }

            private sealed class StubHandler : HttpMessageHandler
            {
                protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    requested = request.RequestUri!.PathAndQuery;
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("\"ok\"", System.Text.Encoding.UTF8, "application/json"),
                    });
                }
            }
        }
        """";

    [Theory]
    [InlineData("/pets", """{"id":5,"name":"Rex"}""", 201, "/pets/5")]
    [InlineData("/jobs", null, 202, "/jobs/7")]
    public async Task ACreatedOrAcceptedBody_CanSayWhereTheResourceIs(
        string path,
        string? body,
        int expectedStatus,
        string expectedLocation)
    {
        await using var host = await XmlTestHost.StartAsync(LocationYaml, LocationHandlerSource, "ErrorBodies.RuntimeTest.LocationRegistration");
        using var client = new HttpClient { BaseAddress = host.BaseAddress };

        using var content = body is null ? null : new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(
            new Uri(path, UriKind.Relative),
            content,
            TestContext.Current.CancellationToken);

        Assert.Equal(expectedStatus, (int)response.StatusCode);
        Assert.Equal(expectedLocation, response.Headers.Location?.OriginalString);
        Assert.NotEmpty(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}