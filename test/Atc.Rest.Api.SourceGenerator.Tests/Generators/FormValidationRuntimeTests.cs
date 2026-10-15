namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A form endpoint validates its request as a JSON body of the same model does, hosted on a real Kestrel server: the
/// same 400 with the same keys, and a registered validator for the model runs (Atc.Rest.MinimalApi's
/// <c>RequestValidation</c>, called after the endpoint builds the parameters).
/// </summary>
public class FormValidationRuntimeTests
{
    private const string Marker = """
        {
          "useMinimalApiPackage": "Enabled",
          "useValidationFilter": "Enabled",
          "useGlobalErrorHandler": "Enabled"
        }
        """;

    private const string Yaml = """
        openapi: 3.0.0
        info:
          title: Items
          version: 1.0.0
        paths:
          /items/form:
            post:
              operationId: createItemFromForm
              requestBody:
                required: true
                content:
                  multipart/form-data:
                    schema:
                      $ref: '#/components/schemas/Item'
              responses:
                '200':
                  description: OK
                '400':
                  description: Bad request
          /items/json:
            post:
              operationId: createItemFromJson
              requestBody:
                required: true
                content:
                  application/json:
                    schema:
                      $ref: '#/components/schemas/Item'
              responses:
                '200':
                  description: OK
                '400':
                  description: Bad request
        components:
          schemas:
            Item:
              type: object
              required: [itemName, quantity]
              properties:
                itemName:
                  type: string
                  minLength: 3
                quantity:
                  type: integer
                  format: int32
                  minimum: 1
        """;

    /// <summary>
    /// Stand-ins for the domain project: two handlers, and a validator for the model a JSON body and a form share.
    /// </summary>
    private const string DomainSource = """
        using FluentValidation;
        using Items.Generated.Handlers;
        using Items.Generated.Models;
        using Items.Generated.Parameters;
        using Items.Generated.Results;

        namespace Items.RuntimeTest;

        public sealed class CreateItemFromFormHandler : ICreateItemFromFormHandler
        {
            public Task<CreateItemFromFormResult> ExecuteAsync(CreateItemFromFormParameters parameters, CancellationToken cancellationToken = default)
                => Task.FromResult(CreateItemFromFormResult.Ok());
        }

        public sealed class CreateItemFromJsonHandler : ICreateItemFromJsonHandler
        {
            public Task<CreateItemFromJsonResult> ExecuteAsync(CreateItemFromJsonParameters parameters, CancellationToken cancellationToken = default)
                => Task.FromResult(CreateItemFromJsonResult.Ok());
        }

        public sealed class ItemValidator : AbstractValidator<Item>
        {
            public ItemValidator()
                => RuleFor(x => x.ItemName).NotEqual("forbidden").WithMessage("That name is taken.");
        }
        """;

    [Theory]
    [InlineData("ab", "0")]
    [InlineData("forbidden", "2")]
    [InlineData("ok-name", "2")]
    public async Task AFormAndAJsonBodyOfTheSameModel_AreValidatedAlike(
        string itemName,
        string quantity)
    {
        await using var host = await StartHostAsync();

        using var itemNameField = new StringContent(itemName);
        using var quantityField = new StringContent(quantity);
        using var form = new MultipartFormDataContent
        {
            { itemNameField, "itemName" },
            { quantityField, "quantity" },
        };
        using var formResponse = await host.Client.PostAsync(new Uri("/items/form", UriKind.Relative), form, TestContext.Current.CancellationToken);

        using var json = new StringContent($$"""{"itemName":"{{itemName}}","quantity":{{quantity}}}""", Encoding.UTF8, "application/json");
        using var jsonResponse = await host.Client.PostAsync(new Uri("/items/json", UriKind.Relative), json, TestContext.Current.CancellationToken);

        Assert.Equal(jsonResponse.StatusCode, formResponse.StatusCode);
        Assert.Equal(await ErrorsAsync(jsonResponse), await ErrorsAsync(formResponse));

        if (itemName == "ok-name")
        {
            Assert.Equal(HttpStatusCode.OK, formResponse.StatusCode);
        }
        else
        {
            Assert.Equal(HttpStatusCode.BadRequest, formResponse.StatusCode);
            Assert.Contains("itemName", await ErrorsAsync(formResponse), StringComparison.Ordinal);
        }

        if (itemName == "forbidden")
        {
            // The registered validator for the model ran for the form too
            Assert.Contains("That name is taken.", await ErrorsAsync(formResponse), StringComparison.Ordinal);
        }
    }

    // The problem's errors, keys and messages in a stable order ("" when there are none)
    private static async Task<string> ErrorsAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        if (string.IsNullOrEmpty(body))
        {
            return string.Empty;
        }

        using var document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("errors", out var errors))
        {
            return string.Empty;
        }

        return string.Join(
            "; ",
            errors.EnumerateObject()
                .OrderBy(e => e.Name, StringComparer.Ordinal)
                .Select(e => $"{e.Name}: {string.Join(" | ", e.Value.EnumerateArray().Select(m => m.GetString()))}"));
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

        foreach (var name in new[] { "CreateItemFromForm", "CreateItemFromJson" })
        {
            builder.Services.AddScoped(
                assembly.GetType($"Items.Generated.Handlers.I{name}Handler", throwOnError: true)!,
                assembly.GetType($"Items.RuntimeTest.{name}Handler", throwOnError: true)!);
        }

        var model = assembly.GetType("Items.Generated.Models.Item", throwOnError: true)!;
        builder.Services.AddScoped(
            typeof(FluentValidation.IValidator<>).MakeGenericType(model),
            assembly.GetType("Items.RuntimeTest.ItemValidator", throwOnError: true)!);

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