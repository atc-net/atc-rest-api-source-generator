namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// An enum is on the wire as the specification writes it, whatever enum converter the host registers. An integer enum
/// is a number (a host-wide <c>JsonStringEnumConverter</c> - the samples register one, the typed client had one - used
/// to write <c>"Medium"</c> where the specification says <c>2</c>); a string enum is its strings and nothing else (a
/// number such as <c>{"state":42}</c> is a 400, not a member that does not exist); two values that differ in case only
/// (<c>active</c>, <c>Active</c>) are two members that each keep their value (the second used to be written
/// <c>"Active2"</c>); a value listed twice is one member.
/// </summary>
[Collection(nameof(EnumWireTests))]
[CollectionDefinition(nameof(EnumWireTests), DisableParallelization = true)]
public sealed class EnumWireTests
{
    internal const string Yaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /items:
            get:
              operationId: getItem
              parameters:
                - name: level
                  in: query
                  schema:
                    $ref: '#/components/schemas/Level'
              responses:
                '200':
                  description: An item at the level asked for
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Item'
            post:
              operationId: createItem
              requestBody:
                required: true
                content:
                  application/json:
                    schema:
                      $ref: '#/components/schemas/Item'
              responses:
                '200':
                  description: The same item
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Item'
        components:
          schemas:
            Level:
              type: integer
              enum: [1, 2, 3]
              x-enum-varnames: [Low, Medium, High]
            State:
              type: string
              enum: [active, Active, archived, archived]
            Item:
              type: object
              required: [level, state]
              properties:
                level:
                  $ref: '#/components/schemas/Level'
                state:
                  $ref: '#/components/schemas/State'
        """;

    private const string HandlerSource = """"
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public sealed class GetItemHandlerImpl : IGetItemHandler
        {
            public System.Threading.Tasks.Task<GetItemResult> ExecuteAsync(GetItemParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(GetItemResult.Ok(new Item(parameters.Level ?? Level.Low, State.Archived)));
        }

        public sealed class CreateItemHandlerImpl : ICreateItemHandler
        {
            public System.Threading.Tasks.Task<CreateItemResult> ExecuteAsync(CreateItemParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(CreateItemResult.Ok(parameters.Request));
        }

        public static class EnumRegistration
        {
            public static void Register(IServiceCollection services)
            {
                services.AddScoped<IGetItemHandler, GetItemHandlerImpl>();
                services.AddScoped<ICreateItemHandler, CreateItemHandlerImpl>();
            }
        }
        """";

    /// <summary>
    /// Sends an item (level Medium, the state member whose value is <c>Active</c>) through a generated client and asks
    /// for one at level High, against a stub that records the requests and answers <c>{"level":3,"state":"active"}</c>.
    /// Returns: the POST body, the GET path and query, and the level and state of the item read back, as written.
    /// </summary>
    private const string ClientProbeSource = """"
        using System.Net;
        using System.Reflection;
        using System.Text.Json;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public static class EnumProbe
        {
            private static readonly List<string> Wire = [];

            public static async Task<List<string>> RunAsync(bool typedClient)
            {
                var assembly = typeof(EnumProbe).Assembly;
                var levelType = assembly.GetTypes().Single(t => t.Name == "Level");
                var stateType = assembly.GetTypes().Single(t => t.Name == "State");
                var itemType = assembly.GetTypes().Single(t => t.Name == "Item");
                var medium = Enum.Parse(levelType, "Medium");
                var high = Enum.Parse(levelType, "High");
                var state = Member(stateType, "Active");
                var item = Activator.CreateInstance(itemType, medium, state)!;

                var createParameters = Create("CreateItemParameters", item);
                var getParameters = Create("GetItemParameters", high);

                object created;
                if (typedClient)
                {
                    var clientType = assembly.GetTypes().Single(t => t.IsClass && !t.IsAbstract && t.GetMethod("CreateItemAsync") is not null);
                    var client = Activator.CreateInstance(clientType, new HttpClient(new StubHandler()) { BaseAddress = new Uri("http://stub/") })!;
                    created = await Result(clientType.GetMethod("CreateItemAsync")!.Invoke(client, [createParameters, CancellationToken.None])!);
                    await Result(clientType.GetMethod("GetItemAsync")!.Invoke(client, [getParameters, CancellationToken.None])!);
                }
                else
                {
                    var httpClientName = (string)assembly.GetTypes().Single(t => t.Name == "Constants").GetField("HttpClientName")!.GetValue(null)!;
                    var services = new ServiceCollection();
                    services
                        .AddHttpClient(httpClientName, c => c.BaseAddress = new Uri("http://stub/"))
                        .ConfigurePrimaryHttpMessageHandler(() => new StubHandler());
                    assembly.GetTypes().SelectMany(t => t.GetMethods()).Single(m => m.Name == "AddErrorBodiesEndpoints").Invoke(null, [services]);
                    using var provider = services.BuildServiceProvider();
                    var createType = assembly.GetTypes().Single(t => t.Name == "ICreateItemEndpoint");
                    var createResult = await Result(createType.GetMethod("ExecuteAsync")!.Invoke(provider.GetRequiredService(createType), [createParameters, httpClientName, CancellationToken.None])!);
                    created = createResult.GetType().GetProperty("OkContent")!.GetValue(createResult)!;
                    var getType = assembly.GetTypes().Single(t => t.Name == "IGetItemEndpoint");
                    await Result(getType.GetMethod("ExecuteAsync")!.Invoke(provider.GetRequiredService(getType), [getParameters, httpClientName, CancellationToken.None])!);
                }

                Wire.Add(created.GetType().GetProperty("Level")!.GetValue(created)!.ToString()!);
                Wire.Add(JsonSerializer.Serialize(created.GetType().GetProperty("State")!.GetValue(created)!, stateType, new JsonSerializerOptions()));
                return Wire;

                object Create(string name, object? value)
                {
                    var type = assembly.GetTypes().Single(t => t.Name == name);
                    var ctor = type.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
                    return ctor.Invoke([value]);
                }
            }

            // The member of a string enum (a C# enum or an extensible enum struct) whose wire value is the one given
            private static object Member(Type type, string wireValue)
                => JsonSerializer.Deserialize("\"" + wireValue + "\"", type)!;

            private static async Task<object> Result(object task)
            {
                await (Task)task;
                return task.GetType().GetProperty("Result")!.GetValue(task)!;
            }

            private sealed class StubHandler : HttpMessageHandler
            {
                protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    Wire.Add(request.Content is null ? request.RequestUri!.PathAndQuery : await request.Content.ReadAsStringAsync(cancellationToken));
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{\"level\":3,\"state\":\"active\"}", System.Text.Encoding.UTF8, "application/json"),
                    };
                }
            }
        }
        """";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Server_WritesAnIntegerEnumAsItsNumber_AndEachStringAsItIs(
        bool hostWideStringEnumConverter)
    {
        await using var host = await StartAsync(hostWideStringEnumConverter);

        var (postStatus, _, posted) = await host.SendRawAsync(HttpMethod.Post, "/items", accept: "application/json", body: """{"level":2,"state":"Active"}""", contentType: "application/json");
        var (lowerStatus, _, lower) = await host.SendRawAsync(HttpMethod.Post, "/items", accept: "application/json", body: """{"level":1,"state":"active"}""", contentType: "application/json");
        var (getStatus, _, got) = await host.SendRawAsync(HttpMethod.Get, "/items?level=3", accept: "application/json");

        Assert.True(postStatus == 200, posted);
        Assert.Equal("""{"level":2,"state":"Active"}""", posted);
        Assert.True(lowerStatus == 200, lower);
        Assert.Equal("""{"level":1,"state":"active"}""", lower);
        Assert.True(getStatus == 200, got);
        Assert.Equal("""{"level":3,"state":"archived"}""", got);
    }

    [Theory]
    [InlineData(false, """{"level":2,"state":42}""")]
    [InlineData(true, """{"level":2,"state":42}""")]
    [InlineData(false, """{"level":2,"state":"Active2"}""")]
    [InlineData(true, """{"level":2,"state":"Active2"}""")]
    [InlineData(true, """{"level":"Medium","state":"active"}""")]
    public async Task Server_AValueTheSpecificationDoesNotList_IsA400(
        bool hostWideStringEnumConverter,
        string body)
    {
        await using var host = await StartAsync(hostWideStringEnumConverter);

        var (status, _, response) = await host.SendRawAsync(HttpMethod.Post, "/items", accept: "application/json", body: body, contentType: "application/json");

        Assert.True(status == 400, $"{status}: {response}");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Clients_SendAndReadAnIntegerEnumAsItsNumber_AndEachStringAsItIs(
        bool typedClient)
    {
        var marker = typedClient
            ? """{ "generationMode": "TypedClient" }"""
            : ErrorResponseBodyRuntimeTests.ClientOperationMarker("ProblemDetails");
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", marker, includeMinimalApi: false, Yaml);
        sources.Add(("EnumProbe.cs", ClientProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);

        var wire = await (Task<List<string>>)assembly.GetType("ErrorBodies.RuntimeTest.EnumProbe", throwOnError: true)!.GetMethod("RunAsync")!.Invoke(null, [typedClient])!;

        // Atc.Rest.Client writes indented JSON
        Assert.Equal("""{"level":2,"state":"Active"}""", JsonNode.Parse(wire[0])!.ToJsonString());
        Assert.Equal("/items?level=3", wire[1]);
        Assert.Equal("High", wire[2]);
        Assert.Equal("\"active\"", wire[3]);
    }

    private static Task<XmlTestHost> StartAsync(
        bool hostWideStringEnumConverter)
        => XmlTestHost.StartAsync(
            Yaml,
            HandlerSource,
            "ErrorBodies.RuntimeTest.EnumRegistration",
            configureServices: hostWideStringEnumConverter
                ? services => services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()))
                : null);
}