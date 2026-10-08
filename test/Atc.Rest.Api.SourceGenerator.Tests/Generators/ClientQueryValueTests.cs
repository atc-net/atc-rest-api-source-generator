namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// The C# clients write a parameter value the way the server reads it: an integer culture-independently (a negative
/// number under a culture whose minus sign is U+2212, such as sv-SE, is still <c>-5</c>), and an <c>allowReserved</c>
/// value with the reserved characters left as they are but every other character encoded - and never a raw <c>#</c>,
/// which would cut the URL at the fragment.
/// </summary>
public sealed class ClientQueryValueTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /items/{n}:
            get:
              operationId: getItem
              parameters:
                - name: n
                  in: path
                  required: true
                  schema: { type: integer, format: int32 }
                - name: m
                  in: query
                  schema: { type: integer, format: int64 }
                - name: q
                  in: query
                  allowReserved: true
                  schema: { type: string }
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema: { type: string }
        """;

    private const string ProbeSource = """"
        using System.Globalization;
        using System.Net;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public static class QueryValueProbe
        {
            private static string? requested;

            public static async Task<string> RunAsync(bool typedClient)
            {
                var previous = CultureInfo.CurrentCulture;
                CultureInfo.CurrentCulture = new CultureInfo("sv-SE");
                try
                {
                    var assembly = typeof(QueryValueProbe).Assembly;
                    var ctor = assembly.GetTypes().Single(t => t.Name == "GetItemParameters").GetConstructors().Single();
                    var parameters = ctor.Invoke(ctor.GetParameters().Select(p => p.Name switch
                    {
                        "N" => (object?)(-5),
                        "M" => -6L,
                        _ => "C#/a b?x=1",
                    }).ToArray());

                    if (typedClient)
                    {
                        var clientType = assembly.GetTypes().Single(t => t.IsClass && !t.IsAbstract && t.GetMethod("GetItemAsync") is not null);
                        var client = Activator.CreateInstance(clientType, new HttpClient(new StubHandler()) { BaseAddress = new Uri("http://stub/") })!;
                        await (Task)clientType.GetMethod("GetItemAsync")!.Invoke(client, [parameters, CancellationToken.None])!;
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
                        var endpointType = assembly.GetTypes().Single(t => t.Name == "IGetItemEndpoint");
                        await (Task)endpointType.GetMethod("ExecuteAsync")!.Invoke(provider.GetRequiredService(endpointType), [parameters, httpClientName, CancellationToken.None])!;
                    }

                    return requested!;
                }
                finally
                {
                    CultureInfo.CurrentCulture = previous;
                }
            }

            private sealed class StubHandler : HttpMessageHandler
            {
                protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    requested = request.RequestUri!.GetComponents(UriComponents.PathAndQuery | UriComponents.Fragment, UriFormat.UriEscaped);
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("\"ok\"", System.Text.Encoding.UTF8, "application/json"),
                    });
                }
            }
        }
        """";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Clients_WriteIntegersInvariant_AndAnAllowReservedValueWithoutARawFragment(
        bool typedClient)
    {
        var marker = typedClient
            ? """{ "generationMode": "TypedClient" }"""
            : ErrorResponseBodyRuntimeTests.ClientOperationMarker("ProblemDetails");
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", marker, includeMinimalApi: false, Yaml);
        sources.Add(("QueryValueProbe.cs", ProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);

        var requested = await (Task<string>)assembly.GetType("ErrorBodies.RuntimeTest.QueryValueProbe", throwOnError: true)!.GetMethod("RunAsync")!.Invoke(null, [typedClient])!;

        Assert.StartsWith("/items/-5?", requested, StringComparison.Ordinal);
        Assert.Contains("m=-6", requested, StringComparison.Ordinal);
        Assert.DoesNotContain("#", requested, StringComparison.Ordinal);
        var q = System.Web.HttpUtility.ParseQueryString(new Uri("http://stub" + requested).Query)["q"];
        Assert.Equal("C#/a b?x=1", q);
    }
}