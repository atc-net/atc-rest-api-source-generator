// ReSharper disable StringLiteralTypo
namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// End-to-end security and rate-limit matrix: the generated server runs on Kestrel with two test authentication
/// schemes (a bearer token that spells out its subject, scopes and roles; an API key), and every generated client
/// calls its secured and rate-limited operations over HTTP - without credentials, with the wrong ones, with the right
/// ones, and past a limiter's budget. Each cell compares the statuses the client saw, call by call, with what the
/// specification and the documented contract (Working with Security, Working with Rate Limiting, Working with
/// Resilience) say, and the server's own log of the requests proves what the client's retry policy did with a 429:
/// a <c>Retry-After</c> it can wait for is waited for, one past the cap is handed back at once, and a 429 without
/// one is retried with the policy's back-off.
/// </summary>
/// <remarks>
/// Not exercised: a real JWT (the bearer handler here reads a plain-text token), OpenID Connect, and a limiter
/// partitioned by IP (every client runs on the same machine; the limited operations partition by user).
/// </remarks>
public partial class ErrorResponseBodyRuntimeTests
{
    internal static readonly (string Id, string Group, string Label, string Marker, bool Typed)[] GuardClients =
    [
        ("cs-epo-ProblemDetails", "C# EndpointPerOperation", "errorResponseFormat: ProblemDetails", """{ "generationMode": "EndpointPerOperation", "errorResponseFormat": "ProblemDetails" }""", false),
        ("cs-epo-PlainText", "C# EndpointPerOperation", "errorResponseFormat: PlainText", """{ "generationMode": "EndpointPerOperation", "errorResponseFormat": "PlainText" }""", false),
        ("cs-epo-PlainTextOnly", "C# EndpointPerOperation", "errorResponseFormat: PlainTextOnly", """{ "generationMode": "EndpointPerOperation", "errorResponseFormat": "PlainTextOnly" }""", false),
        ("cs-epo-Custom", "C# EndpointPerOperation", "errorResponseFormat: Custom (Monta ErrorResponse)", MontaCustomMarker, false),
        ("cs-typed-Throw", "C# TypedClient", "typedClientResultStyle: Throw", """{ "generationMode": "TypedClient", "typedClientResultStyle": "Throw" }""", true),
        ("cs-typed-Result", "C# TypedClient", "typedClientResultStyle: Result", """{ "generationMode": "TypedClient", "typedClientResultStyle": "Result" }""", true),
        ("ts-fetch", "TypeScript", "Fetch client", string.Empty, false),
        ("ts-axios", "TypeScript", "Axios client", string.Empty, false),
    ];

    // A bearer token spells out who the caller is: "sub=alice;scopes=things:read,admin;scope=things:read admin;roles=admin".
    // "scopes" become one scope claim each, "scope" one space-separated claim (as a JWT handler maps the claim).
    private const string GuardTokenAlice = "sub=alice";

    private const string GuardTokenReader = "sub=reader;scopes=things:read";

    private const string GuardTokenReaderOneClaim = "sub=reader-one;scope=things:read things:write";

    private const string GuardTokenWriter = "sub=writer;scopes=things:write";

    private const string GuardTokenWriterAdmin = "sub=writer-admin;scopes=things:write,admin";

    private const string GuardTokenAdmin = "sub=admin;roles=admin";

    private const string GuardTokenOwner = "sub=owner";

    private const string GuardApiKey = "key-contoso";

    private const string GuardYaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        x-retry-policy: standard
        x-retry-max-attempts: 3
        x-retry-delay-seconds: 0.2
        x-retry-backoff: constant
        x-retry-use-jitter: false
        x-retry-timeout-seconds: 10
        x-retry-handle-429: true
        x-authorize-roles: [admin]
        security:
          - bearer_auth: []
        paths:
          /guard/open:
            get:
              operationId: guardOpen
              security: []
              responses:
                '200': { description: OK, content: { application/json: { schema: { $ref: '#/components/schemas/Thing' } } } }
          /guard/bearer:
            get:
              operationId: guardBearer
              responses:
                '200': { description: OK, content: { application/json: { schema: { $ref: '#/components/schemas/Thing' } } } }
          /guard/apikey:
            get:
              operationId: guardApiKey
              security:
                - api_key: []
              responses:
                '200': { description: OK, content: { application/json: { schema: { $ref: '#/components/schemas/Thing' } } } }
          /guard/scope:
            get:
              operationId: guardScope
              security:
                - oauth2: ["things:read"]
              responses:
                '200': { description: OK, content: { application/json: { schema: { $ref: '#/components/schemas/Thing' } } } }
          /guard/scopes:
            get:
              operationId: guardScopes
              security:
                - oauth2: ["things:write", admin]
              responses:
                '200': { description: OK, content: { application/json: { schema: { $ref: '#/components/schemas/Thing' } } } }
          /guard/either:
            get:
              operationId: guardEither
              security:
                - api_key: []
                - oauth2: ["things:read"]
              responses:
                '200': { description: OK, content: { application/json: { schema: { $ref: '#/components/schemas/Thing' } } } }
          /guard/role:
            get:
              operationId: guardRole
              x-authentication-required: true
              x-authorize-roles: [admin]
              responses:
                '200': { description: OK, content: { application/json: { schema: { $ref: '#/components/schemas/Thing' } } } }
          /guard/mine:
            get:
              operationId: guardMine
              responses:
                '200': { description: OK, content: { application/json: { schema: { $ref: '#/components/schemas/Thing' } } } }
                '403': { description: Not the owner }
          /limit/fixed:
            x-ratelimit-policy: fixed-two
            x-ratelimit-permit-limit: 2
            x-ratelimit-window-seconds: 60
            x-ratelimit-algorithm: fixed
            x-ratelimit-partition: user
            get:
              operationId: limitFixed
              responses:
                '200': { description: OK, content: { application/json: { schema: { $ref: '#/components/schemas/Thing' } } } }
          /limit/sliding:
            x-ratelimit-policy: sliding-one
            x-ratelimit-permit-limit: 1
            x-ratelimit-window-seconds: 60
            x-ratelimit-algorithm: sliding
            x-ratelimit-partition: user
            get:
              operationId: limitSliding
              responses:
                '200': { description: OK, content: { application/json: { schema: { $ref: '#/components/schemas/Thing' } } } }
          /limit/bucket:
            x-ratelimit-policy: bucket-one
            x-ratelimit-permit-limit: 1
            x-ratelimit-window-seconds: 1
            x-ratelimit-algorithm: token-bucket
            x-ratelimit-partition: user
            get:
              operationId: limitBucket
              responses:
                '200': { description: OK, content: { application/json: { schema: { $ref: '#/components/schemas/Thing' } } } }
          /limit/off:
            x-ratelimit-policy: fixed-two
            x-ratelimit-partition: user
            get:
              operationId: limitOff
              x-ratelimit-enabled: false
              responses:
                '200': { description: OK, content: { application/json: { schema: { $ref: '#/components/schemas/Thing' } } } }
        components:
          securitySchemes:
            bearer_auth:
              type: http
              scheme: bearer
            api_key:
              type: apiKey
              in: header
              name: X-API-Key
            oauth2:
              type: oauth2
              x-authentication-scheme: Bearer
              flows:
                clientCredentials:
                  tokenUrl: https://auth.contoso.example/token
                  scopes:
                    "things:read": Read things
                    "things:write": Write things
                    admin: Everything
          schemas:
            Thing:
              type: object
              required: [name]
              properties:
                name: { type: string }
        """;

    private const string GuardHandlerSource = """"
        using Microsoft.AspNetCore.Http;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public sealed class GuardOpenHandlerImpl : IGuardOpenHandler
        {
            public System.Threading.Tasks.Task<GuardOpenResult> ExecuteAsync(System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(GuardOpenResult.Ok(new Thing("open")));
        }

        public sealed class GuardBearerHandlerImpl : IGuardBearerHandler
        {
            public System.Threading.Tasks.Task<GuardBearerResult> ExecuteAsync(System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(GuardBearerResult.Ok(new Thing("bearer")));
        }

        public sealed class GuardApiKeyHandlerImpl : IGuardApiKeyHandler
        {
            public System.Threading.Tasks.Task<GuardApiKeyResult> ExecuteAsync(System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(GuardApiKeyResult.Ok(new Thing("apikey")));
        }

        public sealed class GuardScopeHandlerImpl : IGuardScopeHandler
        {
            public System.Threading.Tasks.Task<GuardScopeResult> ExecuteAsync(System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(GuardScopeResult.Ok(new Thing("scope")));
        }

        public sealed class GuardScopesHandlerImpl : IGuardScopesHandler
        {
            public System.Threading.Tasks.Task<GuardScopesResult> ExecuteAsync(System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(GuardScopesResult.Ok(new Thing("scopes")));
        }

        public sealed class GuardEitherHandlerImpl : IGuardEitherHandler
        {
            public System.Threading.Tasks.Task<GuardEitherResult> ExecuteAsync(System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(GuardEitherResult.Ok(new Thing("either")));
        }

        public sealed class GuardRoleHandlerImpl : IGuardRoleHandler
        {
            public System.Threading.Tasks.Task<GuardRoleResult> ExecuteAsync(System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(GuardRoleResult.Ok(new Thing("role")));
        }

        // The handler's own 403: a caller who is authenticated but is not the owner
        public sealed class GuardMineHandlerImpl(IHttpContextAccessor accessor) : IGuardMineHandler
        {
            public System.Threading.Tasks.Task<GuardMineResult> ExecuteAsync(System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(
                    accessor.HttpContext?.User.FindFirst("sub")?.Value == "owner"
                        ? GuardMineResult.Ok(new Thing("mine"))
                        : GuardMineResult.Forbidden("not yours"));
        }

        public sealed class LimitFixedHandlerImpl : ILimitFixedHandler
        {
            public System.Threading.Tasks.Task<LimitFixedResult> ExecuteAsync(System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(LimitFixedResult.Ok(new Thing("fixed")));
        }

        public sealed class LimitSlidingHandlerImpl : ILimitSlidingHandler
        {
            public System.Threading.Tasks.Task<LimitSlidingResult> ExecuteAsync(System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(LimitSlidingResult.Ok(new Thing("sliding")));
        }

        public sealed class LimitBucketHandlerImpl : ILimitBucketHandler
        {
            public System.Threading.Tasks.Task<LimitBucketResult> ExecuteAsync(System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(LimitBucketResult.Ok(new Thing("bucket")));
        }

        public sealed class LimitOffHandlerImpl : ILimitOffHandler
        {
            public System.Threading.Tasks.Task<LimitOffResult> ExecuteAsync(System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(LimitOffResult.Ok(new Thing("off")));
        }

        public static class GuardRegistration
        {
            public static void Register(IServiceCollection services)
            {
                services.AddHttpContextAccessor();
                services.AddScoped<IGuardOpenHandler, GuardOpenHandlerImpl>();
                services.AddScoped<IGuardBearerHandler, GuardBearerHandlerImpl>();
                services.AddScoped<IGuardApiKeyHandler, GuardApiKeyHandlerImpl>();
                services.AddScoped<IGuardScopeHandler, GuardScopeHandlerImpl>();
                services.AddScoped<IGuardScopesHandler, GuardScopesHandlerImpl>();
                services.AddScoped<IGuardEitherHandler, GuardEitherHandlerImpl>();
                services.AddScoped<IGuardRoleHandler, GuardRoleHandlerImpl>();
                services.AddScoped<IGuardMineHandler, GuardMineHandlerImpl>();
                services.AddScoped<ILimitFixedHandler, LimitFixedHandlerImpl>();
                services.AddScoped<ILimitSlidingHandler, LimitSlidingHandlerImpl>();
                services.AddScoped<ILimitBucketHandler, LimitBucketHandlerImpl>();
                services.AddScoped<ILimitOffHandler, LimitOffHandlerImpl>();
            }
        }
        """";

    internal sealed record GuardCase(
        string Name,
        string Group,
        string Operation,
        int[] Statuses,
        (string Name, string Value)[] Headers,
        string? RetryAfter = null,
        int? Attempts = null,
        int? MinAttempts = null,
        string? Detail = null,
        bool Declared = false);

    /// <summary>
    /// Every generated client, called over HTTP against the generated server, gets the status the specification's
    /// security and rate-limit declarations promise - call by call - and reads it through the accessor the contract
    /// gives it; the server answers each 401 with a challenge, each 429 as the limiter's algorithm allows, and the
    /// client's retry policy treats the 429 as documented (no <c>fail</c> verdict outside <see cref="KnownGuardFailures"/>).
    /// </summary>
    [Fact]
    [Trait("Category", "TypeScriptToolchain")]
    public async Task EndToEnd_EveryClientIsGuardedAsTheSpecificationSays()
    {
        if (!IsNodeAvailable(out var reason))
        {
            if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Fail(reason + " (running under CI where the security and rate-limit matrix is required)");
            }

            Assert.Skip(reason);
        }

        var matrix = await CollectGuardMatrixAsync();
        if (Environment.GetEnvironmentVariable("GUARD_E2E_JSON") is { Length: > 0 } dumpPath)
        {
            await File.WriteAllTextAsync(dumpPath, matrix.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), TestContext.Current.CancellationToken);
        }

        var failures = RequestBindingFailingCells(matrix);
        var unexpected = failures.Where(f => !KnownGuardFailures.ContainsKey(f.Key)).ToList();
        var fixedCells = KnownGuardFailures.Keys.Where(k => failures.All(f => f.Key != k)).Order(StringComparer.Ordinal).ToList();

        Assert.True(
            unexpected.Count == 0,
            $"{unexpected.Count} failing cells:\n" + string.Join("\n", unexpected.Take(120).Select(f => $"{f.Key}: {f.Why}")));
        Assert.True(
            fixedCells.Count == 0,
            $"{fixedCells.Count} cells pass now. Remove them from {nameof(KnownGuardFailures)}:\n" + string.Join("\n", fixedCells));
    }

    /// <summary>
    /// The same matrix from the specification as an OpenAPI 3.1 document: the security schemes, the scopes and the
    /// rate limits must guard every operation as they do from the 3.0 document.
    /// </summary>
    [Fact]
    [Trait("Category", "TypeScriptToolchain")]
    public async Task EndToEnd_EveryClientIsGuardedAsTheSpecificationSays_WithAnOpenApi31Document()
    {
        if (!IsNodeAvailable(out var reason))
        {
            if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Fail(reason + " (running under CI where the security and rate-limit matrix is required)");
            }

            Assert.Skip(reason);
        }

        var matrix = await CollectGuardMatrixAsync(openApi31: true);
        Assert.Equal("3.1.0", (string?)matrix["openApi"]);
        if (Environment.GetEnvironmentVariable("GUARD_E2E_31_JSON") is { Length: > 0 } dumpPath)
        {
            await File.WriteAllTextAsync(dumpPath, matrix.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), TestContext.Current.CancellationToken);
        }

        var failures = RequestBindingFailingCells(matrix);
        var unexpected = failures.Where(f => !KnownGuardFailures.ContainsKey(f.Key)).ToList();
        var fixedCells = KnownGuardFailures.Keys.Where(k => failures.All(f => f.Key != k)).Order(StringComparer.Ordinal).ToList();

        Assert.True(
            unexpected.Count == 0,
            $"{unexpected.Count} failing cells:\n" + string.Join("\n", unexpected.Take(120).Select(f => $"{f.Key}: {f.Why}")));
        Assert.True(
            fixedCells.Count == 0,
            $"{fixedCells.Count} cells pass now. Remove them from {nameof(KnownGuardFailures)}:\n" + string.Join("\n", fixedCells));
    }

    /// <summary>
    /// Calls every case through a generated C# client against the live host, as many times as the case says, with
    /// the case's credentials on every request; each call's outcome is the status, the accessor the client offers
    /// for it, and the <c>Retry-After</c> it exposes.
    /// </summary>
    private const string GuardProbeSource = """"
        using System.Net.Http;
        using System.Reflection;
        using System.Text.Json;
        using System.Text.Json.Nodes;
        using Atc.Rest.Client;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        // Every request carries the case's credentials and the probe id the server logs the request under
        public sealed class ProbeStampHandler : DelegatingHandler
        {
            public static Dictionary<string, string> Current { get; set; } = new(StringComparer.Ordinal);

            protected override System.Threading.Tasks.Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
            {
                foreach (var header in Current)
                {
                    request.Headers.Remove(header.Key);
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                return base.SendAsync(request, cancellationToken);
            }
        }

        public static class GuardProbe
        {
            public static async System.Threading.Tasks.Task<Dictionary<string, string>> RunAsync(string baseAddress, string casesJson, bool typed, string clientId)
            {
                var assembly = typeof(GuardProbe).Assembly;
                var results = new Dictionary<string, string>(StringComparer.Ordinal);
                var services = new ServiceCollection();
                services.AddTransient<ProbeStampHandler>();
                var httpClientName = typed
                    ? "typed"
                    : (string)assembly.GetTypes().Single(t => t.Name == "Constants").GetField("HttpClientName")!.GetValue(null)!;
                var builder = services.AddHttpClient(httpClientName, c => c.BaseAddress = new Uri(baseAddress)).AddHttpMessageHandler<ProbeStampHandler>();
                ErrorBodies.Generated.Resilience.ApiResilienceHttpClientBuilderExtensions.AddApiResilience(builder, ResiliencePolicies.Standard);
                if (!typed)
                {
                    foreach (var register in assembly.GetTypes().SelectMany(t => t.GetMethods()).Where(m => m.IsStatic && m.Name.StartsWith("AddErrorBodies", StringComparison.Ordinal) && m.Name.EndsWith("Endpoints", StringComparison.Ordinal)))
                    {
                        register.Invoke(null, [services, null]);
                    }
                }

                using var provider = services.BuildServiceProvider();
                foreach (var item in JsonNode.Parse(casesJson)!.AsArray())
                {
                    var name = (string)item!["name"]!;
                    var headers = item["headers"]!.AsObject().ToDictionary(h => h.Key, h => (string)h.Value!, StringComparer.Ordinal);
                    headers["X-Probe"] = clientId + "|" + name;
                    ProbeStampHandler.Current = headers;
                    var calls = new JsonArray();
                    for (var i = 0; i < (int)item["calls"]!; i++)
                    {
                        try
                        {
                            calls.Add(typed
                                ? await CallTypedAsync(assembly, provider, httpClientName, (string)item["operation"]!)
                                : await CallEndpointAsync(assembly, provider, httpClientName, (string)item["operation"]!));
                        }
                        catch (Exception ex)
                        {
                            var inner = ex is TargetInvocationException { InnerException: { } e } ? e : ex;
                            calls.Add(new JsonObject
                            {
                                ["status"] = inner is HttpRequestException { StatusCode: { } code } ? (int)code : 0,
                                ["threw"] = inner.GetType().Name + ": " + Short(inner.Message),

                                // The Throw style's ApiException carries the response's headers
                                ["retryAfter"] = inner.GetType().GetProperty("Headers")?.GetValue(inner) is IReadOnlyDictionary<string, IEnumerable<string>> responseHeaders &&
                                                 responseHeaders.TryGetValue("Retry-After", out var retryAfterValues)
                                    ? string.Join(",", retryAfterValues)
                                    : null,
                            });
                        }
                    }

                    results[name] = new JsonObject { ["calls"] = calls }.ToJsonString();
                }

                return results;
            }

            private static async System.Threading.Tasks.Task<JsonObject> CallEndpointAsync(Assembly assembly, ServiceProvider provider, string httpClientName, string operation)
            {
                var endpointType = assembly.GetTypes().Single(t => t.IsInterface && t.Name == $"I{operation}Endpoint");
                var execute = endpointType.GetMethod("ExecuteAsync")!;
                var task = (System.Threading.Tasks.Task)execute.Invoke(provider.GetRequiredService(endpointType), [httpClientName, System.Threading.CancellationToken.None])!;
                await task;
                var result = task.GetType().GetProperty("Result")!.GetValue(task)!;
                return Describe((EndpointResponse)result);
            }

            private static async System.Threading.Tasks.Task<JsonObject> CallTypedAsync(Assembly assembly, ServiceProvider provider, string httpClientName, string operation)
            {
                var clientType = assembly.GetTypes().Single(t => t.IsClass && !t.IsAbstract && t.GetMethod(operation + "Async") is not null);
                var http = provider.GetRequiredService<IHttpClientFactory>().CreateClient(httpClientName);
                var client = Activator.CreateInstance(clientType, http)!;
                var task = (System.Threading.Tasks.Task)clientType.GetMethod(operation + "Async")!.Invoke(client, [System.Threading.CancellationToken.None])!;
                await task;
                var value = task.GetType().GetProperty("Result")!.GetValue(task);
                return value is EndpointResponse response
                    ? Describe(response)
                    : new JsonObject { ["status"] = 200, ["success"] = true, ["accessor"] = "returned " + (value?.GetType().Name ?? "null") };
            }

            // The status, IsSuccess, the accessor the Is* flag leads to (its type and value), and Retry-After
            private static JsonObject Describe(EndpointResponse response)
            {
                var type = response.GetType();
                var accessor = "(none)";
                foreach (var flag in type.GetProperties().Where(p => p.PropertyType == typeof(bool) && p.Name.StartsWith("Is", StringComparison.Ordinal) && p.Name != "IsSuccess"))
                {
                    if (!(bool)flag.GetValue(response)!)
                    {
                        continue;
                    }

                    var content = type.GetProperty(flag.Name[2..] + "Content");
                    if (content is null)
                    {
                        accessor = flag.Name + " (no content property)";
                        break;
                    }

                    try
                    {
                        var value = content.GetValue(response);
                        accessor = value is null
                            ? content.Name + " => null"
                            : content.Name + " => " + value.GetType().Name + " " + JsonSerializer.Serialize(value, value.GetType());
                    }
                    catch (TargetInvocationException ex)
                    {
                        accessor = content.Name + " THROWS " + ex.InnerException!.GetType().Name + ": " + ex.InnerException.Message;
                    }

                    break;
                }

                string? retryAfter = null;
                if (response.Headers is { } headers)
                {
                    foreach (var header in headers)
                    {
                        if (string.Equals(header.Key, "Retry-After", StringComparison.OrdinalIgnoreCase))
                        {
                            retryAfter = string.Join(",", header.Value);
                        }
                    }
                }

                return new JsonObject
                {
                    ["status"] = (int)response.StatusCode,
                    ["success"] = response.IsSuccess,
                    ["accessor"] = Short(accessor),
                    ["retryAfter"] = retryAfter,
                    ["content"] = Short(response.Content ?? string.Empty),
                };
            }

            private static string Short(string text)
                => text.Length > 300 ? text[..300] + "..." : text;
        }
        """";

    // Calls every case through the generated ApiService against the live host, with the case's credentials as the
    // service's default headers; each call's outcome is the arm, the status, the error body's status and Retry-After.
    private const string GuardHarness = """
        import { readFileSync, writeFileSync } from 'node:fs';

        const which = process.argv[2];
        const { baseUrl, cases } = JSON.parse(readFileSync('cases.json', 'utf8'));
        const { ApiService } = await import(`./${which}/client/ApiService.ts`);

        const results: Record<string, unknown>[] = [];
        for (const c of cases) {
          const api: any = new ApiService(baseUrl, { defaultHeaders: { ...c.headers, 'X-Probe': `ts-${which}|${c.name}` } });
          const calls: Record<string, unknown>[] = [];
          for (let i = 0; i < c.calls; i++) {
            try {
              const r = await api[c.segment][c.method]() as any;
              const response = r.response ?? r.error?.response;
              const headers = response?.headers;
              const retryAfter = headers?.get ? headers.get('retry-after') : headers?.['retry-after'];
              calls.push({
                arm: r.status,
                status: r.status === 'ok' ? 200 : (r.error?.status ?? response?.status ?? 0),
                retryAfter: retryAfter ?? null,
                bodyStatus: r.status === 'ok' ? 200 : (r.error?.body?.status ?? 0),
                errorClass: r.error?.constructor?.name ?? null,
              });
            } catch (err: any) {
              calls.push({ arm: 'threw', status: 0, retryAfter: null, bodyStatus: 0, detail: String(err?.message ?? err).slice(0, 200) });
            }
          }
          results.push({ name: c.name, calls });
        }

        writeFileSync(`results-${which}.json`, JSON.stringify(results));
        """;

    /// <summary>
    /// The cells that fail today, as <c>case [client]</c>: a cell not listed here must pass, and a listed cell that
    /// passes fails the test asking to be removed.
    /// </summary>
    private static readonly Dictionary<string, string> KnownGuardFailures = new(StringComparer.Ordinal);

    /// <summary>
    /// The verdict is not blind: a wrong status, a missing accessor, a lost Retry-After, a 401 without a challenge and
    /// a retry the server did not see each fail, and the documented outcome passes.
    /// </summary>
    [Fact]
    public void GuardVerdicts_FailAWrongStatusAccessorOrRetry()
    {
        var limited = GuardCases().Single(c => c.Name.StartsWith("fixed window: the third", StringComparison.Ordinal));
        var client = GuardClients[0];
        JsonObject Call(
            int status,
            string accessor,
            string? retryAfter = null)
            => new() { ["status"] = status, ["success"] = status < 400, ["accessor"] = accessor, ["retryAfter"] = retryAfter };
        var ok = Call(200, "OkContent => Thing {\"Name\":\"fixed\"}");
        var tooMany = Call(429, "TooManyRequestsContent => ProblemDetails {\"Status\":429}", "60");
        List<GuardLogEntry> Log(params int[] statuses)
            => [.. statuses.Select(s => new GuardLogEntry("/limit/fixed", s, s == 429 ? "60" : null, null, s == 429 ? "application/problem+json" : "application/json", s == 429 ? "{\"status\":429}" : "{}"))];

        Assert.Equal(VerdictPass, (string?)GuardVerdict(limited, client, Calls(ok, ok, tooMany), Log(200, 200, 429))["verdict"]);
        Assert.Equal(VerdictFail, (string?)GuardVerdict(limited, client, Calls(ok, ok, ok), Log(200, 200, 200))["verdict"]);
        Assert.Equal(VerdictFail, (string?)GuardVerdict(limited, client, Calls(ok, ok, Call(429, "(none)", "60")), Log(200, 200, 429))["verdict"]);
        Assert.Equal(VerdictFail, (string?)GuardVerdict(limited, client, Calls(ok, ok, Call(429, "TooManyRequestsContent => ProblemDetails {\"Status\":429}")), Log(200, 200, 429))["verdict"]);
        Assert.Equal(VerdictFail, (string?)GuardVerdict(limited, client, Calls(ok, ok, tooMany), Log(200, 200, 429, 429))["verdict"]);
        Assert.Equal(VerdictFail, (string?)GuardVerdict(limited, client, Calls(ok, ok), Log(200, 200))["verdict"]);

        var unauthorized = GuardCases().Single(c => c.Name == "bearer: no token");
        var challenge = new GuardLogEntry("/guard/bearer", 401, null, "Bearer", "application/problem+json", "{\"status\":401}");
        Assert.Equal(VerdictPass, (string?)GuardVerdict(unauthorized, client, Calls(Call(401, "UnauthorizedContent => ProblemDetails {\"Status\":401}")), [challenge])["verdict"]);
        Assert.Equal(VerdictFail, (string?)GuardVerdict(unauthorized, client, Calls(Call(401, "UnauthorizedContent => ProblemDetails {\"Status\":401}")), [challenge with { WwwAuthenticate = null }])["verdict"]);
        Assert.Equal(VerdictFail, (string?)GuardVerdict(unauthorized, client, Calls(Call(401, "UnauthorizedContent => ProblemDetails {\"Status\":401}")), [challenge with { MediaType = "text/plain", Body = "Unauthorized" }])["verdict"]);
        Assert.Equal(VerdictFail, (string?)GuardVerdict(unauthorized, client, Calls(Call(401, "UnauthorizedContent => string \"\"")), [challenge])["verdict"]);

        var forbidden = GuardCases().Single(c => c.Name.StartsWith("handler: Forbidden", StringComparison.Ordinal));
        var forbids = new GuardLogEntry("/guard/mine", 403, null, null, "application/problem+json", "{\"status\":403,\"detail\":\"not yours\"}");
        Assert.Equal(VerdictPass, (string?)GuardVerdict(forbidden, client, Calls(Call(403, "ForbiddenContent => ProblemDetails {\"Status\":403,\"Detail\":\"not yours\"}")), [forbids])["verdict"]);
        Assert.Equal(VerdictFail, (string?)GuardVerdict(forbidden, client, Calls(Call(403, "ForbiddenContent => ProblemDetails {\"Status\":403,\"Detail\":\"not yours\"}")), [forbids with { Body = "{\"status\":403}" }])["verdict"]);

        var typedThrow = GuardClients.Single(c => c.Id == "cs-typed-Throw");

        // The Throw style throws the generated ApiException - a bare HttpRequestException has no headers - with the
        // status and the body, and a 429's Retry-After among its headers
        Assert.Equal(VerdictPass, (string?)GuardVerdict(unauthorized, typedThrow, Calls(new JsonObject { ["status"] = 401, ["threw"] = "ApiException: HTTP 401 (Unauthorized): {\"status\":401}" }), [challenge])["verdict"]);
        Assert.Equal(VerdictFail, (string?)GuardVerdict(unauthorized, typedThrow, Calls(new JsonObject { ["status"] = 401, ["threw"] = "ApiException: HTTP 401 (Unauthorized): " }), [challenge])["verdict"]);
        Assert.Equal(VerdictFail, (string?)GuardVerdict(unauthorized, typedThrow, Calls(new JsonObject { ["status"] = 401, ["threw"] = "HttpRequestException: HTTP 401 (Unauthorized): {\"status\":401}" }), [challenge])["verdict"]);
        JsonObject Thrown429(string? retryAfter)
            => new() { ["status"] = 429, ["threw"] = "ApiException: HTTP 429 (Too Many Requests): {\"status\":429}", ["retryAfter"] = retryAfter };
        Assert.Equal(VerdictPass, (string?)GuardVerdict(limited, typedThrow, Calls(new JsonObject { ["status"] = 200 }, new JsonObject { ["status"] = 200 }, Thrown429("60")), Log(200, 200, 429))["verdict"]);
        Assert.Equal(VerdictFail, (string?)GuardVerdict(limited, typedThrow, Calls(new JsonObject { ["status"] = 200 }, new JsonObject { ["status"] = 200 }, Thrown429(null)), Log(200, 200, 429))["verdict"]);

        var ts = GuardClients.Single(c => c.Id == "ts-fetch");
        JsonObject TsCall(
            int status,
            string arm,
            string? retryAfter = null,
            int? bodyStatus = null)
            => new() { ["status"] = status, ["arm"] = arm, ["retryAfter"] = retryAfter, ["bodyStatus"] = bodyStatus ?? status };
        Assert.Equal(VerdictPass, (string?)GuardVerdict(limited, ts, Calls(TsCall(200, "ok"), TsCall(200, "ok"), TsCall(429, "tooManyRequests", "60")), Log(200, 200, 429))["verdict"]);
        Assert.Equal(VerdictFail, (string?)GuardVerdict(limited, ts, Calls(TsCall(200, "ok"), TsCall(200, "ok"), TsCall(429, "clientError", "60")), Log(200, 200, 429))["verdict"]);
        Assert.Equal(VerdictFail, (string?)GuardVerdict(unauthorized, ts, Calls(TsCall(401, "unauthorized", bodyStatus: 0)), [challenge])["verdict"]);

        static JsonArray Calls(params JsonObject[] calls)
            => new([.. calls.Select(c => c.DeepClone())]);
    }

    /// <summary>
    /// The cases: the operation, the credentials every request carries, and the statuses the calls must get, in
    /// order. <c>Attempts</c> is what the server must have logged for the cell (the retries included); a limited case
    /// carries a distinct subject per client, so each cell has its own budget.
    /// </summary>
    internal static List<GuardCase> GuardCases()
        =>
        [
            new("open: no credentials", "authentication", "GuardOpen", [200], []),
            new("bearer: no token", "authentication", "GuardBearer", [401], []),
            new("bearer: a token", "authentication", "GuardBearer", [200], [("Authorization", "Bearer " + GuardTokenAlice)]),
            new("bearer: a token the handler rejects", "authentication", "GuardBearer", [401], [("Authorization", "Bearer bad")]),
            new("api key: no key", "authentication", "GuardApiKey", [401], []),
            new("api key: the key", "authentication", "GuardApiKey", [200], [("X-API-Key", GuardApiKey)]),
            new("api key: a bearer token instead", "authentication", "GuardApiKey", [401], [("Authorization", "Bearer " + GuardTokenAlice)]),
            new("scope: a token without the scope", "authorization", "GuardScope", [403], [("Authorization", "Bearer " + GuardTokenWriter)]),
            new("scope: a token with the scope", "authorization", "GuardScope", [200], [("Authorization", "Bearer " + GuardTokenReader)]),
            new("scope: the scopes in one space-separated claim", "authorization", "GuardScope", [200], [("Authorization", "Bearer " + GuardTokenReaderOneClaim)]),
            new("two scopes: a token with one of them", "authorization", "GuardScopes", [403], [("Authorization", "Bearer " + GuardTokenWriter)]),
            new("two scopes: a token with both", "authorization", "GuardScopes", [200], [("Authorization", "Bearer " + GuardTokenWriterAdmin)]),
            new("key or scope: the key", "authorization", "GuardEither", [200], [("X-API-Key", GuardApiKey)]),
            new("key or scope: a token with the scope", "authorization", "GuardEither", [200], [("Authorization", "Bearer " + GuardTokenReader)]),
            new("key or scope: a token without the scope", "authorization", "GuardEither", [403], [("Authorization", "Bearer " + GuardTokenWriter)]),
            new("key or scope: nothing", "authorization", "GuardEither", [401], []),
            new("role: a token without the role", "authorization", "GuardRole", [403], [("Authorization", "Bearer " + GuardTokenAlice)]),
            new("role: a token with the role", "authorization", "GuardRole", [200], [("Authorization", "Bearer " + GuardTokenAdmin)]),
            new("handler: Forbidden(\"not yours\")", "authorization", "GuardMine", [403], [("Authorization", "Bearer " + GuardTokenAlice)], Detail: "not yours", Declared: true),
            new("handler: the owner", "authorization", "GuardMine", [200], [("Authorization", "Bearer " + GuardTokenOwner)]),
            new("fixed window: the third request is rejected, Retry-After past the cap", "rate limit", "LimitFixed", [200, 200, 429], [("Authorization", "Bearer sub={client}-fixed")], RetryAfter: "60", Attempts: 3),
            new("fixed window: a second user has its own budget", "rate limit", "LimitFixed", [200, 200], [("Authorization", "Bearer sub={client}-fixed-other")], Attempts: 2),
            new("sliding window: no Retry-After, retried with the back-off, then handed back", "rate limit", "LimitSliding", [200, 429], [("Authorization", "Bearer sub={client}-sliding")], Attempts: 5),
            new("token bucket: Retry-After waited for, the retry succeeds", "rate limit", "LimitBucket", [200, 200], [("Authorization", "Bearer sub={client}-bucket")], RetryAfter: "1", MinAttempts: 3),
            new("disabled on the operation", "rate limit", "LimitOff", [200, 200, 200], [("Authorization", "Bearer sub={client}-off")], Attempts: 3),
        ];

    internal sealed record GuardLogEntry(
        string Path,
        int Status,
        string? RetryAfter,
        string? WwwAuthenticate,
        string? MediaType,
        string Body);

    /// <summary>
    /// A cell's verdict: the client's calls against the case's statuses and accessors, and the server's log of the
    /// cell's requests against the case's attempts, challenge and Retry-After.
    /// </summary>
    private static JsonObject GuardVerdict(
        GuardCase guardCase,
        (string Id, string Group, string Label, string Marker, bool Typed) client,
        JsonArray? calls,
        IReadOnlyList<GuardLogEntry> log)
    {
        if (calls is null)
        {
            return Cell(VerdictFail, "no result recorded for the case", "-");
        }

        var problems = new List<string>();
        var observed = calls.Select(c => c!.AsObject()).ToList();
        var summary = string.Join(", ", observed.Select(c => $"{(int?)c["status"]}{((string?)c["retryAfter"] is { } ra ? " RA=" + ra : string.Empty)}"));
        if (observed.Count != guardCase.Statuses.Length || !observed.Select(c => (int?)c["status"] ?? 0).SequenceEqual(guardCase.Statuses))
        {
            problems.Add($"statuses [{string.Join(", ", observed.Select(c => (int?)c["status"]))}], expected [{string.Join(", ", guardCase.Statuses)}]");
        }

        // What the client offers for each call: the accessor (C#) or the arm (TypeScript), Retry-After on a 429
        for (var i = 0; i < System.Math.Min(observed.Count, guardCase.Statuses.Length); i++)
        {
            var call = observed[i];
            var status = guardCase.Statuses[i];
            var isTs = client.Id.StartsWith("ts-", StringComparison.Ordinal);
            var isThrow = client.Id.EndsWith("Throw", StringComparison.Ordinal);
            if (isTs)
            {
                var expectedArm = status switch { 200 => "ok", 401 => "unauthorized", 403 => "forbidden", 429 => "tooManyRequests", _ => "?" };
                if ((string?)call["arm"] != expectedArm)
                {
                    problems.Add($"call {i + 1}: arm '{(string?)call["arm"]}', expected '{expectedArm}'");
                }

                if (status >= 400 && (int?)call["bodyStatus"] != status)
                {
                    problems.Add($"call {i + 1}: the error body's status is {(int?)call["bodyStatus"]}, expected {status}");
                }
            }
            else if (isThrow)
            {
                var threw = (string?)call["threw"];
                if (status < 400 && threw is not null)
                {
                    problems.Add($"call {i + 1}: threw {threw}");
                }
                else if (status >= 400 && (threw is null || !threw.StartsWith("ApiException: ", StringComparison.Ordinal) || !threw.Contains($"HTTP {status}", StringComparison.Ordinal) || !threw.Contains($"\"status\":{status}", StringComparison.Ordinal)))
                {
                    problems.Add($"call {i + 1}: expected the generated ApiException carrying HTTP {status} and the body, got '{threw ?? "returned normally"}'");
                }
            }
            else
            {
                var accessor = (string?)call["accessor"] ?? "(none)";
                if (status < 400)
                {
                    if ((bool?)call["success"] != true || (!client.Typed && !accessor.StartsWith("OkContent => Thing ", StringComparison.Ordinal)))
                    {
                        problems.Add($"call {i + 1}: expected IsSuccess and OkContent => Thing, got '{accessor}'");
                    }
                }
                else if (!client.Typed)
                {
                    // A status the client adds for a secured or limited operation is a ProblemDetails in every format
                    // but Custom; one the specification declares without a body follows the errorResponseFormat table
                    // (Working with Error Responses): a string under PlainText and PlainTextOnly - the problem's detail.
                    var name = status switch { 401 => "Unauthorized", 403 => "Forbidden", 429 => "TooManyRequests", _ => "?" };
                    var format = client.Label["errorResponseFormat: ".Length..].Split(' ')[0];
                    var expectedType = format switch
                    {
                        "Custom" => "ErrorResponse",
                        "PlainText" or "PlainTextOnly" when guardCase.Declared => "String",
                        _ => "ProblemDetails",
                    };
                    if ((bool?)call["success"] != false || !accessor.StartsWith($"{name}Content => {expectedType} ", StringComparison.Ordinal))
                    {
                        problems.Add($"call {i + 1}: expected {name}Content => {expectedType}, got '{accessor}'");
                    }
                    else if (expectedType == "String" && guardCase.Detail is { } text && !accessor.EndsWith($"\"{text}\"", StringComparison.Ordinal))
                    {
                        problems.Add($"call {i + 1}: the string should be the problem's detail '{text}': '{accessor}'");
                    }
                    else if (expectedType == "ProblemDetails" && !accessor.Contains($"\"Status\":{status}", StringComparison.Ordinal))
                    {
                        problems.Add($"call {i + 1}: the ProblemDetails has no status {status}: '{accessor}'");
                    }
                    else if (guardCase.Detail is { } detail && expectedType == "ProblemDetails" && !accessor.Contains($"\"Detail\":\"{detail}\"", StringComparison.Ordinal))
                    {
                        problems.Add($"call {i + 1}: the ProblemDetails has no detail '{detail}': '{accessor}'");
                    }
                }
                else if ((bool?)call["success"] != false)
                {
                    problems.Add($"call {i + 1}: IsSuccess should be false for a {status}");
                }
            }

            if (status == 429 && guardCase.RetryAfter is { } wanted && (string?)call["retryAfter"] != wanted)
            {
                problems.Add($"call {i + 1}: Retry-After '{(string?)call["retryAfter"] ?? "(none)"}' exposed, expected '{wanted}'");
            }
        }

        // The server's side of the cell: the attempts (retries included), the challenge, the limiter's answer
        if (guardCase.Attempts is { } attempts && log.Count != attempts)
        {
            problems.Add($"the server logged {log.Count} requests, expected {attempts}");
        }

        if (guardCase.MinAttempts is { } minimum && (log.Count < minimum || log.All(e => e.Status != 429)))
        {
            problems.Add($"the server logged {log.Count} requests ({string.Join(",", log.Select(e => e.Status))}), expected at least {minimum} with a 429 among them");
        }

        foreach (var entry in log.Where(e => e.Status >= 400))
        {
            if (!string.Equals(entry.MediaType, "application/problem+json", StringComparison.Ordinal) || TryParseJson(entry.Body) is not JsonObject problem || (int?)problem["status"] != entry.Status)
            {
                problems.Add($"the server's {entry.Status} is no problem+json with its status ({entry.MediaType ?? "(none)"}: {Short(entry.Body)})");
                break;
            }

            if (entry.Status == 401 && string.IsNullOrEmpty(entry.WwwAuthenticate))
            {
                problems.Add("the server's 401 carries no WWW-Authenticate challenge");
                break;
            }

            if (entry.Status == 403 && guardCase.Detail is { } detail && (string?)problem["detail"] != detail)
            {
                problems.Add($"the server's 403 has detail '{(string?)problem["detail"] ?? "(none)"}', expected '{detail}'");
                break;
            }

            if (entry.Status == 429 && !string.Equals(entry.RetryAfter, guardCase.RetryAfter, StringComparison.Ordinal))
            {
                problems.Add($"the server's 429 has Retry-After '{entry.RetryAfter ?? "(none)"}', expected '{guardCase.RetryAfter ?? "(none)"}'");
                break;
            }
        }

        return problems.Count == 0
            ? Cell(VerdictPass, "as the specification and the contract say", summary)
            : Cell(VerdictFail, string.Join("; ", problems), summary);

        static string Short(string text)
            => text.Length > 120 ? text[..120] + "..." : text;
    }

    private static async Task<JsonObject> CollectGuardMatrixAsync(
        bool openApi31 = false)
    {
        OpenApi31Run.Value = openApi31;
        var started = DateTimeOffset.Now;
        var cases = GuardCases();
        var log = new ConcurrentQueue<(string Probe, GuardLogEntry Entry)>();

        await using var host = await XmlTestHost.StartAsync(
            Spec(GuardYaml),
            GuardHandlerSource,
            "ErrorBodies.RuntimeTest.GuardRegistration",
            app => app.Use(async (context, next) =>
            {
                // Log every request under its probe id, with the response as the client gets it
                var original = context.Response.Body;
                using var buffer = new MemoryStream();
                context.Response.Body = buffer;
                try
                {
                    await next(context);
                }
                finally
                {
                    buffer.Position = 0;
                    var body = await new StreamReader(buffer, Encoding.UTF8).ReadToEndAsync(context.RequestAborted);
                    buffer.Position = 0;
                    await buffer.CopyToAsync(original, context.RequestAborted);
                    context.Response.Body = original;
                    if (context.Request.Headers.TryGetValue("X-Probe", out var probe))
                    {
                        log.Enqueue((probe.ToString(), new GuardLogEntry(
                            context.Request.Path,
                            context.Response.StatusCode,
                            context.Response.Headers.RetryAfter.Count > 0 ? context.Response.Headers.RetryAfter.ToString() : null,
                            context.Response.Headers.WWWAuthenticate.Count > 0 ? context.Response.Headers.WWWAuthenticate.ToString() : null,
                            context.Response.ContentType?.Split(';')[0].Trim(),
                            body)));
                    }
                }
            }),
            services => services
                .AddAuthentication("Bearer")
                .AddScheme<AuthenticationSchemeOptions, GuardBearerHandler>("Bearer", _ => { })
                .AddScheme<AuthenticationSchemeOptions, GuardApiKeyHandler>("api_key", _ => { }));

        var rows = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        foreach (var client in GuardClients.Where(c => c.Marker.Length > 0))
        {
            var sources = Generate(new ApiClientGenerator(), ".atc-rest-api-client", client.Marker, includeMinimalApi: false, Spec(GuardYaml));
            var usings = sources
                .Select(s => Regex.Match(s.Source, @"^namespace (?<ns>[\w.]+);", RegexOptions.Multiline | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1)).Groups["ns"].Value)
                .Where(ns => ns.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .Select(ns => $"global using {ns};");
            sources.Add(("GuardProbe.cs", string.Join(Environment.NewLine, usings) + Environment.NewLine + GuardProbeSource));
            var assembly = CompilationVerificationHarness.EmitAndLoad(sources);
            var run = (Task<Dictionary<string, string>>)assembly.GetType("ErrorBodies.RuntimeTest.GuardProbe", throwOnError: true)!
                .GetMethod("RunAsync")!
                .Invoke(null, [host.BaseAddress.ToString(), GuardCasesJson(cases, client.Id).ToJsonString(), client.Typed, client.Id])!;
            rows[client.Id] = await run;
        }

        var typeScript = RunGuardTypeScriptClients(host.BaseAddress, cases);

        var casesArray = new JsonArray();
        foreach (var guardCase in cases)
        {
            var clients = new JsonObject();
            foreach (var client in GuardClients)
            {
                var tsRecord = client.Id.StartsWith("ts-", StringComparison.Ordinal) ? typeScript[client.Id].GetValueOrDefault(guardCase.Name) : null;
                var observed = client.Id.StartsWith("ts-", StringComparison.Ordinal)
                    ? tsRecord?["calls"]?.AsArray()
                    : rows[client.Id].GetValueOrDefault(guardCase.Name) is { } row ? JsonNode.Parse(row)!["calls"]!.AsArray() : null;
                var probe = client.Id + "|" + guardCase.Name;
                var entries = log.Where(e => e.Probe == probe).Select(e => e.Entry).ToList();
                var cell = HarnessCell(tsRecord) ?? GuardVerdict(guardCase, client, observed, entries);
                cell["server"] = string.Join(", ", entries.Select(e => $"{e.Status}{(e.RetryAfter is null ? string.Empty : " RA=" + e.RetryAfter)}"));
                clients[client.Id] = cell;
            }

            casesArray.Add(new JsonObject
            {
                ["id"] = guardCase.Name,
                ["group"] = guardCase.Group,
                ["operation"] = guardCase.Operation,
                ["expects"] = string.Join(", ", guardCase.Statuses) + (guardCase.RetryAfter is null ? string.Empty : $" (Retry-After {guardCase.RetryAfter})") + (guardCase.Attempts is null ? string.Empty : $", {guardCase.Attempts} requests at the server"),
                ["clients"] = clients,
            });
        }

        return new JsonObject
        {
            ["generatedAt"] = started.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
            ["openApi"] = OpenApiVersionOf(Spec(GuardYaml)),
            ["durationSeconds"] = System.Math.Round((DateTimeOffset.Now - started).TotalSeconds, 1),
            ["clients"] = new JsonArray([.. GuardClients.Select(c => (JsonNode)new JsonObject { ["id"] = c.Id, ["group"] = c.Group, ["label"] = c.Label })]),
            ["cases"] = casesArray,
        };
    }

    private static JsonArray GuardCasesJson(
        IEnumerable<GuardCase> cases,
        string clientId)
        => new([
            .. cases.Select(c => (JsonNode)new JsonObject
            {
                ["name"] = c.Name,
                ["operation"] = c.Operation,
                ["segment"] = c.Operation.StartsWith("Guard", StringComparison.Ordinal) ? "guard" : "limit",
                ["method"] = char.ToLowerInvariant(c.Operation[0]) + c.Operation[1..],
                ["calls"] = c.Statuses.Length,
                ["headers"] = new JsonObject(c.Headers.Select(h => new KeyValuePair<string, JsonNode?>(h.Name, h.Value.Replace("{client}", clientId, StringComparison.Ordinal)))),
            }),
        ]);

    private static Dictionary<string, Dictionary<string, JsonObject>> RunGuardTypeScriptClients(
        Uri baseAddress,
        List<GuardCase> cases)
    {
        var work = Path.Combine(Path.GetTempPath(), "atc-guard-e2e", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            var yamlPath = Path.Combine(work, "ErrorBodies.yaml");
            File.WriteAllText(yamlPath, Spec(GuardYaml));

            var cli = FindCliExecutable();
            foreach (var client in (string[])["Fetch", "Axios"])
            {
                var output = Path.Combine(work, client.ToLowerInvariant());
                var generate = RunTool(cli, $"generate client-typescript -s \"{yamlPath}\" -o \"{output}\" --client-type {client} --no-strict", work, TimeSpan.FromMinutes(3));
                Assert.True(generate.ExitCode == 0, $"generate client-typescript ({client}) failed:\n{generate.Output}");
            }

            File.WriteAllText(Path.Combine(work, "package.json"), BodyMediaPackageJson);
            File.WriteAllText(Path.Combine(work, "tsconfig.json"), E2ETsConfig);
            File.WriteAllText(Path.Combine(work, "harness.mts"), GuardHarness);

            NodeWorkspace.Link(work);

            var result = new Dictionary<string, Dictionary<string, JsonObject>>(StringComparer.Ordinal);
            foreach (var client in (string[])["fetch", "axios"])
            {
                File.WriteAllText(Path.Combine(work, "cases.json"), new JsonObject { ["baseUrl"] = baseAddress.ToString().TrimEnd('/'), ["cases"] = GuardCasesJson(cases, "ts-" + client) }.ToJsonString());
                var run = RunTool("npx", $"tsx harness.mts {client}", work, TimeSpan.FromMinutes(5));

                // A harness that did not run is one red column, not the end of the matrix
                result["ts-" + client] = ReadHarnessResults(work, client, run.ExitCode, run.Output, cases.Select(c => c.Name));
            }

            return result;
        }
        finally
        {
            try
            {
                Directory.Delete(work, recursive: true);
            }
            catch (IOException)
            {
                // node_modules can stay locked for a moment; the temp folder is cleaned up by the OS.
            }
            catch (UnauthorizedAccessException)
            {
                // As above.
            }
        }
    }

    /// <summary>
    /// Accepts a bearer token that spells out the caller: <c>sub=alice;scopes=a,b;scope=a b;roles=admin</c>. Challenges
    /// with <c>WWW-Authenticate: Bearer</c>, as the JWT handler does.
    /// </summary>
    private sealed class GuardBearerHandler(
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

            var claims = new List<Claim>();
            foreach (var part in header["Bearer ".Length..].Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var separator = part.IndexOf('=', StringComparison.Ordinal);
                if (separator < 0)
                {
                    return Task.FromResult(AuthenticateResult.Fail("not a token"));
                }

                var (key, value) = (part[..separator], part[(separator + 1)..]);
                switch (key)
                {
                    case "sub":
                        claims.Add(new Claim("sub", value));
                        claims.Add(new Claim(ClaimTypes.NameIdentifier, value));
                        break;
                    case "scopes":
                        claims.AddRange(value.Split(',').Select(s => new Claim("scope", s)));
                        break;
                    case "scope":
                        claims.Add(new Claim("scope", value));
                        break;
                    case "roles":
                        claims.AddRange(value.Split(',').Select(r => new Claim(ClaimTypes.Role, r)));
                        break;
                    default:
                        return Task.FromResult(AuthenticateResult.Fail("not a token"));
                }
            }

            // As Microsoft.IdentityModel builds a JWT's identity: an authentication type that is not the scheme name
            var identity = new ClaimsIdentity(claims, "AuthenticationTypes.Federation");
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }

        protected override Task HandleChallengeAsync(
            AuthenticationProperties properties)
        {
            Response.StatusCode = 401;
            Response.Headers.WWWAuthenticate = "Bearer";
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Accepts one API key in <c>X-API-Key</c>; the identity's authentication type is the scheme name, as a
    /// hand-written key handler sets it.
    /// </summary>
    private sealed class GuardApiKeyHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-API-Key", out var key))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            if (!string.Equals(key.ToString(), GuardApiKey, StringComparison.Ordinal))
            {
                return Task.FromResult(AuthenticateResult.Fail("unknown key"));
            }

            var identity = new ClaimsIdentity([new Claim("sub", "key-holder"), new Claim(ClaimTypes.NameIdentifier, "key-holder")], Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }

        protected override Task HandleChallengeAsync(
            AuthenticationProperties properties)
        {
            Response.StatusCode = 401;
            Response.Headers.Append("WWW-Authenticate", "ApiKey");
            return Task.CompletedTask;
        }
    }
}