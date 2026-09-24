namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// Runtime tests for the <c>SecurityBearerOnly</c> scenario: a bearer scheme applied to an
/// operation with no role, policy or scope, hosted on a real Kestrel server.
/// </summary>
/// <remarks>
/// The compilation gate proves the generated code builds. These tests prove the two things only a
/// running host can show: the app starts even though the host never calls
/// <c>AddAuthorization()</c> itself (the generated <c>UseAuthorization()</c> needs it), and the
/// scheme name the endpoint asks for is the one <c>AddJwtBearer()</c> registers - <c>"Bearer"</c>,
/// not the securityScheme key <c>BearerAuth</c>.
/// </remarks>
public class SecurityBearerOnlyRuntimeTests
{
    private const string ScenarioName = "SecurityBearerOnly";
    private const string ValidToken = "valid-demo-token";

    /// <summary>
    /// A stand-in for the domain project's handler, so an authorized request produces a real 200.
    /// </summary>
    private const string HandlerSource = """
        using SecurityBearerOnly.Generated.Handlers;
        using SecurityBearerOnly.Generated.Models;
        using SecurityBearerOnly.Generated.Results;

        namespace SecurityBearerOnly.RuntimeTest;

        public sealed class GetCurrentUserHandler : IGetCurrentUserHandler
        {
            public Task<GetCurrentUserResult> ExecuteAsync(CancellationToken cancellationToken = default)
                => Task.FromResult(GetCurrentUserResult.Ok(new User("someone@example.com")));
        }
        """;

    [Fact]
    public async Task WhoAmI_WithoutToken_Returns401()
    {
        await using var host = await StartHostAsync();

        using var response = await host.Client.GetAsync(
            new Uri("/api/v1/whoami", UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WhoAmI_WithToken_ReachesHandlerWithoutAnyRole()
    {
        await using var host = await StartHostAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/whoami");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ValidToken);

        using var response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("someone@example.com", body, StringComparison.Ordinal);
    }

    private static async Task<RunningHost> StartHostAsync()
    {
        var sources = GeneratorSnapshotHarness.RunRaw(ScenarioName, GeneratorSnapshotHarness.ServerMasterFolder);
        sources.Add(("RuntimeTestHandler.cs", HandlerSource));

        var assembly = CompilationVerificationHarness.EmitAndLoad(sources, includeMinimalApi: true);
        var handlerInterface = assembly.GetType("SecurityBearerOnly.Generated.Handlers.IGetCurrentUserHandler", throwOnError: true)!;
        var handlerType = assembly.GetType("SecurityBearerOnly.RuntimeTest.GetCurrentUserHandler", throwOnError: true)!;
        var serviceExtensions = assembly.GetType("SecurityBearerOnly.Generated.Extensions.UnifiedServiceCollectionExtensions", throwOnError: true)!;
        var appExtensions = assembly.GetType("SecurityBearerOnly.Generated.Extensions.UnifiedWebApplicationExtensions", throwOnError: true)!;

        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();

        // What AddJwtBearer() does, minus token validation: a handler registered under
        // JwtBearerDefaults.AuthenticationScheme ("Bearer"). Deliberately no AddAuthorization() -
        // the generated AddSecurityBearerOnlyApi() has to provide it.
        builder.Services
            .AddAuthentication("Bearer")
            .AddScheme<AuthenticationSchemeOptions, DemoBearerHandler>("Bearer", _ => { });

        builder.Services.AddScoped(handlerInterface, handlerType);
        serviceExtensions
            .GetMethod("AddSecurityBearerOnlyApi")!
            .Invoke(null, [builder.Services, null]);

        var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");

        appExtensions
            .GetMethod("MapSecurityBearerOnlyApi")!
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
    /// Accepts exactly one bearer token, so the tests control who is authenticated.
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
            if (!string.Equals(header, $"Bearer {ValidToken}", StringComparison.Ordinal))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "someone")], Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}