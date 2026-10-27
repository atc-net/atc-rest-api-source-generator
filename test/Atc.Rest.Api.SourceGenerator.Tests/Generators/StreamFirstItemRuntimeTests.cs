namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A streamed response starts at its first item, on a real Kestrel server, whichever of the five framings it streams in
/// (a JSON array, JSON Lines, a JSON Text Sequence, multipart/mixed, Server-Sent Events): a stream that fails before its
/// first item is a problem answer - the error handler still can write one - not a 200 with a cut body; a stream of items
/// reads whole, an empty JSON array is <c>[]</c>. (A quiet stream still sends its headers before its first item:
/// <see cref="StreamStartRuntimeTests"/>.)
/// </summary>
public sealed class StreamFirstItemRuntimeTests
{
    private const string Marker = """
        {
          "useMinimalApiPackage": "Enabled",
          "useGlobalErrorHandler": "Enabled"
        }
        """;

    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: Streams
          version: 1.0.0
        paths:
          /things/array:
            get:
              operationId: listArray
              x-return-async-enumerable: true
              parameters:
                - { name: mode, in: query, required: true, schema: { type: string } }
              responses:
                '200': { description: Things, content: { application/json: { schema: { type: array, items: { $ref: '#/components/schemas/Thing' } } } } }
          /things/lines:
            get:
              operationId: listLines
              x-return-async-enumerable: true
              parameters:
                - { name: mode, in: query, required: true, schema: { type: string } }
              responses:
                '200': { description: Things, content: { application/jsonl: { schema: { $ref: '#/components/schemas/Thing' } } } }
          /things/seq:
            get:
              operationId: listSeq
              x-return-async-enumerable: true
              parameters:
                - { name: mode, in: query, required: true, schema: { type: string } }
              responses:
                '200': { description: Things, content: { application/json-seq: { schema: { $ref: '#/components/schemas/Thing' } } } }
          /things/mixed:
            get:
              operationId: listMixed
              x-return-async-enumerable: true
              parameters:
                - { name: mode, in: query, required: true, schema: { type: string } }
              responses:
                '200': { description: Things, content: { multipart/mixed: { schema: { $ref: '#/components/schemas/Thing' } } } }
          /things/sse:
            get:
              operationId: listSse
              x-return-async-enumerable: true
              parameters:
                - { name: mode, in: query, required: true, schema: { type: string } }
              responses:
                '200': { description: Things, content: { text/event-stream: { schema: { $ref: '#/components/schemas/Thing' } } } }
        components:
          schemas:
            Thing:
              type: object
              required: [name]
              properties:
                name: { type: string }
        """;

    private static readonly string[] Operations = ["Array", "Lines", "Seq", "Mixed", "Sse"];

    // One handler per operation: the stream fails before its first item, is empty, or has two items
    private static readonly string DomainSource = """
            using System.Runtime.CompilerServices;
            using Streams.Generated.Handlers;
            using Streams.Generated.Models;
            using Streams.Generated.Parameters;
            using Streams.Generated.Results;

            namespace Streams.RuntimeTest;

            public static class Things
            {
                public static async IAsyncEnumerable<Thing> Of(string mode, [EnumeratorCancellation] CancellationToken cancellationToken = default)
                {
                    switch (mode)
                    {
                        case "fail":
                            await Task.Yield();
                            throw new InvalidOperationException("The source is down.");
                        case "empty":
                            yield break;
                    }

                    yield return new Thing("a");
                    yield return new Thing("b");
                }
            }

            """ + string.Concat(Operations.Select(name => $$"""

            public sealed class List{{name}}Handler : IList{{name}}Handler
            {
                public Task<List{{name}}Result> ExecuteAsync(List{{name}}Parameters parameters, CancellationToken cancellationToken = default)
                    => Task.FromResult(List{{name}}Result.Ok(Things.Of(parameters.Mode, cancellationToken)));
            }

            """));

    [Theory]
    [InlineData("array")]
    [InlineData("lines")]
    [InlineData("seq")]
    [InlineData("mixed")]
    [InlineData("sse")]
    public async Task AStreamThatFailsBeforeItsFirstItem_IsAProblem(
        string framing)
    {
        await using var host = await StartHostAsync();

        using var response = await host.Client.GetAsync(new Uri($"/things/{framing}?mode=fail", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("array", "[{\"name\":\"a\"},{\"name\":\"b\"}]")]
    [InlineData("lines", "{\"name\":\"a\"}\n{\"name\":\"b\"}\n")]
    public async Task AStreamOfItems_ReadsWhole(
        string framing,
        string expected)
    {
        await using var host = await StartHostAsync();

        using var response = await host.Client.GetAsync(new Uri($"/things/{framing}?mode=ok", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expected, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AnEmptyJsonArrayStream_IsAnEmptyArray()
    {
        await using var host = await StartHostAsync();

        using var response = await host.Client.GetAsync(new Uri("/things/array?mode=empty", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("[]", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// A handler test reads every stream result alike: its status code and its items, through the framework's
    /// IStatusCodeHttpResult and IValueHttpResult on the result inside the operation's result.
    /// </summary>
    [Theory]
    [InlineData("Array")]
    [InlineData("Lines")]
    [InlineData("Seq")]
    [InlineData("Mixed")]
    [InlineData("Sse")]
    public void EveryStreamResult_HasItsStatusCodeAndItsItems(string operation)
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", Marker, includeMinimalApi: true, Yaml);
        sources.Add(("RuntimeTestDomain.cs", DomainSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources, includeMinimalApi: true);
        var resultType = assembly.GetType($"Streams.Generated.Results.List{operation}Result", throwOnError: true)!;
        var items = assembly.GetType("Streams.RuntimeTest.Things", throwOnError: true)!.GetMethod("Of")!.Invoke(null, ["ok", CancellationToken.None])!;

        var result = (INestedHttpResult)resultType.GetMethod("Ok", [items.GetType().GetInterfaces().Single(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))])!.Invoke(null, [items])!;

        Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result.Result).StatusCode);

        // The items (Server-Sent Events: the framework's result hands them on as its own stream)
        var value = Assert.IsAssignableFrom<IValueHttpResult>(result.Result).Value;
        Assert.Contains(value!.GetType().GetInterfaces(), i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>));
    }

    private static async Task<RunningHost> StartHostAsync()
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", Marker, includeMinimalApi: true, Yaml);
        sources.Add(("RuntimeTestDomain.cs", DomainSource));

        var assembly = CompilationVerificationHarness.EmitAndLoad(sources, includeMinimalApi: true);
        var serviceExtensions = assembly.GetType("Streams.Generated.Extensions.UnifiedServiceCollectionExtensions", throwOnError: true)!;
        var appExtensions = assembly.GetType("Streams.Generated.Extensions.UnifiedWebApplicationExtensions", throwOnError: true)!;

        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        foreach (var name in Operations)
        {
            builder.Services.AddScoped(
                assembly.GetType($"Streams.Generated.Handlers.IList{name}Handler", throwOnError: true)!,
                assembly.GetType($"Streams.RuntimeTest.List{name}Handler", throwOnError: true)!);
        }

        serviceExtensions
            .GetMethod("AddStreamsApi")!
            .Invoke(null, [builder.Services, null]);

        var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");

        appExtensions
            .GetMethod("MapStreamsApi")!
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
}