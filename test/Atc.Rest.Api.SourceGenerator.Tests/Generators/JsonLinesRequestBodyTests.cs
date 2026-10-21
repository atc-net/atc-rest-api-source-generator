namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A request body declared as JSON Lines (<c>application/jsonl</c> and its aliases) or as JSON Text Sequences
/// (<c>application/json-seq</c>) is a stream of records: the handler gets a <c>StreamedRequestBody&lt;T&gt;</c> it reads
/// with <c>await foreach</c>, one record at a time and validated as it arrives. A record that is not valid stops the
/// read with a 400 that names the record (<c>[1].value</c>), as for a JSON list body. Every case runs on the generated
/// host and through the Request Delegate Generator (a Native AOT build).
/// </summary>
public sealed class JsonLinesRequestBodyTests
{
    internal const string Yaml = """
        openapi: 3.1.1
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /readings:
            post:
              operationId: importReadings
              requestBody:
                required: true
                content:
                  application/jsonl:
                    schema:
                      $ref: '#/components/schemas/Reading'
              responses:
                '200':
                  description: Imported
                  content:
                    text/plain:
                      schema:
                        type: string
          /readings/sequence:
            post:
              operationId: importReadingSequence
              requestBody:
                required: true
                content:
                  application/json-seq:
                    schema:
                      type: array
                      minItems: 1
                      maxItems: 3
                      items:
                        $ref: '#/components/schemas/Reading'
              responses:
                '200':
                  description: Imported
                  content:
                    text/plain:
                      schema:
                        type: string
          /readings/either:
            post:
              operationId: importReadingsAnyFormat
              requestBody:
                required: true
                content:
                  application/jsonl:
                    schema:
                      $ref: '#/components/schemas/Reading'
                  application/json:
                    schema:
                      type: array
                      items:
                        $ref: '#/components/schemas/Reading'
              responses:
                '200':
                  description: Imported
                  content:
                    text/plain:
                      schema:
                        type: string
          /readings/optional:
            put:
              operationId: updateReadings
              requestBody:
                required: false
                content:
                  application/x-ndjson:
                    schema:
                      $ref: '#/components/schemas/Reading'
              responses:
                '200':
                  description: Replaced
                  content:
                    text/plain:
                      schema:
                        type: string
          /readings/normalize:
            post:
              operationId: normalizeReadings
              requestBody:
                required: true
                content:
                  application/jsonl:
                    schema:
                      $ref: '#/components/schemas/Reading'
              responses:
                '200':
                  description: The readings, normalized
                  content:
                    application/jsonl:
                      schema:
                        $ref: '#/components/schemas/Reading'
          /readings/export:
            get:
              operationId: listReadings
              responses:
                '200':
                  description: The readings, without x-return-async-enumerable
                  content:
                    application/x-ndjson:
                      schema:
                        $ref: '#/components/schemas/Reading'
        components:
          schemas:
            Reading:
              title: Reading
              type: object
              required: [deviceId, value]
              properties:
                deviceId:
                  type: string
                  minLength: 1
                  maxLength: 64
                value:
                  type: number
                  format: double
                  minimum: -100
                  maximum: 100
        """;

    internal const string HandlerSource = """"
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public static class ReadingLog
        {
            public static async System.Threading.Tasks.Task<string> ReadAsync(System.Collections.Generic.IAsyncEnumerable<Reading> readings, System.Threading.CancellationToken cancellationToken)
            {
                var ids = new System.Collections.Generic.List<string>();
                await foreach (var reading in readings.WithCancellation(cancellationToken))
                {
                    ids.Add(reading.DeviceId);
                }

                return $"{ids.Count} {string.Join(",", ids)}";
            }
        }

        public sealed class ImportReadingsHandlerImpl : IImportReadingsHandler
        {
            public async System.Threading.Tasks.Task<ImportReadingsResult> ExecuteAsync(ImportReadingsParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => ImportReadingsResult.Ok(await ReadingLog.ReadAsync(parameters.Request, cancellationToken));
        }

        public sealed class ImportReadingSequenceHandlerImpl : IImportReadingSequenceHandler
        {
            public async System.Threading.Tasks.Task<ImportReadingSequenceResult> ExecuteAsync(ImportReadingSequenceParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => ImportReadingSequenceResult.Ok(await ReadingLog.ReadAsync(parameters.Request, cancellationToken));
        }

        public sealed class ImportReadingsAnyFormatHandlerImpl : IImportReadingsAnyFormatHandler
        {
            public async System.Threading.Tasks.Task<ImportReadingsAnyFormatResult> ExecuteAsync(ImportReadingsAnyFormatParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => ImportReadingsAnyFormatResult.Ok(await ReadingLog.ReadAsync(parameters.Request, cancellationToken));
        }

        public sealed class UpdateReadingsHandlerImpl : IUpdateReadingsHandler
        {
            public async System.Threading.Tasks.Task<UpdateReadingsResult> ExecuteAsync(UpdateReadingsParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => UpdateReadingsResult.Ok(await ReadingLog.ReadAsync(parameters.Request, cancellationToken));
        }

        public sealed class NormalizeReadingsHandlerImpl : INormalizeReadingsHandler
        {
            public System.Threading.Tasks.Task<NormalizeReadingsResult> ExecuteAsync(NormalizeReadingsParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(NormalizeReadingsResult.Ok(Normalize(parameters.Request, cancellationToken)));

            private static async System.Collections.Generic.IAsyncEnumerable<Reading> Normalize(
                System.Collections.Generic.IAsyncEnumerable<Reading> readings,
                [System.Runtime.CompilerServices.EnumeratorCancellation] System.Threading.CancellationToken cancellationToken = default)
            {
                await foreach (var reading in readings.WithCancellation(cancellationToken))
                {
                    yield return reading with { DeviceId = reading.DeviceId.ToUpperInvariant() };
                }
            }
        }

        public sealed class ListReadingsHandlerImpl : IListReadingsHandler
        {
            public System.Threading.Tasks.Task<ListReadingsResult> ExecuteAsync(
                System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(ListReadingsResult.Ok(Two()));

            private static async System.Collections.Generic.IAsyncEnumerable<Reading> Two()
            {
                yield return new Reading("a", 1);
                await System.Threading.Tasks.Task.Yield();
                yield return new Reading("b", 2);
            }
        }

        public static class ReadingRegistration
        {
            public static void Register(IServiceCollection services)
            {
                services.AddScoped<IImportReadingsHandler, ImportReadingsHandlerImpl>();
                services.AddScoped<IImportReadingSequenceHandler, ImportReadingSequenceHandlerImpl>();
                services.AddScoped<IImportReadingsAnyFormatHandler, ImportReadingsAnyFormatHandlerImpl>();
                services.AddScoped<IUpdateReadingsHandler, UpdateReadingsHandlerImpl>();
                services.AddScoped<INormalizeReadingsHandler, NormalizeReadingsHandlerImpl>();
                services.AddScoped<IListReadingsHandler, ListReadingsHandlerImpl>();
            }
        }
        """";

    private const string Rs = "\u001e";

    public static TheoryData<bool> Hosts() => new() { false, true };

    [Fact]
    public void Server_TheBodyIsAStreamedRequestBodyOfTheRecord()
    {
        var parameters = ServerSource("Parameters");

        Assert.Contains("StreamedRequestBody<Reading> Request", parameters, StringComparison.Ordinal);
        Assert.DoesNotContain("[property: FromBody", parameters, StringComparison.Ordinal);

        // minItems / maxItems are the reader's: a count attribute on a stream would throw when it is validated
        Assert.DoesNotContain("MinLength(1)", parameters, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxLength(3)", parameters, StringComparison.Ordinal);
    }

    [Fact]
    public void Server_TheEndpointAcceptsTheDeclaredMediaTypes_AndAnswersARejectedRecordAsAProblem()
    {
        var endpoints = ServerSource("EndpointDefinition");

        Assert.Contains(".Accepts<Reading>(\"application/jsonl\"", endpoints, StringComparison.Ordinal);
        Assert.Contains(".Accepts<Reading>(\"application/json-seq\"", endpoints, StringComparison.Ordinal);
        Assert.Contains(".WithRequestBodyProblems()", endpoints, StringComparison.Ordinal);
    }

    [Fact]
    public void Server_TheStreamedRequestBodyCompilesWithoutWarnings()
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", ErrorResponseBodyRuntimeTests.ServerMarker, includeMinimalApi: true, Yaml);

        Assert.Empty(CompilationVerificationHarness.CompileGeneratedSourcesForWarnings(sources, includeMinimalApi: true));
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    public async Task LiveHost_TheHandlerReadsEveryRecord(bool rdg)
    {
        await using var host = await StartAsync(rdg);

        Assert.Equal("200 text/plain 3 a,b,c", await Post(host, "/readings", "application/jsonl", Lines("a", "b", "c")));

        // CRLF, a blank line, no newline after the last record
        Assert.Equal("200 text/plain 2 a,b", await Post(host, "/readings", "application/jsonl", "{\"deviceId\":\"a\",\"value\":1}\r\n\r\n{\"deviceId\":\"b\",\"value\":2}"));

        // No records: an empty import
        Assert.Equal("200 text/plain 0 ", await Post(host, "/readings", "application/jsonl", string.Empty));
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    public async Task LiveHost_EveryJsonLinesNameIsAccepted_WhereverOneIsDeclared(
        bool rdg)
    {
        await using var host = await StartAsync(rdg);

        Assert.Equal("200 text/plain 1 a", await Post(host, "/readings", "application/x-ndjson", Lines("a")));
        Assert.Equal("200 text/plain 1 a", await Post(host, "/readings", "application/x-jsonlines", Lines("a")));
        Assert.Equal("200 text/plain 1 a", await Post(host, "/readings", "application/jsonl; charset=utf-8", Lines("a")));
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    public async Task LiveHost_AnInvalidRecordIsA400NamingTheRecord(bool rdg)
    {
        await using var host = await StartAsync(rdg);

        Assert.Equal(
            "400 application/problem+json errors=[1].value",
            await Post(host, "/readings", "application/jsonl", "{\"deviceId\":\"a\",\"value\":1}\n{\"deviceId\":\"b\",\"value\":500}\n"));
        Assert.Equal(
            "400 application/problem+json errors=[0].deviceId",
            await Post(host, "/readings", "application/jsonl", "{\"value\":1}\n"));
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    public async Task LiveHost_ALineThatIsNotJsonIsA400NamingTheRecordAndTheLine(
        bool rdg)
    {
        await using var host = await StartAsync(rdg);

        var answer = await Post(host, "/readings", "application/jsonl", "{\"deviceId\":\"a\",\"value\":1}\n{\"deviceId\":\n", withMessages: true);

        Assert.StartsWith("400 application/problem+json errors=[1]: ", answer, StringComparison.Ordinal);
        Assert.Contains("line 2", answer, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    public async Task LiveHost_AValueOfTheWrongTypeIsA400NamingTheMember(
        bool rdg)
    {
        await using var host = await StartAsync(rdg);

        Assert.Equal(
            "400 application/problem+json errors=[1].value",
            await Post(host, "/readings", "application/jsonl", "{\"deviceId\":\"a\",\"value\":1}\n{\"deviceId\":\"b\",\"value\":\"high\"}\n"));
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    public async Task LiveHost_ANullRecordIsA400(bool rdg)
    {
        await using var host = await StartAsync(rdg);

        Assert.Equal("400 application/problem+json errors=[0]", await Post(host, "/readings", "application/jsonl", "null\n"));
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    public async Task LiveHost_AnUndeclaredMediaTypeIs415(bool rdg)
    {
        await using var host = await StartAsync(rdg);

        // A JSON array is not JSON Lines: only an operation that declares application/json too takes one
        Assert.StartsWith("415 application/problem+json", await Post(host, "/readings", "application/json", "[{\"deviceId\":\"a\",\"value\":1}]"), StringComparison.Ordinal);
        Assert.StartsWith("415 application/problem+json", await Post(host, "/readings", "application/json-seq", Rs + "{\"deviceId\":\"a\",\"value\":1}\n"), StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    public async Task LiveHost_ARequiredBodyThatIsMissingIs400_AnOptionalOneIsNoRecords(
        bool rdg)
    {
        await using var host = await StartAsync(rdg);

        var missing = await host.SendRawAsync(HttpMethod.Post, "/readings", accept: null);
        var optional = await host.SendRawAsync(HttpMethod.Put, "/readings/optional", accept: null);

        Assert.Equal((400, "application/problem+json"), (missing.Status, missing.MediaType));
        Assert.Equal((200, "0 "), (optional.Status, optional.Body));
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    public async Task LiveHost_AJsonTextSequence_IsReadRecordByRecord_WithinItsCount(
        bool rdg)
    {
        await using var host = await StartAsync(rdg);

        Assert.Equal(
            "200 text/plain 2 a,b",
            await Post(host, "/readings/sequence", "application/json-seq", $"{Rs}{{\"deviceId\":\"a\",\"value\":1}}\n{Rs}{{\n  \"deviceId\": \"b\",\n  \"value\": 2\n}}\n"));

        // maxItems: 3 - the fourth record is one too many; minItems: 1 - no records is too few
        Assert.StartsWith("400 application/problem+json", await Post(host, "/readings/sequence", "application/json-seq", string.Concat(Enumerable.Range(0, 4).Select(i => $"{Rs}{{\"deviceId\":\"d{i}\",\"value\":1}}\n"))), StringComparison.Ordinal);
        Assert.StartsWith("400 application/problem+json", await Post(host, "/readings/sequence", "application/json-seq", string.Empty), StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    public async Task LiveHost_ABodyOfferedAsJsonLinesOrAJsonArray_IsTheSameStreamToTheHandler(
        bool rdg)
    {
        await using var host = await StartAsync(rdg);

        Assert.Equal("200 text/plain 2 a,b", await Post(host, "/readings/either", "application/jsonl", Lines("a", "b")));
        Assert.Equal("200 text/plain 2 a,b", await Post(host, "/readings/either", "application/json", "[{\"deviceId\":\"a\",\"value\":1},{\"deviceId\":\"b\",\"value\":2}]"));
        Assert.Equal(
            "400 application/problem+json errors=[1].value",
            await Post(host, "/readings/either", "application/json", "[{\"deviceId\":\"a\",\"value\":1},{\"deviceId\":\"b\",\"value\":500}]"));
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    public async Task LiveHost_AStreamInAndAStreamOut(bool rdg)
    {
        await using var host = await StartAsync(rdg);

        var answer = await host.SendRawAsync(HttpMethod.Post, "/readings/normalize", accept: null, "application/jsonl", Lines("a", "b"));

        Assert.Equal(200, answer.Status);
        Assert.Equal("application/jsonl", answer.MediaType);
        Assert.Equal(["A", "B"], answer.Body.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => JsonNode.Parse(l)!["deviceId"]!.GetValue<string>()), StringComparer.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    public async Task LiveHost_AResponseDeclaredByItsMediaTypeAlone_StreamsWithThatMediaType(
        bool rdg)
    {
        await using var host = await StartAsync(rdg);

        var answer = await host.SendRawAsync(HttpMethod.Get, "/readings/export", accept: null);

        Assert.Equal(200, answer.Status);
        Assert.Equal("application/x-ndjson", answer.MediaType);
        Assert.Equal(2, answer.Body.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public async Task LiveHost_ARecordLongerThanTheLimitIs413()
    {
        await using var host = await StartAsync(rdg: false);

        var huge = $"{{\"deviceId\":\"{new string('x', (16 * 1024 * 1024) + 1)}\",\"value\":1}}\n";

        Assert.StartsWith("413 application/problem+json", await Post(host, "/readings", "application/jsonl", huge), StringComparison.Ordinal);
    }

    private static string Lines(params string[] deviceIds)
        => string.Concat(deviceIds.Select(id => $"{{\"deviceId\":\"{id}\",\"value\":1}}\n"));

    private static Task<XmlTestHost> StartAsync(bool rdg)
        => XmlTestHost.StartAsync(
            Yaml,
            HandlerSource,
            "ErrorBodies.RuntimeTest.ReadingRegistration",
            configure: app => app.Use((context, next) =>
            {
                // The long-record case sends more than Kestrel's default 30 MB body limit allows only when it is lifted
                if (context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
                {
                    limit.MaxRequestBodySize = null;
                }

                return next(context);
            }),
            useRequestDelegateGenerator: rdg);

    private static Task<string> Post(
        XmlTestHost host,
        string path,
        string contentType,
        string body,
        bool withMessages = false)
        => host.PostAsync(path, contentType, body, accept: null, withMessages);

    private static string ServerSource(string hintPart)
        => string.Join(
            Environment.NewLine,
            ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", ErrorResponseBodyRuntimeTests.ServerMarker, includeMinimalApi: true, Yaml)
                .Where(s => s.HintName.Contains(hintPart, StringComparison.Ordinal))
                .Select(s => s.Source));
}