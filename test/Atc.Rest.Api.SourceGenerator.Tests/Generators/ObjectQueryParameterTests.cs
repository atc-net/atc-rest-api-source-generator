namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// An object in the query, as OpenAPI serializes it: <c>style: deepObject</c> sends each property as
/// <c>filter[name]=value</c>, the default for an object (<c>style: form</c>, <c>explode: true</c>) each property as its
/// own key, <c>page=3</c>. The server binds the model from those keys (through RDG's request delegates as through
/// <c>RequestDelegateFactory</c>), answers a property value that does not parse with a 400 naming the key, describes
/// the parameter to ApiExplorer as the query parameter it is, and both C# clients send those keys.
/// </summary>
[Collection(nameof(ServerRequestDelegateGeneratorTests))]
public sealed class ObjectQueryParameterTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /items:
            get:
              operationId: listItems
              parameters:
                - name: filter
                  in: query
                  style: deepObject
                  explode: true
                  schema: { $ref: '#/components/schemas/Filter' }
                - name: paging
                  in: query
                  schema: { $ref: '#/components/schemas/Paging' }
                - name: limit
                  in: query
                  schema: { type: integer }
              responses:
                '200':
                  description: The bound values
                  content:
                    text/plain:
                      schema: { type: string }
        components:
          schemas:
            Filter:
              type: object
              properties:
                name: { type: string }
                size: { type: integer }
                color: { $ref: '#/components/schemas/Color' }
            Paging:
              type: object
              properties:
                page: { type: integer }
                perPage: { type: integer }
            Color:
              type: string
              enum: [available, busy-blocked]
        """;

    private const string HandlerSource = """"
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public sealed class ListItemsHandlerImpl : IListItemsHandler
        {
            public System.Threading.Tasks.Task<ListItemsResult> ExecuteAsync(ListItemsParameters parameters, System.Threading.CancellationToken cancellationToken = default)
            {
                var filter = parameters.Filter is { } f
                    ? $"name:{f.Name ?? "<null>"},size:{f.Size?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "<null>"},color:{f.Color?.ToString() ?? "<null>"}"
                    : "<null>";
                var paging = parameters.Paging is { } p
                    ? $"page:{p.Page?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "<null>"},perPage:{p.PerPage?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "<null>"}"
                    : "<null>";
                return System.Threading.Tasks.Task.FromResult(ListItemsResult.Ok(
                    $"filter={filter} paging={paging} limit={parameters.Limit?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "<null>"}"));
            }
        }

        public static class ItemsRegistration
        {
            public static void Register(IServiceCollection services)
                => services.AddScoped<IListItemsHandler, ListItemsHandlerImpl>();
        }
        """";

    private const string ProbeSource = """"
        using System.Net;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public static class QueryProbe
        {
            private static readonly List<string> Wire = [];

            public static async Task<List<string>> RunAsync(bool typedClient)
            {
                var assembly = typeof(QueryProbe).Assembly;
                var colorType = assembly.GetTypes().Single(t => t.Name == "Color");
                var busyBlocked = colorType.IsEnum
                    ? Enum.Parse(colorType, "BusyBlocked")
                    : (colorType.GetProperty("BusyBlocked")?.GetValue(null) ?? colorType.GetField("BusyBlocked")!.GetValue(null));
                var filter = Create(assembly, "Filter", new Dictionary<string, object?> { ["name"] = "a b", ["size"] = 2L, ["color"] = busyBlocked });
                var paging = Create(assembly, "Paging", new Dictionary<string, object?> { ["page"] = 3L });
                foreach (var (f, p) in new[] { (filter, paging), ((object?)null, (object?)null) })
                {
                    var parameters = Create(assembly, "ListItemsParameters", new Dictionary<string, object?> { ["filter"] = f, ["paging"] = p });
                    if (typedClient)
                    {
                        var clientType = assembly.GetTypes().Single(t => t.IsClass && !t.IsAbstract && t.GetMethod("ListItemsAsync") is not null);
                        var client = Activator.CreateInstance(clientType, new HttpClient(new StubHandler()) { BaseAddress = new Uri("http://stub/") })!;
                        await (Task)clientType.GetMethod("ListItemsAsync")!.Invoke(client, [parameters, CancellationToken.None])!;
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
                        var endpointType = assembly.GetTypes().Single(t => t.Name == "IListItemsEndpoint");
                        var endpoint = provider.GetRequiredService(endpointType);
                        await (Task)endpointType.GetMethod("ExecuteAsync")!.Invoke(endpoint, [parameters, httpClientName, CancellationToken.None])!;
                    }
                }

                return Wire;
            }

            // A record through its constructor, by parameter name (case-insensitive); the rest are their defaults
            private static object Create(System.Reflection.Assembly assembly, string typeName, Dictionary<string, object?> values)
            {
                var type = assembly.GetTypes().Single(t => t.Name == typeName);
                var constructor = type.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
                var arguments = constructor.GetParameters()
                    .Select(p => values.FirstOrDefault(v => string.Equals(v.Key, p.Name, StringComparison.OrdinalIgnoreCase)) is { Key: not null } pair
                        ? Convert(pair.Value, p.ParameterType)
                        : (p.HasDefaultValue ? p.DefaultValue : null))
                    .ToArray();
                return constructor.Invoke(arguments);
            }

            private static object? Convert(object? value, Type target)
            {
                var underlying = Nullable.GetUnderlyingType(target) ?? target;
                return value is long number && underlying != typeof(long)
                    ? System.Convert.ChangeType(number, underlying, System.Globalization.CultureInfo.InvariantCulture)
                    : value;
            }

            private sealed class StubHandler : HttpMessageHandler
            {
                protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    Wire.Add(request.RequestUri!.PathAndQuery);
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("items", System.Text.Encoding.UTF8, "text/plain") });
                }
            }
        }
        """";

    private static readonly string[] ExpectedWire =
    [
        "/items?filter%5Bname%5D=a%20b&filter%5Bsize%5D=2&filter%5Bcolor%5D=busy-blocked&page=3",
        "/items",
    ];

    public static TheoryData<bool, string, int, string> ServerCases
    {
        get
        {
            var data = new TheoryData<bool, string, int, string>();
            foreach (var useRequestDelegateGenerator in new[] { false, true })
            {
                data.Add(useRequestDelegateGenerator, "/items", 200, "filter=<null> paging=<null> limit=<null>");
                data.Add(
                    useRequestDelegateGenerator,
                    "/items?filter[name]=a%20b&filter%5Bsize%5D=2&filter[color]=busy-blocked&page=3&perPage=10&limit=5",
                    200,
                    "filter=name:a b,size:2,color:BusyBlocked paging=page:3,perPage:10 limit=5");
                data.Add(useRequestDelegateGenerator, "/items?filter[name]=a", 200, "filter=name:a,size:<null>,color:<null> paging=<null> limit=<null>");
                data.Add(useRequestDelegateGenerator, "/items?perPage=10", 200, "filter=<null> paging=page:<null>,perPage:10 limit=<null>");
                data.Add(useRequestDelegateGenerator, "/items?filter[size]=x", 400, "filter[size]");
                data.Add(useRequestDelegateGenerator, "/items?filter[color]=nope", 400, "filter[color]");
                data.Add(useRequestDelegateGenerator, "/items?page=x", 400, "page");
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(ServerCases))]
    public async Task Server_BindsTheObject_FromItsKeys(
        bool useRequestDelegateGenerator,
        string path,
        int expectedStatus,
        string expected)
    {
        await using var host = await XmlTestHost.StartAsync(
            Yaml,
            HandlerSource,
            "ErrorBodies.RuntimeTest.ItemsRegistration",
            useRequestDelegateGenerator: useRequestDelegateGenerator);

        var (status, _, body) = await host.SendRawAsync(HttpMethod.Get, path, accept: null);

        Assert.True(status == expectedStatus, $"{status}: {body}");
        if (expectedStatus == 200)
        {
            Assert.Equal(expected, body);
        }
        else
        {
            // The problem names the key the client sent
            Assert.Contains($"\"{expected}\"", body, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Server_DescribesTheObject_AsAQueryParameter()
    {
        await using var host = await XmlTestHost.StartAsync(Yaml, HandlerSource, "ErrorBodies.RuntimeTest.ItemsRegistration");

        Assert.Equal(
            [
                "GET items/: filter query Filter required=False default=<none>",
                "GET items/: limit query Nullable`1 required=False default=<none>",
                "GET items/: paging query Paging required=False default=<none>",
            ],
            host.DescribeParameters(),
            StringComparer.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Clients_SendTheObject_AsItsKeys(bool typedClient)
    {
        var marker = typedClient
            ? """{ "generationMode": "TypedClient" }"""
            : ErrorResponseBodyRuntimeTests.ClientOperationMarker("ProblemDetails");
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", marker, includeMinimalApi: false, Yaml);
        sources.Add(("QueryProbe.cs", ProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);

        var wire = await (Task<List<string>>)assembly.GetType("ErrorBodies.RuntimeTest.QueryProbe", throwOnError: true)!.GetMethod("RunAsync")!.Invoke(null, [typedClient])!;

        Assert.Equal(ExpectedWire, wire, StringComparer.Ordinal);
    }
}