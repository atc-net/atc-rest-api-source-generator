namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A URL-encoded form body reaches the wire as one from both C# clients: its set fields, each as its wire text, and a
/// form whose optional fields are all unset still as a form - an empty body with the form media type, not no body.
/// </summary>
public sealed class ClientUrlEncodedFormWireTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /search:
            post:
              operationId: search
              requestBody:
                required: true
                content:
                  application/x-www-form-urlencoded:
                    schema:
                      $ref: '#/components/schemas/SearchForm'
              responses:
                '204':
                  description: Searched
        components:
          schemas:
            SearchForm:
              type: object
              properties:
                query: { type: string }
                exact: { type: boolean }
        """;

    private const string ProbeSource = """"
        using System.Net;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public static class FormProbe
        {
            private static readonly List<string> Wire = [];

            public static async Task<List<string>> RunAsync(bool typedClient)
            {
                var assembly = typeof(FormProbe).Assembly;
                var formType = assembly.GetTypes().Single(t => t.Name == "SearchForm");
                var empty = Activator.CreateInstance(formType, [null, null])!;
                var filled = Activator.CreateInstance(formType, ["a b", true])!;

                foreach (var form in new[] { empty, filled })
                {
                    if (typedClient)
                    {
                        var clientType = assembly.GetTypes().Single(t => t.IsClass && !t.IsAbstract && t.GetMethod("SearchAsync") is not null);
                        var client = Activator.CreateInstance(clientType, new HttpClient(new StubHandler()) { BaseAddress = new Uri("http://stub/") })!;
                        var method = clientType.GetMethod("SearchAsync")!;
                        var parameters = Activator.CreateInstance(method.GetParameters()[0].ParameterType, form);
                        await (Task)method.Invoke(client, [parameters, CancellationToken.None])!;
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
                        var parameters = Activator.CreateInstance(assembly.GetTypes().Single(t => t.Name == "SearchParameters"), form);
                        var endpoint = provider.GetRequiredService(endpointType);
                        await (Task)endpointType.GetMethod("ExecuteAsync")!.Invoke(endpoint, [parameters, httpClientName, CancellationToken.None])!;
                    }
                }

                return Wire;
            }

            private sealed class StubHandler : HttpMessageHandler
            {
                protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    var body = request.Content is null ? "(no body)" : await request.Content.ReadAsStringAsync(cancellationToken);
                    Wire.Add($"{request.Content?.Headers.ContentType?.MediaType} [{body}]");
                    return new HttpResponseMessage(HttpStatusCode.NoContent);
                }
            }
        }
        """";

    private static readonly string[] ExpectedWire =
    [
        "application/x-www-form-urlencoded []",
        "application/x-www-form-urlencoded [query=a+b&exact=true]",
    ];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Clients_UrlEncodedForm_IsSentAsAForm_WithOrWithoutAField(
        bool typedClient)
    {
        var marker = typedClient
            ? """{ "generationMode": "TypedClient" }"""
            : ErrorResponseBodyRuntimeTests.ClientOperationMarker("ProblemDetails");
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", marker, includeMinimalApi: false, Yaml);
        sources.Add(("FormProbe.cs", ProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);

        var wire = await (Task<List<string>>)assembly.GetType("ErrorBodies.RuntimeTest.FormProbe", throwOnError: true)!.GetMethod("RunAsync")!.Invoke(null, [typedClient])!;

        Assert.Equal(ExpectedWire, wire, StringComparer.Ordinal);
    }
}