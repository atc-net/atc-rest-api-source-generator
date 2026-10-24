namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A request body that is text (<c>text/plain</c>, a string) reaches the handler as the text that was sent, on a real
/// Kestrel server: read by its charset, validated by the schema's <c>minLength</c>, <c>maxLength</c> and <c>pattern</c>
/// (a 400), required (an empty body is a 400), and only as the declared media type (another is a 415). Both C# clients
/// send the text as it is, as UTF-8 with the declared media type.
/// </summary>
public sealed class TextRequestBodyRuntimeTests
{
    private const string Marker = """
        {
          "useMinimalApiPackage": "Enabled",
          "useValidationFilter": "Enabled",
          "useGlobalErrorHandler": "Enabled"
        }
        """;

    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: Notes
          version: 1.0.0
        paths:
          /notes:
            post:
              operationId: createNote
              requestBody:
                required: true
                content:
                  text/plain:
                    schema:
                      type: string
                      minLength: 2
                      maxLength: 20
                      pattern: '^[^#]*$'
              responses:
                '200':
                  description: The note as the server read it
                  content:
                    text/plain:
                      schema:
                        type: string
                '400':
                  description: Not a note
        """;

    // The handler answers the text it was given, so a test sees what the server read
    private const string DomainSource = """
        using Notes.Generated.Handlers;
        using Notes.Generated.Parameters;
        using Notes.Generated.Results;

        namespace Notes.RuntimeTest;

        public sealed class CreateNoteHandler : ICreateNoteHandler
        {
            public Task<CreateNoteResult> ExecuteAsync(CreateNoteParameters parameters, CancellationToken cancellationToken = default)
            {
                string note = parameters.Request;
                return Task.FromResult(CreateNoteResult.Ok(note));
            }
        }
        """;

    [Theory]
    [InlineData("Grüße ✓", "text/plain; charset=utf-8")]
    [InlineData("Grüße", "text/plain; charset=iso-8859-1")]
    [InlineData("plain ascii", "text/plain")]
    public async Task Server_TheText_ReachesTheHandlerAsSent_ByItsCharset(
        string text,
        string contentType)
    {
        await using var host = await StartHostAsync();
        var mediaType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(contentType);
        var encoding = mediaType.CharSet is { } charSet ? Encoding.GetEncoding(charSet) : Encoding.UTF8;
        using var body = new ByteArrayContent(encoding.GetBytes(text));
        body.Headers.ContentType = mediaType;

        using var response = await host.Client.PostAsync(new Uri("/notes", UriKind.Relative), body, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(text, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("a", "minimum length")]
    [InlineData("a note that is far too long", "maximum length")]
    [InlineData("no # here", "regular expression")]
    [InlineData("", "required")]
    public async Task Server_TheSchemasRules_AreA400(
        string text,
        string expected)
    {
        await using var host = await StartHostAsync();
        using var body = new StringContent(text, Encoding.UTF8, "text/plain");

        using var response = await host.Client.PostAsync(new Uri("/notes", UriKind.Relative), body, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains(expected, problem, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("application/json")]
    [InlineData("text/plain; charset=x-no-such-charset")]
    public async Task Server_AnotherMediaTypeOrAnUnknownCharset_IsA415(
        string contentType)
    {
        await using var host = await StartHostAsync();
        using var body = new ByteArrayContent(Encoding.UTF8.GetBytes("\"a note\""));
        body.Headers.TryAddWithoutValidation("Content-Type", contentType);

        using var response = await host.Client.PostAsync(new Uri("/notes", UriKind.Relative), body, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    private const string ClientProbeSource = """"
        using System.Net;
        using System.Text;
        using Microsoft.Extensions.DependencyInjection;

        namespace Notes.RuntimeTest;

        public static class TextProbe
        {
            public static async Task<string> RunAsync(bool typedClient, string text)
            {
                var assembly = typeof(TextProbe).Assembly;
                var handler = new StubHandler();
                if (typedClient)
                {
                    var clientType = assembly.GetTypes().Single(t => t.IsClass && !t.IsAbstract && t.GetMethod("CreateNoteAsync") is not null);
                    var client = Activator.CreateInstance(clientType, new HttpClient(handler) { BaseAddress = new Uri("http://stub/") })!;
                    var method = clientType.GetMethod("CreateNoteAsync")!;
                    var parameters = Activator.CreateInstance(method.GetParameters()[0].ParameterType, text);
                    await (Task)method.Invoke(client, [parameters, CancellationToken.None])!;
                }
                else
                {
                    var httpClientName = (string)assembly.GetTypes().Single(t => t.Name == "Constants").GetField("HttpClientName")!.GetValue(null)!;
                    var services = new ServiceCollection();
                    services
                        .AddHttpClient(httpClientName, c => c.BaseAddress = new Uri("http://stub/"))
                        .ConfigurePrimaryHttpMessageHandler(() => handler);
                    assembly.GetTypes().SelectMany(t => t.GetMethods()).Single(m => m.Name == "AddNotesEndpoints").Invoke(null, [services, null]);
                    using var provider = services.BuildServiceProvider();
                    var endpointType = assembly.GetTypes().Single(t => t.Name == "ICreateNoteEndpoint");
                    var parameters = Activator.CreateInstance(assembly.GetTypes().Single(t => t.Name == "CreateNoteParameters"), text);
                    var endpoint = provider.GetRequiredService(endpointType);
                    await (Task)endpointType.GetMethod("ExecuteAsync")!.Invoke(endpoint, [parameters, httpClientName, CancellationToken.None])!;
                }

                return handler.Wire!;
            }

            private sealed class StubHandler : HttpMessageHandler
            {
                public string? Wire { get; private set; }

                protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    var bytes = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken);
                    Wire = $"{request.Content?.Headers.ContentType} [{Encoding.UTF8.GetString(bytes)}]";
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok", Encoding.UTF8, "text/plain") };
                }
            }
        }
        """";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Clients_SendTheText_AsUtf8WithTheDeclaredMediaType(
        bool typedClient)
    {
        var marker = typedClient
            ? """{ "generationMode": "TypedClient" }"""
            : ErrorResponseBodyRuntimeTests.ClientOperationMarker("ProblemDetails");
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", marker, includeMinimalApi: false, Yaml);
        sources.Add(("TextProbe.cs", ClientProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);

        var wire = await (Task<string>)assembly.GetType("Notes.RuntimeTest.TextProbe", throwOnError: true)!.GetMethod("RunAsync")!.Invoke(null, [typedClient, "Grüße ✓"])!;

        Assert.Equal("text/plain; charset=utf-8 [Grüße ✓]", wire);
    }

    private static async Task<RunningHost> StartHostAsync()
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", Marker, includeMinimalApi: true, Yaml);
        sources.Add(("RuntimeTestDomain.cs", DomainSource));

        var assembly = CompilationVerificationHarness.EmitAndLoad(sources, includeMinimalApi: true);
        var serviceExtensions = assembly.GetType("Notes.Generated.Extensions.UnifiedServiceCollectionExtensions", throwOnError: true)!;
        var appExtensions = assembly.GetType("Notes.Generated.Extensions.UnifiedWebApplicationExtensions", throwOnError: true)!;

        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddScoped(
            assembly.GetType("Notes.Generated.Handlers.ICreateNoteHandler", throwOnError: true)!,
            assembly.GetType("Notes.RuntimeTest.CreateNoteHandler", throwOnError: true)!);

        serviceExtensions
            .GetMethod("AddNotesApi")!
            .Invoke(null, [builder.Services, null]);

        var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");

        appExtensions
            .GetMethod("MapNotesApi")!
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