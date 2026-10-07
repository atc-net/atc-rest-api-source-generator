namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// What the generated server binds and the generated typed client sends, on the wire, in the cases the hardening
/// audit found wrong: a list of numbers under a host culture that writes a comma (bound as 15, not 1.5); a form the
/// framework cannot read (a 500 where a 400 belongs); a file field named in snake_case (bound by the C# name, so
/// null); a typed client whose BaseAddress carries a path (dropped by a URL that starts with a slash).
/// </summary>
[Collection(nameof(BindingHardeningWireTests))]
[CollectionDefinition(nameof(BindingHardeningWireTests), DisableParallelization = true)]
public sealed class BindingHardeningWireTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /prices:
            get:
              operationId: listPrices
              parameters:
                - name: prices
                  in: query
                  schema:
                    type: array
                    items:
                      type: number
                      format: double
              responses:
                '200':
                  description: The prices, as bound
                  content:
                    application/json:
                      schema:
                        type: array
                        items:
                          type: number
                          format: double
          /profiles:
            post:
              operationId: createProfile
              requestBody:
                required: true
                content:
                  application/x-www-form-urlencoded:
                    schema:
                      $ref: '#/components/schemas/ProfileForm'
              responses:
                '201':
                  description: Created
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/ProfileForm'
          /uploads:
            post:
              operationId: uploadFile
              requestBody:
                required: true
                content:
                  multipart/form-data:
                    schema:
                      $ref: '#/components/schemas/UploadForm'
              responses:
                '200':
                  description: What the server received
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/UploadInfo'
        components:
          schemas:
            ProfileForm:
              type: object
              required: [name]
              properties:
                name:
                  type: string
                age:
                  type: integer
            UploadForm:
              type: object
              required: [upload_file]
              properties:
                upload_file:
                  type: string
                  format: binary
                note:
                  type: string
            UploadInfo:
              type: object
              required: [name, size]
              properties:
                name:
                  type: string
                size:
                  type: integer
                  format: int64
        """;

    /// <summary>
    /// Echoes what the server bound: the prices, the profile, the uploaded file's name and size.
    /// </summary>
    private const string HandlerSource = """"
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public sealed class ListPricesHandlerImpl : IListPricesHandler
        {
            public System.Threading.Tasks.Task<ListPricesResult> ExecuteAsync(ListPricesParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(ListPricesResult.Ok(parameters.Prices is null ? new System.Collections.Generic.List<double>() : new System.Collections.Generic.List<double>(parameters.Prices)));
        }

        public sealed class CreateProfileHandlerImpl : ICreateProfileHandler
        {
            public System.Threading.Tasks.Task<CreateProfileResult> ExecuteAsync(CreateProfileParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(CreateProfileResult.Created(parameters.Request));
        }

        public sealed class UploadFileHandlerImpl : IUploadFileHandler
        {
            public System.Threading.Tasks.Task<UploadFileResult> ExecuteAsync(UploadFileParameters parameters, System.Threading.CancellationToken cancellationToken = default)
            {
                var file = parameters.Request.UploadFile;
                return System.Threading.Tasks.Task.FromResult(UploadFileResult.Ok(new UploadInfo(file?.FileName ?? "(none)", file?.Length ?? 0)));
            }
        }

        public static class BindingRegistration
        {
            public static void Register(IServiceCollection services)
            {
                services.AddScoped<IListPricesHandler, ListPricesHandlerImpl>();
                services.AddScoped<ICreateProfileHandler, CreateProfileHandlerImpl>();
                services.AddScoped<IUploadFileHandler, UploadFileHandlerImpl>();
            }
        }
        """";

    private const string TypedMarker = """
        {
          "generationMode": "TypedClient",
          "validateSpecificationStrategy": "None"
        }
        """;

    /// <summary>
    /// A list of numbers is bound as the specification writes them, whatever culture the host runs under: under
    /// da-DK, <c>1.5</c> must not become <c>15</c>.
    /// </summary>
    [Fact]
    public async Task Server_BindsAListOfNumbers_UnderADanishHostCulture()
    {
        var culture = CultureInfo.DefaultThreadCurrentCulture;
        CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("da-DK");
        try
        {
            await using var host = await XmlTestHost.StartAsync(Yaml, HandlerSource, "ErrorBodies.RuntimeTest.BindingRegistration");
            var (status, _, body) = await host.SendRawAsync(HttpMethod.Get, "/prices?prices=1.5&prices=2.25", accept: "application/json");

            Assert.Equal(200, status);
            Assert.Equal("[1.5,2.25]", body);
        }
        finally
        {
            CultureInfo.DefaultThreadCurrentCulture = culture;
        }
    }

    /// <summary>
    /// A form the framework cannot read - a multipart body without a boundary, more fields than the limit - is the
    /// caller's mistake: a 400 (or a 413), never a 500.
    /// </summary>
    [Theory]
    [InlineData("/uploads", "multipart/form-data", "name=a")] // a multipart body without a boundary
    [InlineData("/profiles", "application/x-www-form-urlencoded", "too-many-fields")] // over the framework's 1024-field limit
    public async Task Server_AFormTheFrameworkCannotRead_IsA400NotA500(
        string path,
        string contentType,
        string shape)
    {
        await using var host = await XmlTestHost.StartAsync(Yaml, HandlerSource, "ErrorBodies.RuntimeTest.BindingRegistration");
        using var client = new HttpClient { BaseAddress = host.BaseAddress };
        var payload = shape == "too-many-fields"
            ? string.Join("&", Enumerable.Range(0, 1100).Select(i => $"f{i}=v"))
            : shape;
        using var content = new StringContent(payload);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);

        using var response = await client.PostAsync(new Uri(path, UriKind.Relative), content, TestContext.Current.CancellationToken);

        Assert.True(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.RequestEntityTooLarge,
            $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)}");
    }

    /// <summary>
    /// A file field is bound by the name the specification gives it (<c>upload_file</c>), not by the C# parameter
    /// name the generator derives from it.
    /// </summary>
    [Fact]
    public async Task Server_BindsAFileFieldByItsSpecificationName()
    {
        await using var host = await XmlTestHost.StartAsync(Yaml, HandlerSource, "ErrorBodies.RuntimeTest.BindingRegistration");
        using var client = new HttpClient { BaseAddress = host.BaseAddress };
        using var file = new ByteArrayContent("hello"u8.ToArray());
        using var note = new StringContent("a note");
        using var form = new MultipartFormDataContent
        {
            { file, "upload_file", "a.txt" },
            { note, "note" },
        };

        using var response = await client.PostAsync(new Uri("/uploads", UriKind.Relative), form, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("""{"name":"a.txt","size":5}""", body);
    }

    /// <summary>
    /// The typed client sends its requests under the path of the HttpClient's BaseAddress (a gateway, a sub-path):
    /// <c>https://host/svc-a/</c> plus <c>/prices</c> is <c>/svc-a/prices</c>, not <c>/prices</c>.
    /// </summary>
    [Fact]
    public async Task TypedClient_KeepsTheBaseAddressPath()
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", TypedMarker, includeMinimalApi: false, Yaml);
        sources.Add(("Probe.cs", """
            namespace ErrorBodies.RuntimeTest;

            public static class Probe
            {
                public static async System.Threading.Tasks.Task RunAsync(System.Net.Http.HttpClient http)
                    => await new ErrorBodies.Generated.Prices.Client.PricesClient(http).ListPricesAsync(new ErrorBodies.Generated.Prices.Client.ListPricesParameters(null));
            }
            """));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);
        var probe = assembly.GetType("ErrorBodies.RuntimeTest.Probe", throwOnError: true)!.GetMethod("RunAsync")!;

        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        string? seenPath = null;
        app.Map("/{**path}", (HttpContext context) =>
        {
            seenPath = context.Request.Path.Value;
            return Results.Json(Array.Empty<double>());
        });
        await app.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri(app.Urls.First().TrimEnd('/') + "/svc-a/") };
            await (Task)probe.Invoke(null, [http])!;

            Assert.Equal("/svc-a/prices", seenPath);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
            await app.DisposeAsync();
        }
    }
}