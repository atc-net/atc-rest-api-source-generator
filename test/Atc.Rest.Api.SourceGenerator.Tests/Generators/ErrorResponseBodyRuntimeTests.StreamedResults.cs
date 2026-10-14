namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A streamed operation of the EndpointPerOperation client has a result class like every other operation: the items
/// are its <c>Content</c>, and an error is typed - <c>Is{Status}</c>, <c>{Status}Content</c>, the <c>default</c>
/// model, the declared headers - whichever of the five framings the operation streams in.
/// </summary>
public sealed partial class ErrorResponseBodyRuntimeTests
{
    private const string StreamedResultsYaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /things/plain:
            get:
              operationId: listPlain
              parameters:
                - { name: status, in: query, required: true, schema: { type: integer, format: int32 } }
              responses:
                '200':
                  description: Things
                  headers: { X-Rate-Limit: { schema: { type: integer, format: int32 } } }
                  content: { application/json: { schema: { type: array, items: { $ref: '#/components/schemas/Thing' } } } }
                '404': { description: Missing, content: { application/json: { schema: { $ref: '#/components/schemas/Missing' } } } }
                '409': { description: Conflict, content: { application/json: { schema: { type: string } } } }
                default: { description: Error, content: { application/json: { schema: { $ref: '#/components/schemas/ApiError' } } } }
          /things/sse:
            get:
              operationId: listSse
              x-return-async-enumerable: true
              parameters:
                - { name: status, in: query, required: true, schema: { type: integer, format: int32 } }
              responses:
                '200':
                  description: Things
                  headers: { X-Rate-Limit: { schema: { type: integer, format: int32 } } }
                  content: { text/event-stream: { schema: { $ref: '#/components/schemas/Thing' } } }
                '404': { description: Missing, content: { application/json: { schema: { $ref: '#/components/schemas/Missing' } } } }
                '409': { description: Conflict, content: { application/json: { schema: { type: string } } } }
                default: { description: Error, content: { application/json: { schema: { $ref: '#/components/schemas/ApiError' } } } }
          /things/lines:
            get:
              operationId: listLines
              x-return-async-enumerable: true
              parameters:
                - { name: status, in: query, required: true, schema: { type: integer, format: int32 } }
              responses:
                '200':
                  description: Things
                  headers: { X-Rate-Limit: { schema: { type: integer, format: int32 } } }
                  content: { application/jsonl: { schema: { $ref: '#/components/schemas/Thing' } } }
                '404': { description: Missing, content: { application/json: { schema: { $ref: '#/components/schemas/Missing' } } } }
                '409': { description: Conflict, content: { application/json: { schema: { type: string } } } }
                default: { description: Error, content: { application/json: { schema: { $ref: '#/components/schemas/ApiError' } } } }
          /things/array:
            get:
              operationId: listArray
              x-return-async-enumerable: true
              parameters:
                - { name: status, in: query, required: true, schema: { type: integer, format: int32 } }
              responses:
                '200':
                  description: Things
                  headers: { X-Rate-Limit: { schema: { type: integer, format: int32 } } }
                  content: { application/json: { schema: { type: array, items: { $ref: '#/components/schemas/Thing' } } } }
                '404': { description: Missing, content: { application/json: { schema: { $ref: '#/components/schemas/Missing' } } } }
                '409': { description: Conflict, content: { application/json: { schema: { type: string } } } }
                default: { description: Error, content: { application/json: { schema: { $ref: '#/components/schemas/ApiError' } } } }
          /things/seq:
            get:
              operationId: listSeq
              x-return-async-enumerable: true
              parameters:
                - { name: status, in: query, required: true, schema: { type: integer, format: int32 } }
              responses:
                '200':
                  description: Things
                  headers: { X-Rate-Limit: { schema: { type: integer, format: int32 } } }
                  content: { application/json-seq: { schema: { $ref: '#/components/schemas/Thing' } } }
                '404': { description: Missing, content: { application/json: { schema: { $ref: '#/components/schemas/Missing' } } } }
                '409': { description: Conflict, content: { application/json: { schema: { type: string } } } }
                default: { description: Error, content: { application/json: { schema: { $ref: '#/components/schemas/ApiError' } } } }
          /things/mixed:
            get:
              operationId: listMixed
              x-return-async-enumerable: true
              parameters:
                - { name: status, in: query, required: true, schema: { type: integer, format: int32 } }
              responses:
                '200':
                  description: Things
                  headers: { X-Rate-Limit: { schema: { type: integer, format: int32 } } }
                  content: { multipart/mixed: { schema: { $ref: '#/components/schemas/Thing' } } }
                '404': { description: Missing, content: { application/json: { schema: { $ref: '#/components/schemas/Missing' } } } }
                '409': { description: Conflict, content: { application/json: { schema: { type: string } } } }
                default: { description: Error, content: { application/json: { schema: { $ref: '#/components/schemas/ApiError' } } } }
        components:
          schemas:
            Thing:
              type: object
              required: [name]
              properties:
                name: { type: string }
            Missing:
              type: object
              required: [reason]
              properties:
                reason: { type: string }
            ApiError:
              type: object
              required: [code]
              properties:
                code: { type: string }
        """;

    private const string StreamedResultsProbe = """"
        using System.Text.Json;
        using Atc.Rest.Client;
        using Microsoft.Extensions.DependencyInjection;
        using ErrorBodies.Generated;
        using ErrorBodies.Generated.Client;
        using ErrorBodies.Generated.Endpoints.Interfaces;
        using ErrorBodies.Generated.Endpoints.Results;
        using ErrorBodies.Generated.Models;

        namespace ErrorBodies.RuntimeTest;

        public static class Probe
        {
            // operation -> status -> what the result says: every Is{Status} that is true, the typed content of each, the
            // items, the declared header
            public static async Task<Dictionary<string, Dictionary<int, string>>> RunAsync(int[] statuses)
            {
                var services = new ServiceCollection();
                services
                    .AddHttpClient(Constants.HttpClientName, c => c.BaseAddress = new Uri("http://stub/"))
                    .ConfigurePrimaryHttpMessageHandler(() => new StubHandler());
                services.AddErrorBodiesEndpoints();
                using var provider = services.BuildServiceProvider();

                var calls = new Dictionary<string, Func<int, Task<object>>>
                {
                    ["Plain"] = async s => await provider.GetRequiredService<IListPlainEndpoint>().ExecuteAsync(new ListPlainParameters(s)),
                    ["Sse"] = async s => await provider.GetRequiredService<IListSseEndpoint>().ExecuteAsync(new ListSseParameters(s)),
                    ["Lines"] = async s => await provider.GetRequiredService<IListLinesEndpoint>().ExecuteAsync(new ListLinesParameters(s)),
                    ["Array"] = async s => await provider.GetRequiredService<IListArrayEndpoint>().ExecuteAsync(new ListArrayParameters(s)),
                    ["Seq"] = async s => await provider.GetRequiredService<IListSeqEndpoint>().ExecuteAsync(new ListSeqParameters(s)),
                    ["Mixed"] = async s => await provider.GetRequiredService<IListMixedEndpoint>().ExecuteAsync(new ListMixedParameters(s)),
                };

                var seen = new Dictionary<string, Dictionary<int, string>>();
                foreach (var (operation, call) in calls)
                {
                    seen[operation] = new Dictionary<int, string>();
                    foreach (var status in statuses)
                    {
                        var result = await call(status);
                        seen[operation][status] = await DescribeAsync(result);
                        (result as IDisposable)?.Dispose();
                    }
                }

                return seen;
            }

            // The id of the last event read, through the contract: before the stream is read, at each item, at its end
            public static async Task<List<string>> LastEventIdsAsync()
            {
                var services = new ServiceCollection();
                services
                    .AddHttpClient(Constants.HttpClientName, c => c.BaseAddress = new Uri("http://stub/"))
                    .ConfigurePrimaryHttpMessageHandler(() => new StubHandler());
                services.AddErrorBodiesEndpoints();
                using var provider = services.BuildServiceProvider();

                using var result = await provider.GetRequiredService<IListSseEndpoint>().ExecuteAsync(new ListSseParameters(200));
                IListSseEndpointResult contract = result;
                var seen = new List<string> { "start=" + (contract.LastEventId ?? "(null)") };
                await foreach (var item in contract.Content!)
                {
                    seen.Add(item!.Name + "=" + (contract.LastEventId ?? "(null)"));
                }

                seen.Add("end=" + (contract.LastEventId ?? "(null)"));
                return seen;
            }

            // The options a caller gives ExecuteAsync: what is told per event, and a limit on one record
            public static async Task<List<string>> StreamReadOptionsAsync()
            {
                var services = new ServiceCollection();
                services
                    .AddHttpClient(Constants.HttpClientName, c => c.BaseAddress = new Uri("http://stub/"))
                    .ConfigurePrimaryHttpMessageHandler(() => new StubHandler());
                services.AddErrorBodiesEndpoints();
                using var provider = services.BuildServiceProvider();

                var seen = new List<string>();
                var options = new StreamReadOptions { OnEvent = e => seen.Add("event id=" + (e.LastEventId ?? "(null)")) };
                using (var result = await provider.GetRequiredService<IListSseEndpoint>().ExecuteAsync(new ListSseParameters(200), streamReadOptions: options))
                {
                    await foreach (var item in result.Content!)
                    {
                        seen.Add(item!.Name);
                    }
                }

                using var limited = await provider.GetRequiredService<IListLinesEndpoint>().ExecuteAsync(new ListLinesParameters(200), streamReadOptions: new StreamReadOptions { MaxRecordLength = 5 });
                try
                {
                    await foreach (var item in limited.Content!)
                    {
                        seen.Add(item!.Name);
                    }
                }
                catch (System.IO.InvalidDataException)
                {
                    seen.Add("a line past the limit of 5 bytes");
                }

                return seen;
            }

            // What a test builds without a server: the factories of a streamed result
            public static async Task<List<string>> FactoriesAsync()
            {
                using var ok = ListSseEndpointResult.Ok(Two());
                using var missing = ListSseEndpointResult.NotFound(new Missing("nowhere"));
                using var other = ListSseEndpointResult.DefaultError(System.Net.HttpStatusCode.BadGateway, new ApiError("E502"));
                StreamingEndpointResponse<Thing> asBase = ok;
                IListSseEndpointResult asContract = missing;
                return [await DescribeAsync(asBase), await DescribeAsync(asContract), await DescribeAsync(other)];
            }

            private static async IAsyncEnumerable<Thing?> Two()
            {
                yield return new Thing("a");
                await Task.Yield();
                yield return new Thing("b");
            }

            private static async Task<string> DescribeAsync(object result)
            {
                var parts = new List<string>();
                var type = result.GetType();
                foreach (var flag in type.GetProperties().Where(p => p.PropertyType == typeof(bool) && p.Name.StartsWith("Is", StringComparison.Ordinal) && p.Name != "IsSuccess").OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    if (!(bool)flag.GetValue(result)!)
                    {
                        continue;
                    }

                    // The typed body of the status that is true; a success of a streamed operation has none (its items)
                    var content = type.GetProperty(flag.Name.Substring(2) + "Content");
                    var body = content is null || (result is not EndpointResponse && flag.Name == "IsOk")
                        ? null
                        : content.GetValue(result);
                    parts.Add(flag.Name + (content is null || body is null ? string.Empty : "=" + JsonSerializer.Serialize(body, body.GetType(), JsonSerializerOptions.Web)));
                }

                if (result is StreamingEndpointResponse<Thing> { Content: { } items })
                {
                    var names = new List<string>();
                    await foreach (var item in items)
                    {
                        names.Add(item!.Name);
                    }

                    parts.Add("items=" + string.Join(",", names));
                }
                else if (result is ListPlainEndpointResult { IsOk: true } plain)
                {
                    parts[parts.IndexOf(parts.First(p => p.StartsWith("IsOk", StringComparison.Ordinal)))] = "IsOk";
                    parts.Add("items=" + string.Join(",", plain.OkContent.Select(t => t.Name)));
                }

                parts.Add("rate=" + (type.GetProperty("XRateLimitHeader")!.GetValue(result)?.ToString() ?? "(null)"));
                return string.Join(" ", parts);
            }

            // The status the request asks for: a 200 with two things in the operation's framing, a 404 with the Missing
            // model, a 409 with a JSON string, any other error with the default model. Header names in lower case.
            private sealed class StubHandler : HttpMessageHandler
            {
                protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    var status = int.Parse(request.RequestUri!.Query.Split('=')[1], System.Globalization.CultureInfo.InvariantCulture);
                    var kind = request.RequestUri.AbsolutePath.Split('/')[^1];
                    var (body, mediaType) = status switch
                    {
                        200 => kind switch
                        {
                            "sse" => (": hello\n\ndata: {\"name\":\"t1\"}\n\nevent: ping\ndata: {\"name\":\"no item\"}\n\nid: 41\ndata: {\"name\":\"t2\"}\n\nevent: ping\nid: 42\n\n", "text/event-stream"),
                            "lines" => ("{\"name\":\"t1\"}\n\n{\"name\":\"t2\"}\n", "application/jsonl"),
                            "seq" => ("\u001e{\"name\":\"t1\"}\n\u001e{\"name\":\"t2\"}\n", "application/json-seq"),
                            "mixed" => ("--b\r\nContent-Type: application/json\r\n\r\n{\"name\":\"t1\"}\r\n--b\r\n\r\n{\"name\":\"t2\"}\r\n--b--\r\n", "multipart/mixed; boundary=b"),
                            _ => ("[{\"name\":\"t1\"},{\"name\":\"t2\"}]", "application/json"),
                        },
                        404 => ("{\"reason\":\"gone\"}", "application/json"),
                        409 => ("\"taken\"", "application/json"),
                        _ => ("{\"code\":\"E" + status + "\"}", "application/json"),
                    };

                    var response = new HttpResponseMessage((System.Net.HttpStatusCode)status)
                    {
                        RequestMessage = request,
                        Content = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(body)),
                    };
                    response.Content.Headers.TryAddWithoutValidation("content-type", mediaType);
                    response.Headers.TryAddWithoutValidation("x-rate-limit", "7");
                    return Task.FromResult(response);
                }
            }
        }
        """";

    private static readonly string[] StreamedFramings = ["Sse", "Lines", "Array", "Seq", "Mixed"];

    /// <summary>
    /// Every error status (400-599) of a streamed operation, in each of the five framings, reads as the same status
    /// of the same operation read whole: the same <c>Is{Status}</c>, the same typed body (the declared model, the
    /// JSON string, the <c>default</c> model, the problem of a status the generator adds), the same header. A success
    /// yields the items of its framing - a named event of an event stream is no item.
    /// </summary>
    [Fact]
    public async Task OperationClient_StreamedResult_IsTypedLikeAResultReadWhole_ForEveryErrorStatus()
    {
        var sources = Generate(new ApiClientGenerator(), ".atc-rest-api-client", ClientOperationMarker("ProblemDetails"), includeMinimalApi: false, StreamedResultsYaml);
        sources.Add(("Probe.cs", StreamedResultsProbe));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);
        var probe = assembly.GetType("ErrorBodies.RuntimeTest.Probe", throwOnError: true)!;
        int[] statuses = [200, .. Enumerable.Range(400, 200)];

        var seen = await (Task<Dictionary<string, Dictionary<int, string>>>)probe.GetMethod("RunAsync")!.Invoke(null, [statuses])!;

        var plain = seen["Plain"];
        Assert.Equal("IsOk items=t1,t2 rate=7", plain[200]);
        Assert.Equal("""IsNotFound={"reason":"gone"} rate=7""", plain[404]);
        Assert.Equal("""IsConflict="taken" rate=7""", plain[409]);
        Assert.Equal("""IsDefaultError={"code":"E418"} rate=7""", plain[418]);
        Assert.Equal("""IsDefaultError={"code":"E502"} rate=7""", plain[502]);

        Assert.All(
            StreamedFramings,
            framing => Assert.All(
                statuses,
                status => Assert.True(
                    plain[status] == seen[framing][status],
                    $"{framing} {status}: streamed '{seen[framing][status]}', read whole '{plain[status]}'")));

        // The result is the library's response and the generated contract: what code written against either holds
        var result = assembly.GetTypes().Single(t => t.Name == "ListSseEndpointResult");
        Assert.Equal("StreamingEndpointResponse`1", result.BaseType!.Name);
        Assert.Contains(result.GetInterfaces(), i => i.Name == "IListSseEndpointResult");
        Assert.Contains(assembly.GetTypes().Single(t => t.Name == "IListSseEndpointResult").GetInterfaces(), i => i.Name == "IStreamingEndpointResponse`1");
        Assert.Null(result.GetProperty("OkContent"));
        Assert.DoesNotContain(assembly.GetTypes(), t => t.Name == "StreamReaders");

        // An event stream's result hands out the id of the last event read - a heartbeat's too - for a resume the
        // caller writes; the result of another framing has no such member
        var ids = await (Task<List<string>>)probe.GetMethod("LastEventIdsAsync")!.Invoke(null, [])!;
        Assert.Equal(["start=(null)", "t1=(null)", "t2=41", "end=42"], ids);
        Assert.NotNull(assembly.GetTypes().Single(t => t.Name == "IListSseEndpointResult").GetProperty("LastEventId"));
        Assert.Null(assembly.GetTypes().Single(t => t.Name == "IListLinesEndpointResult").GetProperty("LastEventId"));

        // The options a caller hands ExecuteAsync reach the reader: each item's event is told before the item, and a
        // limit of the caller's own stops a record that is longer
        var withOptions = await (Task<List<string>>)probe.GetMethod("StreamReadOptionsAsync")!.Invoke(null, [])!;
        Assert.Equal(["event id=(null)", "t1", "event id=41", "t2", "a line past the limit of 5 bytes"], withOptions);

        var built = await (Task<List<string>>)probe.GetMethod("FactoriesAsync")!.Invoke(null, [])!;
        Assert.Equal(
            [
                "IsOk items=a,b rate=(null)",
                """IsNotFound={"reason":"nowhere"} rate=(null)""",
                """IsDefaultError={"code":"E502"} rate=(null)""",
            ],
            built);
    }
}