namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// The generated C# clients are library code: no continuation of theirs comes back to the caller's
/// <see cref="SynchronizationContext"/>. A WPF or WinForms consumer that blocks on a client call (<c>.Result</c>,
/// <c>.Wait()</c>) on its UI thread deadlocked when a continuation was posted to that thread. The gate reads every
/// generated client snapshot (they are what the generator emits, the snapshot tests hold them to it); the probe runs a
/// typed client and an operation client under a context that counts what is posted to it.
/// </summary>
public sealed class ClientConfigureAwaitTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /thing:
            get:
              operationId: getThing
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Thing'
          /feed:
            get:
              operationId: streamFeed
              x-return-async-enumerable: true
              responses:
                '200':
                  description: OK
                  content:
                    application/x-ndjson:
                      schema:
                        $ref: '#/components/schemas/Thing'
        components:
          schemas:
            Thing:
              type: object
              properties:
                n: { type: integer }
        """;

    private const string ProbeSource = """"
        using System.Net;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public static class ContextProbe
        {
            public static int Run(string method, bool operationClient)
            {
                // Blocks like a UI thread would. Each call into the client starts on the counting context; a
                // continuation posted to it is counted (and still run, on the pool, so the probe itself never hangs).
                var context = new CountingContext();
                CallAsync(method, operationClient, context).GetAwaiter().GetResult();
                return context.Posted;
            }

            private static T OnContext<T>(SynchronizationContext context, Func<T> call)
            {
                var previous = SynchronizationContext.Current;
                SynchronizationContext.SetSynchronizationContext(context);
                try
                {
                    return call();
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(previous);
                }
            }

            private static async Task CallAsync(string method, bool operationClient, SynchronizationContext context)
            {
                var assembly = typeof(ContextProbe).Assembly;
                object result;
                if (!operationClient)
                {
                    var clientType = assembly.GetTypes().Single(t => t.IsClass && !t.IsAbstract && t.GetMethod(method + "Async") is not null);
                    var client = Activator.CreateInstance(clientType, new HttpClient(new StubHandler()) { BaseAddress = new Uri("http://stub/") })!;
                    result = OnContext(context, () => clientType.GetMethod(method + "Async")!.Invoke(client, [CancellationToken.None])!);
                }
                else
                {
                    var httpClientName = (string)assembly.GetTypes().Single(t => t.Name == "Constants").GetField("HttpClientName")!.GetValue(null)!;
                    var services = new ServiceCollection();
                    services
                        .AddHttpClient(httpClientName, c => c.BaseAddress = new Uri("http://stub/"))
                        .ConfigurePrimaryHttpMessageHandler(() => new StubHandler());
                    assembly.GetTypes().SelectMany(t => t.GetMethods()).Single(m => m.Name == "AddErrorBodiesEndpoints").Invoke(null, [services]);
                    var provider = services.BuildServiceProvider();
                    var endpointType = assembly.GetTypes().Single(t => t.Name == "I" + method + "Endpoint");
                    var endpoint = provider.GetRequiredService(endpointType);
                    result = OnContext(context, () => endpointType.GetMethod("ExecuteAsync")!.Invoke(endpoint, [httpClientName, CancellationToken.None])!);
                }

                if (result is Task task)
                {
                    await task.ConfigureAwait(false);
                    result = task.GetType().GetProperty("Result")?.GetValue(task) ?? result;
                    var okContent = result.GetType().GetProperty("OkContent")?.GetValue(result)
                        ?? result.GetType().GetProperty("Content")?.GetValue(result);
                    if (okContent is not null && okContent.GetType().GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>)))
                    {
                        result = okContent;
                    }
                }

                var enumerable = result.GetType().GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>));
                if (enumerable is not null)
                {
                    var enumerator = enumerable.GetMethod("GetAsyncEnumerator")!.Invoke(result, [CancellationToken.None])!;
                    var moveNext = enumerator.GetType().GetInterfaces().Single(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAsyncEnumerator<>)).GetMethod("MoveNextAsync")!;
                    while (await OnContext(context, () => (ValueTask<bool>)moveNext.Invoke(enumerator, null)!).ConfigureAwait(false))
                    {
                    }
                }
            }

            private sealed class CountingContext : SynchronizationContext
            {
                private int posted;

                public int Posted => Volatile.Read(ref posted);

                public override void Post(SendOrPostCallback d, object? state)
                {
                    Interlocked.Increment(ref posted);
                    ThreadPool.QueueUserWorkItem(_ => d(state));
                }

                public override void Send(SendOrPostCallback d, object? state)
                {
                    Interlocked.Increment(ref posted);
                    d(state);
                }
            }

            private sealed class StubHandler : HttpMessageHandler
            {
                // Completes on the pool, after the caller has started waiting: a continuation that captured the
                // caller's context is posted to it
                protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                    => Task.Run(
                        async () =>
                        {
                            await Task.Delay(20, cancellationToken).ConfigureAwait(false);
                            var ndjson = request.RequestUri!.AbsolutePath.EndsWith("/feed", StringComparison.Ordinal);
                            var content = new StringContent(ndjson ? "{\"n\":1}\n{\"n\":2}\n" : "{\"n\":1}");
                            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(ndjson ? "application/x-ndjson" : "application/json");
                            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
                        },
                        cancellationToken);
            }
        }
        """";

    public static TheoryData<string> ClientSnapshotFolders
    {
        get
        {
            var root = GeneratorSnapshotHarness.GetScenariosRoot();
            var data = new TheoryData<string>();
            foreach (var folder in Directory
                         .GetDirectories(root, "Client-*", SearchOption.AllDirectories)
                         .Select(d => Path.GetRelativePath(root, d).Replace('\\', '/'))
                         .Order(StringComparer.Ordinal))
            {
                data.Add(folder);
            }

            return data;
        }
    }

    public static TheoryData<string, string> Calls
        => new()
        {
            { "TypedClient Throw", "GetThing" },
            { "TypedClient Throw", "StreamFeed" },
            { "TypedClient Result", "GetThing" },
            { "TypedClient Result", "StreamFeed" },
            { "EndpointPerOperation", "GetThing" },
            { "EndpointPerOperation", "StreamFeed" },
        };

    [Theory]
    [MemberData(nameof(ClientSnapshotFolders))]
    public void EveryAwaitOfTheGeneratedClient_LeavesTheCallersContext(
        string folder)
    {
        var unconfigured = new List<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(GeneratorSnapshotHarness.GetScenariosRoot(), folder), "*.verified.cs"))
        {
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file), cancellationToken: TestContext.Current.CancellationToken).GetRoot(TestContext.Current.CancellationToken);
            var awaited = root.DescendantNodes().OfType<AwaitExpressionSyntax>().Select(a => (Node: (SyntaxNode)a, a.Expression))
                .Concat(root.DescendantNodes().OfType<ForEachStatementSyntax>().Where(f => f.AwaitKeyword != default).Select(f => (Node: (SyntaxNode)f, f.Expression)));
            foreach (var (node, expression) in awaited)
            {
                if (!IsConfiguredAwait(expression))
                {
                    var line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    unconfigured.Add($"{Path.GetFileName(file)}:{line}: {expression}");
                }
            }

            foreach (var awaitUsing in root.DescendantNodes().OfType<LocalDeclarationStatementSyntax>().Where(l => l.AwaitKeyword != default))
            {
                var line = awaitUsing.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                unconfigured.Add($"{Path.GetFileName(file)}:{line}: {awaitUsing}");
            }
        }

        Assert.Empty(unconfigured);
    }

    [Theory]
    [MemberData(nameof(Calls))]
    public void ACallBlockedOnByTheCaller_PostsNothingToItsContext(
        string client,
        string method)
    {
        var operationClient = client == "EndpointPerOperation";
        var marker = operationClient
            ? ErrorResponseBodyRuntimeTests.ClientOperationMarker("ProblemDetails")
            : $$"""{ "generationMode": "TypedClient", "typedClientResultStyle": "{{client.Split(' ')[1]}}" }""";
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", marker, includeMinimalApi: false, Yaml);
        sources.Add(("ContextProbe.cs", ProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);

        var posted = (int)assembly.GetType("ErrorBodies.RuntimeTest.ContextProbe", throwOnError: true)!.GetMethod("Run")!.Invoke(null, [method, operationClient])!;

        Assert.Equal(0, posted);
    }

    private static bool IsConfiguredAwait(ExpressionSyntax expression)
        => expression is InvocationExpressionSyntax invocation
           && invocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "ConfigureAwait" }
           && invocation.ArgumentList.Arguments is [{ Expression: LiteralExpressionSyntax literal }]
           && literal.IsKind(SyntaxKind.FalseLiteralExpression);
}