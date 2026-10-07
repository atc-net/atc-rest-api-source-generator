namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A query array is on the wire in the style the specification declares, both ways: repeated keys for
/// <c>form</c> with <c>explode: true</c> (the default) - where <c>?tags=a,b</c> is ONE item, <c>a,b</c>; one value with
/// commas for <c>form</c> with <c>explode: false</c>; a pipe for <c>pipeDelimited</c>; a space for
/// <c>spaceDelimited</c> (<c>%20</c> or <c>+</c>). An item keeps its spaces. The server used to split a single value on
/// commas whatever the style and trim every item; the C# clients sent every style as repeated keys.
/// </summary>
[Collection(nameof(ArraySerializationWireTests))]
[CollectionDefinition(nameof(ArraySerializationWireTests), DisableParallelization = true)]
public sealed class ArraySerializationWireTests
{
    internal const string Yaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /lists:
            get:
              operationId: getLists
              parameters:
                - name: exploded
                  in: query
                  schema: { type: array, items: { type: string } }
                - name: csv
                  in: query
                  explode: false
                  schema: { type: array, items: { type: string } }
                - name: piped
                  in: query
                  style: pipeDelimited
                  schema: { type: array, items: { type: string } }
                - name: spaced
                  in: query
                  style: spaceDelimited
                  schema: { type: array, items: { type: string } }
                - name: numbers
                  in: query
                  explode: false
                  schema: { type: array, items: { type: integer } }
              responses:
                '200':
                  description: The lists as the server bound them
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Lists'
        components:
          schemas:
            Lists:
              type: object
              properties:
                exploded: { type: array, items: { type: string } }
                csv: { type: array, items: { type: string } }
                piped: { type: array, items: { type: string } }
                spaced: { type: array, items: { type: string } }
                numbers: { type: array, items: { type: integer } }
        """;

    private const string HandlerSource = """"
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public sealed class GetListsHandlerImpl : IGetListsHandler
        {
            public System.Threading.Tasks.Task<GetListsResult> ExecuteAsync(GetListsParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(GetListsResult.Ok(new Lists(
                    Exploded: parameters.Exploded?.ToList(),
                    Csv: parameters.Csv?.ToList(),
                    Piped: parameters.Piped?.ToList(),
                    Spaced: parameters.Spaced?.ToList(),
                    Numbers: parameters.Numbers?.ToList())));
        }

        public static class ListsRegistration
        {
            public static void Register(IServiceCollection services)
                => services.AddScoped<IGetListsHandler, GetListsHandlerImpl>();
        }
        """";

    /// <summary>
    /// Calls <c>getLists</c> through a generated client with exploded ["a,b", " c "], csv ["a", "b"], piped ["a", "b"],
    /// spaced ["a", "b"] and numbers [1, 2], against a stub that records the request line.
    /// </summary>
    private const string ClientProbeSource = """"
        using System.Net;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public static class ListsProbe
        {
            private static string? requestLine;

            public static async Task<string> RunAsync(bool typedClient)
            {
                var assembly = typeof(ListsProbe).Assembly;
                var parametersType = assembly.GetTypes().Single(t => t.Name == "GetListsParameters");
                var ctor = parametersType.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
                var parameters = ctor.Invoke(ctor.GetParameters().Select(p => Value(p.Name!, p.ParameterType)).ToArray());

                if (typedClient)
                {
                    var clientType = assembly.GetTypes().Single(t => t.IsClass && !t.IsAbstract && t.GetMethod("GetListsAsync") is not null);
                    var client = Activator.CreateInstance(clientType, new HttpClient(new StubHandler()) { BaseAddress = new Uri("http://stub/") })!;
                    await (Task)clientType.GetMethod("GetListsAsync")!.Invoke(client, [parameters, CancellationToken.None])!;
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
                    var endpointType = assembly.GetTypes().Single(t => t.Name == "IGetListsEndpoint");
                    await (Task)endpointType.GetMethod("ExecuteAsync")!.Invoke(provider.GetRequiredService(endpointType), [parameters, httpClientName, CancellationToken.None])!;
                }

                return requestLine!;
            }

            private static object? Value(string name, Type type)
            {
                object items = name switch
                {
                    "Exploded" => new List<string> { "a,b", " c " },
                    "Numbers" => new List<int> { 1, 2 },
                    _ => new List<string> { "a", "b" },
                };

                // The parameter's list type (a List<T> or a generated parsable list built from one)
                return type.IsInstanceOfType(items) ? items : Activator.CreateInstance(type, items);
            }

            private sealed class StubHandler : HttpMessageHandler
            {
                protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    requestLine = request.RequestUri!.Query;
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
                    });
                }
            }
        }
        """";

    [Theory]
    [InlineData("?exploded=a,b", "exploded", """["a,b"]""")]
    [InlineData("?exploded=a&exploded=b", "exploded", """["a","b"]""")]
    [InlineData("?exploded=%20a%20", "exploded", """[" a "]""")]
    [InlineData("?csv=a,b", "csv", """["a","b"]""")]
    [InlineData("?csv=%20a%20,b", "csv", """[" a ","b"]""")]
    [InlineData("?piped=a%7Cb", "piped", """["a","b"]""")]
    [InlineData("?piped=a,b", "piped", """["a,b"]""")]
    [InlineData("?spaced=a%20b", "spaced", """["a","b"]""")]
    [InlineData("?spaced=a+b", "spaced", """["a","b"]""")]
    [InlineData("?numbers=1,2", "numbers", """[1,2]""")]
    public async Task Server_BindsEachStyleAsDeclared(
        string query,
        string property,
        string expected)
    {
        await using var host = await XmlTestHost.StartAsync(Yaml, HandlerSource, "ErrorBodies.RuntimeTest.ListsRegistration");

        var (status, _, body) = await host.SendRawAsync(HttpMethod.Get, "/lists" + query, accept: "application/json");

        Assert.True(status == 200, $"{status}: {body}");
        Assert.Equal(expected, JsonNode.Parse(body)![property]?.ToJsonString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Clients_SendEachStyleAsDeclared(
        bool typedClient)
    {
        var marker = typedClient
            ? """{ "generationMode": "TypedClient" }"""
            : ErrorResponseBodyRuntimeTests.ClientOperationMarker("ProblemDetails");
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", marker, includeMinimalApi: false, Yaml);
        sources.Add(("ListsProbe.cs", ClientProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);

        var query = await (Task<string>)assembly.GetType("ErrorBodies.RuntimeTest.ListsProbe", throwOnError: true)!.GetMethod("RunAsync")!.Invoke(null, [typedClient])!;

        var values = System.Web.HttpUtility.ParseQueryString(query);
        Assert.Equal(["a,b", " c "], values.GetValues("exploded"));
        Assert.Equal(["a,b"], values.GetValues("csv"));
        Assert.Equal(["a|b"], values.GetValues("piped"));
        Assert.Equal(["a b"], values.GetValues("spaced"));
        Assert.Equal(["1,2"], values.GetValues("numbers"));
    }
}