namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// The SignalR hubs a specification declares with <c>x-signalr-hubs</c> are generated in C# on both sides: the server
/// gets the client interface of a typed hub, the interface of the hub's own methods and the hub's path and names; a
/// client project that references the SignalR client gets a typed client per hub. Generated code compiles in every
/// mode, and a generated client talks to a hub built on the generated server contract over a real connection.
/// </summary>
[Collection(nameof(SignalRHubTests))]
[CollectionDefinition(nameof(SignalRHubTests), DisableParallelization = true)]
public sealed class SignalRHubTests
{
    private const string SignalRClientAssembly = "Microsoft.AspNetCore.SignalR.Client";

    // Notice and HubStatus are each used by one path (their models are in that path's namespace), UserActivity by the
    // hub alone. The hub has an event and a method whose names are no C# identifiers, an event declared twice, an
    // event and a method without a payload, and names the generated classes have members of (Events, Start). A method
    // and an event take several arguments (`arguments:`), named like what the generated members take themselves
    // (cancellationToken, clients, handler).
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /notices:
            get:
              operationId: listNotices
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        type: array
                        items:
                          $ref: '#/components/schemas/Notice'
          /status:
            get:
              operationId: getStatus
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/HubStatus'
        components:
          schemas:
            Notice:
              type: object
              required: [text]
              properties:
                text: { type: string }
            HubStatus:
              type: object
              required: [connections]
              properties:
                connections: { type: integer, format: int32 }
            UserActivity:
              type: object
              required: [user]
              properties:
                user: { type: string }
            SwapDirection:
              type: string
              enum: [left, right]
        x-signalr-hubs:
          notifications:
            url: /hubs/notifications
            events:
              - name: SystemNotification
                payload: '#/components/schemas/Notice'
              - name: user-activity
                payload: '#/components/schemas/UserActivity'
              - name: Ping
              - name: Events
                payload: string
              - name: SystemNotification
                payload: '#/components/schemas/Notice'
              - name: CamerasSwapped
                arguments:
                  - name: cameraId
                    payload: { type: string, format: uuid }
                  - name: direction
                    payload: '#/components/schemas/SwapDirection'
                  - name: clients
                    payload: integer
                  - name: handler
                    payload: string
            methods:
              - name: Subscribe
                payload: string[]
              - name: GetHistory
                payload: { type: integer, format: int32 }
                returns: { type: array, items: { $ref: '#/components/schemas/Notice' } }
              - name: GetStatus
                returns: '#/components/schemas/HubStatus'
              - name: Start
              - name: send-message
                payload: '#/components/schemas/Notice'
                returns: boolean
              - name: SwapCameras
                arguments:
                  - name: cameraId
                    payload: { type: string, format: uuid }
                  - name: direction
                    payload: SwapDirection
                  - name: cancellationToken
                    payload: boolean
                returns: integer
        """;

    private const string HubSource = """
        using Microsoft.AspNetCore.Builder;
        using Microsoft.AspNetCore.SignalR;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public sealed class NotificationsHub(IHubContext<NotificationsHub> untyped) : Hub<INotificationsHubClient>, INotificationsHub
        {
            public async Task Subscribe(IReadOnlyList<string> payload)
            {
                await Clients.Caller.SystemNotification(new Notice(Text: "subscribed to " + string.Join("+", payload)));
                await Clients.Caller.Ping();
                await Clients.Caller.Events("typed");
                await untyped.Clients.Client(Context.ConnectionId).SendUserActivityAsync(new UserActivity(User: payload[0]));
            }

            public Task<IReadOnlyList<Notice>> GetHistory(int payload)
                => Task.FromResult<IReadOnlyList<Notice>>([.. Enumerable.Range(1, payload).Select(i => new Notice(Text: "n" + i))]);

            public Task<HubStatus> GetStatus()
                => Task.FromResult(new HubStatus(Connections: 1));

            public Task Start()
                => Clients.Caller.Ping();

            [HubMethodName(NotificationsHubContract.Methods.SendMessage)]
            public Task<bool> SendMessage(Notice payload)
                => Task.FromResult(payload.Text == "hello");

            // Several arguments, in their order on the wire; the event goes back with four
            public async Task<int> SwapCameras(Guid cameraId, SwapDirection direction, bool cancellationToken)
            {
                await Clients.Caller.CamerasSwapped(cameraId, direction, cancellationToken ? 1 : 0, "swapped");
                return direction == SwapDirection.Right ? 2 : 1;
            }
        }

        public static class HubRegistration
        {
            public static void Register(IServiceCollection services)
                => services.AddSignalR();

            public static void Map(WebApplication app)
                => app.MapHub<NotificationsHub>(NotificationsHubContract.Url);
        }
        """;

    private const string ProbeSource = """
        using Microsoft.AspNetCore.SignalR.Client;

        namespace ErrorBodies.RuntimeTest;

        public static class HubProbe
        {
            public static async Task<string> RunAsync(Uri baseAddress)
            {
                var seen = new List<string>();
                var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                void Add(string text)
                {
                    lock (seen)
                    {
                        seen.Add(text);
                        if (seen.Count == 6)
                        {
                            done.TrySetResult();
                        }
                    }
                }

                await using var hub = new NotificationsHubClient(new HubConnectionBuilder().WithUrl(new Uri(baseAddress, NotificationsHubClient.Url)).Build());
                using var notices = hub.OnSystemNotification(notice => Add("notice:" + notice.Text));
                using var activity = hub.OnUserActivity(async activity =>
                {
                    await Task.Yield();
                    Add("activity:" + activity.User);
                });
                using var pings = hub.OnPing(() => Add("ping"));
                using var events = hub.OnEvents2(text => Add("events:" + text));
                using var swapped = hub.OnCamerasSwapped((cameraId, direction, clients, handler) => Add("swapped:" + cameraId + "/" + direction + "/" + clients + "/" + handler));

                await hub.StartAsync();
                await hub.SubscribeAsync(["news", "sport"]);
                var status = await hub.GetStatusAsync();
                var history = await hub.GetHistoryAsync(2);
                await hub.Start2Async();
                var sent = await hub.SendMessageAsync(new Notice(Text: "hello"));
                var swap = await hub.SwapCamerasAsync(new Guid("6f9619ff-8b86-d011-b42d-00c04fc964ff"), SwapDirection.Right, true);
                await done.Task.WaitAsync(TimeSpan.FromSeconds(30));

                lock (seen)
                {
                    return string.Join("|", seen.Order(StringComparer.Ordinal)) + "|status:" + status.Connections + "|sent:" + sent + "|history:" + string.Join(",", history.Select(n => n.Text)) + "|swap:" + swap;
                }
            }
        }
        """;

    [Theory]
    [InlineData("server", "{}")]
    [InlineData("client", """{ "generationMode": "EndpointPerOperation" }""")]
    [InlineData("client", """{ "generationMode": "TypedClient" }""")]
    [InlineData("client", """{ "generationMode": "TypedClient", "clientGranularity": "Single" }""")]
    public void Generator_EmitsTheHubs_AndTheyCompile(
        string kind,
        string marker)
    {
        var (sources, diagnostics) = Run(kind, marker, CompilationVerificationHarness.GetFullFrameworkReferences());
        var qualified = Assert.Single(sources, s => s.HintName.Contains(".Hubs.Notifications", StringComparison.Ordinal)).Source;

        // A model is written in full - its Models namespace (shared, or its path segment's) and its name - so no type
        // the hub file imports can stand in for it; read without the namespaces below
        Assert.Matches(new Regex(@"global::ErrorBodies\.Generated(\.\w+)?\.Models\.Notice\b", RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1)), qualified);
        Assert.DoesNotContain("using ErrorBodies.Generated", qualified, StringComparison.Ordinal);
        var hub = Regex.Replace(qualified, @"global::ErrorBodies\.Generated(\.\w+)?\.Models\.", string.Empty, RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));

        Assert.DoesNotContain(diagnostics, d => d.Id == "ATC_API_DEP012");
        Assert.Contains("namespace ErrorBodies.Generated.Hubs;", hub, StringComparison.Ordinal);
        Assert.Contains("public const string Url = \"/hubs/notifications\";", hub, StringComparison.Ordinal);
        Assert.Contains("public const string UserActivity = \"user-activity\";", hub, StringComparison.Ordinal);
        Assert.Contains("public const string Events2 = \"Events\";", hub, StringComparison.Ordinal);
        Assert.Contains("public const string Start2 = \"Start\";", hub, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(hub, "public const string SystemNotification = ", RegexOptions.None, TimeSpan.FromSeconds(1)));

        if (kind == "server")
        {
            // The typed client interface has the events a method can be named after; the hub's interface has the
            // methods under their names on the wire, or in PascalCase where that is no identifier
            Assert.Contains("Task SystemNotification(Notice payload);", hub, StringComparison.Ordinal);
            Assert.Contains("Task Ping();", hub, StringComparison.Ordinal);
            Assert.Contains("Task Events(string payload);", hub, StringComparison.Ordinal);
            Assert.DoesNotContain("Task UserActivity(", hub, StringComparison.Ordinal);
            Assert.Contains("Task Subscribe(IReadOnlyList<string> payload);", hub, StringComparison.Ordinal);
            Assert.Contains("Task<IReadOnlyList<Notice>> GetHistory(int payload);", hub, StringComparison.Ordinal);
            Assert.Contains("Task<HubStatus> GetStatus();", hub, StringComparison.Ordinal);
            Assert.Contains("Task Start();", hub, StringComparison.Ordinal);
            Assert.Contains("Task<bool> SendMessage(Notice payload);", hub, StringComparison.Ordinal);
            Assert.Contains("Task<int> SwapCameras(Guid cameraId, SwapDirection direction, bool cancellationToken);", hub, StringComparison.Ordinal);
            Assert.Contains("Task CamerasSwapped(Guid cameraId, SwapDirection direction, int clients, string handler);", hub, StringComparison.Ordinal);
            Assert.Contains("=> clients.SendAsync(Events.CamerasSwapped, cameraId, direction, clients2, handler, cancellationToken);", hub, StringComparison.Ordinal);
            Assert.Contains("public static Task SendUserActivityAsync(", hub, StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains("public sealed class NotificationsHubClient : IAsyncDisposable", hub, StringComparison.Ordinal);
            Assert.Contains("public IDisposable OnUserActivity(Action<UserActivity> handler)", hub, StringComparison.Ordinal);
            Assert.Contains("public IDisposable OnPing(Func<Task> handler)", hub, StringComparison.Ordinal);
            Assert.Contains("public Task<HubStatus> GetStatusAsync(", hub, StringComparison.Ordinal);
            Assert.Contains("public Task Start2Async(", hub, StringComparison.Ordinal);
            Assert.Contains("public Task<bool> SendMessageAsync(", hub, StringComparison.Ordinal);
            Assert.Contains("public IDisposable OnCamerasSwapped(Action<Guid, SwapDirection, int, string> handler)", hub, StringComparison.Ordinal);
            Assert.Contains("=> Connection.InvokeAsync<int>(Methods.SwapCameras, cameraId, direction, cancellationToken2, cancellationToken);", hub, StringComparison.Ordinal);
        }

        var compilation = CompilationVerificationHarness.CreateCompilation(sources);
        Assert.Empty(compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    /// <summary>
    /// A payload is the type its schema is generated as: a schema whose name is a .NET type the hub file uses
    /// (<c>Task</c>) is the model, not that type; an array or primitive alias is the type it names (no type is
    /// generated for it); a name that is no identifier is the model's sanitized name.
    /// </summary>
    [Theory]
    [InlineData("server", "{}")]
    [InlineData("client", """{ "generationMode": "EndpointPerOperation" }""")]
    [InlineData("client", """{ "generationMode": "TypedClient" }""")]
    [InlineData("client", """{ "generationMode": "TypedClient", "clientGranularity": "Single" }""")]
    public void Generator_TypesAPayloadAsItsSchemaIsGenerated_AndTheHubCompiles(
        string kind,
        string marker)
    {
        const string yaml = """
            openapi: 3.0.3
            info:
              title: ErrorBodies
              version: 1.0.0
            paths:
              /tasks:
                get:
                  operationId: listTasks
                  responses:
                    '200':
                      description: OK
                      content:
                        application/json:
                          schema:
                            $ref: '#/components/schemas/Tasks'
            components:
              schemas:
                Task:
                  type: object
                  required: [id]
                  properties:
                    id: { type: string }
                Tasks:
                  type: array
                  items:
                    $ref: '#/components/schemas/Task'
                Score:
                  type: integer
                  format: int64
                Device.Status:
                  type: object
                  properties:
                    online: { type: boolean }
            x-signalr-hubs:
              tasks:
                events:
                  - name: TaskChanged
                    payload: '#/components/schemas/Task'
                  - name: StatusChanged
                    payload: '#/components/schemas/Device.Status'
                methods:
                  - name: GetTasks
                    returns: '#/components/schemas/Tasks'
                  - name: GetScore
                    returns: '#/components/schemas/Score'
            """;

        var (sources, diagnostics) = Run(kind, marker, CompilationVerificationHarness.GetFullFrameworkReferences(), yaml);
        var hub = Assert.Single(sources, s => s.HintName.Contains(".Hubs.Tasks", StringComparison.Ordinal)).Source;

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("Task<long>", hub, StringComparison.Ordinal);
        Assert.Matches(new Regex(@"\.Models\.Task[ >]", RegexOptions.None, TimeSpan.FromSeconds(1)), hub);
        Assert.Contains("Task<IReadOnlyList<global::ErrorBodies.Generated", hub, StringComparison.Ordinal);
        Assert.DoesNotContain("<Tasks>", hub, StringComparison.Ordinal);

        var compilation = CompilationVerificationHarness.CreateCompilation(sources);
        Assert.Empty(compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    /// <summary>
    /// A member the generator cannot read is an error naming the hub, the member and what is wrong - not an event or a
    /// method generated without the value it carries: a payload or an answer it does not understand (an inline object or
    /// enum belongs in <c>components/schemas</c>), <c>payload</c> next to <c>arguments</c>, an argument without a name, a
    /// name twice, an argument without a payload, more arguments than SignalR's typed handlers take, an answer on an
    /// event, a member without a name.
    /// </summary>
    [Theory]
    [InlineData("methods", "{ name: Swap, payload: [cameraId, direction] }", "payload")]
    [InlineData("methods", "{ name: Swap, payload: { type: object, properties: { a: { type: string } } } }", "payload")]
    [InlineData("events", "{ name: Swapped, payload: { type: string, enum: [left, right] } }", "payload")]
    [InlineData("methods", "{ name: Swap, returns: { type: object } }", "returns")]
    [InlineData("methods", "{ name: Swap, payload: string, arguments: [ { name: a, payload: string } ] }", "arguments")]
    [InlineData("methods", "{ name: Swap, arguments: [ { payload: string } ] }", "arguments")]
    [InlineData("methods", "{ name: Swap, arguments: [ { name: a, payload: string }, { name: a, payload: integer } ] }", "arguments")]
    [InlineData("events", "{ name: Swapped, arguments: [ { name: a } ] }", "arguments")]
    [InlineData("events", "{ name: Swapped, arguments: [ { name: a1, payload: string }, { name: a2, payload: string }, { name: a3, payload: string }, { name: a4, payload: string }, { name: a5, payload: string }, { name: a6, payload: string }, { name: a7, payload: string }, { name: a8, payload: string }, { name: a9, payload: string } ] }", "arguments")]
    [InlineData("events", "{ name: Swapped, returns: string }", "returns")]
    [InlineData("events", "{ payload: string }", "name")]
    public void Generator_AMemberItCannotRead_ReportsHUB001_NotCodeWithoutTheValue(
        string kind,
        string member,
        string field)
    {
        // The member goes first in the hub's list of its kind
        var yaml = Yaml.Replace("\r\n", "\n", StringComparison.Ordinal).Replace(
            $"    {kind}:\n",
            $"    {kind}:\n      - {member}\n",
            StringComparison.Ordinal);

        foreach (var (generator, marker) in new[] { ("server", "{}"), ("client", """{ "generationMode": "TypedClient" }""") })
        {
            var (sources, diagnostics) = Run(generator, marker, CompilationVerificationHarness.GetFullFrameworkReferences(), yaml);

            var error = Assert.Single(diagnostics, d => d.Id == "ATC_API_HUB001");
            Assert.Equal(DiagnosticSeverity.Error, error.Severity);
            var message = error.GetMessage(CultureInfo.InvariantCulture);
            Assert.Contains("notifications", message, StringComparison.Ordinal);
            Assert.Contains(field, message, StringComparison.Ordinal);
            Assert.Empty(CompilationVerificationHarness.CreateCompilation(sources).GetDiagnostics(TestContext.Current.CancellationToken).Where(d => d.Severity == DiagnosticSeverity.Error));
        }
    }

    [Fact]
    public void Generator_APayloadNamingNoSchema_IsAnErrorNamingIt_NotCodeThatFailsToCompile()
    {
        var yaml = Yaml.Replace("payload: '#/components/schemas/UserActivity'", "payload: '#/components/schemas/Missing'", StringComparison.Ordinal);

        var (sources, diagnostics) = Run("server", "{}", CompilationVerificationHarness.GetFullFrameworkReferences(), yaml);

        var error = Assert.Single(diagnostics, d => d.Id == "ATC_API_SCH013");
        Assert.Contains("Missing", error.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        Assert.Empty(CompilationVerificationHarness.CreateCompilation(sources).GetDiagnostics(TestContext.Current.CancellationToken).Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Theory]
    [InlineData("""{ "generationMode": "EndpointPerOperation" }""")]
    [InlineData("""{ "generationMode": "TypedClient" }""")]
    [InlineData("""{ "generationMode": "TypedClient", "clientGranularity": "Single" }""")]
    public void Client_WithoutTheSignalRClientPackage_ReportsDEP012_AndGeneratesNoHubClient(
        string marker)
    {
        var references = CompilationVerificationHarness.GetFullFrameworkReferences()
            .Where(r => r is not PortableExecutableReference pe ||
                        !Path.GetFileNameWithoutExtension(pe.FilePath ?? string.Empty).Equals(SignalRClientAssembly, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var (sources, diagnostics) = Run("client", marker, references);

        var info = Assert.Single(diagnostics, d => d.Id == "ATC_API_DEP012");
        Assert.Equal(DiagnosticSeverity.Info, info.Severity);
        Assert.Contains("NotificationsHubClient", info.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        Assert.Contains(SignalRClientAssembly, info.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        Assert.DoesNotContain(sources, s => s.HintName.Contains(".Hubs.", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("server", "{}")]
    [InlineData("client", """{ "generationMode": "TypedClient" }""")]
    public void Generator_WithoutTheExtension_EmitsNoHub_AndNoDiagnostic(
        string kind,
        string marker)
    {
        var withoutHubs = Yaml.Substring(0, Yaml.IndexOf("x-signalr-hubs:", StringComparison.Ordinal));

        var (sources, diagnostics) = Run(kind, marker, [.. CompilationVerificationHarness.GetMinimalReferences()], withoutHubs);

        Assert.DoesNotContain(sources, s => s.HintName.Contains(".Hubs.", StringComparison.Ordinal));
        Assert.DoesNotContain(diagnostics, d => d.Id == "ATC_API_DEP012");
    }

    [Theory]
    [InlineData("""{ "generationMode": "EndpointPerOperation" }""")]
    [InlineData("""{ "generationMode": "TypedClient" }""")]
    [InlineData("""{ "generationMode": "TypedClient", "clientGranularity": "Single" }""")]
    public async Task GeneratedHubClient_TalksToAHubOnTheGeneratedServerContract(
        string marker)
    {
        await using var host = await XmlTestHost.StartAsync(
            Yaml,
            HubSource,
            "ErrorBodies.RuntimeTest.HubRegistration",
            plainHost: true,
            configureWithAssembly: (app, assembly) => assembly.GetType("ErrorBodies.RuntimeTest.HubRegistration", throwOnError: true)!.GetMethod("Map")!.Invoke(null, [app]));

        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", marker, includeMinimalApi: false, Yaml);
        var usings = sources
            .Select(s => Regex.Match(s.Source, @"^namespace (?<ns>[\w.]+);", RegexOptions.Multiline | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1)).Groups["ns"].Value)
            .Where(ns => ns.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Select(ns => $"global using {ns};");
        sources.Add(("HubProbe.cs", string.Join(Environment.NewLine, usings) + Environment.NewLine + ProbeSource));
        var client = CompilationVerificationHarness.EmitAndLoad(sources);

        var seen = await (Task<string>)client.GetType("ErrorBodies.RuntimeTest.HubProbe", throwOnError: true)!.GetMethod("RunAsync")!.Invoke(null, [host.BaseAddress])!;

        Assert.Equal("activity:news|events:typed|notice:subscribed to news+sport|ping|ping|swapped:6f9619ff-8b86-d011-b42d-00c04fc964ff/right/1/swapped|status:1|sent:True|history:n1,n2|swap:2", seen);
    }

    private static (List<(string HintName, string Source)> Sources, List<Diagnostic> Diagnostics) Run(
        string kind,
        string marker,
        List<MetadataReference> references,
        string? yaml = null)
    {
        IIncrementalGenerator generator = kind == "server" ? new ApiServerGenerator() : new ApiClientGenerator();
        var compilation = CSharpCompilation.Create(
            "ErrorBodies",
            [],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver
            .Create(generator)
            .AddAdditionalTexts(
            [
                new CompilationVerificationHarness.InMemoryAdditionalText("ErrorBodies.yaml", yaml ?? Yaml),
                new CompilationVerificationHarness.InMemoryAdditionalText(kind == "server" ? ".atc-rest-api-server" : ".atc-rest-api-client", marker),
            ])
            .RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics, TestContext.Current.CancellationToken);

        return (
            driver.GetRunResult().GeneratedTrees.Select(t => (t.FilePath, t.GetText(TestContext.Current.CancellationToken).ToString())).ToList(),
            [.. diagnostics]);
    }
}