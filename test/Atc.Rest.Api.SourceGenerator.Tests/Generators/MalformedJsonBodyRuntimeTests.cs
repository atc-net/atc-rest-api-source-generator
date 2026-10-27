namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A JSON request body that is not well formed is a 400 for the body, on a real Kestrel server: not keyed under the last
/// member the reader got to (a body cut after <c>{"name":</c> is no error of <c>name</c>), while a value that does not
/// convert to its member's type is keyed under that member.
/// </summary>
public sealed class MalformedJsonBodyRuntimeTests
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
          title: Items
          version: 1.0.0
        paths:
          /items:
            post:
              operationId: createItem
              requestBody:
                required: true
                content:
                  application/json:
                    schema:
                      $ref: '#/components/schemas/Item'
              responses:
                '204':
                  description: Created
                '400':
                  description: Not an item
        components:
          schemas:
            Item:
              type: object
              required: [name, id]
              properties:
                name: { type: string }
                id: { type: string, format: uuid }
        """;

    private const string DomainSource = """
        using Items.Generated.Handlers;
        using Items.Generated.Parameters;
        using Items.Generated.Results;

        namespace Items.RuntimeTest;

        public sealed class CreateItemHandler : ICreateItemHandler
        {
            public Task<CreateItemResult> ExecuteAsync(CreateItemParameters parameters, CancellationToken cancellationToken = default)
                => Task.FromResult(CreateItemResult.NoContent());
        }
        """;

    [Theory]
    [InlineData("{\"name\":", "name")]
    [InlineData("{\"name\": \"a\", \"id\": ", "id")]
    [InlineData("{\"name\": \"a\" \"id\": 1}", "name")]
    public async Task AMalformedBody_IsA400ForTheBody_NotForTheLastMemberRead(
        string body,
        string lastMember)
    {
        await using var host = await StartHostAsync();
        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await host.Client.PostAsync(new Uri("/items", UriKind.Relative), content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain(lastMember, await ErrorKeysAsync(response), StringComparer.Ordinal);
    }

    [Fact]
    public async Task AValueThatDoesNotConvert_IsKeyedUnderItsMember()
    {
        await using var host = await StartHostAsync();
        using var content = new StringContent("{\"name\": \"a\", \"id\": \"not-a-uuid\"}", Encoding.UTF8, "application/json");

        using var response = await host.Client.PostAsync(new Uri("/items", UriKind.Relative), content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("id", await ErrorKeysAsync(response), StringComparer.Ordinal);
    }

    private static async Task<List<string>> ErrorKeysAsync(
        HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return document.RootElement.TryGetProperty("errors", out var errors)
            ? [.. errors.EnumerateObject().Select(e => e.Name)]
            : [];
    }

    private static async Task<RunningHost> StartHostAsync()
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", Marker, includeMinimalApi: true, Yaml);
        sources.Add(("RuntimeTestDomain.cs", DomainSource));

        var assembly = CompilationVerificationHarness.EmitAndLoad(sources, includeMinimalApi: true);
        var serviceExtensions = assembly.GetType("Items.Generated.Extensions.UnifiedServiceCollectionExtensions", throwOnError: true)!;
        var appExtensions = assembly.GetType("Items.Generated.Extensions.UnifiedWebApplicationExtensions", throwOnError: true)!;

        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddScoped(
            assembly.GetType("Items.Generated.Handlers.ICreateItemHandler", throwOnError: true)!,
            assembly.GetType("Items.RuntimeTest.CreateItemHandler", throwOnError: true)!);

        serviceExtensions
            .GetMethod("AddItemsApi")!
            .Invoke(null, [builder.Services, null]);

        var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");

        appExtensions
            .GetMethod("MapItemsApi")!
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