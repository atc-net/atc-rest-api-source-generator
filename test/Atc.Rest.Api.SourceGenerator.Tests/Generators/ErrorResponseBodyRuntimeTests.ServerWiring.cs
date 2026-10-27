namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// The generated server features that only show at run time, through the generated <c>Add…Api()</c> and
/// <c>Map…Api()</c>: an output-cache policy serves the second request from the cache, varies by its query key and never
/// stores or serves an authenticated caller's answer; a webhook receiver (mapped by the documented
/// <c>Map…Webhooks()</c>) receives its payload; a <c>head</c> operation answers HEAD with its header and no body.
/// </summary>
public partial class ErrorResponseBodyRuntimeTests
{
    private const string WiringYaml = """
        openapi: 3.1.0
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /counter:
            x-cache-type: output
            x-cache-policy: counter
            x-cache-expiration-seconds: 60
            x-cache-vary-by-query: [key]
            get:
              operationId: getCounter
              security: []
              parameters:
                - name: key
                  in: query
                  schema: { type: string }
              responses:
                '200':
                  description: How often the handler ran
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Count'
          /things/{id}:
            head:
              operationId: headThing
              security: []
              parameters:
                - name: id
                  in: path
                  required: true
                  schema: { type: string }
              responses:
                '200':
                  description: The thing exists
                  headers:
                    X-Version:
                      schema: { type: string }
                '404':
                  description: No such thing
        webhooks:
          thingCreated:
            post:
              operationId: onThingCreated
              requestBody:
                required: true
                content:
                  application/json:
                    schema:
                      $ref: '#/components/schemas/Count'
              responses:
                '200':
                  description: Received
        components:
          securitySchemes:
            bearer_auth:
              type: http
              scheme: bearer
          schemas:
            Count:
              type: object
              required: [value]
              properties:
                value: { type: integer }
        """;

    private const string WiringHandlerSource = """"
        using Microsoft.AspNetCore.Http;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public sealed class GetCounterHandlerImpl : IGetCounterHandler
        {
            private static int runs;

            public System.Threading.Tasks.Task<GetCounterResult> ExecuteAsync(GetCounterParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(GetCounterResult.Ok(new Count(System.Threading.Interlocked.Increment(ref runs))));
        }

        public sealed class HeadThingHandlerImpl(IHttpContextAccessor accessor) : IHeadThingHandler
        {
            public System.Threading.Tasks.Task<HeadThingResult> ExecuteAsync(HeadThingParameters parameters, System.Threading.CancellationToken cancellationToken = default)
            {
                if (parameters.Id != "1")
                {
                    return System.Threading.Tasks.Task.FromResult(HeadThingResult.NotFound());
                }

                accessor.HttpContext!.Response.Headers["X-Version"] = "v1";
                return System.Threading.Tasks.Task.FromResult(HeadThingResult.Ok());
            }
        }

        public sealed class OnThingCreatedWebhookHandlerImpl : IOnThingCreatedWebhookHandler
        {
            public static int? Received { get; private set; }

            public System.Threading.Tasks.Task<OnThingCreatedWebhookResult> ExecuteAsync(OnThingCreatedWebhookParameters parameters, System.Threading.CancellationToken cancellationToken = default)
            {
                Received = parameters.Payload.Value;
                return System.Threading.Tasks.Task.FromResult(OnThingCreatedWebhookResult.Ok());
            }
        }

        public static class WiringRegistration
        {
            public static void Register(IServiceCollection services)
            {
                services.AddHttpContextAccessor();
                services.AddScoped<IGetCounterHandler, GetCounterHandlerImpl>();
                services.AddScoped<IHeadThingHandler, HeadThingHandlerImpl>();
                services.AddScoped<IOnThingCreatedWebhookHandler, OnThingCreatedWebhookHandlerImpl>();
            }
        }
        """";

    /// <summary>
    /// The second request is served from the cache, a different key is not, and an authenticated caller neither gets
    /// the anonymous entry nor leaves one behind (the cache runs after authentication).
    /// </summary>
    [Fact]
    public async Task OutputCachePolicy_ServesFromTheCache_ByItsQueryKey_AndNeverForAnAuthenticatedCaller()
    {
        await using var host = await StartWiringHostAsync();

        async Task<int> CounterAsync(
            string key,
            string? token = null)
        {
            using var client = new HttpClient { BaseAddress = host.BaseAddress };
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"counter?key={key}", UriKind.Relative));
            if (token is not null)
            {
                // A cookie, not an Authorization header: the cache refuses any request with that header on its own, so
                // only the order of the middleware keeps a cookie-authenticated answer out of it
                request.Headers.Add("Cookie", $"who={token}");
            }

            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            return body.RootElement.GetProperty("value").GetInt32();
        }

        Assert.Equal(1, await CounterAsync("a"));
        Assert.Equal(1, await CounterAsync("a"));
        Assert.Equal(2, await CounterAsync("b"));
        Assert.Equal(3, await CounterAsync("a", token: "alice"));
        Assert.Equal(1, await CounterAsync("a"));
    }

    /// <summary>
    /// A host that still calls <c>UseOutputCache()</c> itself (as v2 had it do) keeps working: the generated middleware
    /// stands aside, where a second output-cache middleware throws on every request.
    /// </summary>
    [Fact]
    public async Task OutputCache_AHostThatAddsTheMiddlewareItself_StillCachesOnce()
    {
        await using var host = await StartWiringHostAsync(configure: app => app.UseOutputCache());
        using var client = new HttpClient { BaseAddress = host.BaseAddress };

        var first = await client.GetStringAsync(new Uri("counter?key=a", UriKind.Relative), TestContext.Current.CancellationToken);
        var second = await client.GetStringAsync(new Uri("counter?key=a", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal("""{"value":1}""", first);
        Assert.Equal(first, second);
    }

    /// <summary>
    /// A webhook receiver mapped by the documented <c>Map…Webhooks()</c> gets the payload the provider posts.
    /// </summary>
    [Fact]
    public async Task WebhookReceiver_GetsThePostedPayload()
    {
        await using var host = await StartWiringHostAsync();
        using var client = new HttpClient { BaseAddress = host.BaseAddress };
        using var content = new StringContent("""{"value":7}""", Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(new Uri("webhooks/thing-created", UriKind.Relative), content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var handler = host.Assembly.GetType("ErrorBodies.RuntimeTest.OnThingCreatedWebhookHandlerImpl", throwOnError: true)!;
        Assert.Equal(7, (int?)handler.GetProperty("Received")!.GetValue(null));
    }

    /// <summary>
    /// A <c>head</c> operation answers HEAD with its declared header and no body; an unknown thing is a 404.
    /// </summary>
    [Fact]
    public async Task HeadOperation_AnswersWithItsHeader_AndNoBody()
    {
        await using var host = await StartWiringHostAsync();
        using var client = new HttpClient { BaseAddress = host.BaseAddress };

        using var foundRequest = new HttpRequestMessage(HttpMethod.Head, new Uri("things/1", UriKind.Relative));
        using var missingRequest = new HttpRequestMessage(HttpMethod.Head, new Uri("things/2", UriKind.Relative));
        using var found = await client.SendAsync(foundRequest, TestContext.Current.CancellationToken);
        using var missing = await client.SendAsync(missingRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        Assert.Equal("v1", found.Headers.GetValues("X-Version").Single());
        Assert.Empty(await found.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    private static Task<XmlTestHost> StartWiringHostAsync(
        Action<WebApplication>? configure = null)
        => XmlTestHost.StartAsync(
            WiringYaml,
            WiringHandlerSource,
            "ErrorBodies.RuntimeTest.WiringRegistration",
            configure: configure,
            configureServices: services => services
                .AddAuthentication("Cookie")
                .AddScheme<AuthenticationSchemeOptions, WhoCookieHandler>("Cookie", _ => { }),
            configureWithAssembly: MapWebhooks);

    // The documented call: the receivers are not part of Map…Api() (webhooks are what the API sends)
    private static void MapWebhooks(
        WebApplication app,
        Assembly assembly)
        => assembly
            .GetType("ErrorBodies.Generated.Webhooks.Endpoints.WebhookEndpointExtensions", throwOnError: true)!
            .GetMethod("MapErrorBodiesWebhooks")!
            .Invoke(null, [app]);

    // Authenticates a request whose "who" cookie names the caller
    private sealed class WhoCookieHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
            => Task.FromResult(
                Request.Cookies.TryGetValue("who", out var who)
                    ? AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", who)], Scheme.Name)), Scheme.Name))
                    : AuthenticateResult.NoResult());
    }
}