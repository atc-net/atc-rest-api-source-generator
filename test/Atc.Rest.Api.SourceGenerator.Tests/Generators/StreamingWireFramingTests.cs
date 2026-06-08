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
        var read = streamReaders.GetMethod("ReadServerSentEventsAsync")!.MakeGenericMethod(typeof(JsonElement));

        const string sse = "data: {\"id\":\"a\",\"type\":\"x\"}\n\ndata: {\"id\":\"b\",\"type\":\"y\"}\n\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(sse));
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var items = await EnumerateAsync<JsonElement>(read, stream, options);

        Assert.Equal(2, items.Count);
        Assert.Equal("a", items[0].GetProperty("id").GetString());
        Assert.Equal("b", items[1].GetProperty("id").GetString());
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
        var read = readerType.GetMethod("ReadJsonLinesAsync")!.MakeGenericMethod(typeof(JsonElement));
        var items = await EnumerateAsync<JsonElement>(read, readStream, options);
        Assert.Equal(2, items.Count);
        Assert.Equal("a", items[0].GetProperty("id").GetString());
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

        var write = writerType.GetMethod(methodName)!.MakeGenericMethod(typeof(JsonElement));
        return (Task)write.Invoke(null, [ToAsyncEnumerable(), stream, options, CancellationToken.None])!;
    }

    /// <summary>
    /// Invokes an emitted <c>IAsyncEnumerable&lt;T?&gt;</c> stream reader (via reflection) and
    /// materializes the non-null items into a list.
    /// </summary>
    private static async Task<List<T>> EnumerateAsync<T>(
        MethodInfo read,
        Stream stream,
        JsonSerializerOptions options)
    {
        var asyncEnumerable = read.Invoke(null, [stream, options, CancellationToken.None])!;

        // Drive the IAsyncEnumerable<T?> via its IAsyncEnumerator over reflection — the emitted
        // type lives in a dynamically-loaded assembly, so we can't bind it statically. The
        // compiler-generated iterator implements GetAsyncEnumerator as an explicit interface
        // method, so resolve it through the IAsyncEnumerable<> interface rather than the concrete
        // type (which would not surface the explicit implementation by name).
        var enumerableInterface = asyncEnumerable
            .GetType()
            .GetInterfaces()
            .Single(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>));
        var getEnumerator = enumerableInterface.GetMethod("GetAsyncEnumerator")!;
        var enumerator = getEnumerator.Invoke(asyncEnumerable, [CancellationToken.None])!;
        var enumeratorInterface = enumerator
            .GetType()
            .GetInterfaces()
            .Single(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAsyncEnumerator<>));
        var moveNextAsync = enumeratorInterface.GetMethod("MoveNextAsync")!;
        var currentProperty = enumeratorInterface.GetProperty("Current")!;

        var items = new List<T>();
        try
        {
            while (true)
            {
                var moveNextTask = (ValueTask<bool>)moveNextAsync.Invoke(enumerator, [])!;
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
}