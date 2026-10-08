namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A generated server on Kestrel (random port) with test handlers: the specification is generated, the handler source
/// (which sees every generated namespace) is compiled with it, and the registration type's <c>Register(IServiceCollection)</c>
/// adds the handlers before the generated <c>Add/Map{Project}Api</c>; <c>configure</c> adds middleware before the endpoints.
/// </summary>
internal sealed class XmlTestHost : IAsyncDisposable
{
    private readonly WebApplication app;
    private readonly HttpClient client;

    private XmlTestHost(
        WebApplication app,
        Assembly assembly)
    {
        this.app = app;
        Assembly = assembly;
        BaseAddress = new Uri(app.Urls.First());
        client = new HttpClient { BaseAddress = BaseAddress };
    }

    public Uri BaseAddress { get; }

    /// <summary>
    /// The compiled server (generated code and the test handlers), for a test that reads a handler's static state.
    /// </summary>
    public Assembly Assembly { get; }

    /// <summary>
    /// Generates, compiles and starts the server - through the generated <c>Add/Map{Project}Api</c>, or for the plain
    /// host (<paramref name="plainHost"/>: no <c>Atc.Rest.MinimalApi</c>) through <c>MapEndpoints()</c> with
    /// <c>AddProblemDetails</c>, <c>UseExceptionHandler</c> and <c>UseStatusCodePages</c> written by hand. With
    /// <paramref name="useRequestDelegateGenerator"/> the endpoints run through RDG's request delegates, as in a Native
    /// AOT build.
    /// </summary>
    public static async Task<XmlTestHost> StartAsync(
        string yaml,
        string handlerSource,
        string registrationTypeName,
        Action<WebApplication>? configure = null,
        Action<IServiceCollection>? configureServices = null,
        bool plainHost = false,
        string? serverMarker = null,
        Action<WebApplication, Assembly>? configureWithAssembly = null,
        bool useRequestDelegateGenerator = false)
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(
            new ApiServerGenerator(),
            ".atc-rest-api-server",
            serverMarker ?? (plainHost ? "{}" : ErrorResponseBodyRuntimeTests.ServerMarker),
            includeMinimalApi: !plainHost,
            yaml);

        // The handlers see every generated namespace
        var usings = sources
            .Select(s => Regex.Match(s.Source, @"^namespace (?<ns>[\w.]+);", RegexOptions.Multiline | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1)).Groups["ns"].Value)
            .Where(ns => ns.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Select(ns => $"global using {ns};");
        sources.Add(("XmlRequestHandlers.cs", string.Join(Environment.NewLine, usings) + Environment.NewLine + handlerSource));

        var assembly = useRequestDelegateGenerator
            ? AotAnalyzerHarness.EmitAndLoadWithRequestDelegateGenerator(sources, includeMinimalApi: !plainHost)
            : CompilationVerificationHarness.EmitAndLoad(sources, includeMinimalApi: !plainHost);
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddEndpointsApiExplorer();
        configureServices?.Invoke(builder.Services);
        assembly.GetType(registrationTypeName, throwOnError: true)!.GetMethod("Register")!.Invoke(null, [builder.Services]);
        if (plainHost)
        {
            builder.Services.AddProblemDetails();
        }
        else
        {
            assembly.GetType("ErrorBodies.Generated.Extensions.UnifiedServiceCollectionExtensions", throwOnError: true)!.GetMethod("AddErrorBodiesApi")!.Invoke(null, [builder.Services, null]);
        }

        var webApp = builder.Build();
        webApp.Urls.Add("http://127.0.0.1:0");
        if (plainHost && !webApp.Environment.IsDevelopment())
        {
            webApp.UseExceptionHandler();
        }

        configure?.Invoke(webApp);
        configureWithAssembly?.Invoke(webApp, assembly);
        if (plainHost)
        {
            webApp.UseStatusCodePages();
            assembly.GetType("ErrorBodies.Generated.Endpoints.EndpointMappingExtensions", throwOnError: true)!.GetMethod("MapEndpoints")!.Invoke(null, [webApp]);
        }
        else
        {
            assembly.GetType("ErrorBodies.Generated.Extensions.UnifiedWebApplicationExtensions", throwOnError: true)!.GetMethod("MapErrorBodiesApi")!.Invoke(null, [webApp, null]);
        }

        await webApp.StartAsync(TestContext.Current.CancellationToken);
        return new XmlTestHost(webApp, assembly);
    }

    public async Task<string> PostAsync(
        string path,
        string contentType,
        string body,
        string? accept)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new ByteArrayContent(Encoding.UTF8.GetBytes(body)),
        };
        request.Content.Headers.TryAddWithoutValidation("Content-Type", contentType);
        if (accept is not null)
        {
            request.Headers.TryAddWithoutValidation("Accept", accept);
        }

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var mediaType = response.Content.Headers.ContentType?.ToString();
        if (mediaType?.StartsWith("application/problem+json", StringComparison.Ordinal) == true)
        {
            var problem = JsonNode.Parse(text)!.AsObject();
            return problem["errors"] is JsonObject errors
                ? $"{(int)response.StatusCode} {mediaType} errors={string.Join(",", errors.Select(e => e.Key))}"
                : $"{(int)response.StatusCode} {mediaType} title={problem["title"]}";
        }

        return $"{(int)response.StatusCode} {mediaType} {text}";
    }

    /// <summary>
    /// Sends a request and returns the status, the response media type (with its parameters) and the body.
    /// </summary>
    public async Task<(int Status, string? MediaType, string Body)> SendRawAsync(
        HttpMethod method,
        string path,
        string? accept,
        string? contentType = null,
        string? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(body));
            request.Content.Headers.TryAddWithoutValidation("Content-Type", contentType);
        }

        if (accept is not null)
        {
            request.Headers.TryAddWithoutValidation("Accept", accept);
        }

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        return ((int)response.StatusCode, response.Content.Headers.ContentType?.ToString(), await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// The success and error responses the endpoint metadata describes to ApiExplorer (what the runtime OpenAPI
    /// document is built from): <c>GET reports/{id}: 200 Report [application/json, application/pdf]</c>.
    /// </summary>
    public IReadOnlyList<string> DescribeResponses()
        => app.Services.GetRequiredService<IApiDescriptionGroupCollectionProvider>().ApiDescriptionGroups.Items
            .SelectMany(g => g.Items)
            .SelectMany(d => d.SupportedResponseTypes.Select(r => $"{d.HttpMethod} {d.RelativePath}: {r.StatusCode} {r.Type?.Name} [{string.Join(", ", r.ApiResponseFormats.Select(f => f.MediaType))}]"))
            .ToList();

    /// <summary>
    /// The parameters the endpoint metadata describes to ApiExplorer, sorted:
    /// <c>GET items: sort query String required=False default=name</c>.
    /// </summary>
    public IReadOnlyList<string> DescribeParameters()
        => app.Services.GetRequiredService<IApiDescriptionGroupCollectionProvider>().ApiDescriptionGroups.Items
            .SelectMany(g => g.Items)
            .SelectMany(d => d.ParameterDescriptions.Select(p =>
                $"{d.HttpMethod} {d.RelativePath}: {p.Name} {p.Source.Id.ToLowerInvariant()} {p.Type?.Name} required={p.IsRequired} default={(p.DefaultValue is null or DBNull ? "<none>" : p.DefaultValue)}"))
            .Order(StringComparer.Ordinal)
            .ToList();

    public ValueTask DisposeAsync()
    {
        client.Dispose();
        return app.DisposeAsync();
    }
}