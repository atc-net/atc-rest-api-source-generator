// ReSharper disable StringLiteralTypo
namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// End-to-end feature matrix: the generated server runs on Kestrel with handlers that stream, page, echo and
/// version, and every generated client - the C# EndpointPerOperation and typed clients, the TypeScript Fetch
/// and Axios clients, the Fetch client with Zod runtime validation, and the React Query and SWR hooks rendered
/// with React - runs each feature against it over HTTP: Server-Sent Events, JSON Lines and a JSON array streamed
/// as <c>IAsyncEnumerable</c>, each to the end, with the handler failing mid-stream, and cancelled by the client
/// (the server must see the cancellation); a paginated read walked by its continuation; a <c>oneOf</c> with a
/// discriminator and a three-level <c>allOf</c> round-tripped through the client; an enum by its value and an
/// unknown value from the server; a date and a date-time round-tripped; a conditional 304; and a read the client
/// cancels (the server must see the cancellation).
/// </summary>
/// <remarks>
/// Not exercised: JSON Text Sequence and <c>multipart/mixed</c> framings; a browser runtime (the hooks render with
/// React on jsdom in Node); the React Query and SWR hooks on a 304 (a browser cache sends the condition, not a
/// hook); a cancelled SWR read (SWR hands its fetcher no signal); webhooks and output caching.
/// </remarks>
public partial class ErrorResponseBodyRuntimeTests
{
    private const string FeatureYaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /feature/stream/sse:
            get:
              operationId: featureSse
              x-return-async-enumerable: true
              parameters:
                - { name: count, in: query, required: true, schema: { type: integer, format: int32 } }
                - { name: failAfter, in: query, schema: { type: integer, format: int32 } }
              responses:
                '200': { description: Things, content: { text/event-stream: { schema: { $ref: '#/components/schemas/Thing' } } } }
          /feature/stream/lines:
            get:
              operationId: featureLines
              x-return-async-enumerable: true
              parameters:
                - { name: count, in: query, required: true, schema: { type: integer, format: int32 } }
                - { name: failAfter, in: query, schema: { type: integer, format: int32 } }
              responses:
                '200': { description: Things, content: { application/jsonl: { schema: { $ref: '#/components/schemas/Thing' } } } }
          /feature/stream/array:
            get:
              operationId: featureArray
              x-return-async-enumerable: true
              parameters:
                - { name: count, in: query, required: true, schema: { type: integer, format: int32 } }
                - { name: failAfter, in: query, schema: { type: integer, format: int32 } }
              responses:
                '200': { description: Things, content: { application/json: { schema: { type: array, items: { $ref: '#/components/schemas/Thing' } } } } }
          /feature/page:
            get:
              operationId: featurePage
              parameters:
                - { name: continuation, in: query, schema: { type: string } }
                - { name: pageSize, in: query, schema: { type: integer, format: int32 } }
              responses:
                '200':
                  description: A page
                  content:
                    application/json:
                      schema:
                        allOf:
                          - $ref: '#/components/schemas/PaginatedResult'
                          - type: object
                            properties:
                              results:
                                type: array
                                items:
                                  $ref: '#/components/schemas/Thing'
          /feature/shape:
            post:
              operationId: featureShape
              requestBody:
                required: true
                content:
                  application/json:
                    schema: { $ref: '#/components/schemas/Shape' }
              responses:
                '200': { description: The same shape, content: { application/json: { schema: { $ref: '#/components/schemas/Shape' } } } }
          /feature/nested:
            post:
              operationId: featureNested
              requestBody:
                required: true
                content:
                  application/json:
                    schema: { $ref: '#/components/schemas/Leaf' }
              responses:
                '200': { description: The same leaf, content: { application/json: { schema: { $ref: '#/components/schemas/Leaf' } } } }
          /feature/color:
            get:
              operationId: featureColor
              parameters:
                - { name: color, in: query, required: true, schema: { $ref: '#/components/schemas/Color' } }
              responses:
                '200': { description: The color, content: { application/json: { schema: { $ref: '#/components/schemas/Painted' } } } }
          /feature/dates:
            post:
              operationId: featureDates
              requestBody:
                required: true
                content:
                  application/json:
                    schema: { $ref: '#/components/schemas/Moment' }
              responses:
                '200': { description: The same moment, content: { application/json: { schema: { $ref: '#/components/schemas/Moment' } } } }
          /feature/etag:
            get:
              operationId: featureEtag
              parameters:
                - { name: If-None-Match, in: header, schema: { type: string } }
              responses:
                '200': { description: The current version, content: { application/json: { schema: { $ref: '#/components/schemas/Thing' } } } }
                '304': { description: Not Modified }
          /feature/slow:
            get:
              operationId: featureSlow
              responses:
                '200': { description: A thing after three seconds, content: { application/json: { schema: { $ref: '#/components/schemas/Thing' } } } }
        components:
          schemas:
            Thing:
              type: object
              required: [name]
              properties:
                name: { type: string }
            PaginatedResult:
              type: object
              properties:
                continuation: { type: string, nullable: true }
                results: { type: array, items: {} }
            Shape:
              oneOf:
                - $ref: '#/components/schemas/Circle'
                - $ref: '#/components/schemas/Square'
              discriminator:
                propertyName: kind
                mapping:
                  circle: '#/components/schemas/Circle'
                  square: '#/components/schemas/Square'
            Circle:
              type: object
              required: [kind, radius]
              properties:
                kind: { type: string }
                radius: { type: number, format: double }
            Square:
              type: object
              required: [kind, side]
              properties:
                kind: { type: string }
                side: { type: number, format: double }
            Base:
              type: object
              required: [id]
              properties:
                id: { type: string }
            Mid:
              allOf:
                - $ref: '#/components/schemas/Base'
                - type: object
                  required: [name]
                  properties:
                    name: { type: string }
            Leaf:
              allOf:
                - $ref: '#/components/schemas/Mid'
                - type: object
                  properties:
                    extra: { type: string }
            Color:
              type: string
              enum: [red, dark-blue]
            Painted:
              type: object
              required: [color]
              properties:
                color: { $ref: '#/components/schemas/Color' }
            Moment:
              type: object
              required: [day, at]
              properties:
                day: { type: string, format: date }
                at: { type: string, format: date-time }
        """;

    /// <summary>
    /// The handlers: a stream of <c>t1..tN</c> (endless for <c>count = 0</c>) that fails after <c>failAfter</c>
    /// items and records the probe when it is cancelled; two pages by continuation; echoes; a versioned thing.
    /// </summary>
    private const string FeatureHandlerSource = """"
        using System.Runtime.CompilerServices;
        using Microsoft.AspNetCore.Http;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public static class FeatureLog
        {
            public static System.Collections.Concurrent.ConcurrentDictionary<string, bool> Cancelled { get; } = new(StringComparer.Ordinal);

            public static string Probe(IHttpContextAccessor accessor)
                => accessor.HttpContext?.Request.Headers["X-Probe"].ToString() ?? string.Empty;

            public static async IAsyncEnumerable<Thing> Stream(int count, int? failAfter, string probe, [EnumeratorCancellation] System.Threading.CancellationToken cancellationToken)
            {
                try
                {
                    for (var i = 1; count == 0 || i <= count; i++)
                    {
                        if (failAfter == i - 1)
                        {
                            throw new InvalidOperationException("the handler failed after " + failAfter + " items");
                        }

                        yield return new Thing("t" + i);
                        await System.Threading.Tasks.Task.Delay(count == 0 ? 40 : 5, cancellationToken);
                    }
                }
                finally
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        Cancelled[probe] = true;
                    }
                }
            }
        }

        public sealed class FeatureSseHandlerImpl(IHttpContextAccessor accessor) : IFeatureSseHandler
        {
            public System.Threading.Tasks.Task<FeatureSseResult> ExecuteAsync(FeatureSseParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(FeatureSseResult.Ok(FeatureLog.Stream(parameters.Count, parameters.FailAfter, FeatureLog.Probe(accessor), cancellationToken)));
        }

        public sealed class FeatureLinesHandlerImpl(IHttpContextAccessor accessor) : IFeatureLinesHandler
        {
            public System.Threading.Tasks.Task<FeatureLinesResult> ExecuteAsync(FeatureLinesParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(FeatureLinesResult.Ok(FeatureLog.Stream(parameters.Count, parameters.FailAfter, FeatureLog.Probe(accessor), cancellationToken)));
        }

        public sealed class FeatureArrayHandlerImpl(IHttpContextAccessor accessor) : IFeatureArrayHandler
        {
            public System.Threading.Tasks.Task<FeatureArrayResult> ExecuteAsync(FeatureArrayParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(FeatureArrayResult.Ok(FeatureLog.Stream(parameters.Count, parameters.FailAfter, FeatureLog.Probe(accessor), cancellationToken)));
        }

        // Five things in pages of pageSize (3 by default): the continuation names the next page
        public sealed class FeaturePageHandlerImpl : IFeaturePageHandler
        {
            public System.Threading.Tasks.Task<FeaturePageResult> ExecuteAsync(FeaturePageParameters parameters, System.Threading.CancellationToken cancellationToken = default)
            {
                var size = parameters.PageSize ?? 3;
                var start = parameters.Continuation is { Length: > 0 } c ? int.Parse(c.Substring(1), System.Globalization.CultureInfo.InvariantCulture) : 1;
                var things = Enumerable.Range(start, Math.Max(0, Math.Min(size, 6 - start))).Select(i => new Thing("t" + i)).ToList();
                var next = start + size <= 5 ? "p" + (start + size) : null;
                return System.Threading.Tasks.Task.FromResult(FeaturePageResult.Ok(new PaginatedResult<Thing>(next, things)));
            }
        }

        public sealed class FeatureShapeHandlerImpl : IFeatureShapeHandler
        {
            public System.Threading.Tasks.Task<FeatureShapeResult> ExecuteAsync(FeatureShapeParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(FeatureShapeResult.Ok(parameters.Request));
        }

        public sealed class FeatureNestedHandlerImpl : IFeatureNestedHandler
        {
            public System.Threading.Tasks.Task<FeatureNestedResult> ExecuteAsync(FeatureNestedParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(FeatureNestedResult.Ok(parameters.Request));
        }

        public sealed class FeatureColorHandlerImpl : IFeatureColorHandler
        {
            public System.Threading.Tasks.Task<FeatureColorResult> ExecuteAsync(FeatureColorParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(FeatureColorResult.Ok(new Painted(parameters.Color)));
        }

        public sealed class FeatureDatesHandlerImpl : IFeatureDatesHandler
        {
            public System.Threading.Tasks.Task<FeatureDatesResult> ExecuteAsync(FeatureDatesParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(FeatureDatesResult.Ok(parameters.Request));
        }

        public sealed class FeatureEtagHandlerImpl(IHttpContextAccessor accessor) : IFeatureEtagHandler
        {
            public System.Threading.Tasks.Task<FeatureEtagResult> ExecuteAsync(FeatureEtagParameters parameters, System.Threading.CancellationToken cancellationToken = default)
            {
                if (parameters.IfNoneMatch == "\"v1\"")
                {
                    return System.Threading.Tasks.Task.FromResult(FeatureEtagResult.NotModified());
                }

                accessor.HttpContext!.Response.Headers.ETag = "\"v1\"";
                return System.Threading.Tasks.Task.FromResult(FeatureEtagResult.Ok(new Thing("v1")));
            }
        }

        // A read the server holds for three seconds: a client that cancels it shows up as a cancelled probe
        public sealed class FeatureSlowHandlerImpl(IHttpContextAccessor accessor) : IFeatureSlowHandler
        {
            public async System.Threading.Tasks.Task<FeatureSlowResult> ExecuteAsync(System.Threading.CancellationToken cancellationToken = default)
            {
                var probe = FeatureLog.Probe(accessor);
                try
                {
                    await System.Threading.Tasks.Task.Delay(3_000, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    FeatureLog.Cancelled[probe] = true;
                    throw;
                }

                return FeatureSlowResult.Ok(new Thing("slow"));
            }
        }

        public static class FeatureRegistration
        {
            public static void Register(IServiceCollection services)
            {
                services.AddScoped<IFeatureSlowHandler, FeatureSlowHandlerImpl>();
                services.AddHttpContextAccessor();
                services.AddScoped<IFeatureSseHandler, FeatureSseHandlerImpl>();
                services.AddScoped<IFeatureLinesHandler, FeatureLinesHandlerImpl>();
                services.AddScoped<IFeatureArrayHandler, FeatureArrayHandlerImpl>();
                services.AddScoped<IFeaturePageHandler, FeaturePageHandlerImpl>();
                services.AddScoped<IFeatureShapeHandler, FeatureShapeHandlerImpl>();
                services.AddScoped<IFeatureNestedHandler, FeatureNestedHandlerImpl>();
                services.AddScoped<IFeatureColorHandler, FeatureColorHandlerImpl>();
                services.AddScoped<IFeatureDatesHandler, FeatureDatesHandlerImpl>();
                services.AddScoped<IFeatureEtagHandler, FeatureEtagHandlerImpl>();
            }
        }
        """";

    /// <summary>
    /// Calls every case through a generated C# client (EndpointPerOperation or the typed client in its Result
    /// style) against the live host and records what the client exposes: the streamed names, the pages, the
    /// echoed value and its runtime type, the status, headers, or the exception.
    /// </summary>
    private const string FeatureProbeSource = """"
        using System.Net.Http;
        using System.Text.Json;
        using System.Text.Json.Nodes;
        using System.Text.Json.Serialization;
        using Atc.Rest.Client;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

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

        public static partial class FeatureProbe
        {
            private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

            public static async System.Threading.Tasks.Task<Dictionary<string, string>> RunAsync(string baseAddress, string casesJson, bool typed, string clientId)
            {
                var results = new Dictionary<string, string>(StringComparer.Ordinal);
                var services = new ServiceCollection();
                services.AddTransient<ProbeStampHandler>();
                var httpClientName = Register(services);
                services.AddHttpClient(httpClientName, c => c.BaseAddress = new Uri(baseAddress)).AddHttpMessageHandler<ProbeStampHandler>();

                using var provider = services.BuildServiceProvider();
                foreach (var item in JsonNode.Parse(casesJson)!.AsArray())
                {
                    var c = item!.AsObject();
                    var name = (string)c["name"]!;
                    var headers = c["headers"]!.AsObject().ToDictionary(h => h.Key, h => (string)h.Value!, StringComparer.Ordinal);
                    headers["X-Probe"] = clientId + "|" + name;
                    ProbeStampHandler.Current = headers;
                    try
                    {
                        results[name] = (await CallAsync(provider, httpClientName, c)).ToJsonString();
                    }
                    catch (Exception ex)
                    {
                        results[name] = new JsonObject { ["kind"] = "threw", ["error"] = ex.GetType().Name + ": " + Short(ex.Message) }.ToJsonString();
                    }
                }

                return results;
            }

            // The names streamed until the end, the handler's failure, or the client's own stop; the stop is what a
            // caller does - leave the loop and dispose the response.
            private static async System.Threading.Tasks.Task<JsonObject> StreamAsync(Func<System.Threading.CancellationToken, System.Threading.Tasks.Task<StreamingEndpointResponse<Thing>>> start, int? cancelAfter)
            {
                var names = new List<string>();
                string? error = null;
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(30));
                try
                {
                    using var response = await start(cts.Token);
                    if (!response.IsSuccess)
                    {
                        return Error((int)response.StatusCode, response.ErrorContent);
                    }

                    await foreach (var thing in response.Content!.WithCancellation(cts.Token))
                    {
                        names.Add(thing.Name);
                        if (cancelAfter is { } stop && names.Count >= stop)
                        {
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    error = ex.GetType().Name + ": " + Short(ex.Message);
                }

                return new JsonObject { ["kind"] = "items", ["items"] = new JsonArray([.. names.Select(n => (JsonNode)n)]), ["error"] = error };
            }

            // Starts the slow read, cancels it once the server holds it, and records how the call ended.
            private static async System.Threading.Tasks.Task<JsonObject> CancelAsync(Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> call, int cancelAfterMs)
            {
                using var cts = new System.Threading.CancellationTokenSource(cancelAfterMs);
                try
                {
                    await call(cts.Token);
                    return new JsonObject { ["kind"] = "completed" };
                }
                catch (OperationCanceledException)
                {
                    return new JsonObject { ["kind"] = "cancelled" };
                }
            }

            private static JsonObject Value(object? value)
                => new()
                {
                    ["kind"] = "value",
                    ["type"] = value?.GetType().Name,
                    ["value"] = value is null ? null : JsonNode.Parse(JsonSerializer.Serialize(value, value.GetType(), Json)),
                };

            private static JsonObject Error(int status, string? content)
                => new() { ["kind"] = "error", ["status"] = status, ["error"] = Short(content ?? string.Empty) };

            private static string? Header(IReadOnlyDictionary<string, IEnumerable<string>>? headers, string name)
            {
                if (headers is null)
                {
                    return null;
                }

                foreach (var header in headers)
                {
                    if (string.Equals(header.Key, name, StringComparison.OrdinalIgnoreCase))
                    {
                        return string.Join(",", header.Value);
                    }
                }

                return null;
            }

            private static string Short(string text)
                => text.Length > 300 ? text[..300] + "..." : text;
        }
        """";

    /// <summary>
    /// The EndpointPerOperation side of the probe: one generated endpoint per operation.
    /// </summary>
    private const string FeatureProbeEndpointSource = """"
        using System.Net.Http;
        using System.Text.Json;
        using System.Text.Json.Nodes;
        using Atc.Rest.Client;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public static partial class FeatureProbe
        {
            private static string Register(ServiceCollection services)
            {
                services.AddErrorBodiesEndpoints();
                return Constants.HttpClientName;
            }

            private static async System.Threading.Tasks.Task<JsonObject> CallAsync(ServiceProvider provider, string httpClientName, JsonObject c)
            {
                var kind = (string)c["kind"]!;
                var operation = (string)c["operation"]!;
                switch (kind)
                {
                    case "stream":
                    {
                        var count = (int)c["count"]!;
                        int? failAfter = c["failAfter"] is { } f ? (int)f : null;
                        return await StreamAsync(
                            operation switch
                            {
                                "FeatureSse" => ct => provider.GetRequiredService<IFeatureSseEndpoint>().ExecuteAsync(new FeatureSseParameters(count, failAfter), httpClientName, ct),
                                "FeatureLines" => ct => provider.GetRequiredService<IFeatureLinesEndpoint>().ExecuteAsync(new FeatureLinesParameters(count, failAfter), httpClientName, ct),
                                _ => ct => provider.GetRequiredService<IFeatureArrayEndpoint>().ExecuteAsync(new FeatureArrayParameters(count, failAfter), httpClientName, ct),
                            },
                            c["cancelAfter"] is { } ca ? (int)ca : null);
                    }

                    case "page":
                    {
                        var names = new List<string>();
                        var pages = 0;
                        string? continuation = null;
                        do
                        {
                            var r = await provider.GetRequiredService<IFeaturePageEndpoint>().ExecuteAsync(new FeaturePageParameters(continuation, (int)c["pageSize"]!), httpClientName);
                            if (!r.IsSuccess)
                            {
                                return Error((int)r.StatusCode, r.Content);
                            }

                            names.AddRange(r.OkContent.Results.Select(t => t.Name));
                            pages++;
                            continuation = r.OkContent.Continuation;
                        }
                        while (!string.IsNullOrEmpty(continuation) && pages < 10);

                        return new JsonObject { ["kind"] = "items", ["items"] = new JsonArray([.. names.Select(n => (JsonNode)n)]), ["pages"] = pages };
                    }

                    case "shape":
                    {
                        var r = await provider.GetRequiredService<IFeatureShapeEndpoint>().ExecuteAsync(new FeatureShapeParameters(JsonSerializer.Deserialize<Shape>(c["body"]!.ToJsonString(), Json)!), httpClientName);
                        return r.IsSuccess ? Value(r.OkContent) : Error((int)r.StatusCode, r.Content);
                    }

                    case "nested":
                    {
                        var r = await provider.GetRequiredService<IFeatureNestedEndpoint>().ExecuteAsync(new FeatureNestedParameters(JsonSerializer.Deserialize<Leaf>(c["body"]!.ToJsonString(), Json)!), httpClientName);
                        return r.IsSuccess ? Value(r.OkContent) : Error((int)r.StatusCode, r.Content);
                    }

                    case "color":
                    {
                        var r = await provider.GetRequiredService<IFeatureColorEndpoint>().ExecuteAsync(new FeatureColorParameters(JsonSerializer.Deserialize<Color>("\"" + (string)c["color"]! + "\"", Json)), httpClientName);
                        return r.IsSuccess ? Value(r.OkContent) : Error((int)r.StatusCode, r.Content);
                    }

                    case "dates":
                    {
                        var r = await provider.GetRequiredService<IFeatureDatesEndpoint>().ExecuteAsync(new FeatureDatesParameters(JsonSerializer.Deserialize<Moment>(c["body"]!.ToJsonString(), Json)!), httpClientName);
                        return r.IsSuccess ? Value(r.OkContent) : Error((int)r.StatusCode, r.Content);
                    }

                    case "etag":
                    {
                        var r = await provider.GetRequiredService<IFeatureEtagEndpoint>().ExecuteAsync(new FeatureEtagParameters((string?)c["ifNoneMatch"]), httpClientName);
                        var result = r.IsSuccess ? Value(r.OkContent) : new JsonObject { ["kind"] = "status" };
                        result["status"] = (int)r.StatusCode;
                        result["notModified"] = r.IsNotModified;
                        result["etag"] = Header(r.Headers, "ETag");
                        return result;
                    }

                    case "slow":
                        return await CancelAsync(ct => provider.GetRequiredService<IFeatureSlowEndpoint>().ExecuteAsync(httpClientName, ct), (int)c["cancelAfterMs"]!);

                    default:
                        throw new InvalidOperationException("unknown case kind " + kind);
                }
            }
        }
        """";

    /// <summary>
    /// The typed-client side of the probe (Result style): one client with a method per operation.
    /// </summary>
    private const string FeatureProbeTypedSource = """"
        using System.Net.Http;
        using System.Text.Json;
        using System.Text.Json.Nodes;
        using Atc.Rest.Client;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public static partial class FeatureProbe
        {
            private static string Register(ServiceCollection services)
                => "typed";

            private static async System.Threading.Tasks.Task<JsonObject> CallAsync(ServiceProvider provider, string httpClientName, JsonObject c)
            {
                var http = provider.GetRequiredService<IHttpClientFactory>().CreateClient(httpClientName);
                var client = new ErrorBodiesClient(http);
                var kind = (string)c["kind"]!;
                var operation = (string)c["operation"]!;
                switch (kind)
                {
                    case "stream":
                    {
                        var count = (int)c["count"]!;
                        int? failAfter = c["failAfter"] is { } f ? (int)f : null;
                        return await StreamAsync(
                            operation switch
                            {
                                "FeatureSse" => ct => client.FeatureSseAsync(new FeatureSseParameters(count, failAfter), ct),
                                "FeatureLines" => ct => client.FeatureLinesAsync(new FeatureLinesParameters(count, failAfter), ct),
                                _ => ct => client.FeatureArrayAsync(new FeatureArrayParameters(count, failAfter), ct),
                            },
                            c["cancelAfter"] is { } ca ? (int)ca : null);
                    }

                    case "page":
                    {
                        var names = new List<string>();
                        var pages = 0;
                        string? continuation = null;
                        do
                        {
                            var r = await client.FeaturePageAsync(new FeaturePageParameters(continuation, (int)c["pageSize"]!));
                            if (!r.IsSuccess)
                            {
                                return Error((int)r.StatusCode, r.Content);
                            }

                            names.AddRange(r.SuccessContent.Results.Select(t => t.Name));
                            pages++;
                            continuation = r.SuccessContent.Continuation;
                        }
                        while (!string.IsNullOrEmpty(continuation) && pages < 10);

                        return new JsonObject { ["kind"] = "items", ["items"] = new JsonArray([.. names.Select(n => (JsonNode)n)]), ["pages"] = pages };
                    }

                    case "shape":
                    {
                        var r = await client.FeatureShapeAsync(new FeatureShapeParameters(JsonSerializer.Deserialize<Shape>(c["body"]!.ToJsonString(), Json)!));
                        return r.IsSuccess ? Value(r.SuccessContent) : Error((int)r.StatusCode, r.Content);
                    }

                    case "nested":
                    {
                        var r = await client.FeatureNestedAsync(new FeatureNestedParameters(JsonSerializer.Deserialize<Leaf>(c["body"]!.ToJsonString(), Json)!));
                        return r.IsSuccess ? Value(r.SuccessContent) : Error((int)r.StatusCode, r.Content);
                    }

                    case "color":
                    {
                        var r = await client.FeatureColorAsync(new FeatureColorParameters(JsonSerializer.Deserialize<Color>("\"" + (string)c["color"]! + "\"", Json)));
                        return r.IsSuccess ? Value(r.SuccessContent) : Error((int)r.StatusCode, r.Content);
                    }

                    case "dates":
                    {
                        var r = await client.FeatureDatesAsync(new FeatureDatesParameters(JsonSerializer.Deserialize<Moment>(c["body"]!.ToJsonString(), Json)!));
                        return r.IsSuccess ? Value(r.SuccessContent) : Error((int)r.StatusCode, r.Content);
                    }

                    case "etag":
                    {
                        var r = await client.FeatureEtagAsync(new FeatureEtagParameters((string?)c["ifNoneMatch"]));
                        var result = r.IsSuccess ? Value(r.SuccessContent) : new JsonObject { ["kind"] = "status" };
                        result["status"] = (int)r.StatusCode;
                        result["notModified"] = r.StatusCode == System.Net.HttpStatusCode.NotModified;
                        result["etag"] = Header(r.Headers, "ETag");
                        return result;
                    }

                    case "slow":
                        return await CancelAsync(ct => client.FeatureSlowAsync(ct), (int)c["cancelAfterMs"]!);

                    default:
                        throw new InvalidOperationException("unknown case kind " + kind);
                }
            }
        }
        """";

    // The React Query and SWR hooks render with React on jsdom; Node's own fetch, streams and AbortController are
    // kept, since undici refuses jsdom's. Every case gets a fresh client/cache so nothing is served from memory.
    private const string FeatureHarness = """
        import { readFileSync, writeFileSync } from 'node:fs';

        const which = process.argv[2];
        const { baseUrl, cases } = JSON.parse(readFileSync('cases.json', 'utf8'));
        const hooks = which === 'fetch-reactquery' ? 'reactquery' : which === 'fetch-swr' ? 'swr' : null;

        let React: any, renderHook: any, waitFor: any, act: any, QueryClient: any, QueryClientProvider: any, SWRConfig: any;
        if (hooks) {
          const keep = { fetch, AbortController, AbortSignal, Headers, Request, Response, ReadableStream, TextDecoder, TextEncoder, FormData, Blob, File, URL, URLSearchParams, setTimeout, clearTimeout, setInterval, clearInterval, queueMicrotask, structuredClone };
          (await import('global-jsdom')).default(undefined, { url: baseUrl });
          Object.assign(globalThis, keep);
          (globalThis as any).IS_REACT_ACT_ENVIRONMENT = true;
          React = await import('react');
          ({ renderHook, waitFor, act } = await import('@testing-library/react'));
          if (hooks === 'reactquery') ({ QueryClient, QueryClientProvider } = await import('@tanstack/react-query'));
          if (hooks === 'swr') ({ SWRConfig } = await import('swr'));
        }

        const { ApiService } = await import(`./${which}/client/ApiService.ts`);
        const hookModule: any = hooks ? await import(`./${which}/hooks/index.ts`) : null;
        const names = (items: any[]) => items.map((t) => t.name);
        const WAIT = { timeout: 20000, interval: 25 };

        function wrapperFor(headers: Record<string, string>) {
          const { ApiProvider } = hookModule;
          const queryClient = hooks === 'reactquery' ? new QueryClient({ defaultOptions: { queries: { retry: false } } }) : null;
          return ({ children }: any) => {
            const inner = React.createElement(ApiProvider, { baseUrl, options: { defaultHeaders: headers } }, children);
            if (hooks === 'reactquery') return React.createElement(QueryClientProvider, { client: queryClient }, inner);
            return React.createElement(SWRConfig, { value: { provider: () => new Map(), dedupingInterval: 0 } }, inner);
          };
        }

        async function viaClient(api: any, c: any): Promise<Record<string, unknown>> {
          const f = api.feature;
          const m = c.operation.charAt(0).toLowerCase() + c.operation.slice(1);
          switch (c.kind) {
            case 'stream': {
              const got: string[] = []; let error: string | null = null;
              const controller = new AbortController();
              try {
                for await (const t of f[m]({ count: c.count, failAfter: c.failAfter ?? undefined }, controller.signal)) {
                  got.push(t.name);
                  if (c.cancelAfter && got.length >= c.cancelAfter) { controller.abort(); break; }
                }
              } catch (e: any) { if (!(c.cancelAfter && e?.name === 'AbortError')) error = `${e?.name ?? 'Error'}: ${String(e?.message ?? e).slice(0, 300)}`; }
              return { kind: 'items', items: got, error };
            }
            case 'page': {
              const got: string[] = []; let pages = 0; let continuation: string | undefined;
              do {
                const r = await f.featurePage({ continuation, pageSize: c.pageSize });
                if (r.status !== 'ok') return errorOf(r);
                got.push(...names(r.data.results)); pages++; continuation = r.data.continuation || undefined;
              } while (continuation && pages < 10);
              return { kind: 'items', items: got, pages };
            }
            case 'shape': return valueOf(await f.featureShape(c.body));
            case 'nested': return valueOf(await f.featureNested(c.body));
            case 'color': return valueOf(await f.featureColor({ color: c.color }));
            case 'dates': return valueOf(await f.featureDates(c.body));
            case 'etag': {
              const r = await f.featureEtag(c.ifNoneMatch ? { 'If-None-Match': c.ifNoneMatch } : undefined);
              const response = r.response ?? r.error?.response;
              const h = response?.headers;
              return { ...(r.status === 'ok' ? valueOf(r) : { kind: 'status', arm: r.status }), status: response?.status ?? (r.status === 'ok' ? 200 : 0), notModified: r.status === 'notModified', etag: h?.get ? h.get('etag') : h?.etag ?? null };
            }
            case 'slow': {
              const controller = new AbortController();
              const timer = setTimeout(() => controller.abort(), c.cancelAfterMs);
              try {
                const r = await f.featureSlow(controller.signal);
                return { kind: 'completed', arm: r.status };
              } catch (e: any) {
                return e?.name === 'AbortError' || e?.name === 'CanceledError' ? { kind: 'cancelled' } : { kind: 'threw', error: `${e?.name ?? 'Error'}: ${String(e?.message ?? e).slice(0, 300)}` };
              } finally {
                clearTimeout(timer);
              }
            }
          }
          throw new Error('unknown kind ' + c.kind);
        }

        async function viaHooks(c: any): Promise<Record<string, unknown>> {
          const wrapper = wrapperFor({ ...c.headers, 'X-Probe': `ts-${which}|${c.name}` });
          const H = hookModule;
          switch (c.kind) {
            case 'stream': {
              const hook = H['use' + c.operation + 'Stream'];
              const { result } = renderHook(() => hook({ count: c.count, failAfter: c.failAfter ?? undefined }), { wrapper });
              if (c.cancelAfter) {
                await waitFor(() => { if (result.current.items.length < c.cancelAfter) throw new Error('waiting'); }, WAIT);
                await act(async () => { result.current.cancel(); });
                return { kind: 'items', items: names(result.current.items.slice(0, c.cancelAfter)), error: null };
              }
              await waitFor(() => { if (result.current.status !== 'success' && result.current.status !== 'error') throw new Error('waiting ' + result.current.status); }, WAIT);
              return { kind: 'items', items: names(result.current.items), error: result.current.error ? String(result.current.error.message ?? result.current.error).slice(0, 300) : null };
            }
            case 'page': {
              const { result } = renderHook(() => H.useFeaturePageInfinite({ pageSize: c.pageSize }), { wrapper });
              if (hooks === 'reactquery') {
                await waitFor(() => { if (!result.current.isSuccess) throw new Error('waiting'); }, WAIT);
                while (result.current.hasNextPage) { await act(async () => { await result.current.fetchNextPage(); }); }
                const pages = result.current.data.pages;
                return { kind: 'items', items: pages.flatMap((p: any) => names(p.results)), pages: pages.length };
              }
              await waitFor(() => { if (!result.current.data) throw new Error('waiting'); }, WAIT);
              for (let guard = 0; guard < 10; guard++) {
                const pages = result.current.data as any[];
                const last = pages[pages.length - 1];
                if (!last?.continuation) break;
                await act(async () => { await result.current.setSize(pages.length + 1); });
                await waitFor(() => { if ((result.current.data as any[]).length < pages.length + 1) throw new Error('waiting'); }, WAIT);
              }
              const pages = result.current.data as any[];
              return { kind: 'items', items: pages.flatMap((p: any) => names(p.results)), pages: pages.length };
            }
            case 'shape': case 'nested': case 'dates': {
              const hook = H['use' + c.operation];
              const { result } = renderHook(() => hook(), { wrapper });
              let data: any;
              await act(async () => { data = hooks === 'reactquery' ? await result.current.mutateAsync(c.body) : await result.current.trigger(c.body); });
              return { kind: 'value', value: data };
            }
            case 'color': {
              const { result } = renderHook(() => H.useFeatureColor({ color: c.color }), { wrapper });
              await waitFor(() => { if (result.current.data === undefined && !result.current.error) throw new Error('waiting'); }, WAIT);
              return result.current.error ? { kind: 'error', status: result.current.error.status ?? 0, error: String(result.current.error.message).slice(0, 300) } : { kind: 'value', value: result.current.data };
            }
            case 'etag': {
              if (c.ifNoneMatch) return { kind: 'na', why: 'a browser cache sends the condition, not a hook' };
              const { result } = renderHook(() => H.useFeatureEtag(), { wrapper });
              await waitFor(() => { if (result.current.data === undefined && !result.current.error) throw new Error('waiting'); }, WAIT);
              return { kind: 'value', value: result.current.data, status: 200, notModified: false, etag: null };
            }
            case 'slow': {
              // React Query cancels a query nobody observes any more, when its queryFn took the signal
              if (hooks === 'swr') return { kind: 'na', why: 'SWR hands its fetcher no AbortSignal' };
              const { result, unmount } = renderHook(() => H.useFeatureSlow(), { wrapper });
              await new Promise((resolve) => setTimeout(resolve, c.cancelAfterMs));
              const completed = result.current.data !== undefined;
              unmount();
              // Stay until a request that was not cancelled has ended, so the harness exiting (which closes the
              // connection) is not mistaken for the hook cancelling it
              await new Promise((resolve) => setTimeout(resolve, 3500));
              return { kind: completed ? 'completed' : 'cancelled' };
            }
          }
          throw new Error('unknown kind ' + c.kind);
        }

        const errorOf = (r: any) => ({ kind: r.status === 'schemaMismatch' ? 'schemaMismatch' : 'error', status: r.error?.status ?? r.response?.status ?? 0, error: (r.status + ': ' + (r.issues ? JSON.stringify(r.issues) : JSON.stringify(r.error?.body ?? r.error?.message ?? null))).slice(0, 300), value: r.data ?? null });
        const valueOf = (r: any) => r.status === 'ok' ? { kind: 'value', value: r.data } : errorOf(r);

        const results: Record<string, unknown>[] = [];
        for (const c of cases) {
          let record: Record<string, unknown> = { name: c.name };
          try {
            if (hooks) {
              record = { ...record, ...(await viaHooks(c)) };
            } else {
              const api: any = new ApiService(baseUrl, { defaultHeaders: { ...c.headers, 'X-Probe': `ts-${which}|${c.name}` } });
              record = { ...record, ...(await viaClient(api, c)) };
            }
          } catch (err: any) {
            record = { ...record, kind: 'threw', error: `${err?.name ?? 'Error'}: ${String(err?.message ?? err).slice(0, 300)}` };
          }
          results.push(record);
        }

        writeFileSync(`results-${which}.json`, JSON.stringify(results));
        process.exit(0);
        """;

    private const string FeaturePackageJson = """
        {
          "name": "atc-feature-e2e",
          "version": "0.0.0",
          "private": true,
          "type": "module",
          "dependencies": {
            "@tanstack/react-query": "^5.81.0",
            "axios": "^1.7.0",
            "react": "^18.3.0",
            "react-dom": "^18.3.0",
            "swr": "^2.2.5",
            "zod": "^3.23.0"
          },
          "devDependencies": {
            "@testing-library/react": "^16.0.0",
            "@types/react": "^18.3.0",
            "global-jsdom": "^25.0.0",
            "jsdom": "^25.0.0",
            "tsx": "^4.19.0",
            "typescript": "^5.6.0"
          }
        }
        """;

    internal sealed record FeatureCase(
        string Name,
        string Group,
        string Operation,
        string Kind,
        JsonObject Arguments,
        (string Name, string Value)[]? Headers = null);

    /// <summary>
    /// The cases: the feature, the operation, the arguments every client is given, and (in <see cref="FeatureVerdict"/>)
    /// what the client must expose for them.
    /// </summary>
    internal static List<FeatureCase> FeatureCases()
    {
        var cases = new List<FeatureCase>();
        foreach (var (operation, label) in ((string, string)[])[("FeatureSse", "Server-Sent Events"), ("FeatureLines", "JSON Lines"), ("FeatureArray", "a JSON array as IAsyncEnumerable")])
        {
            cases.Add(new($"stream: {label}, five items", "streaming", operation, "stream", new JsonObject { ["count"] = 5 }));
            cases.Add(new($"stream: {label}, the handler fails after two items", "streaming", operation, "stream", new JsonObject { ["count"] = 5, ["failAfter"] = 2 }));
            cases.Add(new($"stream: {label}, cancelled by the client after two items", "streaming", operation, "stream", new JsonObject { ["count"] = 0, ["cancelAfter"] = 2 }));
        }

        cases.Add(new("pagination: five things in pages of three, by continuation", "pagination", "FeaturePage", "page", new JsonObject { ["pageSize"] = 3 }));
        cases.Add(new("oneOf: a circle round-trips through the discriminator", "polymorphism", "FeatureShape", "shape", new JsonObject { ["body"] = new JsonObject { ["kind"] = "circle", ["radius"] = 2.5 } }));
        cases.Add(new("oneOf: a square round-trips through the discriminator", "polymorphism", "FeatureShape", "shape", new JsonObject { ["body"] = new JsonObject { ["kind"] = "square", ["side"] = 4 } }));
        cases.Add(new("allOf: three levels round-trip with every property", "polymorphism", "FeatureNested", "nested", new JsonObject { ["body"] = new JsonObject { ["id"] = "leaf-1", ["name"] = "Leaf", ["extra"] = "more" } }));
        cases.Add(new("enum: a value with a hyphen round-trips", "values", "FeatureColor", "color", new JsonObject { ["color"] = "dark-blue" }));
        cases.Add(new("enum: a value the client does not know, from the server", "values", "FeatureColor", "color", new JsonObject { ["color"] = "red", ["unknown"] = true }, [("X-Feature-Stub", "unknown-color")]));
        cases.Add(new("dates: a date and a date-time round-trip", "values", "FeatureDates", "dates", new JsonObject { ["body"] = new JsonObject { ["day"] = "2026-03-04", ["at"] = "2026-03-04T05:06:07+02:00" } }));
        cases.Add(new("conditional: If-None-Match matches, 304", "conditional", "FeatureEtag", "etag", new JsonObject { ["ifNoneMatch"] = "\"v1\"" }));
        cases.Add(new("conditional: no condition, 200 with an ETag", "conditional", "FeatureEtag", "etag", new JsonObject()));
        cases.Add(new("cancellation: a read the client cancels stops on the server", "cancellation", "FeatureSlow", "slow", new JsonObject { ["cancelAfterMs"] = 500 }));
        return cases;
    }

    internal static readonly (string Id, string Group, string Label, string Marker, bool Typed, string? TsFlavour)[] FeatureClients =
    [
        ("cs-epo", "C# EndpointPerOperation", "errorResponseFormat: ProblemDetails", """{ "generationMode": "EndpointPerOperation", "errorResponseFormat": "ProblemDetails" }""", false, null),
        ("cs-epo-classic-enums", "C# EndpointPerOperation", "extensibleEnums: false (the classic enum)", """{ "generationMode": "EndpointPerOperation", "errorResponseFormat": "ProblemDetails", "extensibleEnums": false }""", false, null),
        ("cs-typed", "C# TypedClient", "typedClientResultStyle: Result", """{ "generationMode": "TypedClient", "typedClientResultStyle": "Result" }""", true, null),
        ("ts-fetch", "TypeScript", "Fetch client", string.Empty, false, "fetch"),
        ("ts-axios", "TypeScript", "Axios client", string.Empty, false, "axios"),
        ("ts-fetch-zod", "TypeScript", "Fetch client, Zod runtime validation", string.Empty, false, "fetch-zod"),
        ("ts-fetch-reactquery", "TypeScript", "React Query hooks (rendered with React on jsdom)", string.Empty, false, "fetch-reactquery"),
        ("ts-fetch-swr", "TypeScript", "SWR hooks (rendered with React on jsdom)", string.Empty, false, "fetch-swr"),
    ];

    /// <summary>
    /// The cells that fail today, as <c>case [client]</c>: a cell not listed here must pass, and a listed cell that
    /// passes fails the test asking to be removed. The matrix started with 124 red (a discriminated union whose
    /// variants declare the discriminator could not start the server; a three-level allOf lost its base; the typed
    /// client wrote PascalCase; Zod refused a date-time with an offset; a C# client threw on an enum value the server
    /// added later, fixed by the extensible enums) - all fixed.
    /// </summary>
    private static readonly HashSet<string> KnownFeatureFailures = new(StringComparer.Ordinal)
    {
    };

    /// <summary>
    /// Every generated client runs every feature against the generated server as the specification and the
    /// documented contract say: streams arrive whole, a mid-stream failure surfaces, a client's cancellation
    /// reaches the server, pages chain, polymorphic and inherited models round-trip with every property, enum
    /// and date values keep their wire form, and a 304 is a 304 (no <c>fail</c> outside <see cref="KnownFeatureFailures"/>).
    /// </summary>
    [Fact]
    [Trait("Category", "TypeScriptToolchain")]
    public async Task EndToEnd_EveryClientRunsEveryFeature()
    {
        if (!IsNodeAvailable(out var reason))
        {
            if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Fail(reason + " (running under CI where the feature matrix is required)");
            }

            Assert.Skip(reason);
        }

        var matrix = await CollectFeatureMatrixAsync();
        if (Environment.GetEnvironmentVariable("FEATURE_E2E_JSON") is { Length: > 0 } dumpPath)
        {
            await File.WriteAllTextAsync(dumpPath, matrix.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), TestContext.Current.CancellationToken);
        }

        var failures = RequestBindingFailingCells(matrix);
        var unexpected = failures.Where(f => !KnownFeatureFailures.Contains(f.Key)).ToList();
        var fixedCells = KnownFeatureFailures.Where(k => failures.All(f => f.Key != k)).Order(StringComparer.Ordinal).ToList();

        Assert.True(
            unexpected.Count == 0,
            $"{unexpected.Count} failing cells:\n" + string.Join("\n", unexpected.Take(120).Select(f => $"{f.Key}: {f.Why}")));
        Assert.True(
            fixedCells.Count == 0,
            $"{fixedCells.Count} cells pass now. Remove them from {nameof(KnownFeatureFailures)}:\n" + string.Join("\n", fixedCells));
    }

    /// <summary>
    /// The verdict is not blind: a short stream, a failure that does not surface, a cancellation the server did
    /// not see, a lost page, a changed echo, a lost base property, a date that became a date-time and a 304 that
    /// is not one each fail; the documented outcome passes.
    /// </summary>
    [Fact]
    public void FeatureVerdicts_FailALostItemValueOrStatus()
    {
        var cases = FeatureCases();
        var client = FeatureClients[0];
        JsonObject Items(
            string? error,
            params string[] names)
            => new() { ["kind"] = "items", ["items"] = new JsonArray([.. names.Select(n => (JsonNode)n)]), ["error"] = error };

        var whole = cases.Single(c => c.Name == "stream: Server-Sent Events, five items");
        Assert.Equal(VerdictPass, (string?)FeatureVerdict(whole, client, Items(null, "t1", "t2", "t3", "t4", "t5"), cancelled: false)["verdict"]);
        Assert.Equal(VerdictFail, (string?)FeatureVerdict(whole, client, Items(null, "t1", "t2", "t3", "t4"), cancelled: false)["verdict"]);
        Assert.Equal(VerdictFail, (string?)FeatureVerdict(whole, client, Items("IOException: reset", "t1", "t2", "t3", "t4", "t5"), cancelled: false)["verdict"]);

        var failing = cases.Single(c => c.Name == "stream: Server-Sent Events, the handler fails after two items");
        Assert.Equal(VerdictPass, (string?)FeatureVerdict(failing, client, Items("HttpIOException: ended prematurely", "t1", "t2"), cancelled: false)["verdict"]);
        Assert.Equal(VerdictFail, (string?)FeatureVerdict(failing, client, Items(null, "t1", "t2"), cancelled: false)["verdict"]);
        Assert.Equal(VerdictFail, (string?)FeatureVerdict(failing, client, Items("boom", "t1"), cancelled: false)["verdict"]);

        var cancelled = cases.Single(c => c.Name == "stream: Server-Sent Events, cancelled by the client after two items");
        Assert.Equal(VerdictPass, (string?)FeatureVerdict(cancelled, client, Items(null, "t1", "t2"), cancelled: true)["verdict"]);
        Assert.Equal(VerdictFail, (string?)FeatureVerdict(cancelled, client, Items(null, "t1", "t2"), cancelled: false)["verdict"]);

        var page = cases.Single(c => c.Kind == "page");
        var pages = Items(null, "t1", "t2", "t3", "t4", "t5");
        pages["pages"] = 2;
        Assert.Equal(VerdictPass, (string?)FeatureVerdict(page, client, pages, cancelled: false)["verdict"]);
        pages["pages"] = 1;
        Assert.Equal(VerdictFail, (string?)FeatureVerdict(page, client, pages, cancelled: false)["verdict"]);

        JsonObject Value(
            string? type,
            string json)
            => new() { ["kind"] = "value", ["type"] = type, ["value"] = JsonNode.Parse(json) };
        var circle = cases.Single(c => c.Name.StartsWith("oneOf: a circle", StringComparison.Ordinal));
        Assert.Equal(VerdictPass, (string?)FeatureVerdict(circle, client, Value("Circle", """{"kind":"circle","radius":2.5}"""), cancelled: false)["verdict"]);
        Assert.Equal(VerdictFail, (string?)FeatureVerdict(circle, client, Value("Shape", """{"kind":"circle","radius":2.5}"""), cancelled: false)["verdict"]);
        Assert.Equal(VerdictFail, (string?)FeatureVerdict(circle, client, Value("Circle", """{"kind":"circle","radius":3}"""), cancelled: false)["verdict"]);

        var nested = cases.Single(c => c.Kind == "nested");
        Assert.Equal(VerdictPass, (string?)FeatureVerdict(nested, client, Value("Leaf", """{"id":"leaf-1","name":"Leaf","extra":"more"}"""), cancelled: false)["verdict"]);
        Assert.Equal(VerdictFail, (string?)FeatureVerdict(nested, client, Value("Leaf", """{"extra":"more"}"""), cancelled: false)["verdict"]);

        var dates = cases.Single(c => c.Kind == "dates");
        Assert.Equal(VerdictPass, (string?)FeatureVerdict(dates, client, Value("Moment", """{"day":"2026-03-04","at":"2026-03-04T03:06:07+00:00"}"""), cancelled: false)["verdict"]);
        Assert.Equal(VerdictFail, (string?)FeatureVerdict(dates, client, Value("Moment", """{"day":"2026-03-04T00:00:00+00:00","at":"2026-03-04T03:06:07+00:00"}"""), cancelled: false)["verdict"]);
        Assert.Equal(VerdictFail, (string?)FeatureVerdict(dates, client, Value("Moment", """{"day":"2026-03-04","at":"2026-03-04T05:06:07+00:00"}"""), cancelled: false)["verdict"]);

        var unknown = cases.Single(c => c.Name.StartsWith("enum: a value the client does not know", StringComparison.Ordinal));
        var tsFetch = FeatureClients.Single(c => c.Id == "ts-fetch");
        var tsZod = FeatureClients.Single(c => c.Id == "ts-fetch-zod");
        Assert.Equal(VerdictPass, (string?)FeatureVerdict(unknown, tsFetch, Value(null, """{"color":"purple"}"""), cancelled: false)["verdict"]);
        Assert.Equal(VerdictPass, (string?)FeatureVerdict(unknown, tsZod, Value(null, """{"color":"purple"}"""), cancelled: false)["verdict"]);
        Assert.Equal(VerdictFail, (string?)FeatureVerdict(unknown, tsZod, new JsonObject { ["kind"] = "schemaMismatch", ["error"] = "issues" }, cancelled: false)["verdict"]);
        Assert.Equal(VerdictFail, (string?)FeatureVerdict(unknown, client, new JsonObject { ["kind"] = "threw", ["error"] = "JsonException: purple" }, cancelled: false)["verdict"]);

        // Opted out (extensibleEnums: false): the classic enum refuses the value, as the option documents; the value
        // getting through would mean the option did nothing
        var classic = FeatureClients.Single(c => c.Id == "cs-epo-classic-enums");
        Assert.Equal(VerdictPass, (string?)FeatureVerdict(unknown, classic, new JsonObject { ["kind"] = "threw", ["error"] = "JsonException: purple" }, cancelled: false)["verdict"]);
        Assert.Equal(VerdictPass, (string?)FeatureVerdict(unknown, classic, new JsonObject { ["kind"] = "threw", ["error"] = "RestClientDeserializationException: Failed to deserialize response content to Painted for status code 200 (OK)" }, cancelled: false)["verdict"]);
        Assert.Equal(VerdictFail, (string?)FeatureVerdict(unknown, classic, Value("Painted", """{"color":"purple"}"""), cancelled: false)["verdict"]);
        Assert.Equal(VerdictFail, (string?)FeatureVerdict(unknown, classic, new JsonObject { ["kind"] = "threw", ["error"] = "HttpRequestException: refused" }, cancelled: false)["verdict"]);

        var notModified = cases.Single(c => c.Name.StartsWith("conditional: If-None-Match", StringComparison.Ordinal));
        Assert.Equal(VerdictPass, (string?)FeatureVerdict(notModified, client, new JsonObject { ["kind"] = "status", ["status"] = 304, ["notModified"] = true }, cancelled: false)["verdict"]);
        Assert.Equal(VerdictFail, (string?)FeatureVerdict(notModified, client, new JsonObject { ["kind"] = "status", ["status"] = 304, ["notModified"] = false }, cancelled: false)["verdict"]);
        var current = cases.Single(c => c.Name.StartsWith("conditional: no condition", StringComparison.Ordinal));
        var ok = Value("Thing", """{"name":"v1"}""");
        ok["status"] = 200;
        ok["etag"] = "\"v1\"";
        Assert.Equal(VerdictPass, (string?)FeatureVerdict(current, client, ok, cancelled: false)["verdict"]);
        ok["etag"] = null;
        Assert.Equal(VerdictFail, (string?)FeatureVerdict(current, client, ok, cancelled: false)["verdict"]);

        var slow = cases.Single(c => c.Kind == "slow");
        Assert.Equal(VerdictPass, (string?)FeatureVerdict(slow, client, new JsonObject { ["kind"] = "cancelled" }, cancelled: true)["verdict"]);
        Assert.Equal(VerdictFail, (string?)FeatureVerdict(slow, client, new JsonObject { ["kind"] = "cancelled" }, cancelled: false)["verdict"]);
        Assert.Equal(VerdictFail, (string?)FeatureVerdict(slow, client, new JsonObject { ["kind"] = "completed" }, cancelled: false)["verdict"]);
        Assert.Equal(VerdictNotApplicable, (string?)FeatureVerdict(slow, FeatureClients.Single(c => c.Id == "ts-fetch-swr"), new JsonObject { ["kind"] = "na", ["why"] = "SWR" }, cancelled: false)["verdict"]);
    }

    /// <summary>
    /// A cell's verdict: what the client exposed against what the case's feature promises.
    /// </summary>
    private static JsonObject FeatureVerdict(
        FeatureCase featureCase,
        (string Id, string Group, string Label, string Marker, bool Typed, string? TsFlavour) client,
        JsonObject? observed,
        bool cancelled)
    {
        if (observed is null)
        {
            return Cell(VerdictFail, "no result recorded for the case", "-");
        }

        var kind = (string?)observed["kind"];
        var isHooks = client.Id.EndsWith("reactquery", StringComparison.Ordinal) || client.Id.EndsWith("swr", StringComparison.Ordinal);
        var isCSharp = client.Id.StartsWith("cs-", StringComparison.Ordinal);
        if (kind == "na")
        {
            return Cell(VerdictNotApplicable, (string?)observed["why"] ?? "not applicable", "-");
        }

        var arguments = featureCase.Arguments;
        switch (featureCase.Kind)
        {
            case "stream":
            {
                if (kind != "items")
                {
                    return Cell(VerdictFail, $"{kind}: {(string?)observed["error"]}", "-");
                }

                var items = observed["items"]!.AsArray().Select(i => (string)i!).ToList();
                var error = (string?)observed["error"];
                var summary = $"{items.Count} items{(error is null ? string.Empty : ", then " + error)}";
                var expectedCount = arguments["failAfter"] is { } fa ? (int)fa : arguments["cancelAfter"] is { } ca ? (int)ca : (int)arguments["count"]!;
                var expected = Enumerable.Range(1, expectedCount).Select(i => "t" + i).ToList();
                if (!items.SequenceEqual(expected, StringComparer.Ordinal))
                {
                    return Cell(VerdictFail, $"items [{string.Join(",", items)}], expected [{string.Join(",", expected)}]", summary);
                }

                if (arguments["failAfter"] is not null)
                {
                    return error is null
                        ? Cell(VerdictFail, "the handler's failure did not surface: the stream looked complete", summary)
                        : Cell(VerdictPass, "the items before the failure, then an error", summary);
                }

                if (arguments["cancelAfter"] is not null)
                {
                    if (error is not null)
                    {
                        return Cell(VerdictFail, "the client's own stop surfaced as an error: " + error, summary);
                    }

                    return cancelled
                        ? Cell(VerdictPass, "the client stopped and the server saw the cancellation", summary)
                        : Cell(VerdictFail, "the server never saw the cancellation: the stream kept running", summary);
                }

                return error is null ? Cell(VerdictPass, "every item, to the end", summary) : Cell(VerdictFail, "an error after a complete stream: " + error, summary);
            }

            case "page":
            {
                if (kind != "items")
                {
                    return Cell(VerdictFail, $"{kind}: {(string?)observed["error"]}", "-");
                }

                var items = observed["items"]!.AsArray().Select(i => (string)i!).ToList();
                var pages = (int?)observed["pages"] ?? 0;
                var summary = $"{items.Count} items in {pages} pages";
                return items.SequenceEqual(["t1", "t2", "t3", "t4", "t5"], StringComparer.Ordinal) && pages == 2
                    ? Cell(VerdictPass, "both pages, every item, by the continuation", summary)
                    : Cell(VerdictFail, $"items [{string.Join(",", items)}] in {pages} pages, expected t1..t5 in 2", summary);
            }

            case "shape":
            case "nested":
            case "dates":
            {
                if (kind != "value" || observed["value"] is not JsonObject value)
                {
                    return Cell(VerdictFail, $"{kind}: {(string?)observed["error"]}", "-");
                }

                var expected = arguments["body"]!.AsObject();
                var summary = value.ToJsonString();
                if (featureCase.Kind == "dates")
                {
                    var day = (string?)value["day"];
                    var at = (string?)value["at"];
                    if (day != "2026-03-04")
                    {
                        return Cell(VerdictFail, $"day '{day}', expected the date 2026-03-04 as a date", summary);
                    }

                    if (at is null || !DateTimeOffset.TryParse(at, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var instant) || instant.UtcDateTime != new DateTime(2026, 3, 4, 3, 6, 7, DateTimeKind.Utc))
                    {
                        return Cell(VerdictFail, $"at '{at}', expected the instant 2026-03-04T03:06:07Z", summary);
                    }

                    return Cell(VerdictPass, "the date as a date, the date-time as the same instant", summary);
                }

                foreach (var property in expected)
                {
                    if (!value.TryGetPropertyValue(property.Key, out var actual) || !JsonNode.DeepEquals(Normalize(actual), Normalize(property.Value)))
                    {
                        return Cell(VerdictFail, $"{property.Key}: '{actual?.ToJsonString() ?? "(missing)"}', expected '{property.Value?.ToJsonString()}'", summary);
                    }
                }

                if (featureCase.Kind == "shape" && isCSharp)
                {
                    var expectedType = (string)expected["kind"]! == "circle" ? "Circle" : "Square";
                    if ((string?)observed["type"] != expectedType)
                    {
                        return Cell(VerdictFail, $"the typed value is a {(string?)observed["type"]}, expected a {expectedType}", summary);
                    }
                }

                return Cell(VerdictPass, "the echo equals what was sent" + (featureCase.Kind == "shape" && isCSharp ? ", as the derived type" : string.Empty), summary);
            }

            case "color":
            {
                var unknown = arguments["unknown"] is not null;
                if (unknown && client.Marker.Contains("\"extensibleEnums\": false", StringComparison.Ordinal))
                {
                    // Opted out: the classic enum refuses a value it does not know, as the option documents - a
                    // JsonException, which the EndpointPerOperation client passes on as Atc.Rest.Client's
                    // RestClientDeserializationException
                    var error = (string?)observed["error"] ?? string.Empty;
                    return kind == "threw" && (error.StartsWith("JsonException", StringComparison.Ordinal) || error.StartsWith("RestClientDeserializationException", StringComparison.Ordinal))
                        ? Cell(VerdictPass, "the classic enum refuses the unknown value (extensibleEnums: false), as documented", "JsonException")
                        : Cell(VerdictFail, $"{kind}: expected the classic enum to refuse the unknown value, got {observed["value"]?.ToJsonString() ?? error}", "-");
                }

                if (unknown)
                {
                    // A value the client's enum does not have: every client hands it to the caller as it was sent (an
                    // extensible enum) - neither an exception out of the client nor a schema mismatch.
                    if (kind == "value" && (string?)observed["value"]?["color"] == "purple")
                    {
                        return Cell(VerdictPass, "the unknown value reaches the caller as it was sent", "purple");
                    }

                    return Cell(VerdictFail, $"{kind}: {(string?)observed["error"]}", "-");
                }

                return kind == "value" && (string?)observed["value"]?["color"] == (string)arguments["color"]!
                    ? Cell(VerdictPass, "the enum value as the specification writes it", (string)arguments["color"]!)
                    : Cell(VerdictFail, $"{kind}: {observed["value"]?.ToJsonString() ?? (string?)observed["error"]}", "-");
            }

            case "etag":
            {
                var status = (int?)observed["status"];
                if (arguments["ifNoneMatch"] is not null)
                {
                    return status == 304 && (bool?)observed["notModified"] == true
                        ? Cell(VerdictPass, "a 304, exposed as not modified", "304")
                        : Cell(VerdictFail, $"status {status}, notModified {observed["notModified"]}; expected a 304 exposed as not modified", $"{status}");
                }

                if (kind != "value" || (string?)observed["value"]?["name"] != "v1")
                {
                    return Cell(VerdictFail, $"{kind}: {(string?)observed["error"]}", $"{status}");
                }

                if (!isHooks && (string?)observed["etag"] != "\"v1\"")
                {
                    return Cell(VerdictFail, $"ETag '{(string?)observed["etag"] ?? "(none)"}' exposed, expected \"v1\"", $"{status}");
                }

                return Cell(VerdictPass, isHooks ? "the current version" : "the current version with its ETag", $"{status} ETag {(string?)observed["etag"]}");
            }

            case "slow":
            {
                if (kind == "completed")
                {
                    return Cell(VerdictFail, "the call ran to completion: the cancellation was not passed on", "completed");
                }

                if (kind != "cancelled")
                {
                    return Cell(VerdictFail, $"{kind}: {(string?)observed["error"]}", "-");
                }

                return cancelled
                    ? Cell(VerdictPass, "the client cancelled and the server saw the cancellation", "cancelled")
                    : Cell(VerdictFail, "the server never saw the cancellation: the request kept running", "cancelled");
            }

            default:
                return Cell(VerdictFail, "unknown case kind " + featureCase.Kind, "-");
        }

        static JsonNode? Normalize(JsonNode? node)
            => node is JsonValue v && v.TryGetValue<double>(out var d) ? JsonValue.Create(d) : node;
    }

    private static async Task<JsonObject> CollectFeatureMatrixAsync()
    {
        var started = DateTimeOffset.Now;
        var cases = FeatureCases();

        await using var host = await XmlTestHost.StartAsync(
            FeatureYaml,
            FeatureHandlerSource,
            "ErrorBodies.RuntimeTest.FeatureRegistration",
            app => app.Use(async (context, next) =>
            {
                // A value the clients' enum does not have, written by a stub in front of the generated endpoint
                if (context.Request.Headers.TryGetValue("X-Feature-Stub", out var stub) && stub == "unknown-color")
                {
                    context.Response.StatusCode = 200;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync("""{"color":"purple"}""", context.RequestAborted);
                    return;
                }

                await next(context);
            }));

        var casesJson = FeatureCasesJson(cases).ToJsonString();
        var rows = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        foreach (var client in FeatureClients.Where(c => c.Marker.Length > 0))
        {
            var sources = Generate(new ApiClientGenerator(), ".atc-rest-api-client", client.Marker, includeMinimalApi: false, FeatureYaml);
            var usings = sources
                .Select(s => Regex.Match(s.Source, @"^namespace (?<ns>[\w.]+);", RegexOptions.Multiline | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1)).Groups["ns"].Value)
                .Where(ns => ns.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .Select(ns => $"global using {ns};");
            var globalUsings = string.Join(Environment.NewLine, usings) + Environment.NewLine;
            sources.Add(("FeatureProbe.cs", globalUsings + FeatureProbeSource));
            sources.Add(("FeatureProbe.Mode.cs", globalUsings + (client.Typed ? FeatureProbeTypedSource : FeatureProbeEndpointSource)));
            var assembly = CompilationVerificationHarness.EmitAndLoad(sources);
            var run = (Task<Dictionary<string, string>>)assembly.GetType("ErrorBodies.RuntimeTest.FeatureProbe", throwOnError: true)!
                .GetMethod("RunAsync")!
                .Invoke(null, [host.BaseAddress.ToString(), casesJson, client.Typed, client.Id])!;
            rows[client.Id] = await run;
        }

        var typeScript = RunFeatureTypeScriptClients(host.BaseAddress, casesJson);

        // The server's record of the streams and the reads the clients stopped: the abort reaches the handler a moment later
        var cancelledType = host.Assembly.GetType("ErrorBodies.RuntimeTest.FeatureLog", throwOnError: true)!;
        var cancelled = (System.Collections.Concurrent.ConcurrentDictionary<string, bool>)cancelledType.GetProperty("Cancelled")!.GetValue(null)!;
        var cancelProbes = cases
            .Where(c => c.Arguments["cancelAfter"] is not null || c.Kind == "slow")
            .SelectMany(c => FeatureClients.Where(cl => c.Kind != "slow" || cl.Id != "ts-fetch-swr").Select(cl => cl.Id + "|" + c.Name))
            .ToList();
        for (var waited = 0; waited < 100 && cancelProbes.Any(p => !cancelled.ContainsKey(p)); waited++)
        {
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        var casesArray = new JsonArray();
        foreach (var featureCase in cases)
        {
            var clients = new JsonObject();
            foreach (var client in FeatureClients)
            {
                var observed = client.TsFlavour is not null
                    ? typeScript[client.Id].GetValueOrDefault(featureCase.Name)
                    : rows[client.Id].GetValueOrDefault(featureCase.Name) is { } row ? JsonNode.Parse(row)!.AsObject() : null;
                clients[client.Id] = HarnessCell(observed) ?? FeatureVerdict(featureCase, client, observed, cancelled.ContainsKey(client.Id + "|" + featureCase.Name));
            }

            casesArray.Add(new JsonObject
            {
                ["id"] = featureCase.Name,
                ["group"] = featureCase.Group,
                ["operation"] = featureCase.Operation,
                ["expects"] = featureCase.Arguments.ToJsonString(),
                ["clients"] = clients,
            });
        }

        return new JsonObject
        {
            ["generatedAt"] = started.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
            ["durationSeconds"] = System.Math.Round((DateTimeOffset.Now - started).TotalSeconds, 1),
            ["clients"] = new JsonArray([.. FeatureClients.Select(c => (JsonNode)new JsonObject { ["id"] = c.Id, ["group"] = c.Group, ["label"] = c.Label })]),
            ["cases"] = casesArray,
        };
    }

    private static JsonArray FeatureCasesJson(IEnumerable<FeatureCase> cases)
        => new([
            .. cases.Select(c =>
            {
                var item = new JsonObject
                {
                    ["name"] = c.Name,
                    ["operation"] = c.Operation,
                    ["kind"] = c.Kind,
                    ["headers"] = new JsonObject((c.Headers ?? []).Select(h => new KeyValuePair<string, JsonNode?>(h.Name, h.Value))),
                };
                foreach (var argument in c.Arguments)
                {
                    item[argument.Key] = argument.Value?.DeepClone();
                }

                return (JsonNode)item;
            }),
        ]);

    private static Dictionary<string, Dictionary<string, JsonObject>> RunFeatureTypeScriptClients(
        Uri baseAddress,
        string casesJson)
    {
        var work = Path.Combine(Path.GetTempPath(), "atc-feature-e2e", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            var yamlPath = Path.Combine(work, "ErrorBodies.yaml");
            File.WriteAllText(yamlPath, FeatureYaml);

            var cli = FindCliExecutable();
            foreach (var (flavour, options) in ((string, string)[])[("fetch", "--client-type Fetch"), ("axios", "--client-type Axios"), ("fetch-zod", "--client-type Fetch --zod --zod-runtime-validate"), ("fetch-reactquery", "--client-type Fetch --hooks ReactQuery"), ("fetch-swr", "--client-type Fetch --hooks Swr")])
            {
                var output = Path.Combine(work, flavour);
                var generate = RunTool(cli, $"generate client-typescript -s \"{yamlPath}\" -o \"{output}\" {options} --no-strict", work, TimeSpan.FromMinutes(3));
                Assert.True(generate.ExitCode == 0, $"generate client-typescript ({flavour}) failed:\n{generate.Output}");
            }

            File.WriteAllText(Path.Combine(work, "package.json"), FeaturePackageJson);
            File.WriteAllText(Path.Combine(work, "tsconfig.json"), E2ETsConfig.Replace("\"lib\": [\"ES2022\", \"DOM\"]", "\"lib\": [\"ES2022\", \"DOM\", \"DOM.Iterable\"], \"jsx\": \"react-jsx\"", StringComparison.Ordinal));
            File.WriteAllText(Path.Combine(work, "harness.mts"), FeatureHarness);
            File.WriteAllText(Path.Combine(work, "cases.json"), new JsonObject { ["baseUrl"] = baseAddress.ToString().TrimEnd('/'), ["cases"] = JsonNode.Parse(casesJson) }.ToJsonString());

            NodeWorkspace.Link(work);

            var result = new Dictionary<string, Dictionary<string, JsonObject>>(StringComparer.Ordinal);
            foreach (var flavour in (string[])["fetch", "axios", "fetch-zod", "fetch-reactquery", "fetch-swr"])
            {
                var run = RunTool("npx", $"tsx harness.mts {flavour}", work, TimeSpan.FromMinutes(5));

                // A harness that did not run is one red column, not the end of the matrix
                result["ts-" + flavour] = ReadHarnessResults(work, flavour, run.ExitCode, run.Output, JsonNode.Parse(casesJson)!.AsArray().Select(c => (string)c!["name"]!));
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
}