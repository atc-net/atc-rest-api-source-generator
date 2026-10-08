namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A parameter whose schema wraps one reference in an <c>allOf</c> - the OpenAPI 3.0 way to give a referenced enum a
/// description or <c>nullable</c> (<c>allOf: [$ref: State, {}]</c>, what springdoc writes) - is the referenced type:
/// the server binds the enum by its wire value, and both C# clients send that wire value. It was an <c>object</c>, which
/// the server could not bind (no <c>TryParse</c>) and the clients sent as the boxed value's <c>ToString()</c>.
/// </summary>
[Collection(nameof(ServerRequestDelegateGeneratorTests))]
public sealed class WrappedReferenceParameterTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /points:
            get:
              operationId: listPoints
              parameters:
                - name: state
                  in: query
                  schema:
                    description: Filters by state
                    nullable: true
                    allOf:
                      - $ref: '#/components/schemas/State'
                      - {}
              responses:
                '200':
                  description: The bound value
                  content:
                    text/plain:
                      schema: { type: string }
        components:
          schemas:
            State:
              type: string
              enum: [available, busy-blocked]
        """;

    private const string HandlerSource = """"
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public sealed class ListPointsHandlerImpl : IListPointsHandler
        {
            public System.Threading.Tasks.Task<ListPointsResult> ExecuteAsync(ListPointsParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(ListPointsResult.Ok($"state={parameters.State?.ToString() ?? "<null>"}"));
        }

        public static class PointsRegistration
        {
            public static void Register(IServiceCollection services)
                => services.AddScoped<IListPointsHandler, ListPointsHandlerImpl>();
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
                // An enum, or a client's extensible enum (a struct with a static member per value)
                var stateType = assembly.GetTypes().Single(t => t.Name == "State");
                var busyBlocked = stateType.IsEnum
                    ? Enum.Parse(stateType, "BusyBlocked")
                    : (stateType.GetProperty("BusyBlocked")?.GetValue(null) ?? stateType.GetField("BusyBlocked")!.GetValue(null));
                foreach (var state in new[] { busyBlocked, null })
                {
                    var parameters = Activator.CreateInstance(assembly.GetTypes().Single(t => t.Name == "ListPointsParameters"), [state])!;
                    if (typedClient)
                    {
                        var clientType = assembly.GetTypes().Single(t => t.IsClass && !t.IsAbstract && t.GetMethod("ListPointsAsync") is not null);
                        var client = Activator.CreateInstance(clientType, new HttpClient(new StubHandler()) { BaseAddress = new Uri("http://stub/") })!;
                        await (Task)clientType.GetMethod("ListPointsAsync")!.Invoke(client, [parameters, CancellationToken.None])!;
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
                        var endpointType = assembly.GetTypes().Single(t => t.Name == "IListPointsEndpoint");
                        var endpoint = provider.GetRequiredService(endpointType);
                        await (Task)endpointType.GetMethod("ExecuteAsync")!.Invoke(endpoint, [parameters, httpClientName, CancellationToken.None])!;
                    }
                }

                return Wire;
            }

            private sealed class StubHandler : HttpMessageHandler
            {
                protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    Wire.Add(request.RequestUri!.PathAndQuery);
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("state", System.Text.Encoding.UTF8, "text/plain") });
                }
            }
        }
        """";

    private static readonly string[] ExpectedWire = ["/points?state=busy-blocked", "/points"];

    public static TheoryData<bool, string, int, string> ServerCases
    {
        get
        {
            var data = new TheoryData<bool, string, int, string>();
            foreach (var useRequestDelegateGenerator in new[] { false, true })
            {
                data.Add(useRequestDelegateGenerator, "/points?state=busy-blocked", 200, "state=BusyBlocked");
                data.Add(useRequestDelegateGenerator, "/points", 200, "state=<null>");
                data.Add(useRequestDelegateGenerator, "/points?state=nope", 400, string.Empty);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(ServerCases))]
    public async Task Server_BindsTheWrappedEnum_ByItsWireValue(
        bool useRequestDelegateGenerator,
        string path,
        int expectedStatus,
        string expectedBody)
    {
        await using var host = await XmlTestHost.StartAsync(
            Yaml,
            HandlerSource,
            "ErrorBodies.RuntimeTest.PointsRegistration",
            useRequestDelegateGenerator: useRequestDelegateGenerator);

        var (status, _, body) = await host.SendRawAsync(HttpMethod.Get, path, accept: null);

        Assert.True(status == expectedStatus, $"{status}: {body}");
        if (expectedStatus == 200)
        {
            Assert.Equal(expectedBody, body);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Clients_SendTheWrappedEnum_AsItsWireValue(
        bool typedClient)
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