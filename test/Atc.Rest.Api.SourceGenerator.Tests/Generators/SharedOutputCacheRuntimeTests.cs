namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// Output caching on operations that require a signed-in caller, hosted on a real Kestrel server: ASP.NET Core's default
/// policy never caches an authenticated request, and <c>x-cache-shared: true</c> caches an answer that is the same for
/// every caller.
/// </summary>
public class SharedOutputCacheRuntimeTests
{
    // Only the caching matters here: no 401 responses declared, so the specification is not validated
    private const string Marker = """
        {
          "validateSpecificationStrategy": "None",
          "useMinimalApiPackage": "Enabled",
          "useGlobalErrorHandler": "Enabled"
        }
        """;

    private const string Yaml = """
        openapi: 3.0.0
        info:
          title: Fleet
          version: 1.0.0
        security:
          - bearer: []
        paths:
          /devices/shared:
            get:
              operationId: listSharedDevices
              x-cache-policy: shared-devices
              x-cache-expiration-seconds: 60
              x-cache-shared: true
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        type: string
          /devices/own:
            get:
              operationId: listOwnDevices
              x-cache-policy: own-devices
              x-cache-expiration-seconds: 60
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        type: string
          /devices/missing:
            get:
              operationId: getMissingDevice
              x-cache-policy: missing-device
              x-cache-expiration-seconds: 60
              x-cache-shared: true
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        type: string
                '404':
                  description: Not found
        components:
          securitySchemes:
            bearer:
              type: http
              scheme: bearer
        """;

    /// <summary>
    /// Stand-ins for the domain project's handlers: each answers how many times it has run.
    /// </summary>
    private const string HandlerSource = """
        using Fleet.Generated.Handlers;
        using Fleet.Generated.Results;

        namespace Fleet.RuntimeTest;

        public sealed class ListSharedDevicesHandler : IListSharedDevicesHandler
        {
            private static int calls;

            public Task<ListSharedDevicesResult> ExecuteAsync(CancellationToken cancellationToken = default)
                => Task.FromResult(ListSharedDevicesResult.Ok($"run {Interlocked.Increment(ref calls)}"));
        }

        public sealed class ListOwnDevicesHandler : IListOwnDevicesHandler
        {
            private static int calls;

            public Task<ListOwnDevicesResult> ExecuteAsync(CancellationToken cancellationToken = default)
                => Task.FromResult(ListOwnDevicesResult.Ok($"run {Interlocked.Increment(ref calls)}"));
        }

        public sealed class GetMissingDeviceHandler : IGetMissingDeviceHandler
        {
            private static int calls;

            public Task<GetMissingDeviceResult> ExecuteAsync(CancellationToken cancellationToken = default)
                => Task.FromResult(GetMissingDeviceResult.NotFound($"run {Interlocked.Increment(ref calls)}"));
        }
        """;

    [Fact]
    public async Task SignedInCallers_ASharedAnswerIsCached_TheDefaultPolicyCachesNothing_AndAnErrorIsNotStored()
    {
        await using var host = await StartHostAsync();

        // Two different signed-in callers: a shared answer is the same for both, from one run of the handler
        Assert.Equal("\"run 1\"", await GetAsync(host, "/devices/shared", "alice"));
        Assert.Equal("\"run 1\"", await GetAsync(host, "/devices/shared", "bob"));

        // Without x-cache-shared the default policy skips every authenticated request: the handler runs every time
        Assert.Equal("\"run 1\"", await GetAsync(host, "/devices/own", "alice"));
        Assert.Equal("\"run 2\"", await GetAsync(host, "/devices/own", "alice"));

        // Shared, but a 404 is not stored: the handler runs again
        Assert.Contains("run 1", await GetAsync(host, "/devices/missing", "alice"), StringComparison.Ordinal);
        Assert.Contains("run 2", await GetAsync(host, "/devices/missing", "alice"), StringComparison.Ordinal);
    }

    private static async Task<string> GetAsync(
        RunningHost host,
        string path,
        string user)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", user);

        using var response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<RunningHost> StartHostAsync()
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", Marker, includeMinimalApi: true, Yaml);
        sources.Add(("RuntimeTestHandlers.cs", HandlerSource));

        var assembly = CompilationVerificationHarness.EmitAndLoad(sources, includeMinimalApi: true);
        var serviceExtensions = assembly.GetType("Fleet.Generated.Extensions.UnifiedServiceCollectionExtensions", throwOnError: true)!;
        var appExtensions = assembly.GetType("Fleet.Generated.Extensions.UnifiedWebApplicationExtensions", throwOnError: true)!;

        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();

        builder.Services
            .AddAuthentication("Bearer")
            .AddScheme<AuthenticationSchemeOptions, DemoBearerHandler>("Bearer", _ => { });

        foreach (var name in new[] { "ListSharedDevices", "ListOwnDevices", "GetMissingDevice" })
        {
            builder.Services.AddScoped(
                assembly.GetType($"Fleet.Generated.Handlers.I{name}Handler", throwOnError: true)!,
                assembly.GetType($"Fleet.RuntimeTest.{name}Handler", throwOnError: true)!);
        }

        serviceExtensions
            .GetMethod("AddFleetApi")!
            .Invoke(null, [builder.Services, null]);

        var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");

        appExtensions
            .GetMethod("MapFleetApi")!
            .Invoke(null, [app, null]);

        await app.StartAsync(TestContext.Current.CancellationToken);

        return new RunningHost(app);
    }

    private sealed class RunningHost(WebApplication app) : IAsyncDisposable
    {
        public HttpClient Client { get; } = new() { BaseAddress = new Uri(app.Urls.First()) };

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.StopAsync(TestContext.Current.CancellationToken);
            await app.DisposeAsync();
        }
    }

    /// <summary>
    /// Signs in whoever the bearer token names.
    /// </summary>
    private sealed class DemoBearerHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var header = Request.Headers.Authorization.ToString();
            if (!header.StartsWith("Bearer ", StringComparison.Ordinal))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, header["Bearer ".Length..])], Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}