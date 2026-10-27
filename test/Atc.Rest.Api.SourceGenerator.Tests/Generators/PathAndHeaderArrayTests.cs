namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// An array in the path or a header, as OpenAPI serializes it there (<c>style: simple</c>, the default): the items
/// joined by commas, <c>/things/1,2,3</c> and <c>X-Tags: a,b</c>. The server binds the list (through RDG's request
/// delegates as through <c>RequestDelegateFactory</c>), answers an item that does not parse with a 400, and both C#
/// clients send the joined items - an enum item by its wire value.
/// </summary>
[Collection(nameof(ServerRequestDelegateGeneratorTests))]
public sealed class PathAndHeaderArrayTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /things/{ids}:
            get:
              operationId: getThings
              parameters:
                - name: ids
                  in: path
                  required: true
                  schema: { type: array, items: { type: integer } }
                - name: X-Tags
                  in: header
                  schema: { type: array, items: { type: string } }
                - name: X-Colors
                  in: header
                  schema: { type: array, items: { $ref: '#/components/schemas/Color' } }
              responses:
                '200':
                  description: The bound values
                  content:
                    text/plain:
                      schema: { type: string }
                '404':
                  description: Not found
        components:
          schemas:
            Color:
              type: string
              enum: [available, busy-blocked]
        """;

    private const string HandlerSource = """"
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public sealed class GetThingsHandlerImpl : IGetThingsHandler
        {
            public System.Threading.Tasks.Task<GetThingsResult> ExecuteAsync(GetThingsParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(GetThingsResult.Ok(
                    $"ids={string.Join("|", parameters.Ids)} tags={(parameters.Tags is null ? "<null>" : string.Join("|", parameters.Tags))} colors={(parameters.Colors is null ? "<null>" : string.Join("|", parameters.Colors))}"));
        }

        public static class ThingsRegistration
        {
            public static void Register(IServiceCollection services)
                => services.AddScoped<IGetThingsHandler, GetThingsHandlerImpl>();
        }
        """";

    private const string ProbeSource = """"
        using System.Net;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public static class ArrayProbe
        {
            private static readonly List<string> Wire = [];

            public static async Task<List<string>> RunAsync(bool typedClient)
            {
                var assembly = typeof(ArrayProbe).Assembly;
                var colorType = assembly.GetTypes().Single(t => t.Name == "Color");
                var colors = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(colorType))!;
                foreach (var name in new[] { "Available", "BusyBlocked" })
                {
                    colors.Add(colorType.IsEnum ? Enum.Parse(colorType, name) : (colorType.GetProperty(name)?.GetValue(null) ?? colorType.GetField(name)!.GetValue(null)));
                }

                var parametersType = assembly.GetTypes().Single(t => t.Name == "GetThingsParameters");
                var constructor = parametersType.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
                var parameters = constructor.Invoke([.. constructor.GetParameters().Select(p => p.Name!.ToLowerInvariant() switch
                {
                    "ids" => (object?)new List<int> { 1, 2, 3 },
                    "tags" => new List<string> { "a b", "c" },
                    _ => colors,
                })]);
                if (typedClient)
                {
                    var clientType = assembly.GetTypes().Single(t => t.IsClass && !t.IsAbstract && t.GetMethod("GetThingsAsync") is not null);
                    var client = Activator.CreateInstance(clientType, new HttpClient(new StubHandler()) { BaseAddress = new Uri("http://stub/") })!;
                    await (Task)clientType.GetMethod("GetThingsAsync")!.Invoke(client, [parameters, CancellationToken.None])!;
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
                    var endpointType = assembly.GetTypes().Single(t => t.Name == "IGetThingsEndpoint");
                    var endpoint = provider.GetRequiredService(endpointType);
                    await (Task)endpointType.GetMethod("ExecuteAsync")!.Invoke(endpoint, [parameters, httpClientName, CancellationToken.None])!;
                }

                return Wire;
            }

            private sealed class StubHandler : HttpMessageHandler
            {
                protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    Wire.Add(request.RequestUri!.AbsolutePath);
                    Wire.Add("X-Tags: " + string.Join(" / ", request.Headers.TryGetValues("X-Tags", out var tags) ? tags : []));
                    Wire.Add("X-Colors: " + string.Join(" / ", request.Headers.TryGetValues("X-Colors", out var colors) ? colors : []));
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok", System.Text.Encoding.UTF8, "text/plain") });
                }
            }
        }
        """";

    public static TheoryData<bool, string, string?, string?, int, string> ServerCases
    {
        get
        {
            var data = new TheoryData<bool, string, string?, string?, int, string>();
            foreach (var useRequestDelegateGenerator in new[] { false, true })
            {
                data.Add(useRequestDelegateGenerator, "/things/1,2,3", "a,b", "available,busy-blocked", 200, "ids=1|2|3 tags=a|b colors=Available|BusyBlocked");
                data.Add(useRequestDelegateGenerator, "/things/1%2C2", null, null, 200, "ids=1|2 tags=<null> colors=<null>");
                data.Add(useRequestDelegateGenerator, "/things/7", "single", null, 200, "ids=7 tags=single colors=<null>");
                data.Add(useRequestDelegateGenerator, "/things/1,x", null, null, 400, "ids");
                data.Add(useRequestDelegateGenerator, "/things/1", null, "nope", 400, "X-Colors");
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(ServerCases))]
    public async Task Server_BindsTheArray_FromItsCommaSeparatedItems(
        bool useRequestDelegateGenerator,
        string path,
        string? tags,
        string? colors,
        int expectedStatus,
        string expected)
    {
        await using var host = await XmlTestHost.StartAsync(
            Yaml,
            HandlerSource,
            "ErrorBodies.RuntimeTest.ThingsRegistration",
            useRequestDelegateGenerator: useRequestDelegateGenerator);
        using var client = new HttpClient { BaseAddress = host.BaseAddress };
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (tags is not null)
        {
            request.Headers.TryAddWithoutValidation("X-Tags", tags);
        }

        if (colors is not null)
        {
            request.Headers.TryAddWithoutValidation("X-Colors", colors);
        }

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.True((int)response.StatusCode == expectedStatus, $"{(int)response.StatusCode}: {body}");
        if (expectedStatus == 200)
        {
            Assert.Equal(expected, body);
        }
        else
        {
            // The problem names the parameter the client sent
            Assert.Contains(expected, body, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Clients_SendTheArray_AsCommaSeparatedItems(
        bool typedClient)
    {
        var marker = typedClient
            ? """{ "generationMode": "TypedClient" }"""
            : ErrorResponseBodyRuntimeTests.ClientOperationMarker("ProblemDetails");
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", marker, includeMinimalApi: false, Yaml);
        sources.Add(("ArrayProbe.cs", ProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);

        var wire = await (Task<List<string>>)assembly.GetType("ErrorBodies.RuntimeTest.ArrayProbe", throwOnError: true)!.GetMethod("RunAsync")!.Invoke(null, [typedClient])!;

        // A comma is a delimiter a client may send as it is or escaped (%2C): the same value to a server
        Assert.Equal(
            ["/things/1,2,3", "X-Tags: a b,c", "X-Colors: available,busy-blocked"],
            wire.Select(w => w.Replace("%2C", ",", StringComparison.Ordinal)),
            StringComparer.Ordinal);
    }
}