namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// Wire-byte round-trip tests for the emitted <c>StreamReaders</c> helper. The generated
/// client is compiled and loaded, then its stream readers are invoked over a MemoryStream of
/// known wire bytes — proving the emitted reader parses real framing without hosting a server.
/// </summary>
public class StreamingWireFramingTests
{
    [Fact]
    public async Task StreamReaders_ServerSentEvents_ReadsItems()
    {
        var streamReaders = LoadGeneratedType("StreamReaders");
        var read = streamReaders
            .GetMethod("ReadServerSentEventsAsync")
            .MakeGenericMethod(typeof(JsonElement));

        const string sse = "data: {\"id\":\"a\",\"type\":\"x\"}\n\ndata: {\"id\":\"b\",\"type\":\"y\"}\n\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(sse));
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var items = await EnumerateAsync<JsonElement>(read, stream, options);

        Assert.Equal(2, items.Count);
        Assert.Equal("a", items[0].GetProperty("id").GetString());
        Assert.Equal("b", items[1].GetProperty("id").GetString());
    }

    /// <summary>
    /// What a real event stream carries besides the items: a comment (keep-alive), an event without data, a heartbeat
    /// (<c>event: ping</c>, with data that is no item, with data that is JSON, without data) and other named events.
    /// The item event is the unnamed one, as for a browser's <c>EventSource.onmessage</c> - also right after a named
    /// event without data; the rest is read past instead of failing the stream or being taken for an item.
    /// </summary>
    [Fact]
    public async Task StreamReaders_ServerSentEvents_SkipsWhatIsNoItem()
    {
        var streamReaders = LoadGeneratedType("StreamReaders");
        var read = streamReaders
            .GetMethod("ReadServerSentEventsAsync")
            .MakeGenericMethod(typeof(JsonElement));
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(EventStreamWithNamedEvents));

        var items = await EnumerateAsync<JsonElement>(read, stream, new JsonSerializerOptions());

        Assert.Equal(["b", "c", "d"], items.Select(i => i.GetProperty("id").GetString()), StringComparer.Ordinal);
    }

    /// <summary>
    /// A byte order mark in front of the first field is no part of its name, and an <c>event:</c> field without a
    /// value names no event: both events are the item event.
    /// </summary>
    [Fact]
    public async Task StreamReaders_ServerSentEvents_AByteOrderMarkAndAnEmptyEventName_AreNoName()
    {
        var read = LoadGeneratedType("StreamReaders")
            .GetMethod("ReadServerSentEventsAsync")
            .MakeGenericMethod(typeof(JsonElement));
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("\uFEFFdata: {\"id\":\"a\"}\n\nevent:\ndata: {\"id\":\"b\"}\n\nevent: \ndata: {\"id\":\"c\"}\n\n"));

        var items = await EnumerateAsync<JsonElement>(read, stream, new JsonSerializerOptions());

        Assert.Equal(["a", "b", "c"], items.Select(i => i.GetProperty("id").GetString()), StringComparer.Ordinal);
    }

    /// <summary>
    /// An API whose items come in named events says so with <c>x-sse-item-events</c>: those events are items too,
    /// by their exact name, and every other named event is still read past.
    /// </summary>
    [Fact]
    public async Task StreamReaders_ServerSentEvents_TheNamedEventsOfTheOperation_AreItems()
    {
        var streamReaders = LoadGeneratedType("StreamReaders");
        var read = streamReaders
            .GetMethod("ReadServerSentEventsOfAsync")
            .MakeGenericMethod(typeof(JsonElement));
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(EventStreamWithNamedEvents));

        var items = await EnumerateWithArgsAsync<JsonElement>(read, [stream, new JsonSerializerOptions(), new[] { "update" }, CancellationToken.None]);

        Assert.Equal(["a", "b", "c", "d"], items.Select(i => i.GetProperty("id").GetString()), StringComparer.Ordinal);
    }

    /// <summary>
    /// A JSON array with no body - a 204, a 200 without content - is no items; an array after whitespace, and one
    /// behind a byte order mark, is read as ever, and what is no array is still the serializer's to refuse.
    /// </summary>
    [Theory]
    [InlineData("", "")]
    [InlineData(" \r\n\t ", "")]
    [InlineData("[]", "")]
    [InlineData(" \n [ {\"id\":\"a\"} , {\"id\":\"b\"} ] ", "a,b")]
    [InlineData("\uFEFF[{\"id\":\"a\"}]", "a")]
    public async Task StreamReaders_JsonArray_ABodyOfNothing_IsNoItems(
        string body,
        string expected)
    {
        var read = LoadGeneratedType("StreamReaders")
            .GetMethod("ReadJsonArrayAsync")
            .MakeGenericMethod(typeof(JsonElement));
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(body));

        var items = await EnumerateAsync<JsonElement>(read, stream, new JsonSerializerOptions());

        Assert.Equal(expected, string.Join(",", items.Select(i => i.GetProperty("id").GetString())));

        using var noArray = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
        await Assert.ThrowsAnyAsync<JsonException>(() => EnumerateAsync<JsonElement>(read, noArray, new JsonSerializerOptions()));
    }

    /// <summary>
    /// An unnamed event - the item event of a stream - whose data is no JSON is the server's mistake and fails the
    /// stream, as before: only named events (control frames) are skipped.
    /// </summary>
    [Fact]
    public async Task StreamReaders_ServerSentEvents_AnUnnamedEventThatIsNoJson_Throws()
    {
        var streamReaders = LoadGeneratedType("StreamReaders");
        var read = streamReaders
            .GetMethod("ReadServerSentEventsAsync")
            .MakeGenericMethod(typeof(JsonElement));
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("data: not json\n\n"));

        await Assert.ThrowsAnyAsync<JsonException>(() => EnumerateAsync<JsonElement>(read, stream, new JsonSerializerOptions()));
    }

    [Fact]
    public async Task JsonLines_WriteThenRead_RoundTrips()
    {
        var writerType = LoadGeneratedServerType("SequentialStreamWriter");
        var readerType = LoadGeneratedType("StreamReaders");
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var source = new[]
        {
            JsonSerializer.SerializeToElement(new { id = "a", type = "x" }),
            JsonSerializer.SerializeToElement(new { id = "b", type = "y" }),
        };

        using var ms = new MemoryStream();
        await InvokeWriteAsync(writerType, "WriteJsonLinesAsync", source, ms, options);
        var bytes = ms.ToArray();

        var text = Encoding.UTF8.GetString(bytes);
        Assert.EndsWith("\n", text, StringComparison.Ordinal);
        Assert.Equal(2, text.TrimEnd('\n').Split('\n').Length);
        Assert.DoesNotContain("[", text, StringComparison.Ordinal); // not a JSON array

        using var readStream = new MemoryStream(bytes);
        var read = readerType
            .GetMethod("ReadJsonLinesAsync")
            .MakeGenericMethod(typeof(JsonElement));
        var items = await EnumerateAsync<JsonElement>(read, readStream, options);
        Assert.Equal(2, items.Count);
        Assert.Equal("a", items[0].GetProperty("id").GetString());
    }

    [Fact]
    public async Task JsonLinesResult_ExecuteAsync_UsesConfiguredOptions_AndAsyncIo()
    {
        // Load the emitted server assembly and resolve JsonLinesResult<Event> + Event.
        var serverAssembly = CompilationVerificationHarness.EmitAndLoad(
            CompilationVerificationHarness.RunServer("StreamingItemSchema", "StreamingItemSchema.yaml"));
        var eventType = serverAssembly.GetTypes().Single(t => t.Name == "Event");
        var jsonLinesResultOpen = serverAssembly.GetTypes().Single(t => t.Name == "JsonLinesResult`1");
        var jsonLinesResultType = jsonLinesResultOpen.MakeGenericType(eventType);

        // Build an IAsyncEnumerable<Event> of one item via reflection.
        var eventId = Guid.NewGuid();
        var items = BuildAsyncEnumerable(eventType, [CreateEvent(eventType, eventId, "x")]);
        var result = Activator.CreateInstance(jsonLinesResultType, items);

        // RequestServices configured exactly like a real API: camelCase + enum-as-string via
        // ConfigureHttpJsonOptions (registered through IOptions<JsonOptions>, NOT a direct service).
        var services = new ServiceCollection();
        services.AddLogging();
        services.ConfigureHttpJsonOptions(o =>
        {
            o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });
        var provider = services.BuildServiceProvider();

        var innerStream = new MemoryStream();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = provider,
        };
        httpContext.Response.Body = new ThrowOnSyncWriteStream(innerStream);

        // C1: must NOT throw (the writer uses WriteAsync, not the sync WriteByte that Kestrel rejects).
        var executeAsync = jsonLinesResultType.GetMethod("ExecuteAsync");
        await (Task)executeAsync.Invoke(result, [httpContext]);

        Assert.Equal("application/jsonl", httpContext.Response.ContentType);

        var text = Encoding.UTF8.GetString(innerStream.ToArray());
        Assert.EndsWith("\n", text, StringComparison.Ordinal);

        // C2: camelCase property names prove the configured IOptions<JsonOptions> was used, not the
        // bare-default fallback (which would emit PascalCase "Id"/"Type").
        using var doc = JsonDocument.Parse(text.TrimEnd('\n'));
        Assert.True(doc.RootElement.TryGetProperty("id", out var idProp));
        Assert.Equal(eventId.ToString(), idProp.GetString());
        Assert.True(doc.RootElement.TryGetProperty("type", out _));
        Assert.False(doc.RootElement.TryGetProperty("Id", out _));
    }

    [Fact]
    public async Task JsonSequence_WriteThenRead_RoundTrips()
    {
        var writerType = LoadGeneratedServerType("SequentialStreamWriter");
        var readerType = LoadGeneratedType("StreamReaders");
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var source = new[]
        {
            JsonSerializer.SerializeToElement(new { id = "a", type = "x" }),
            JsonSerializer.SerializeToElement(new { id = "b", type = "y" }),
        };

        using var ms = new MemoryStream();
        await InvokeWriteAsync(writerType, "WriteJsonSequenceAsync", source, ms, options);
        var bytes = ms.ToArray();

        // RFC 7464 framing: each record is RS(0x1E) <json> LF(0x0A). Two records => two RS bytes,
        // and the stream must start with RS.
        Assert.Contains((byte)0x1E, bytes);
        Assert.Equal(0x1E, bytes[0]);
        Assert.Equal(2, bytes.Count(b => b == 0x1E));

        using var readStream = new MemoryStream(bytes);
        var read = readerType
            .GetMethod("ReadJsonSequenceAsync")
            .MakeGenericMethod(typeof(JsonElement));
        var items = await EnumerateAsync<JsonElement>(read, readStream, options);
        Assert.Equal(2, items.Count);
        Assert.Equal("a", items[0].GetProperty("id").GetString());
        Assert.Equal("b", items[1].GetProperty("id").GetString());
    }

    [Fact]
    public async Task JsonSequence_Reader_HandlesEmbeddedNewlines()
    {
        var readerType = LoadGeneratedType("StreamReaders");
        var read = readerType
            .GetMethod("ReadJsonSequenceAsync")
            .MakeGenericMethod(typeof(JsonElement));
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // RFC 7464: records are RS(0x1E)-delimited and MAY contain embedded LFs (pretty-printed JSON
        // from a third-party producer). An LF-splitting reader mis-parses this; an RS-splitting one
        // does not, because RS cannot appear inside a JSON value (control chars are escaped).
        const string rs = "\u001e";
        var seq =
            rs + "{\n  \"id\": \"a\",\n  \"type\": \"x\"\n}\n" +
            rs + "{\n  \"id\": \"b\",\n  \"type\": \"y\"\n}\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(seq));

        var items = await EnumerateAsync<JsonElement>(read, stream, options);
        Assert.Equal(2, items.Count);
        Assert.Equal("a", items[0].GetProperty("id").GetString());
        Assert.Equal("b", items[1].GetProperty("id").GetString());
    }

    /// <summary>
    /// A record past <c>StreamReaders.MaxRecordLength</c> (a server that never ends one) is an
    /// <see cref="InvalidDataException"/>, not a buffer that grows until the process runs out of memory.
    /// </summary>
    [Theory]
    [InlineData("ReadJsonSequenceAsync", "\u001e{\"id\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"}\n")]
    [InlineData("ReadMultipartMixedAsync", "--b\r\nContent-Type: application/json\r\n\r\n{\"id\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"}\r\n--b--\r\n")]
    [InlineData("ReadMultipartMixedAsync", "--b\r\nContent-Type: application/json; a-parameter-that-never-ends-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("ReadJsonLinesAsync", "{\"id\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"}\n")]
    [InlineData("ReadJsonLinesAsync", "{\"id\":\"a line that never ends aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("ReadServerSentEventsAsync", "data: {\"id\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"}\n\n")]
    [InlineData("ReadServerSentEventsAsync", ": a comment that never ends aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("ReadServerSentEventsAsync", "data: [1,\ndata: 2,\ndata: 3,\ndata: 4,\ndata: 5]\n\n")]
    [InlineData("ReadServerSentEventsAsync", "data: [1,\r\ndata: 2,\r\ndata: 3,\r\ndata: 4,\r\ndata: 5,\r\n")]
    [InlineData("ReadJsonArrayAsync", "[{\"id\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"}]")]
    [InlineData("ReadJsonArrayAsync", "[1, {\"id\":\"an element that never ends aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("ReadJsonArrayAsync", "[\"a,b,c,d,e,f,g,h,i,j,k,l,m,n,o,p,q,r\"]")]
    public async Task Reader_ARecordPastTheMaximum_IsInvalidData(
        string readerName,
        string wire)
    {
        var readerType = LoadGeneratedType("StreamReaders");
        readerType.GetProperty("MaxRecordLength")!.SetValue(null, 16);
        var read = readerType.GetMethods().Single(m => m.Name == readerName && m.GetParameters()[0].ParameterType == typeof(Stream)).MakeGenericMethod(typeof(JsonElement));
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(wire));

        var arguments = read.GetParameters().Length switch
        {
            3 => new object?[] { stream, new JsonSerializerOptions(), CancellationToken.None },
            _ => new object?[] { stream, "b", new JsonSerializerOptions(), CancellationToken.None },
        };
        var items = (IAsyncEnumerable<JsonElement>)read.Invoke(null, arguments)!;

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await foreach (var item in items)
            {
                Assert.NotEqual(JsonValueKind.Undefined, item.ValueKind);
            }
        });
    }

    /// <summary>
    /// The limit is per record, not per stream: records that each fit are all read from a stream longer than the
    /// limit - whichever line ending the server writes.
    /// </summary>
    [Theory]
    [InlineData("ReadJsonLinesAsync", "1234567\n1234567\n1234567\n1234567\n")]
    [InlineData("ReadJsonLinesAsync", "1234567\r\n1234567\r\n1234567\r\n1234567")]
    [InlineData("ReadServerSentEventsAsync", "data: 1234567\n\ndata: 1234567\n\ndata: 1234567\n\ndata: 1234567\n\n")]
    [InlineData("ReadServerSentEventsAsync", "data: 1234567\r\n\r\ndata: 1234567\r\n\r\ndata: 1234567\r\n\r\ndata: 1234567\r\n\r\n")]
    [InlineData("ReadServerSentEventsAsync", "data: 1234567\r\rdata: 1234567\r\rdata: 1234567\r\rdata: 1234567\r\r")]
    [InlineData("ReadServerSentEventsAsync", ": keep-alive\n\nid:1\ndata:1234567\n\n: keep-alive\n\ndata: 1234567\n\ndata: 1234567\n\ndata: 1234567\n\n")]
    [InlineData("ReadJsonArrayAsync", "[1234567,1234567,1234567,1234567]")]
    [InlineData("ReadJsonArrayAsync", "[\n  1234567,\n  1234567,\n  1234567,\n  1234567\n]")]
    public async Task Reader_RecordsWithinTheMaximum_AreAllRead(
        string readerName,
        string wire)
    {
        var readerType = LoadGeneratedType("StreamReaders");
        readerType.GetProperty("MaxRecordLength")!.SetValue(null, 16);
        var read = readerType.GetMethods().Single(m => m.Name == readerName && m.GetParameters()[0].ParameterType == typeof(Stream)).MakeGenericMethod(typeof(JsonElement));
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(wire));

        var items = (IAsyncEnumerable<JsonElement>)read.Invoke(null, [stream, new JsonSerializerOptions(), CancellationToken.None])!;

        var numbers = new List<int>();
        await foreach (var item in items)
        {
            numbers.Add(item.GetInt32());
        }

        Assert.Equal([1234567, 1234567, 1234567, 1234567], numbers);
    }

    [Fact]
    public async Task JsonSequenceResult_ExecuteAsync_UsesConfiguredOptions_AndAsyncIo()
    {
        // Load the emitted server assembly and resolve JsonSequenceResult<Event> + Event.
        var serverAssembly = CompilationVerificationHarness.EmitAndLoad(
            CompilationVerificationHarness.RunServer("StreamingItemSchema", "StreamingItemSchema.yaml"));
        var eventType = serverAssembly.GetTypes().Single(t => t.Name == "Event");
        var jsonSequenceResultOpen = serverAssembly.GetTypes().Single(t => t.Name == "JsonSequenceResult`1");
        var jsonSequenceResultType = jsonSequenceResultOpen.MakeGenericType(eventType);

        // Build an IAsyncEnumerable<Event> of one item via reflection.
        var eventId = Guid.NewGuid();
        var items = BuildAsyncEnumerable(eventType, [CreateEvent(eventType, eventId, "x")]);
        var result = Activator.CreateInstance(jsonSequenceResultType, items);

        // RequestServices configured exactly like a real API: camelCase + enum-as-string via
        // ConfigureHttpJsonOptions (registered through IOptions<JsonOptions>, NOT a direct service).
        var services = new ServiceCollection();
        services.AddLogging();
        services.ConfigureHttpJsonOptions(o =>
        {
            o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });
        var provider = services.BuildServiceProvider();

        var innerStream = new MemoryStream();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = provider,
        };
        httpContext.Response.Body = new ThrowOnSyncWriteStream(innerStream);

        // C1: must NOT throw (the writer uses WriteAsync, not the sync WriteByte that Kestrel rejects).
        var executeAsync = jsonSequenceResultType.GetMethod("ExecuteAsync");
        await (Task)executeAsync.Invoke(result, [httpContext]);

        Assert.Equal("application/json-seq", httpContext.Response.ContentType);

        var bytes = innerStream.ToArray();

        // RFC 7464 framing: the body starts with the RS byte (0x1E).
        Assert.Equal(0x1E, bytes[0]);

        // C2: camelCase property names prove the configured IOptions<JsonOptions> was used, not the
        // bare-default fallback (which would emit PascalCase "Id"/"Type"). Parse from the first JSON
        // brace so the leading RS framing byte and trailing LF are ignored without a control-char literal.
        var text = Encoding.UTF8.GetString(bytes);
        var jsonStart = text.IndexOf('{', StringComparison.Ordinal);
        using var doc = JsonDocument.Parse(text.Substring(jsonStart));
        Assert.True(doc.RootElement.TryGetProperty("id", out var idProp));
        Assert.Equal(eventId.ToString(), idProp.GetString());
        Assert.True(doc.RootElement.TryGetProperty("type", out _));
        Assert.False(doc.RootElement.TryGetProperty("Id", out _));
    }

    [Fact]
    public async Task MultipartMixed_WriteThenRead_RoundTrips()
    {
        var writerType = LoadGeneratedServerType("SequentialStreamWriter");
        var readerType = LoadGeneratedType("StreamReaders");
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        const string boundary = "atc-stream-boundary";

        var source = new[]
        {
            JsonSerializer.SerializeToElement(new { id = "a", type = "x" }),
            JsonSerializer.SerializeToElement(new { id = "b", type = "y" }),
        };

        using var ms = new MemoryStream();
        await InvokeWriteMultipartAsync(writerType, source, ms, boundary, options);
        var bytes = ms.ToArray();

        var text = Encoding.UTF8.GetString(bytes);
        Assert.Contains("--" + boundary, text, StringComparison.Ordinal);
        Assert.EndsWith("--" + boundary + "--\r\n", text, StringComparison.Ordinal);

        using var readStream = new MemoryStream(bytes);
        var read = readerType
            .GetMethod("ReadMultipartMixedAsync")
            .MakeGenericMethod(typeof(JsonElement));
        var items = await EnumerateWithArgsAsync<JsonElement>(read, [readStream, boundary, options, CancellationToken.None]);
        Assert.Equal(2, items.Count);
        Assert.Equal("a", items[0].GetProperty("id").GetString());
        Assert.Equal("b", items[1].GetProperty("id").GetString());
    }

    [Fact]
    public async Task MultipartMixed_Reader_HandlesMultiLineParts()
    {
        var readerType = LoadGeneratedType("StreamReaders");
        var read = readerType
            .GetMethod("ReadMultipartMixedAsync")
            .MakeGenericMethod(typeof(JsonElement));
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        const string boundary = "atc-stream-boundary";

        // Multipart parts whose bodies are PRETTY-PRINTED (multi-line) JSON — a third-party producer
        // may format like this. The reader must accumulate body lines into one valid JSON document.
        const string multipart =
            "--atc-stream-boundary\r\nContent-Type: application/json\r\n\r\n{\n  \"id\": \"a\",\n  \"type\": \"x\"\n}\r\n" +
            "--atc-stream-boundary\r\nContent-Type: application/json\r\n\r\n{\n  \"id\": \"b\",\n  \"type\": \"y\"\n}\r\n" +
            "--atc-stream-boundary--\r\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(multipart));

        var items = await EnumerateWithArgsAsync<JsonElement>(read, [stream, boundary, options, CancellationToken.None]);
        Assert.Equal(2, items.Count);
        Assert.Equal("a", items[0].GetProperty("id").GetString());
        Assert.Equal("b", items[1].GetProperty("id").GetString());
    }

    [Fact]
    public async Task MultipartMixedResult_ExecuteAsync_UsesConfiguredOptions_AndAsyncIo()
    {
        // Load the emitted server assembly and resolve MultipartMixedResult<Event> + Event.
        var serverAssembly = CompilationVerificationHarness.EmitAndLoad(
            CompilationVerificationHarness.RunServer("StreamingItemSchema", "StreamingItemSchema.yaml"));
        var eventType = serverAssembly.GetTypes().Single(t => t.Name == "Event");
        var multipartResultOpen = serverAssembly.GetTypes().Single(t => t.Name == "MultipartMixedResult`1");
        var multipartResultType = multipartResultOpen.MakeGenericType(eventType);

        // Build an IAsyncEnumerable<Event> of one item via reflection.
        var eventId = Guid.NewGuid();
        var items = BuildAsyncEnumerable(eventType, [CreateEvent(eventType, eventId, "x")]);
        var result = Activator.CreateInstance(multipartResultType, items);

        // RequestServices configured exactly like a real API: camelCase + enum-as-string via
        // ConfigureHttpJsonOptions (registered through IOptions<JsonOptions>, NOT a direct service).
        var services = new ServiceCollection();
        services.AddLogging();
        services.ConfigureHttpJsonOptions(o =>
        {
            o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });
        var provider = services.BuildServiceProvider();

        var innerStream = new MemoryStream();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = provider,
        };
        httpContext.Response.Body = new ThrowOnSyncWriteStream(innerStream);

        // C1: must NOT throw (the writer uses WriteAsync, not the sync WriteByte that Kestrel rejects).
        var executeAsync = multipartResultType.GetMethod("ExecuteAsync");
        await (Task)executeAsync.Invoke(result, [httpContext]);

        Assert.StartsWith("multipart/mixed; boundary=", httpContext.Response.ContentType, StringComparison.Ordinal);

        var text = Encoding.UTF8.GetString(innerStream.ToArray());
        Assert.Contains("--atc-stream-boundary", text, StringComparison.Ordinal);

        // C2: camelCase property names prove the configured IOptions<JsonOptions> was used, not the
        // bare-default fallback (which would emit PascalCase "Id"/"Type"). Parse from the first JSON
        // brace so the multipart framing (boundary + part headers) is ignored.
        var jsonStart = text.IndexOf('{', StringComparison.Ordinal);
        var jsonEnd = text.IndexOf('}', jsonStart);
        using var doc = JsonDocument.Parse(text.Substring(jsonStart, jsonEnd - jsonStart + 1));
        Assert.True(doc.RootElement.TryGetProperty("id", out var idProp));
        Assert.Equal(eventId.ToString(), idProp.GetString());
        Assert.True(doc.RootElement.TryGetProperty("type", out _));
        Assert.False(doc.RootElement.TryGetProperty("Id", out _));
    }

    /// <summary>
    /// A boundary line is the boundary and nothing else: with the boundary <c>b</c> the preamble line
    /// <c>--b--x</c> is not the closing boundary (the parts after it are read), and <c>--b</c> followed by spaces
    /// or tabs is the boundary.
    /// </summary>
    [Fact]
    public async Task MultipartMixed_Reader_ALineThatOnlyStartsLikeTheBoundary_IsNoBoundary()
    {
        var (read, serializer) = Reader("ReadMultipartMixedAsync");
        const string padded = "--b \t\r\nContent-Type: application/json\r\n\r\n[1,\r\n2]\r\n--b-- \r\n";
        const string lookalike = "--b--x\r\n--bx\r\n--b\r\n\r\n[1,\r\n2,3]\r\n--b--\r\n";

        using var paddedStream = new MemoryStream(Encoding.UTF8.GetBytes(padded));
        var items = await EnumerateWithArgsAsync<JsonElement>(read, [paddedStream, "b", serializer, CancellationToken.None]);
        Assert.Equal(2, Assert.Single(items).GetArrayLength());

        using var lookalikeStream = new MemoryStream(Encoding.UTF8.GetBytes(lookalike));
        items = await EnumerateWithArgsAsync<JsonElement>(read, [lookalikeStream, "b", serializer, CancellationToken.None]);
        Assert.Equal(3, Assert.Single(items).GetArrayLength());
    }

    /// <summary>
    /// The limit of a multipart part is in bytes, as for every other framing: a part of 15 characters that is 23
    /// bytes on the wire is past a limit of 16.
    /// </summary>
    [Fact]
    public async Task MultipartMixed_Reader_ThePartLimit_IsInBytes()
    {
        var (read, serializer) = Reader("ReadMultipartMixedAsync", maxRecordLength: 16);
        const string wire = "--b\r\n\r\n[\r\n\"\u00e6\u00e6\u00e6\u00e6\",\r\n\"\u00e6\u00e6\u00e6\u00e6\"\r\n]\r\n--b--\r\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(wire));

        var exception = await Assert.ThrowsAnyAsync<Exception>(()
            => EnumerateWithArgsAsync<JsonElement>(read, [stream, "b", serializer, CancellationToken.None]));
        Assert.IsType<InvalidDataException>(exception.InnerException ?? exception);
    }

    /// <summary>
    /// What stands between two records and is only whitespace is no record: an empty line or a line of spaces in
    /// JSON Lines, a stray line feed between two records of a JSON Text Sequence.
    /// </summary>
    [Theory]
    [InlineData("ReadJsonLinesAsync", "1\n\n \t\r\n2\n   \n")]
    [InlineData("ReadJsonSequenceAsync", "\u001e1\n\n\u001e2\n\r\n \n")]
    [InlineData("ReadJsonSequenceAsync", "\u001e\n\u001e1\n\u001e \u001e2")]
    public async Task Reader_WhitespaceBetweenRecords_IsNoRecord(
        string readerName,
        string wire)
    {
        var (read, serializer) = Reader(readerName);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(wire));

        var items = await EnumerateWithArgsAsync<JsonElement>(read, [stream, serializer, CancellationToken.None]);

        Assert.Equal([1, 2], items.Select(x => x.GetInt32()));
    }

    /// <summary>
    /// A pretty-printed record or part of many lines is one item: whether it is complete is kept up as it arrives
    /// (not found by reading all of it again at every line), through strings that hold quotes, brackets and braces.
    /// </summary>
    [Theory]
    [InlineData("ReadJsonSequenceAsync")]
    [InlineData("ReadMultipartMixedAsync")]
    public async Task Reader_ARecordOfManyLines_IsOneItem(string readerName)
    {
        const int count = 20_000;
        var (read, serializer) = Reader(readerName);
        var json = "[\n" + string.Join(",\n", Enumerable.Range(0, count).Select(i => "\"a \\\" [ { " + i.ToString(CultureInfo.InvariantCulture) + "\"")) + "\n]\n";
        var isMultipart = readerName == "ReadMultipartMixedAsync";
        var wire = isMultipart ? "--b\r\n\r\n" + json + "--b--\r\n" : "\u001e" + json;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(wire));

        object?[] arguments = isMultipart
            ? [stream, "b", serializer, CancellationToken.None]
            : [stream, serializer, CancellationToken.None];
        var items = await EnumerateWithArgsAsync<JsonElement>(read, arguments);

        Assert.Equal(count, Assert.Single(items).GetArrayLength());
    }

    /// <summary>
    /// Creates an instance of the generated <c>Event</c> record (Id: Guid, Type: string) via
    /// reflection, coercing each argument to the constructor's declared parameter type.
    /// </summary>
    private static object CreateEvent(
        Type eventType,
        Guid id,
        string type)
    {
        // The generated Event record has a primary constructor (Id, Type).
        var ctor = eventType.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
        return ctor.Invoke([id, type]);
    }

    /// <summary>
    /// Builds a strongly-typed <c>IAsyncEnumerable&lt;T&gt;</c> (T = <paramref name="elementType"/>)
    /// from the given items, so it can be passed to the emitted <c>JsonLinesResult&lt;T&gt;</c> ctor.
    /// </summary>
    private static object BuildAsyncEnumerable(
        Type elementType,
        object[] items)
    {
        var generic = typeof(StreamingWireFramingTests)
            .GetMethod(nameof(ToAsyncEnumerableTyped), BindingFlags.NonPublic | BindingFlags.Static)
            .MakeGenericMethod(elementType);
        return generic.Invoke(null, [items]);
    }

    private static async IAsyncEnumerable<T> ToAsyncEnumerableTyped<T>(
        object[] items)
    {
        foreach (var item in items)
        {
            await Task.Yield();
            yield return (T)item;
        }
    }

    private const string EventStreamWithNamedEvents =
        ": keep-alive\n\n" +
        "event: update\ndata: {\"id\":\"a\"}\n\n" +
        "data:\n\n" +
        "event: ping\ndata: ping\n\n" +
        "event: ping\ndata: {\"id\":\"no item\"}\n\n" +
        "event: ping\n\n" +
        "data: {\"id\":\"b\"}\n\n" +
        "event: message\ndata: {\"id\":\"c\"}\n\n" +
        "event: Update\ndata: {\"id\":\"no item\"}\n\n" +
        "id: 4\nretry: 100\ndata: {\"id\":\r\ndata: \"d\"}\r\n\r\n";

    // A reader of the typed client, with the options it deserializes through. (The EndpointPerOperation client has no
    // readers of its own: Atc.Rest.Client reads its streamed responses.)
    private static (MethodInfo Read, JsonSerializerOptions Serializer) Reader(
        string name,
        int maxRecordLength = 16 * 1024 * 1024)
    {
        var readerType = LoadGeneratedType("StreamReaders");
        readerType.GetProperty("MaxRecordLength")!.SetValue(null, maxRecordLength);
        var read = readerType
            .GetMethods()
            .Single(m => m.Name == name && m.GetParameters()[0].ParameterType == typeof(Stream))
            .MakeGenericMethod(typeof(JsonElement));
        return (read, new JsonSerializerOptions());
    }

    /// <summary>
    /// Generates the StreamingItemSchema typed client, compiles + loads it, and returns the
    /// emitted type with the given simple name.
    /// </summary>
    private static Type LoadGeneratedType(string name)
        => CompilationVerificationHarness
            .EmitAndLoad(CompilationVerificationHarness.RunClient("StreamingItemSchema", "StreamingItemSchema.yaml"))
            .GetTypes()
            .Single(t => t.Name == name);

    /// <summary>
    /// Generates the StreamingItemSchema server, compiles + loads it, and returns the emitted
    /// type with the given simple name (e.g. the server-side <c>SequentialStreamWriter</c>).
    /// </summary>
    private static Type LoadGeneratedServerType(string name)
        => CompilationVerificationHarness
            .EmitAndLoad(CompilationVerificationHarness.RunServer("StreamingItemSchema", "StreamingItemSchema.yaml"))
            .GetTypes()
            .Single(t => t.Name == name);

    /// <summary>
    /// Invokes an emitted server writer (e.g. <c>WriteJsonLinesAsync&lt;T&gt;</c>) via reflection
    /// over an <see cref="IAsyncEnumerable{T}"/> built from <paramref name="items"/> and awaits the
    /// returned <see cref="Task"/>, so the written bytes land in <paramref name="stream"/>.
    /// </summary>
    private static Task InvokeWriteAsync(
        Type writerType,
        string methodName,
        JsonElement[] items,
        Stream stream,
        JsonSerializerOptions options)
    {
        async IAsyncEnumerable<JsonElement> ToAsyncEnumerable()
        {
            foreach (var item in items)
            {
                await Task.Yield();
                yield return item;
            }
        }

        var write = writerType
            .GetMethod(methodName)
            .MakeGenericMethod(typeof(JsonElement));
        return (Task)write.Invoke(null, [ToAsyncEnumerable(), stream, options, CancellationToken.None]);
    }

    /// <summary>
    /// Invokes the emitted <c>WriteMultipartMixedAsync&lt;T&gt;(items, stream, boundary, options, ct)</c>
    /// server writer via reflection (its signature carries an extra <paramref name="boundary"/> arg).
    /// </summary>
    private static Task InvokeWriteMultipartAsync(
        Type writerType,
        JsonElement[] items,
        Stream stream,
        string boundary,
        JsonSerializerOptions options)
    {
        async IAsyncEnumerable<JsonElement> ToAsyncEnumerable()
        {
            foreach (var item in items)
            {
                await Task.Yield();
                yield return item;
            }
        }

        var write = writerType
            .GetMethod("WriteMultipartMixedAsync")
            .MakeGenericMethod(typeof(JsonElement));
        return (Task)write.Invoke(null, [ToAsyncEnumerable(), stream, boundary, options, CancellationToken.None]);
    }

    /// <summary>
    /// Invokes an emitted <c>IAsyncEnumerable&lt;T?&gt;</c> stream reader (via reflection) and
    /// materializes the non-null items into a list.
    /// </summary>
    private static Task<List<T>> EnumerateAsync<T>(
        MethodInfo read,
        Stream stream,
        JsonSerializerOptions options)
        => EnumerateWithArgsAsync<T>(read, [stream, options, CancellationToken.None]);

    /// <summary>
    /// Drives an emitted <c>IAsyncEnumerable&lt;T?&gt;</c> stream reader invoked with an explicit
    /// argument list — used by readers whose signature differs from the common
    /// <c>(stream, options, ct)</c> shape (e.g. multipart's <c>(stream, boundary, options, ct)</c>).
    /// </summary>
    private static async Task<List<T>> EnumerateWithArgsAsync<T>(
        MethodInfo read,
        object?[] readerArgs)
    {
        var asyncEnumerable = read.Invoke(null, readerArgs);

        // Drive the IAsyncEnumerable<T?> via its IAsyncEnumerator over reflection — the emitted
        // type lives in a dynamically-loaded assembly, so we can't bind it statically. The
        // compiler-generated iterator implements GetAsyncEnumerator as an explicit interface
        // method, so resolve it through the IAsyncEnumerable<> interface rather than the concrete
        // type (which would not surface the explicit implementation by name).
        var enumerableInterface = asyncEnumerable
            .GetType()
            .GetInterfaces()
            .Single(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>));
        var getEnumerator = enumerableInterface.GetMethod("GetAsyncEnumerator");
        var enumerator = getEnumerator.Invoke(asyncEnumerable, [CancellationToken.None]);
        var enumeratorInterface = enumerator
            .GetType()
            .GetInterfaces()
            .Single(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAsyncEnumerator<>));
        var moveNextAsync = enumeratorInterface.GetMethod("MoveNextAsync");
        var currentProperty = enumeratorInterface.GetProperty("Current");

        var items = new List<T>();
        try
        {
            while (true)
            {
                var moveNextTask = (ValueTask<bool>)moveNextAsync.Invoke(enumerator, []);
                if (!await moveNextTask)
                {
                    break;
                }

                var current = currentProperty.GetValue(enumerator);
                if (current is T value)
                {
                    items.Add(value);
                }
            }
        }
        finally
        {
            if (enumerator is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
        }

        return items;
    }

    /// <summary>
    /// A stream that mimics Kestrel's <c>Response.Body</c> with <c>AllowSynchronousIO=false</c>:
    /// any synchronous <c>Write</c>/<c>WriteByte</c>/<c>Flush</c> throws, while the async overloads
    /// delegate to an inner <see cref="MemoryStream"/> so written bytes can be inspected.
    /// </summary>
    private sealed class ThrowOnSyncWriteStream(MemoryStream inner) : Stream
    {
        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => inner.Length;

        public override long Position
        {
            get => inner.Position;
            set => inner.Position = value;
        }

        public override void Write(
            byte[] buffer,
            int offset,
            int count)
            => throw new InvalidOperationException("Synchronous writes are disallowed (AllowSynchronousIO=false).");

        public override void WriteByte(byte value)
            => throw new InvalidOperationException("Synchronous writes are disallowed (AllowSynchronousIO=false).");

        public override void Flush()
            => throw new InvalidOperationException("Synchronous flush is disallowed (AllowSynchronousIO=false).");

        public override Task WriteAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
            => inner.WriteAsync(buffer, offset, count, cancellationToken);

        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
            => inner.WriteAsync(buffer, cancellationToken);

        public override Task FlushAsync(CancellationToken cancellationToken)
            => inner.FlushAsync(cancellationToken);

        public override int Read(
            byte[] buffer,
            int offset,
            int count)
            => throw new NotSupportedException();

        public override long Seek(
            long offset,
            SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(long value)
            => throw new NotSupportedException();
    }
}