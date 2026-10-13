// ReSharper disable StringLiteralTypo
namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// End-to-end feature matrix: the generated server runs on Kestrel with handlers that stream, page, echo and
/// version, and every generated client - the C# EndpointPerOperation and typed clients, the TypeScript Fetch
/// and Axios clients, the Fetch client with Zod runtime validation, and the React Query and SWR hooks rendered
/// with React - runs each feature against it over HTTP: Server-Sent Events, JSON Lines, a JSON array, a JSON Text
/// Sequence and multipart/mixed streamed as <c>IAsyncEnumerable</c>, each to the end, with the handler failing
/// mid-stream, and cancelled by the client
/// (the server must see the cancellation); a paginated read walked by its continuation; a <c>oneOf</c> with a
/// discriminator and a three-level <c>allOf</c> round-tripped through the client; an enum by its value and an
/// unknown value from the server; a date and a date-time round-tripped; a conditional 304; a read the client
/// cancels before the response and one it cancels mid-body (the server must see the cancellation); the wire failures -
/// a connection reset mid-body, a server that never answers (the client's own timeout and the operation's
/// x-retry-timeout-seconds), a body that stalls after the headers, a port no server listens on and a host name that
/// does not resolve - and a handler that throws a ValidationException.
/// </summary>
/// <remarks>
/// Not exercised: a browser runtime (the hooks render with
/// React on jsdom in Node); the React Query and SWR hooks on a 304 (a browser cache sends the condition, not a
/// hook); a cancelled SWR read (SWR hands its fetcher no signal); the hooks with a timeout of the caller's own (a hook
/// takes no signal); webhooks and output caching.
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
          /feature/stream/sse-named:
            get:
              operationId: featureSseNamed
              x-return-async-enumerable: true
              x-sse-item-events: [thing]
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
          /feature/stream/seq:
            get:
              operationId: featureSeq
              x-return-async-enumerable: true
              parameters:
                - { name: count, in: query, required: true, schema: { type: integer, format: int32 } }
                - { name: failAfter, in: query, schema: { type: integer, format: int32 } }
              responses:
                '200': { description: Things, content: { application/json-seq: { schema: { $ref: '#/components/schemas/Thing' } } } }
          /feature/stream/mixed:
            get:
              operationId: featureMixed
              x-return-async-enumerable: true
              parameters:
                - { name: count, in: query, required: true, schema: { type: integer, format: int32 } }
                - { name: failAfter, in: query, schema: { type: integer, format: int32 } }
              responses:
                '200': { description: Things, content: { multipart/mixed: { schema: { $ref: '#/components/schemas/Thing' } } } }
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
                '200': { description: The current version, headers: { ETag: { schema: { type: string } } }, content: { application/json: { schema: { $ref: '#/components/schemas/Thing' } } } }
                '304': { description: Not Modified }
          /feature/slow:
            get:
              operationId: featureSlow
              responses:
                '200': { description: A thing after three seconds, content: { application/json: { schema: { $ref: '#/components/schemas/Thing' } } } }
          /feature/net/reset:
            get:
              operationId: featureReset
              responses:
                '200': { description: Half a thing and then a connection reset, content: { application/json: { schema: { $ref: '#/components/schemas/Thing' } } } }
          /feature/net/hang:
            get:
              operationId: featureHang
              responses:
                '200': { description: Never answered, content: { application/json: { schema: { $ref: '#/components/schemas/Thing' } } } }
          /feature/net/stall:
            get:
              operationId: featureStall
              responses:
                '200': { description: Headers and half a thing and then nothing, content: { application/json: { schema: { $ref: '#/components/schemas/Thing' } } } }
          /feature/net/timeout:
            get:
              operationId: featureTimeout
              x-retry-policy: one-second
              x-retry-enabled: false
              x-retry-timeout-seconds: 1
              responses:
                '200': { description: Never answered, content: { application/json: { schema: { $ref: '#/components/schemas/Thing' } } } }
          /feature/net/stall-timeout:
            get:
              operationId: featureStallTimeout
              x-retry-enabled: false
              x-retry-timeout-seconds: 1
              responses:
                '200': { description: Headers and half a thing and then nothing, content: { application/json: { schema: { $ref: '#/components/schemas/Thing' } } } }
          /feature/invalid:
            post:
              operationId: featureInvalid
              requestBody:
                required: true
                content:
                  application/json:
                    schema: { $ref: '#/components/schemas/Thing' }
              responses:
                '200': { description: Never - the handler rejects the thing, content: { application/json: { schema: { $ref: '#/components/schemas/Thing' } } } }
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

            // The slow reads the handler holds: a client cancels once its probe is here (GET /__started?probe=...),
            // not after a fixed delay a cold client could spend before its request reaches the server
            public static System.Collections.Concurrent.ConcurrentDictionary<string, bool> Started { get; } = new(StringComparer.Ordinal);

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

        public sealed class FeatureSseNamedHandlerImpl(IHttpContextAccessor accessor) : IFeatureSseNamedHandler
        {
            public System.Threading.Tasks.Task<FeatureSseNamedResult> ExecuteAsync(FeatureSseNamedParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(FeatureSseNamedResult.Ok(FeatureLog.Stream(parameters.Count, parameters.FailAfter, FeatureLog.Probe(accessor), cancellationToken)));
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

        public sealed class FeatureSeqHandlerImpl(IHttpContextAccessor accessor) : IFeatureSeqHandler
        {
            public System.Threading.Tasks.Task<FeatureSeqResult> ExecuteAsync(FeatureSeqParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(FeatureSeqResult.Ok(FeatureLog.Stream(parameters.Count, parameters.FailAfter, FeatureLog.Probe(accessor), cancellationToken)));
        }

        public sealed class FeatureMixedHandlerImpl(IHttpContextAccessor accessor) : IFeatureMixedHandler
        {
            public System.Threading.Tasks.Task<FeatureMixedResult> ExecuteAsync(FeatureMixedParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(FeatureMixedResult.Ok(FeatureLog.Stream(parameters.Count, parameters.FailAfter, FeatureLog.Probe(accessor), cancellationToken)));
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

        // The ETag is a header the 200 declares: the result's factory takes it and writes it
        public sealed class FeatureEtagHandlerImpl : IFeatureEtagHandler
        {
            public System.Threading.Tasks.Task<FeatureEtagResult> ExecuteAsync(FeatureEtagParameters parameters, System.Threading.CancellationToken cancellationToken = default)
            {
                if (parameters.IfNoneMatch == "\"v1\"")
                {
                    return System.Threading.Tasks.Task.FromResult(FeatureEtagResult.NotModified());
                }

                return System.Threading.Tasks.Task.FromResult(FeatureEtagResult.Ok(new Thing("v1"), eTag: "\"v1\""));
            }
        }

        // A read the server holds for three seconds: a client that cancels it shows up as a cancelled probe
        public sealed class FeatureSlowHandlerImpl(IHttpContextAccessor accessor) : IFeatureSlowHandler
        {
            public async System.Threading.Tasks.Task<FeatureSlowResult> ExecuteAsync(System.Threading.CancellationToken cancellationToken = default)
            {
                var probe = FeatureLog.Probe(accessor);
                FeatureLog.Started[probe] = true;
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

        // The wire failures, answered in front of the generated endpoints (which never see these requests): a body cut
        // off by a connection reset, a request never answered, and headers with half a body and then nothing. A held
        // request is recorded like the slow read: started once the server holds it, cancelled when the client goes.
        public static class WireFailure
        {
            public static async System.Threading.Tasks.Task<bool> TryHandleAsync(HttpContext context)
            {
                switch (context.Request.Path.Value)
                {
                    case "/feature/net/reset":
                        await WriteHalfAsync(context);
                        context.Abort();
                        return true;
                    case "/feature/net/hang" or "/feature/net/timeout":
                        await HoldAsync(context);
                        context.Abort();
                        return true;
                    case "/feature/net/stall" or "/feature/net/stall-timeout":
                        await WriteHalfAsync(context);
                        await HoldAsync(context);
                        context.Abort();
                        return true;
                    default:
                        return false;
                }
            }

            // A 200 that promises 64 bytes of JSON and sends the first 12
            private static async System.Threading.Tasks.Task WriteHalfAsync(HttpContext context)
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                context.Response.ContentLength = 64;
                await context.Response.WriteAsync("{\"name\":\"cut");
                await context.Response.Body.FlushAsync();
            }

            // Until the client goes; thirty seconds at most (hang protection: then the connection is reset)
            private static async System.Threading.Tasks.Task HoldAsync(HttpContext context)
            {
                var probe = context.Request.Headers["X-Probe"].ToString();
                FeatureLog.Started[probe] = true;
                try
                {
                    await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(30), context.RequestAborted);
                }
                catch (OperationCanceledException)
                {
                    FeatureLog.Cancelled[probe] = true;
                }
            }
        }

        // The wire-failure operations are answered by WireFailure; their handlers only complete the registration
        public sealed class FeatureResetHandlerImpl : IFeatureResetHandler
        {
            public System.Threading.Tasks.Task<FeatureResetResult> ExecuteAsync(System.Threading.CancellationToken cancellationToken = default)
                => throw new NotSupportedException();
        }

        public sealed class FeatureHangHandlerImpl : IFeatureHangHandler
        {
            public System.Threading.Tasks.Task<FeatureHangResult> ExecuteAsync(System.Threading.CancellationToken cancellationToken = default)
                => throw new NotSupportedException();
        }

        public sealed class FeatureStallHandlerImpl : IFeatureStallHandler
        {
            public System.Threading.Tasks.Task<FeatureStallResult> ExecuteAsync(System.Threading.CancellationToken cancellationToken = default)
                => throw new NotSupportedException();
        }

        public sealed class FeatureTimeoutHandlerImpl : IFeatureTimeoutHandler
        {
            public System.Threading.Tasks.Task<FeatureTimeoutResult> ExecuteAsync(System.Threading.CancellationToken cancellationToken = default)
                => throw new NotSupportedException();
        }

        public sealed class FeatureStallTimeoutHandlerImpl : IFeatureStallTimeoutHandler
        {
            public System.Threading.Tasks.Task<FeatureStallTimeoutResult> ExecuteAsync(System.Threading.CancellationToken cancellationToken = default)
                => throw new NotSupportedException();
        }

        // A handler that validates the thing itself and rejects it, as a FluentValidation validator does
        public sealed class FeatureInvalidHandlerImpl : IFeatureInvalidHandler
        {
            public System.Threading.Tasks.Task<FeatureInvalidResult> ExecuteAsync(FeatureInvalidParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => throw new FluentValidation.ValidationException([new FluentValidation.Results.ValidationFailure("Name", "'Name' must be a known thing.")]);
        }

        // Answers GET /__started?probe=... with 200 once the slow handler holds that probe's read, else 404
        public sealed class StartedProbeFilter : Microsoft.AspNetCore.Hosting.IStartupFilter
        {
            public Action<Microsoft.AspNetCore.Builder.IApplicationBuilder> Configure(Action<Microsoft.AspNetCore.Builder.IApplicationBuilder> next)
                => app =>
                {
                    Microsoft.AspNetCore.Builder.UseExtensions.Use(app, async (context, nextMiddleware) =>
                    {
                        if (context.Request.Path == "/__started")
                        {
                            context.Response.StatusCode = FeatureLog.Started.ContainsKey(context.Request.Query["probe"].ToString()) ? 200 : 404;
                            return;
                        }

                        // GET /__cancelled?probe=...: 200 once the server saw that probe's request cancelled, else 404
                        if (context.Request.Path == "/__cancelled")
                        {
                            context.Response.StatusCode = FeatureLog.Cancelled.ContainsKey(context.Request.Query["probe"].ToString()) ? 200 : 404;
                            return;
                        }

                        if (await WireFailure.TryHandleAsync(context))
                        {
                            return;
                        }

                        await nextMiddleware(context);
                    });
                    next(app);
                };
        }

        public static class FeatureRegistration
        {
            public static void Register(IServiceCollection services)
            {
                services.AddTransient<Microsoft.AspNetCore.Hosting.IStartupFilter, StartedProbeFilter>();
                services.AddScoped<IFeatureSlowHandler, FeatureSlowHandlerImpl>();
                services.AddHttpContextAccessor();
                services.AddScoped<IFeatureSseHandler, FeatureSseHandlerImpl>();
                services.AddScoped<IFeatureSseNamedHandler, FeatureSseNamedHandlerImpl>();
                services.AddScoped<IFeatureLinesHandler, FeatureLinesHandlerImpl>();
                services.AddScoped<IFeatureArrayHandler, FeatureArrayHandlerImpl>();
                services.AddScoped<IFeatureSeqHandler, FeatureSeqHandlerImpl>();
                services.AddScoped<IFeatureMixedHandler, FeatureMixedHandlerImpl>();
                services.AddScoped<IFeaturePageHandler, FeaturePageHandlerImpl>();
                services.AddScoped<IFeatureShapeHandler, FeatureShapeHandlerImpl>();
                services.AddScoped<IFeatureNestedHandler, FeatureNestedHandlerImpl>();
                services.AddScoped<IFeatureColorHandler, FeatureColorHandlerImpl>();
                services.AddScoped<IFeatureDatesHandler, FeatureDatesHandlerImpl>();
                services.AddScoped<IFeatureEtagHandler, FeatureEtagHandlerImpl>();
                services.AddScoped<IFeatureResetHandler, FeatureResetHandlerImpl>();
                services.AddScoped<IFeatureHangHandler, FeatureHangHandlerImpl>();
                services.AddScoped<IFeatureStallHandler, FeatureStallHandlerImpl>();
                services.AddScoped<IFeatureTimeoutHandler, FeatureTimeoutHandlerImpl>();
                services.AddScoped<IFeatureStallTimeoutHandler, FeatureStallTimeoutHandlerImpl>();
                services.AddScoped<IFeatureInvalidHandler, FeatureInvalidHandlerImpl>();
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

            // The HttpClient with a one-second HttpClient.Timeout, and the one with the generated resilience handler
            // (each operation's x-retry-*), whose own HttpClient.Timeout is longer than any operation's timeout
            private const string ShortTimeoutClient = "short-timeout";
            private const string ResilientClient = "resilient";

            // The HttpClient of a case that is sent to an address of its own (no server there): this prefix + the address
            private const string AddressClient = "address:";

            private static string serverAddress = string.Empty;

            public static async System.Threading.Tasks.Task<Dictionary<string, string>> RunAsync(string baseAddress, string casesJson, bool typed, string clientId)
            {
                serverAddress = baseAddress;
                var results = new Dictionary<string, string>(StringComparer.Ordinal);
                var services = new ServiceCollection();
                services.AddTransient<ProbeStampHandler>();
                var httpClientName = Register(services);
                services.AddHttpClient(httpClientName, c => c.BaseAddress = new Uri(baseAddress)).AddHttpMessageHandler<ProbeStampHandler>();
                services.AddHttpClient(ShortTimeoutClient, c =>
                {
                    c.BaseAddress = new Uri(baseAddress);
                    c.Timeout = TimeSpan.FromSeconds(1);
                }).AddHttpMessageHandler<ProbeStampHandler>();
                services.AddHttpClient(ResilientClient, c =>
                {
                    c.BaseAddress = new Uri(baseAddress);
                    c.Timeout = TimeSpan.FromSeconds(15);
                }).AddHttpMessageHandler<ProbeStampHandler>().AddApiResilience(ResiliencePolicies.OneSecond);
                foreach (var address in JsonNode.Parse(casesJson)!.AsArray().Select(item => (string?)item!["address"]).Where(a => a is not null).Distinct(StringComparer.Ordinal))
                {
                    services.AddHttpClient(AddressClient + address, c => c.BaseAddress = new Uri(address!)).AddHttpMessageHandler<ProbeStampHandler>();
                }

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

            // Waits (at most ten seconds) until the server holds the probe's slow read.
            private static async System.Threading.Tasks.Task WaitStartedAsync(string probe)
            {
                using var http = new HttpClient { BaseAddress = new Uri(serverAddress) };
                for (var attempt = 0; attempt < 400; attempt++)
                {
                    using var answer = await http.GetAsync("__started?probe=" + Uri.EscapeDataString(probe));
                    if (answer.IsSuccessStatusCode)
                    {
                        return;
                    }

                    await System.Threading.Tasks.Task.Delay(25);
                }
            }

            // Starts the slow read, cancels it once the server holds it, and records how the call ended.
            private static async System.Threading.Tasks.Task<JsonObject> CancelAsync(Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> call)
            {
                using var cts = new System.Threading.CancellationTokenSource();
                var running = call(cts.Token);
                await WaitStartedAsync(ProbeStampHandler.Current["X-Probe"]);
                await cts.CancelAsync();
                try
                {
                    await running;
                    return new JsonObject { ["kind"] = "completed" };
                }
                catch (OperationCanceledException ex)
                {
                    return new JsonObject { ["kind"] = "cancelled", ["type"] = ex.GetType().Name, ["callerToken"] = ex.CancellationToken == cts.Token };
                }
            }

            // Runs a call that fails on the wire (or that the caller cancels once the server holds it, mid-body) through
            // the client its scenario names, and records how it ended: the exception, its inner exception, whether it is
            // an HttpRequestException, and whether a cancellation carries the caller's token. A call still running after
            // twenty seconds is recorded as hung (hang protection; the server resets a held request after thirty).
            private static async System.Threading.Tasks.Task<JsonObject> NetAsync(JsonObject c, string httpClientName, Func<string, System.Threading.CancellationToken, System.Threading.Tasks.Task<JsonObject>> call)
            {
                var scenario = (string)c["scenario"]!;
                var clientName = scenario switch
                {
                    "client-timeout" => ShortTimeoutClient,
                    "retry-timeout" or "stall-timeout" => ResilientClient,
                    _ when (string?)c["address"] is { } address => AddressClient + address,
                    _ => httpClientName,
                };

                using var cts = new System.Threading.CancellationTokenSource();
                var running = call(clientName, cts.Token);
                if (scenario == "cancel-mid-body")
                {
                    await WaitStartedAsync(ProbeStampHandler.Current["X-Probe"]);
                    await cts.CancelAsync();
                }

                if (await System.Threading.Tasks.Task.WhenAny(running, System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(20))) != running)
                {
                    return new JsonObject { ["kind"] = "hung" };
                }

                try
                {
                    return await running;
                }
                catch (Exception ex)
                {
                    return new JsonObject
                    {
                        ["kind"] = "threw",
                        ["type"] = ex.GetType().Name,
                        ["inner"] = ex.InnerException?.GetType().Name,
                        ["httpRequestException"] = ex is HttpRequestException,
                        ["httpRequestError"] = (ex as HttpRequestException)?.HttpRequestError.ToString(),
                        ["callerToken"] = ex is OperationCanceledException oce && cts.IsCancellationRequested && oce.CancellationToken == cts.Token,
                        ["error"] = ex.GetType().Name + ": " + Short(ex.Message),
                    };
                }
            }

            // A rejected request: the status, and the error body's detail and field errors (a ProblemDetails or a
            // ValidationProblemDetails), as the client exposes them
            private static JsonObject Rejected(int status, string? body)
            {
                var result = new JsonObject { ["kind"] = "error", ["status"] = status, ["error"] = Short(body ?? string.Empty) };
                var start = body?.IndexOf('{', StringComparison.Ordinal) ?? -1;
                if (start >= 0 && JsonNode.Parse(body![start..]) is JsonObject problem)
                {
                    result["detail"] = (string?)problem["detail"];
                    result["errors"] = problem["errors"] is JsonObject errors ? new JsonArray([.. errors.Select(e => (JsonNode)e.Key)]) : new JsonArray();
                    result["messages"] = problem["errors"] is JsonObject fieldErrors
                        ? new JsonArray([.. fieldErrors.SelectMany(e => e.Value is JsonArray texts ? texts.Select(t => (JsonNode?)t?.ToString()) : [])])
                        : new JsonArray();
                }

                return result;
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
                                "FeatureSse" => async ct => await provider.GetRequiredService<IFeatureSseEndpoint>().ExecuteAsync(new FeatureSseParameters(count, failAfter), httpClientName, ct),
                                "FeatureSseNamed" => async ct => await provider.GetRequiredService<IFeatureSseNamedEndpoint>().ExecuteAsync(new FeatureSseNamedParameters(count, failAfter), httpClientName, ct),
                                "FeatureLines" => async ct => await provider.GetRequiredService<IFeatureLinesEndpoint>().ExecuteAsync(new FeatureLinesParameters(count, failAfter), httpClientName, ct),
                                "FeatureSeq" => async ct => await provider.GetRequiredService<IFeatureSeqEndpoint>().ExecuteAsync(new FeatureSeqParameters(count, failAfter), httpClientName, ct),
                                "FeatureMixed" => async ct => await provider.GetRequiredService<IFeatureMixedEndpoint>().ExecuteAsync(new FeatureMixedParameters(count, failAfter), httpClientName, ct),
                                _ => async ct => await provider.GetRequiredService<IFeatureArrayEndpoint>().ExecuteAsync(new FeatureArrayParameters(count, failAfter), httpClientName, ct),
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
                        return await CancelAsync(ct => provider.GetRequiredService<IFeatureSlowEndpoint>().ExecuteAsync(httpClientName, ct));

                    case "net":
                        return await NetAsync(c, httpClientName, async (name, ct) =>
                        {
                            switch (operation)
                            {
                                case "FeatureReset":
                                {
                                    var r = await provider.GetRequiredService<IFeatureResetEndpoint>().ExecuteAsync(name, ct);
                                    return r.IsSuccess ? Value(r.OkContent) : Error((int)r.StatusCode, r.Content);
                                }

                                case "FeatureHang":
                                {
                                    var r = await provider.GetRequiredService<IFeatureHangEndpoint>().ExecuteAsync(name, ct);
                                    return r.IsSuccess ? Value(r.OkContent) : Error((int)r.StatusCode, r.Content);
                                }

                                case "FeatureStall":
                                {
                                    var r = await provider.GetRequiredService<IFeatureStallEndpoint>().ExecuteAsync(name, ct);
                                    return r.IsSuccess ? Value(r.OkContent) : Error((int)r.StatusCode, r.Content);
                                }

                                case "FeatureTimeout":
                                {
                                    var r = await provider.GetRequiredService<IFeatureTimeoutEndpoint>().ExecuteAsync(name, ct);
                                    return r.IsSuccess ? Value(r.OkContent) : Error((int)r.StatusCode, r.Content);
                                }

                                default:
                                {
                                    var r = await provider.GetRequiredService<IFeatureStallTimeoutEndpoint>().ExecuteAsync(name, ct);
                                    return r.IsSuccess ? Value(r.OkContent) : Error((int)r.StatusCode, r.Content);
                                }
                            }
                        });

                    case "invalid":
                    {
                        var r = await provider.GetRequiredService<IFeatureInvalidEndpoint>().ExecuteAsync(new FeatureInvalidParameters(new Thing("bad")), httpClientName);
                        return r.IsSuccess ? Value(r.OkContent) : Rejected((int)r.StatusCode, r.Content);
                    }

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
                                "FeatureSseNamed" => ct => client.FeatureSseNamedAsync(new FeatureSseNamedParameters(count, failAfter), ct),
                                "FeatureLines" => ct => client.FeatureLinesAsync(new FeatureLinesParameters(count, failAfter), ct),
                                "FeatureSeq" => ct => client.FeatureSeqAsync(new FeatureSeqParameters(count, failAfter), ct),
                                "FeatureMixed" => ct => client.FeatureMixedAsync(new FeatureMixedParameters(count, failAfter), ct),
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
                        return await CancelAsync(ct => client.FeatureSlowAsync(ct));

                    case "net":
                        return await NetAsync(c, httpClientName, async (name, ct) =>
                        {
                            var named = new ErrorBodiesClient(provider.GetRequiredService<IHttpClientFactory>().CreateClient(name));
                            var r = operation switch
                            {
                                "FeatureReset" => await named.FeatureResetAsync(ct),
                                "FeatureHang" => await named.FeatureHangAsync(ct),
                                "FeatureStall" => await named.FeatureStallAsync(ct),
                                "FeatureTimeout" => await named.FeatureTimeoutAsync(ct),
                                _ => await named.FeatureStallTimeoutAsync(ct),
                            };
                            return r.IsSuccess ? Value(r.SuccessContent) : Error((int)r.StatusCode, r.Content);
                        });

                    case "invalid":
                    {
                        var r = await client.FeatureInvalidAsync(new FeatureInvalidParameters(new Thing("bad")));
                        return r.IsSuccess ? Value(r.SuccessContent) : Rejected((int)r.StatusCode, r.Content);
                    }

                    default:
                        throw new InvalidOperationException("unknown case kind " + kind);
                }
            }
        }
        """";

    /// <summary>
    /// The typed-client side of the probe in its Throw style (the default): a method per operation that returns the
    /// value and throws an <c>HttpRequestException</c> for an error status; it exposes no headers.
    /// </summary>
    private const string FeatureProbeThrowSource = """"
        using System.Net.Http;
        using System.Text.Json;
        using System.Text.Json.Nodes;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public static partial class FeatureProbe
        {
            private static string Register(ServiceCollection services)
                => "typed";

            // The names streamed until the end, the handler's failure, or the client's own stop (as StreamAsync)
            private static async System.Threading.Tasks.Task<JsonObject> StreamItemsAsync(Func<System.Threading.CancellationToken, IAsyncEnumerable<Thing>> start, int? cancelAfter)
            {
                var names = new List<string>();
                string? error = null;
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(30));
                try
                {
                    await foreach (var thing in start(cts.Token).WithCancellation(cts.Token))
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
                        return await StreamItemsAsync(
                            operation switch
                            {
                                "FeatureSse" => ct => client.FeatureSseAsync(new FeatureSseParameters(count, failAfter), ct),
                                "FeatureSseNamed" => ct => client.FeatureSseNamedAsync(new FeatureSseNamedParameters(count, failAfter), ct),
                                "FeatureLines" => ct => client.FeatureLinesAsync(new FeatureLinesParameters(count, failAfter), ct),
                                "FeatureSeq" => ct => client.FeatureSeqAsync(new FeatureSeqParameters(count, failAfter), ct),
                                "FeatureMixed" => ct => client.FeatureMixedAsync(new FeatureMixedParameters(count, failAfter), ct),
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
                            var page = await client.FeaturePageAsync(new FeaturePageParameters(continuation, (int)c["pageSize"]!));
                            names.AddRange(page.Results.Select(t => t.Name));
                            pages++;
                            continuation = page.Continuation;
                        }
                        while (!string.IsNullOrEmpty(continuation) && pages < 10);

                        return new JsonObject { ["kind"] = "items", ["items"] = new JsonArray([.. names.Select(n => (JsonNode)n)]), ["pages"] = pages };
                    }

                    case "shape":
                        return Value(await client.FeatureShapeAsync(new FeatureShapeParameters(JsonSerializer.Deserialize<Shape>(c["body"]!.ToJsonString(), Json)!)));

                    case "nested":
                        return Value(await client.FeatureNestedAsync(new FeatureNestedParameters(JsonSerializer.Deserialize<Leaf>(c["body"]!.ToJsonString(), Json)!)));

                    case "color":
                        return Value(await client.FeatureColorAsync(new FeatureColorParameters(JsonSerializer.Deserialize<Color>("\"" + (string)c["color"]! + "\"", Json))));

                    case "dates":
                        return Value(await client.FeatureDatesAsync(new FeatureDatesParameters(JsonSerializer.Deserialize<Moment>(c["body"]!.ToJsonString(), Json)!)));

                    case "etag":
                    {
                        try
                        {
                            var result = Value(await client.FeatureEtagAsync(new FeatureEtagParameters((string?)c["ifNoneMatch"])));
                            result["status"] = 200;
                            return result;
                        }
                        catch (HttpRequestException ex) when (ex.StatusCode is { } status)
                        {
                            return new JsonObject { ["kind"] = "status", ["status"] = (int)status, ["notModified"] = status == System.Net.HttpStatusCode.NotModified, ["error"] = Short(ex.Message) };
                        }
                    }

                    case "slow":
                        return await CancelAsync(ct => client.FeatureSlowAsync(ct));

                    case "net":
                        return await NetAsync(c, httpClientName, async (name, ct) =>
                        {
                            var named = new ErrorBodiesClient(provider.GetRequiredService<IHttpClientFactory>().CreateClient(name));
                            return Value(operation switch
                            {
                                "FeatureReset" => await named.FeatureResetAsync(ct),
                                "FeatureHang" => await named.FeatureHangAsync(ct),
                                "FeatureStall" => await named.FeatureStallAsync(ct),
                                "FeatureTimeout" => await named.FeatureTimeoutAsync(ct),
                                _ => await named.FeatureStallTimeoutAsync(ct),
                            });
                        });

                    case "invalid":
                    {
                        try
                        {
                            return Value(await client.FeatureInvalidAsync(new FeatureInvalidParameters(new Thing("bad"))));
                        }
                        catch (HttpRequestException ex) when (ex.StatusCode is { } status)
                        {
                            return Rejected((int)status, ex.Message);
                        }
                    }

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
        const { ApiError } = await import(`./${which}/errors/ApiError.ts`);
        const hookModule: any = hooks ? await import(`./${which}/hooks/index.ts`) : null;
        const names = (items: any[]) => items.map((t) => t.name);
        const WAIT = { timeout: 20000, interval: 25 };
        const sleep = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));

        // Only the x-retry-timeout-seconds cases run the ApiClient's retry policy; every other case is sent once,
        // without a policy timeout, as before the feature specification had an x-retry operation
        const usesRetry = (c: any) => c.scenario === 'retry-timeout' || c.scenario === 'stall-timeout';
        const optionsFor = (c: any, headers: Record<string, string>) => usesRetry(c) ? { defaultHeaders: headers } : { defaultHeaders: headers, retryPolicy: false };

        // How a call failed: the error's name, class, code and cause, and whether it is the client's ApiError
        const describe = (e: any) => ({ kind: 'threw', errorName: e?.name ?? null, ctor: e?.constructor?.name ?? null, code: e?.code ?? null, cause: e?.cause?.code ?? e?.cause?.name ?? null, isApiError: e instanceof ApiError, isAxiosError: e?.isAxiosError === true, error: `${e?.name ?? 'Error'}: ${String(e?.message ?? e).slice(0, 300)}` });

        // A rejected request as the client exposes it: the status, the error's class, its message and field errors
        const rejectedOf = (e: any, arm: string | null) => ({ kind: 'error', arm, status: e?.status ?? 0, errorName: e?.name ?? null, detail: e?.detail ?? null, errors: Object.keys(e?.errors ?? {}), messages: Object.values(e?.errors ?? {}).flat().map(String), error: String(e?.message ?? e).slice(0, 300) });

        // Until the server holds the probe's slow read (at most ten seconds): a cancel is sent only then
        async function waitStarted(probe: string) {
          for (let attempt = 0; attempt < 400; attempt++) {
            const answer = await fetch(new URL('__started?probe=' + encodeURIComponent(probe), baseUrl));
            if (answer.ok) return;
            await sleep(25);
          }
        }

        // Until the server saw the probe's request cancelled (at most five seconds), while this process still holds it
        async function waitCancelled(probe: string): Promise<boolean> {
          for (let attempt = 0; attempt < 200; attempt++) {
            const answer = await fetch(new URL('__cancelled?probe=' + encodeURIComponent(probe), baseUrl));
            if (answer.ok) return true;
            await sleep(25);
          }

          return false;
        }

        // The call's outcome; still pending after twenty seconds it is hung (hang protection)
        async function settle(pending: Promise<any>): Promise<Record<string, unknown>> {
          pending.catch(() => {});
          const hung = Symbol('hung');
          try {
            const r = await Promise.race([pending, sleep(20000).then(() => hung)]);
            if (r === hung) return { kind: 'hung' };
            return r.status === 'ok' ? { kind: 'value', value: r.data } : { ...errorOf(r), arm: r.status };
          } catch (e: any) {
            return describe(e);
          }
        }

        function wrapperFor(c: any, headers: Record<string, string>) {
          const { ApiProvider } = hookModule;
          const queryClient = hooks === 'reactquery' ? new QueryClient({ defaultOptions: { queries: { retry: false } } }) : null;
          const options = optionsFor(c, headers);
          return ({ children }: any) => {
            const inner = React.createElement(ApiProvider, { baseUrl: c.address ?? baseUrl, options }, children);
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
              } catch (e: any) { if (!(c.cancelAfter && e?.name === 'AbortError')) error = `${e?.name ?? 'Error'}${e?.status ? ' ' + e.status : ''}: ${String(e?.message ?? e).slice(0, 300)}`; }
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
              const pending = f.featureSlow(controller.signal);
              pending.catch(() => {});
              await waitStarted(`ts-${which}|${c.name}`);
              controller.abort();
              try {
                const r = await pending;
                return { kind: 'completed', arm: r.status };
              } catch (e: any) {
                return e?.name === 'AbortError' || e?.name === 'CanceledError' ? { kind: 'cancelled' } : { kind: 'threw', error: `${e?.name ?? 'Error'}: ${String(e?.message ?? e).slice(0, 300)}` };
              }
            }
            case 'net': {
              // The client's own timeout is, in TypeScript, the caller's AbortSignal.timeout: the clients have no other
              const controller = new AbortController();
              const pending = f[m](c.scenario === 'client-timeout' ? AbortSignal.timeout(1000) : controller.signal);
              if (c.scenario === 'cancel-mid-body') {
                await waitStarted(`ts-${which}|${c.name}`);
                controller.abort();
              }

              return settle(pending);
            }
            case 'invalid': {
              const r = await f.featureInvalid({ name: 'bad' });
              return r.status === 'ok' ? { kind: 'value', value: r.data } : rejectedOf(r.error, r.status);
            }
          }
          throw new Error('unknown kind ' + c.kind);
        }

        async function viaHooks(c: any): Promise<Record<string, unknown>> {
          const wrapper = wrapperFor(c, { ...c.headers, 'X-Probe': `ts-${which}|${c.name}` });
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
              return { kind: 'items', items: names(result.current.items), error: result.current.error ? ((result.current.error as any).status ? 'ApiError ' + (result.current.error as any).status + ': ' : '') + String(result.current.error.message ?? result.current.error).slice(0, 300) : null };
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
              await waitStarted(`ts-${which}|${c.name}`);
              const completed = result.current.data !== undefined;
              unmount();
              // Stay until a request that was not cancelled has ended, so the harness exiting (which closes the
              // connection) is not mistaken for the hook cancelling it
              await new Promise((resolve) => setTimeout(resolve, 3500));
              return { kind: completed ? 'completed' : 'cancelled' };
            }
            case 'net': {
              const hook = H['use' + c.operation];
              if (c.scenario === 'client-timeout') return { kind: 'na', why: 'a hook takes no AbortSignal of its own: its timeout is the operation\'s x-retry-timeout-seconds' };
              if (c.scenario === 'cancel-mid-body') {
                if (hooks === 'swr') return { kind: 'na', why: 'SWR hands its fetcher no AbortSignal' };
                const { unmount } = renderHook(() => hook(), { wrapper });
                await waitStarted(`ts-${which}|${c.name}`);
                unmount();
                // Cancelled only when the server sees it while this process still holds the connection
                return { kind: (await waitCancelled(`ts-${which}|${c.name}`)) ? 'cancelled' : 'completed' };
              }

              const { result } = renderHook(() => hook(), { wrapper });
              await waitFor(() => { if (result.current.data === undefined && !result.current.error) throw new Error('waiting'); }, WAIT);
              return result.current.error ? describe(result.current.error) : { kind: 'value', value: result.current.data };
            }
            case 'invalid': {
              const { result } = renderHook(() => H.useFeatureInvalid(), { wrapper });
              let failure: any = null;
              let data: any;
              await act(async () => {
                try {
                  data = hooks === 'reactquery' ? await result.current.mutateAsync({ name: 'bad' }) : await result.current.trigger({ name: 'bad' });
                } catch (e) {
                  failure = e;
                }
              });
              return failure ? rejectedOf(failure, null) : { kind: 'value', value: data };
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
              // A case with an address of its own is sent there: no server listens, or no host has the name
              const api: any = new ApiService(c.address ?? baseUrl, optionsFor(c, { ...c.headers, 'X-Probe': `ts-${which}|${c.name}` }));
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
        foreach (var (operation, label) in ((string, string)[])[("FeatureSse", "Server-Sent Events"), ("FeatureLines", "JSON Lines"), ("FeatureArray", "a JSON array as IAsyncEnumerable"), ("FeatureSeq", "JSON Text Sequence"), ("FeatureMixed", "multipart/mixed")])
        {
            cases.Add(new($"stream: {label}, five items", "streaming", operation, "stream", new JsonObject { ["count"] = 5 }));
            cases.Add(new($"stream: {label}, the handler fails after two items", "streaming", operation, "stream", new JsonObject { ["count"] = 5, ["failAfter"] = 2 }));
            cases.Add(new($"stream: {label}, cancelled by the client after two items", "streaming", operation, "stream", new JsonObject { ["count"] = 0, ["cancelAfter"] = 2 }));
        }

        // What a stream carries on the wire besides its items, written by a stub in front of the generated endpoint
        // (the generated server writes none of it): every client reads the three items and nothing else
        foreach (var (stub, operation, name) in FeatureWireStubs)
        {
            cases.Add(new($"wire: {name}", "stream formats on the wire", operation, "stream", new JsonObject { ["count"] = stub == "array-empty" ? 0 : 3 }, [("X-Feature-Stub", stub)]));
        }

        // A stream that fails before its first item: 503 with a problem, in each format
        foreach (var (operation, label) in ((string, string)[])[("FeatureSse", "Server-Sent Events"), ("FeatureLines", "JSON Lines"), ("FeatureArray", "a JSON array"), ("FeatureSeq", "JSON Text Sequence"), ("FeatureMixed", "multipart/mixed")])
        {
            cases.Add(new($"wire: {label}, 503 with a problem before the first item", "stream formats on the wire", operation, "stream", new JsonObject { ["count"] = 3, ["status"] = 503 }, [("X-Feature-Stub", "closed")]));
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
        cases.Add(new("cancellation: a read the client cancels stops on the server", "cancellation", "FeatureSlow", "slow", new JsonObject()));
        cases.Add(new("cancellation: the caller cancels mid-body, after the headers", "cancellation", "FeatureStall", "net", new JsonObject { ["scenario"] = "cancel-mid-body" }));
        cases.Add(new("network: the connection resets mid-body", "network", "FeatureReset", "net", new JsonObject { ["scenario"] = "reset" }));
        cases.Add(new("network: no answer, the client's own timeout (C#: HttpClient.Timeout; TypeScript: the caller's AbortSignal.timeout)", "network", "FeatureHang", "net", new JsonObject { ["scenario"] = "client-timeout" }));
        cases.Add(new("network: no answer, the operation's x-retry-timeout-seconds", "network", "FeatureTimeout", "net", new JsonObject { ["scenario"] = "retry-timeout" }));
        cases.Add(new("network: headers, then a body that stalls, the operation's x-retry-timeout-seconds", "network", "FeatureStallTimeout", "net", new JsonObject { ["scenario"] = "stall-timeout" }));
        cases.Add(new("network: no server listens on the port (connection refused)", "network", "FeatureReset", "net", new JsonObject { ["scenario"] = "refused" }));
        cases.Add(new("network: the host name does not resolve", "network", "FeatureReset", "net", new JsonObject { ["scenario"] = "dns" }));
        cases.Add(new("validation: the handler throws FluentValidation's ValidationException", "validation", "FeatureInvalid", "invalid", new JsonObject()));
        return cases;
    }

    /// <summary>
    /// The streams a stub writes: the stub's name (the <c>X-Feature-Stub</c> header), the operation it answers for,
    /// and what the case is called. Each carries the items <c>t1</c>, <c>t2</c>, <c>t3</c> (the empty array: none).
    /// </summary>
    internal static readonly (string Stub, string Operation, string Name)[] FeatureWireStubs =
    [
        ("sse-noise", "FeatureSse", "Server-Sent Events with a comment, id and retry fields, a heartbeat without data and a named event between the items"),
        ("sse-crlf", "FeatureSse", "Server-Sent Events with CRLF line ends and data over two lines"),
        ("sse-named", "FeatureSseNamed", "Server-Sent Events whose items are named events (x-sse-item-events: [thing]), between a heartbeat and an event of another name"),
        ("lines-blank", "FeatureLines", "JSON Lines with blank lines, CRLF and no line feed after the last item"),
        ("seq-blank", "FeatureSeq", "JSON Text Sequence with a line feed between the records and a pretty-printed record"),
        ("mixed-padded", "FeatureMixed", "multipart/mixed with a preamble, padded boundaries, part headers and a pretty-printed part"),
        ("array-pretty", "FeatureArray", "a JSON array that is pretty-printed"),
        ("array-empty", "FeatureArray", "a JSON array with no body at all (200, no content)"),
    ];

    /// <summary>
    /// Writes the stream (or the failure) a stub stands for; <see langword="false"/> when the request names none.
    /// </summary>
    private static async Task<bool> WriteFeatureStubAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue("X-Feature-Stub", out var stub))
        {
            return false;
        }

        const string rs = "\u001e";
        var (status, contentType, body) = stub.ToString() switch
        {
            // A value the clients' enum does not have
            "unknown-color" => (200, "application/json", """{"color":"purple"}"""),
            "closed" => (503, "application/problem+json", """{"type":"about:blank","title":"Service Unavailable","status":503,"detail":"The exchange is closed."}"""),
            "sse-noise" => (200, "text/event-stream", ": keep-alive\n\ndata: {\"name\":\"t1\"}\n\nevent: ping\n\nid: 2\nretry: 1000\ndata: {\"name\":\"t2\"}\n\nevent: ping\ndata: {\"name\":\"no item\"}\n\n: another comment\ndata:{\"name\":\"t3\"}\n\n"),
            "sse-named" => (200, "text/event-stream", "event: thing\ndata: {\"name\":\"t1\"}\n\nevent: ping\ndata: {\"name\":\"no item\"}\n\nevent: ping\n\ndata: {\"name\":\"t2\"}\n\nevent: Thing\ndata: {\"name\":\"no item\"}\n\nevent: thing\ndata: {\"name\":\"t3\"}\n\n"),
            "sse-crlf" => (200, "text/event-stream", "data: {\"name\":\r\ndata: \"t1\"}\r\n\r\ndata: {\"name\":\"t2\"}\r\n\r\ndata: {\"name\":\"t3\"}\r\n\r\n"),
            "lines-blank" => (200, "application/jsonl", "{\"name\":\"t1\"}\n\n   \n{\"name\":\"t2\"}\r\n{\"name\":\"t3\"}"),
            "seq-blank" => (200, "application/json-seq", rs + "{\"name\":\"t1\"}\n\n" + rs + "{\n  \"name\": \"t2\"\n}\n" + rs + "{\"name\":\"t3\"}\n"),
            "mixed-padded" => (200, "multipart/mixed; boundary=\"b-1\"", "a preamble\r\n--b-1  \r\nContent-Type: application/json\r\nX-Part: 1\r\n\r\n{\"name\":\"t1\"}\r\n--b-1\r\n\r\n{\r\n  \"name\": \"t2\"\r\n}\r\n--b-1\r\nContent-Type: application/json\r\n\r\n{\"name\":\"t3\"}\r\n--b-1--\r\nan epilogue\r\n"),
            "array-pretty" => (200, "application/json", "[\n  { \"name\": \"t1\" },\n  { \"name\": \"t2\" },\n  { \"name\": \"t3\" }\n]\n"),
            "array-empty" => (200, "application/json", string.Empty),
            _ => (0, string.Empty, string.Empty),
        };

        if (status == 0)
        {
            return false;
        }

        context.Response.StatusCode = status;
        context.Response.ContentType = contentType;
        await context.Response.WriteAsync(body, context.RequestAborted);
        return true;
    }

    internal static readonly (string Id, string Group, string Label, string Marker, bool Typed, string? TsFlavour)[] FeatureClients =
    [
        ("cs-epo", "C# EndpointPerOperation", "errorResponseFormat: ProblemDetails", """{ "generationMode": "EndpointPerOperation", "errorResponseFormat": "ProblemDetails" }""", false, null),
        ("cs-epo-classic-enums", "C# EndpointPerOperation", "extensibleEnums: false (the classic enum)", """{ "generationMode": "EndpointPerOperation", "errorResponseFormat": "ProblemDetails", "extensibleEnums": false }""", false, null),
        ("cs-typed", "C# TypedClient", "typedClientResultStyle: Result", """{ "generationMode": "TypedClient", "typedClientResultStyle": "Result" }""", true, null),
        ("cs-typed-throw", "C# TypedClient", "typedClientResultStyle: Throw (the default)", """{ "generationMode": "TypedClient" }""", true, null),
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
    /// added later, fixed by the extensible enums) - all fixed. The network, timeout and validation cases came in with
    /// 15 red: the Axios client's timeout rejected with a CanceledError and the C# resilience handler's timeout did
    /// not cover a body that stalls - fixed; Atc.Rest.MinimalApi 3.0 keeps the field errors of a ValidationException a
    /// handler throws - fixed.
    /// </summary>
    private static readonly Dictionary<string, string> KnownFeatureFailures = new(StringComparer.Ordinal);

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
        var unexpected = failures.Where(f => !KnownFeatureFailures.ContainsKey(f.Key)).ToList();
        var fixedCells = KnownFeatureFailures.Keys.Where(k => failures.All(f => f.Key != k)).Order(StringComparer.Ordinal).ToList();

        Assert.True(
            unexpected.Count == 0,
            $"{unexpected.Count} failing cells:\n" + string.Join("\n", unexpected.Take(120).Select(f => $"{f.Key}: {f.Why}")));
        Assert.True(
            fixedCells.Count == 0,
            $"{fixedCells.Count} cells pass now. Remove them from {nameof(KnownFeatureFailures)}:\n" + string.Join("\n", fixedCells));
    }

    /// <summary>
    /// The same matrix from the specification as an OpenAPI 3.1 document (its nullable member a type array): every
    /// feature must run as it does from the 3.0 document.
    /// </summary>
    [Fact]
    [Trait("Category", "TypeScriptToolchain")]
    public async Task EndToEnd_EveryClientRunsEveryFeature_WithAnOpenApi31Document()
    {
        if (!IsNodeAvailable(out var reason))
        {
            if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Fail(reason + " (running under CI where the feature matrix is required)");
            }

            Assert.Skip(reason);
        }

        var matrix = await CollectFeatureMatrixAsync(openApi31: true);
        Assert.Equal("3.1.0", (string?)matrix["openApi"]);
        if (Environment.GetEnvironmentVariable("FEATURE_E2E_31_JSON") is { Length: > 0 } dumpPath)
        {
            await File.WriteAllTextAsync(dumpPath, matrix.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), TestContext.Current.CancellationToken);
        }

        var failures = RequestBindingFailingCells(matrix);
        var unexpected = failures.Where(f => !KnownFeatureFailures.ContainsKey(f.Key)).ToList();
        var fixedCells = KnownFeatureFailures.Keys.Where(k => failures.All(f => f.Key != k)).Order(StringComparer.Ordinal).ToList();

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
        Assert.Equal(VerdictPass, (string?)FeatureVerdict(slow, client, new JsonObject { ["kind"] = "cancelled", ["callerToken"] = true }, cancelled: true)["verdict"]);
        Assert.Equal(VerdictFail, (string?)FeatureVerdict(slow, client, new JsonObject { ["kind"] = "cancelled", ["callerToken"] = true }, cancelled: false)["verdict"]);
        Assert.Equal(VerdictFail, (string?)FeatureVerdict(slow, client, new JsonObject { ["kind"] = "completed" }, cancelled: false)["verdict"]);
        Assert.Equal(VerdictNotApplicable, (string?)FeatureVerdict(slow, FeatureClients.Single(c => c.Id == "ts-fetch-swr"), new JsonObject { ["kind"] = "na", ["why"] = "SWR" }, cancelled: false)["verdict"]);

        // A C# cancellation must carry the caller's token
        Assert.Equal(VerdictFail, (string?)FeatureVerdict(slow, client, new JsonObject { ["kind"] = "cancelled", ["type"] = "TaskCanceledException", ["callerToken"] = false }, cancelled: true)["verdict"]);

        var tsAxios = FeatureClients.Single(c => c.Id == "ts-axios");
        JsonObject Threw(
            string type,
            string? inner = null,
            bool callerToken = false,
            bool httpRequestException = false)
            => new() { ["kind"] = "threw", ["type"] = type, ["inner"] = inner, ["callerToken"] = callerToken, ["httpRequestException"] = httpRequestException, ["error"] = type + ": message" };
        JsonObject TsThrew(
            string errorName,
            string? code = null,
            bool isAxiosError = false,
            bool isApiError = false)
            => new() { ["kind"] = "threw", ["errorName"] = errorName, ["code"] = code, ["isAxiosError"] = isAxiosError, ["isApiError"] = isApiError, ["error"] = errorName + ": message" };
        FeatureCase Net(string scenario)
            => cases.Single(c => (string?)c.Arguments["scenario"] == scenario);

        // A reset: the transport's network error - an HttpRequestException, fetch's TypeError, an AxiosError (named
        // after the socket error it came from) - never an ApiError or a cancellation
        Assert.Equal(VerdictPass, (string?)FeatureVerdict(Net("reset"), client, Threw("HttpIOException", "IOException", httpRequestException: true), cancelled: false)["verdict"]);
        Assert.Equal(VerdictFail, (string?)FeatureVerdict(Net("reset"), client, Threw("JsonException"), cancelled: false)["verdict"]);
        Assert.Equal(VerdictPass, (string?)FeatureVerdict(Net("reset"), tsFetch, TsThrew("TypeError"), cancelled: false)["verdict"]);
        Assert.Equal(VerdictFail, (string?)FeatureVerdict(Net("reset"), tsFetch, TsThrew("ApiError", isApiError: true), cancelled: false)["verdict"]);
        Assert.Equal(VerdictPass, (string?)FeatureVerdict(Net("reset"), tsAxios, TsThrew("Error", "ECONNRESET", isAxiosError: true), cancelled: false)["verdict"]);
        Assert.Equal(VerdictFail, (string?)FeatureVerdict(Net("reset"), tsAxios, TsThrew("CanceledError", "ERR_CANCELED", isAxiosError: true), cancelled: false)["verdict"]);
        Assert.Equal(VerdictFail, (string?)FeatureVerdict(Net("reset"), client, new JsonObject { ["kind"] = "hung" }, cancelled: false)["verdict"]);

        // No server on the port, a host name that does not resolve: the transport's network error naming which - an
        // HttpRequestException with its HttpRequestError, fetch's TypeError with the cause's code, an AxiosError with
        // its code - never the other failure, a bare error, an ApiError or a call that hangs
        foreach (var (scenario, requestError, otherRequestError, code, otherCode) in ((string, string, string, string, string)[])[
            ("refused", "ConnectionError", "NameResolutionError", "ECONNREFUSED", "ENOTFOUND"),
            ("dns", "NameResolutionError", "ConnectionError", "ENOTFOUND", "ECONNREFUSED")])
        {
            JsonObject Refused(string? error)
            {
                var threw = Threw("HttpRequestException", "SocketException", httpRequestException: true);
                threw["httpRequestError"] = error;
                return threw;
            }

            JsonObject TsRefused(
                string errorName,
                string? cause,
                string? errorCode = null,
                bool isAxiosError = false)
            {
                var threw = TsThrew(errorName, errorCode, isAxiosError);
                threw["cause"] = cause;
                return threw;
            }

            Assert.Equal(VerdictPass, (string?)FeatureVerdict(Net(scenario), client, Refused(requestError), cancelled: false)["verdict"]);
            Assert.Equal(VerdictFail, (string?)FeatureVerdict(Net(scenario), client, Refused(otherRequestError), cancelled: false)["verdict"]);
            Assert.Equal(VerdictFail, (string?)FeatureVerdict(Net(scenario), client, Refused(null), cancelled: false)["verdict"]);
            Assert.Equal(VerdictFail, (string?)FeatureVerdict(Net(scenario), client, Threw("TaskCanceledException", "TimeoutException"), cancelled: false)["verdict"]);
            Assert.Equal(VerdictFail, (string?)FeatureVerdict(Net(scenario), client, new JsonObject { ["kind"] = "hung" }, cancelled: false)["verdict"]);
            Assert.Equal(VerdictPass, (string?)FeatureVerdict(Net(scenario), tsFetch, TsRefused("TypeError", code), cancelled: false)["verdict"]);
            Assert.Equal(VerdictFail, (string?)FeatureVerdict(Net(scenario), tsFetch, TsRefused("TypeError", otherCode), cancelled: false)["verdict"]);
            Assert.Equal(VerdictFail, (string?)FeatureVerdict(Net(scenario), tsFetch, TsRefused("TypeError", null), cancelled: false)["verdict"]);
            Assert.Equal(VerdictFail, (string?)FeatureVerdict(Net(scenario), tsFetch, TsThrew("ApiError", isApiError: true), cancelled: false)["verdict"]);
            Assert.Equal(VerdictPass, (string?)FeatureVerdict(Net(scenario), tsAxios, TsRefused("Error", null, code, isAxiosError: true), cancelled: false)["verdict"]);
            Assert.Equal(VerdictFail, (string?)FeatureVerdict(Net(scenario), tsAxios, TsRefused("Error", null, otherCode, isAxiosError: true), cancelled: false)["verdict"]);
            Assert.Equal(VerdictFail, (string?)FeatureVerdict(Net(scenario), tsAxios, TsRefused("Error", null, code), cancelled: false)["verdict"]);
        }

        // HttpClient.Timeout: a TaskCanceledException with an inner TimeoutException, not the caller's token
        Assert.Equal(VerdictPass, (string?)FeatureVerdict(Net("client-timeout"), client, Threw("TaskCanceledException", "TimeoutException"), cancelled: false)["verdict"]);
        Assert.Equal(VerdictFail, (string?)FeatureVerdict(Net("client-timeout"), client, Threw("TaskCanceledException", callerToken: true), cancelled: false)["verdict"]);

        // The operation's timeout is a timeout, never what the caller's own abort rejects with - also for a stalled body
        foreach (var scenario in (string[])["retry-timeout", "stall-timeout"])
        {
            Assert.Equal(VerdictPass, (string?)FeatureVerdict(Net(scenario), client, Threw("TimeoutRejectedException", "TaskCanceledException"), cancelled: false)["verdict"]);
            Assert.Equal(VerdictFail, (string?)FeatureVerdict(Net(scenario), client, Threw("TaskCanceledException", "TimeoutException"), cancelled: false)["verdict"]);
            Assert.Equal(VerdictPass, (string?)FeatureVerdict(Net(scenario), tsAxios, TsThrew("TimeoutError", "23"), cancelled: false)["verdict"]);
            Assert.Equal(VerdictFail, (string?)FeatureVerdict(Net(scenario), tsAxios, TsThrew("CanceledError", "ERR_CANCELED", isAxiosError: true), cancelled: false)["verdict"]);
        }

        // The caller's cancellation mid-body: a cancellation with the caller's token, which reaches the server
        Assert.Equal(VerdictPass, (string?)FeatureVerdict(Net("cancel-mid-body"), client, Threw("TaskCanceledException", callerToken: true), cancelled: true)["verdict"]);
        Assert.Equal(VerdictFail, (string?)FeatureVerdict(Net("cancel-mid-body"), client, Threw("TaskCanceledException", callerToken: true), cancelled: false)["verdict"]);
        Assert.Equal(VerdictFail, (string?)FeatureVerdict(Net("cancel-mid-body"), client, Threw("HttpRequestException", httpRequestException: true), cancelled: true)["verdict"]);
        Assert.Equal(VerdictPass, (string?)FeatureVerdict(Net("cancel-mid-body"), tsAxios, TsThrew("CanceledError", "ERR_CANCELED", isAxiosError: true), cancelled: true)["verdict"]);
        Assert.Equal(VerdictFail, (string?)FeatureVerdict(Net("cancel-mid-body"), tsFetch, TsThrew("TypeError"), cancelled: true)["verdict"]);

        // A ValidationException: a 400 with the validator's message and its field error (a ValidationError in TypeScript)
        var invalid = cases.Single(c => c.Kind == "invalid");
        JsonObject Rejected(
            string? errorName,
            params string[] errors)
            => new() { ["kind"] = "error", ["status"] = 400, ["errorName"] = errorName, ["detail"] = "'Name' must be a known thing.", ["errors"] = new JsonArray([.. errors.Select(e => (JsonNode)e)]) };
        Assert.Equal(VerdictPass, (string?)FeatureVerdict(invalid, client, Rejected(null, "name"), cancelled: false)["verdict"]);
        Assert.Equal(VerdictFail, (string?)FeatureVerdict(invalid, client, Rejected(null), cancelled: false)["verdict"]);
        Assert.Equal(VerdictPass, (string?)FeatureVerdict(invalid, tsFetch, Rejected("ValidationError", "name"), cancelled: false)["verdict"]);
        Assert.Equal(VerdictFail, (string?)FeatureVerdict(invalid, tsFetch, Rejected("ApiError", "name"), cancelled: false)["verdict"]);

        // Atc.Rest.MinimalApi 3.0 writes the failures as field errors, with no detail: the message reaches the caller in
        // the field error's messages, as for a 400 from the validation filter
        JsonObject FieldErrors(
            string? errorName,
            params string[] messages)
            => new() { ["kind"] = "error", ["status"] = 400, ["errorName"] = errorName, ["error"] = "One or more validation errors occurred.", ["errors"] = new JsonArray("name"), ["messages"] = new JsonArray([.. messages.Select(m => (JsonNode)m)]) };
        Assert.Equal(VerdictPass, (string?)FeatureVerdict(invalid, client, FieldErrors(null, "'Name' must be a known thing."), cancelled: false)["verdict"]);
        Assert.Equal(VerdictPass, (string?)FeatureVerdict(invalid, tsFetch, FieldErrors("ValidationError", "'Name' must be a known thing."), cancelled: false)["verdict"]);
        Assert.Equal(VerdictFail, (string?)FeatureVerdict(invalid, tsFetch, FieldErrors("ValidationError"), cancelled: false)["verdict"]);
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
                if (arguments["status"] is { } failed)
                {
                    // No item, and the status and the problem's detail reach the caller: as the client's typed error
                    // (EndpointPerOperation), or as the exception the enumeration throws
                    var status = ((int)failed).ToString(CultureInfo.InvariantCulture);
                    var text = (string?)observed["error"] ?? string.Empty;
                    var noItems = observed["items"] is not JsonArray { Count: > 0 };
                    var named = kind == "error"
                        ? (int?)observed["status"] == (int)failed
                        : text.Contains(status, StringComparison.Ordinal);
                    var detailed = text.Contains("The exchange is closed.", StringComparison.Ordinal);
                    return noItems && named && detailed
                        ? Cell(VerdictPass, kind == "error" ? "no item; the status and the problem, typed" : "no item; the error names the status and the problem's detail", text)
                        : Cell(VerdictFail, $"expected no item and an error with {status} and the problem's detail; {kind}: {text}", text);
                }

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

                if (client.Id == "cs-typed-throw")
                {
                    return Cell(VerdictPass, "the current version (the Throw style returns the body, no headers)", $"{status}");
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

                if (isCSharp && (bool?)observed["callerToken"] != true)
                {
                    return Cell(VerdictFail, $"a {(string?)observed["type"]} that does not carry the caller's token", "cancelled");
                }

                return cancelled
                    ? Cell(VerdictPass, "the client cancelled and the server saw the cancellation", "cancelled")
                    : Cell(VerdictFail, "the server never saw the cancellation: the request kept running", "cancelled");
            }

            case "net":
                return NetVerdict((string)arguments["scenario"]!, client.Id, isCSharp, observed, cancelled);

            case "invalid":
            {
                // The handler's ValidationException is the caller's mistake: a 400 whose message and field errors
                // reach the caller as the client exposes any 400 (a ValidationError in TypeScript)
                var status = (int?)observed["status"];
                var errors = observed["errors"] is JsonArray keys ? keys.Select(k => (string?)k).ToList() : [];
                var summary = $"{status} {(string?)observed["errorName"] ?? string.Empty} errors [{string.Join(",", errors)}]".Replace("  ", " ", StringComparison.Ordinal);
                if (kind != "error" || status != 400)
                {
                    return Cell(VerdictFail, $"{kind} {status}: expected a 400 ({(string?)observed["error"]})", summary);
                }

                // In the detail, the error's message or a field error's messages (Atc.Rest.MinimalApi 3.0: no detail)
                var messages = observed["messages"] is JsonArray texts ? texts.Select(t => (string?)t ?? string.Empty) : [];
                if (!messages.Prepend((string?)observed["detail"] ?? (string?)observed["error"] ?? string.Empty).Any(m => m.Contains("must be a known thing", StringComparison.Ordinal)))
                {
                    return Cell(VerdictFail, "the validator's message did not reach the caller: " + (string?)observed["error"], summary);
                }

                if (!client.Id.StartsWith("cs-", StringComparison.Ordinal) && ((string?)observed["errorName"] != "ValidationError" || (string?)observed["arm"] is not (null or "badRequest")))
                {
                    return Cell(VerdictFail, $"{(string?)observed["arm"]} {(string?)observed["errorName"]}: expected the badRequest arm's ValidationError", summary);
                }

                return errors.Contains("name", StringComparer.OrdinalIgnoreCase)
                    ? Cell(VerdictPass, "a 400 with the validator's message and its field error", summary)
                    : Cell(VerdictFail, "a 400 with the validator's message, but without its field error: the server wrote a ProblemDetails, not a ValidationProblemDetails", summary);
            }

            default:
                return Cell(VerdictFail, "unknown case kind " + featureCase.Kind, "-");
        }

        static JsonNode? Normalize(JsonNode? node)
            => node is JsonValue v && v.TryGetValue<double>(out var d) ? JsonValue.Create(d) : node;
    }

    /// <summary>
    /// A wire failure's verdict. Nothing reached the caller as a response, so no client returns a result (the C#
    /// Result style and the TypeScript result union describe responses): every client throws, and what it throws
    /// says what happened. A broken connection is the transport's own network error (C#: an
    /// <c>HttpRequestException</c>; TypeScript: fetch's <c>TypeError</c>, Axios' <c>AxiosError</c>). A timeout is a
    /// timeout, never the caller's cancellation (C#: <c>HttpClient.Timeout</c> a <c>TaskCanceledException</c> with an
    /// inner <c>TimeoutException</c>, the resilience handler's timeout Polly's <c>TimeoutRejectedException</c>;
    /// TypeScript: a <c>TimeoutError</c>), also when the headers arrived and the body stalls. The caller's
    /// cancellation is a cancellation (C#: an <c>OperationCanceledException</c> with the caller's token; TypeScript:
    /// an <c>AbortError</c>, Axios' <c>CanceledError</c>) that reaches the server.
    /// </summary>
    private static JsonObject NetVerdict(
        string scenario,
        string clientId,
        bool isCSharp,
        JsonObject observed,
        bool cancelled)
    {
        var kind = (string?)observed["kind"];
        var type = isCSharp ? (string?)observed["type"] : (string?)observed["errorName"];
        var summary = isCSharp
            ? $"{type}{((string?)observed["inner"] is { } inner ? " (" + inner + ")" : string.Empty)}"
            : $"{type}{(observed["code"] is { } code ? " " + code : string.Empty)}{(observed["cause"] is { } cause ? " (" + cause + ")" : string.Empty)}";
        if (kind == "hung")
        {
            return Cell(VerdictFail, "the call never ended (twenty seconds)", "hung");
        }

        if (scenario == "cancel-mid-body" && kind == "cancelled")
        {
            // The React Query hook: unmounted once the server held the read
            return cancelled
                ? Cell(VerdictPass, "the hook cancelled and the server saw the cancellation", "cancelled")
                : Cell(VerdictFail, "the server never saw the cancellation: the read kept running", "-");
        }

        if (kind != "threw")
        {
            return Cell(VerdictFail, $"{kind}: the call ended without an error ({observed["value"]?.ToJsonString() ?? (string?)observed["error"]})", kind ?? "-");
        }

        var error = (string?)observed["error"];
        if (!isCSharp && (bool?)observed["isApiError"] == true)
        {
            return Cell(VerdictFail, "an ApiError, which stands for an error response, for a request that got none: " + error, summary);
        }

        var isAxios = clientId == "ts-axios";
        switch (scenario)
        {
            case "reset":
                if (isCSharp)
                {
                    return (bool?)observed["httpRequestException"] == true
                        ? Cell(VerdictPass, "an HttpRequestException: the connection broke", summary)
                        : Cell(VerdictFail, "expected an HttpRequestException, got " + error, summary);
                }

                // An AxiosError made from a socket error keeps that error's name ("Error"): it is told by isAxiosError
                var isNetworkError = isAxios
                    ? (bool?)observed["isAxiosError"] == true && observed["code"]?.ToString() is not "ERR_CANCELED"
                    : type == "TypeError";
                return isNetworkError
                    ? Cell(VerdictPass, "the transport's network error: the connection broke", summary)
                    : Cell(VerdictFail, "expected the transport's network error, got " + error, summary);

            case "refused":
            case "dns":
            {
                // The request never reaches a server: the transport's network error, naming which of the two it was
                var what = scenario == "refused" ? "no server listens on the port" : "the host name does not resolve";
                if (isCSharp)
                {
                    var requestError = scenario == "refused" ? "ConnectionError" : "NameResolutionError";
                    return (bool?)observed["httpRequestException"] == true && (string?)observed["httpRequestError"] == requestError
                        ? Cell(VerdictPass, $"{what}: an HttpRequestException whose HttpRequestError is {requestError}", $"{summary} {requestError}")
                        : Cell(VerdictFail, $"{what}: expected an HttpRequestException whose HttpRequestError is {requestError}, got {error} (HttpRequestError {(string?)observed["httpRequestError"] ?? "none"})", summary);
                }

                // Without a network at all a resolver answers "try again" for any name
                string[] codes = scenario == "refused" ? ["ECONNREFUSED"] : ["ENOTFOUND", "EAI_AGAIN"];
                var networkCode = (isAxios ? observed["code"] : observed["cause"])?.ToString();
                var isTransportError = isAxios ? (bool?)observed["isAxiosError"] == true : type == "TypeError";
                return isTransportError && codes.Contains(networkCode, StringComparer.Ordinal)
                    ? Cell(VerdictPass, $"{what}: the transport's network error naming it ({networkCode}{(isAxios ? " as the AxiosError's code" : " as the TypeError's cause")})", summary)
                    : Cell(VerdictFail, $"{what}: expected {(isAxios ? "an AxiosError with the code" : "a TypeError whose cause has the code")} {string.Join(" or ", codes)}, got {error} ({networkCode ?? "no code"})", summary);
            }

            case "client-timeout":
                if (isCSharp)
                {
                    return type == "TaskCanceledException" && (string?)observed["inner"] == "TimeoutException" && (bool?)observed["callerToken"] != true
                        ? Cell(VerdictPass, "HttpClient.Timeout: a TaskCanceledException with an inner TimeoutException, not the caller's token", summary)
                        : Cell(VerdictFail, "expected HttpClient.Timeout's TaskCanceledException with an inner TimeoutException, got " + error, summary);
                }

                // The caller's own signal: its abort, as any abort the caller makes (Axios: its CanceledError)
                return type == (isAxios ? "CanceledError" : "TimeoutError")
                    ? Cell(VerdictPass, isAxios ? "the caller's signal aborted: a CanceledError, as any caller abort with Axios" : "the caller's AbortSignal.timeout: its TimeoutError", summary)
                    : Cell(VerdictFail, $"expected {(isAxios ? "a CanceledError" : "a TimeoutError")}, got " + error, summary);

            case "retry-timeout":
            case "stall-timeout":
            {
                var where = scenario == "retry-timeout" ? "no answer" : "a body that stalls after the headers";
                if (isCSharp)
                {
                    return type == "TimeoutRejectedException"
                        ? Cell(VerdictPass, $"{where}: the operation's timeout (Polly's TimeoutRejectedException)", summary)
                        : Cell(VerdictFail, $"{where}: expected the operation's timeout (TimeoutRejectedException), got " + error, summary);
                }

                return type == "TimeoutError"
                    ? Cell(VerdictPass, $"{where}: the operation's timeout, a TimeoutError", summary)
                    : Cell(VerdictFail, $"{where}: expected the operation's timeout (a TimeoutError), got " + error, summary);
            }

            case "cancel-mid-body":
            {
                var isCancellation = isCSharp
                    ? type is "TaskCanceledException" or "OperationCanceledException" && (bool?)observed["callerToken"] == true
                    : type == (isAxios ? "CanceledError" : "AbortError");
                if (!isCancellation)
                {
                    return Cell(VerdictFail, "expected the caller's cancellation" + (isCSharp ? " (an OperationCanceledException with the caller's token)" : string.Empty) + ", got " + error, summary);
                }

                return cancelled
                    ? Cell(VerdictPass, "the caller's cancellation, and the server saw it", summary)
                    : Cell(VerdictFail, "the caller's cancellation, but the server never saw it", summary);
            }

            default:
                return Cell(VerdictFail, "unknown network scenario " + scenario, "-");
        }
    }

    private static async Task<JsonObject> CollectFeatureMatrixAsync(
        bool openApi31 = false)
    {
        OpenApi31Run.Value = openApi31;
        var started = DateTimeOffset.Now;
        var cases = FeatureCases();

        await using var host = await XmlTestHost.StartAsync(
            Spec(FeatureYaml),
            FeatureHandlerSource,
            "ErrorBodies.RuntimeTest.FeatureRegistration",
            app => app.Use(async (context, next) =>
            {
                // What a stub in front of the generated endpoint answers: a value the clients' enum does not have, a
                // stream as another server writes it
                if (!await WriteFeatureStubAsync(context))
                {
                    await next(context);
                }
            }));

        var casesJson = FeatureCasesJson(cases).ToJsonString();
        var rows = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        foreach (var client in FeatureClients.Where(c => c.Marker.Length > 0))
        {
            var sources = Generate(new ApiClientGenerator(), ".atc-rest-api-client", client.Marker, includeMinimalApi: false, Spec(FeatureYaml));
            var usings = sources
                .Select(s => Regex.Match(s.Source, @"^namespace (?<ns>[\w.]+);", RegexOptions.Multiline | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1)).Groups["ns"].Value)
                .Where(ns => ns.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .Select(ns => $"global using {ns};");
            var globalUsings = string.Join(Environment.NewLine, usings) + Environment.NewLine;
            sources.Add(("FeatureProbe.cs", globalUsings + FeatureProbeSource));
            sources.Add(("FeatureProbe.Mode.cs", globalUsings + (client.Id == "cs-typed-throw" ? FeatureProbeThrowSource : client.Typed ? FeatureProbeTypedSource : FeatureProbeEndpointSource)));
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
            .Where(c => c.Arguments["cancelAfter"] is not null || c.Kind == "slow" || (string?)c.Arguments["scenario"] == "cancel-mid-body")
            .SelectMany(c => FeatureClients.Where(cl => c.Arguments["cancelAfter"] is not null || cl.Id != "ts-fetch-swr").Select(cl => cl.Id + "|" + c.Name))
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
            ["openApi"] = OpenApiVersionOf(Spec(FeatureYaml)),
            ["durationSeconds"] = System.Math.Round((DateTimeOffset.Now - started).TotalSeconds, 1),
            ["clients"] = new JsonArray([.. FeatureClients.Select(c => (JsonNode)new JsonObject { ["id"] = c.Id, ["group"] = c.Group, ["label"] = c.Label })]),
            ["cases"] = casesArray,
        };
    }

    private static JsonArray FeatureCasesJson(IEnumerable<FeatureCase> cases)
    {
        // The two cases that never reach the server are sent to an address of their own (`address`): a loopback port
        // nothing listens on, and a host under .invalid, which no resolver answers (RFC 2606)
        var refused = new Lazy<string>(ClosedLoopbackAddress);
        return new([
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

                switch ((string?)c.Arguments["scenario"])
                {
                    case "refused":
                        item["address"] = refused.Value;
                        break;
                    case "dns":
                        item["address"] = "http://atc-feature-matrix-no-such-host.invalid:8080";
                        break;
                }

                return (JsonNode)item;
            }),
        ]);
    }

    // A loopback address no server listens on: the port of a listener that was started and stopped again
    private static string ClosedLoopbackAddress()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return $"http://127.0.0.1:{port}";
    }

    private static Dictionary<string, Dictionary<string, JsonObject>> RunFeatureTypeScriptClients(
        Uri baseAddress,
        string casesJson)
    {
        var work = Path.Combine(Path.GetTempPath(), "atc-feature-e2e", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            var yamlPath = Path.Combine(work, "ErrorBodies.yaml");
            File.WriteAllText(yamlPath, Spec(FeatureYaml));

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