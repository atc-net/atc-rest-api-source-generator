namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// The C# clients' credentials per operation, from the specification: each generated request names its operation and the
/// credentials it accepts in <c>HttpRequestMessage.Options</c> (<c>ApiRequest</c>), and the generated
/// <c>ApiCredentialsHandler</c> sends an access token or an API key only where the operation asks for that scheme - none
/// on an anonymous one (<c>security: []</c>). An opt-in renewal sends a read once more after a 401.
/// </summary>
public sealed class ApiCredentialsTests
{
    private const string Yaml = """
        openapi: 3.1.0
        info:
          title: ErrorBodies
          version: 1.0.0
        security:
          - bearerAuth: []
        paths:
          /orders:
            get:
              operationId: listOrders
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        type: array
                        items:
                          type: string
                '401':
                  description: Unauthorized
            post:
              operationId: createOrder
              responses:
                '204':
                  description: No Content
                '401':
                  description: Unauthorized
          /sessions:
            post:
              operationId: createSession
              security: []
              responses:
                '204':
                  description: No Content
          /devices:
            get:
              operationId: listDevices
              security:
                - deviceKey: []
                - deviceQueryKey: []
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        type: array
                        items:
                          type: string
        components:
          securitySchemes:
            bearerAuth:
              type: http
              scheme: bearer
            deviceKey:
              type: apiKey
              in: header
              name: X-API-Key
            deviceQueryKey:
              type: apiKey
              in: query
              name: api_key
        """;

    private const string NoSecurityYaml = """
        openapi: 3.1.0
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /orders:
            get:
              operationId: listOrders
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        type: array
                        items:
                          type: string
        """;

    /// <summary>
    /// Calls operations through a generated client with an access token ("token-1", renewed to "token-2" after a 401
    /// when <c>renew</c>) and the two device keys, against a stub that records each request it gets - and answers the
    /// first request with token-1 with a 401 when <c>renew</c>.
    /// </summary>
    private const string ProbeSource = """"
        using System.Net;
        using Microsoft.Extensions.DependencyInjection;
        using ErrorBodies.Generated;

        namespace ErrorBodies.RuntimeTest;

        public static class CredentialsProbe
        {
            private static readonly List<string> Requests = [];
            private static bool renew;

            public static async Task<List<string>> RunAsync(bool typedClient, bool withRenewal, string[] operations)
            {
                Requests.Clear();
                renew = withRenewal;
                var token = "token-1";
                void Configure(ApiCredentials credentials)
                {
                    credentials.AccessToken = _ => new ValueTask<string?>(token);
                    credentials.ApiKeys["deviceKey"] = _ => new ValueTask<string?>("header-key");
                    credentials.ApiKeys["deviceQueryKey"] = _ => new ValueTask<string?>("query key");
                    if (withRenewal)
                    {
                        credentials.RenewAccessToken = _ =>
                        {
                            token = "token-2";
                            return new ValueTask<bool>(true);
                        };
                    }
                }

                var assembly = typeof(CredentialsProbe).Assembly;
                if (typedClient)
                {
                    var credentials = new ApiCredentials();
                    Configure(credentials);
                    var httpClient = new HttpClient(new ApiCredentialsHandler(credentials) { InnerHandler = new StubHandler() }) { BaseAddress = new Uri("http://stub/") };
                    foreach (var operation in operations)
                    {
                        // A client per path segment
                        var clientType = assembly.GetTypes().Single(t => t.IsClass && !t.IsAbstract && t.GetMethod(operation + "Async") is not null);
                        var client = Activator.CreateInstance(clientType, httpClient)!;
                        var method = clientType.GetMethod(operation + "Async")!;
                        try
                        {
                            await (Task)method.Invoke(client, method.GetParameters().Select(p => p.ParameterType == typeof(CancellationToken) ? (object)CancellationToken.None : null).ToArray())!;
                        }
                        catch (HttpRequestException)
                        {
                            // A 401 the typed client throws for
                        }
                    }
                }
                else
                {
                    var httpClientName = (string)assembly.GetTypes().Single(t => t.Name == "Constants").GetField("HttpClientName")!.GetValue(null)!;
                    var services = new ServiceCollection();
                    services
                        .AddHttpClient(httpClientName, c => c.BaseAddress = new Uri("http://stub/"))
                        .AddApiCredentials((_, credentials) => Configure(credentials))
                        .ConfigurePrimaryHttpMessageHandler(() => new StubHandler());
                    assembly.GetTypes().SelectMany(t => t.GetMethods()).Single(m => m.Name == "AddErrorBodiesEndpoints").Invoke(null, [services, null]);
                    using var provider = services.BuildServiceProvider();
                    foreach (var operation in operations)
                    {
                        var endpointType = assembly.GetTypes().Single(t => t.Name == $"I{operation}Endpoint");
                        var execute = endpointType.GetMethod("ExecuteAsync")!;
                        await (Task)execute.Invoke(provider.GetRequiredService(endpointType), execute.GetParameters().Select(p => p.ParameterType == typeof(CancellationToken) ? (object)CancellationToken.None : httpClientName).ToArray())!;
                    }
                }

                return Requests;
            }

            private sealed class StubHandler : HttpMessageHandler
            {
                protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    var security = ApiRequest.SecurityOf(request);
                    var schemes = security is null ? "unspecified" : security.IsAnonymous ? "anonymous" : string.Join("+", security.Schemes.Select(s => s.Name));
                    Requests.Add(
                        $"{request.Method} {request.RequestUri!.PathAndQuery} op={ApiRequest.OperationOf(request)} security={schemes}" +
                        $" auth={request.Headers.Authorization?.ToString() ?? "-"}" +
                        $" key={(request.Headers.TryGetValues("X-API-Key", out var keys) ? string.Join(",", keys) : "-")}");

                    var status = renew && request.Headers.Authorization?.Parameter == "token-1"
                        ? HttpStatusCode.Unauthorized
                        : request.Method == HttpMethod.Get ? HttpStatusCode.OK : HttpStatusCode.NoContent;
                    return Task.FromResult(new HttpResponseMessage(status)
                    {
                        Content = status == HttpStatusCode.OK ? new StringContent("[]", System.Text.Encoding.UTF8, "application/json") : null,
                    });
                }
            }
        }
        """";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EachOperation_GetsTheCredentialsItsSecurityAsksFor(
        bool typedClient)
    {
        // Act
        var requests = await RunAsync(Yaml, typedClient, withRenewal: false, "ListOrders", "CreateSession", "ListDevices");

        // Assert
        Assert.Equal(
            [
                "GET /orders op=ListOrders security=bearerAuth auth=Bearer token-1 key=-",
                "POST /sessions op=CreateSession security=anonymous auth=- key=-",
                "GET /devices?api_key=query%20key op=ListDevices security=deviceKey+deviceQueryKey auth=- key=header-key",
            ],
            requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AReadAnswered401_IsRenewedAndSentOnceMore_AWriteIsNot(
        bool typedClient)
    {
        // Act
        var requests = await RunAsync(Yaml, typedClient, withRenewal: true, "CreateOrder", "ListOrders");

        // Assert - the write is answered with its 401 (a body may not be sent twice); the read is sent again with the
        // renewed token
        Assert.Equal(
            [
                "POST /orders op=CreateOrder security=bearerAuth auth=Bearer token-1 key=-",
                "GET /orders op=ListOrders security=bearerAuth auth=Bearer token-1 key=-",
                "GET /orders op=ListOrders security=bearerAuth auth=Bearer token-2 key=-",
            ],
            requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ASpecificationWithoutSecurity_SendsTheAccessToken(
        bool typedClient)
    {
        // Act
        var requests = await RunAsync(NoSecurityYaml, typedClient, withRenewal: false, "ListOrders");

        // Assert
        Assert.Equal(["GET /orders op=ListOrders security=unspecified auth=Bearer token-1 key=-"], requests);
    }

    [Fact]
    public async Task TheCredentials_KeepTheGeneratedCodeTrimAndAotSafe()
    {
        // Arrange
        var client = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", ErrorResponseBodyRuntimeTests.ClientOperationMarker("ProblemDetails"), includeMinimalApi: false, Yaml);

        // Act
        var problems = await AotAnalyzerHarness.AnalyzeAsync(client, includeMinimalApi: false);

        // Assert
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    private static Task<List<string>> RunAsync(
        string yaml,
        bool typedClient,
        bool withRenewal,
        params string[] operations)
    {
        var marker = typedClient
            ? """{ "generationMode": "TypedClient" }"""
            : ErrorResponseBodyRuntimeTests.ClientOperationMarker("ProblemDetails");
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", marker, includeMinimalApi: false, yaml);
        sources.Add(("CredentialsProbe.cs", ProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);

        return (Task<List<string>>)assembly.GetType("ErrorBodies.RuntimeTest.CredentialsProbe", throwOnError: true)!.GetMethod("RunAsync")!.Invoke(null, [typedClient, withRenewal, operations])!;
    }
}