namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// Roles on an operation whose security is a standard requirement with alternatives (a bearer token or an API key),
/// hosted on a real Kestrel server: the roles are checked for a caller signed in by either scheme, not only by the
/// host's default scheme.
/// </summary>
public class RoleSchemesRuntimeTests
{
    private const string ValidToken = "valid-demo-token";
    private const string OperatorKey = "operator-key";
    private const string ViewerKey = "viewer-key";

    // Only the security matters here: no 401 / 403 responses declared, so the specification is not validated
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
          title: Cameras
          version: 1.0.0
        security:
          - bearer: []
          - apiKey: []
        x-authorize-roles: [viewer, operator]
        paths:
          /cameras:
            post:
              operationId: createCamera
              x-authorize-roles: [operator]
              responses:
                '200':
                  description: Created
        components:
          securitySchemes:
            bearer:
              type: http
              scheme: bearer
            apiKey:
              type: apiKey
              in: header
              name: X-API-Key
        """;

    /// <summary>
    /// A stand-in for the domain project's handler, so an authorized request produces a real 200.
    /// </summary>
    private const string HandlerSource = """
        using Cameras.Generated.Handlers;
        using Cameras.Generated.Results;

        namespace Cameras.RuntimeTest;

        public sealed class CreateCameraHandler : ICreateCameraHandler
        {
            public Task<CreateCameraResult> ExecuteAsync(CancellationToken cancellationToken = default)
                => Task.FromResult(CreateCameraResult.Ok());
        }
        """;

    [Theory]
    [InlineData("apiKey", OperatorKey, HttpStatusCode.OK)]
    [InlineData("Bearer", ValidToken, HttpStatusCode.OK)]
    [InlineData("apiKey", ViewerKey, HttpStatusCode.Forbidden)]
    [InlineData(null, null, HttpStatusCode.Unauthorized)]
    public async Task CreateCamera_TheRoleIsCheckedForACallerOfEitherScheme(
        string? scheme,
        string? credential,
        HttpStatusCode expected)
    {
        await using var host = await StartHostAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/cameras");
        if (scheme == "Bearer")
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", credential);
        }
        else if (scheme == "apiKey")
        {
            request.Headers.Add("X-API-Key", credential);
        }

        using var response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(expected, response.StatusCode);
    }

    private static async Task<RunningHost> StartHostAsync()
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", Marker, includeMinimalApi: true, Yaml);
        sources.Add(("RuntimeTestHandler.cs", HandlerSource));

        var assembly = CompilationVerificationHarness.EmitAndLoad(sources, includeMinimalApi: true);
        var handlerInterface = assembly.GetType("Cameras.Generated.Handlers.ICreateCameraHandler", throwOnError: true)!;
        var handlerType = assembly.GetType("Cameras.RuntimeTest.CreateCameraHandler", throwOnError: true)!;
        var serviceExtensions = assembly.GetType("Cameras.Generated.Extensions.UnifiedServiceCollectionExtensions", throwOnError: true)!;
        var appExtensions = assembly.GetType("Cameras.Generated.Extensions.UnifiedWebApplicationExtensions", throwOnError: true)!;

        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();

        // Bearer is the host's default scheme; the API key is a scheme of its own, as the specification names it
        builder.Services
            .AddAuthentication("Bearer")
            .AddScheme<AuthenticationSchemeOptions, DemoBearerHandler>("Bearer", _ => { })
            .AddScheme<AuthenticationSchemeOptions, DemoApiKeyHandler>("apiKey", _ => { });

        builder.Services.AddScoped(handlerInterface, handlerType);
        serviceExtensions
            .GetMethod("AddCamerasApi")!
            .Invoke(null, [builder.Services, null]);

        var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");

        appExtensions
            .GetMethod("UseCamerasApi")!
            .Invoke(null, [app, null]);
        appExtensions
            .GetMethod("MapCamerasApi")!
            .Invoke(null, [app, true]);

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
    /// Accepts exactly one bearer token, an operator's.
    /// </summary>
    private sealed class DemoBearerHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
            => Task.FromResult(
                string.Equals(Request.Headers.Authorization.ToString(), $"Bearer {ValidToken}", StringComparison.Ordinal)
                    ? Success(Scheme.Name, "operator")
                    : AuthenticateResult.NoResult());
    }

    /// <summary>
    /// Accepts an operator's key and a viewer's key from the <c>X-API-Key</c> header.
    /// </summary>
    private sealed class DemoApiKeyHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
            => Task.FromResult(Request.Headers["X-API-Key"].ToString() switch
            {
                OperatorKey => Success(Scheme.Name, "operator"),
                ViewerKey => Success(Scheme.Name, "viewer"),
                _ => AuthenticateResult.NoResult(),
            });
    }

    private static AuthenticateResult Success(
        string scheme,
        string role)
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "someone"), new Claim(ClaimTypes.Role, role)], scheme);

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), scheme));
    }
}