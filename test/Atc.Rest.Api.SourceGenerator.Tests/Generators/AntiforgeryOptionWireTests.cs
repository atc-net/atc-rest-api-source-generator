namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A form endpoint skips antiforgery validation by default (an API's callers send a token in a header, which a
/// cross-site form cannot) - and validates it with <c>useAntiforgery: true</c>, for a host whose browser clients
/// authenticate with a cookie: a form posted without the antiforgery token is then rejected.
/// </summary>
[Collection(nameof(AntiforgeryOptionWireTests))]
[CollectionDefinition(nameof(AntiforgeryOptionWireTests), DisableParallelization = true)]
public sealed class AntiforgeryOptionWireTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /notes:
            post:
              operationId: createNote
              requestBody:
                required: true
                content:
                  application/x-www-form-urlencoded:
                    schema:
                      $ref: '#/components/schemas/NoteForm'
              responses:
                '204':
                  description: Created
          /files:
            post:
              operationId: uploadFile
              requestBody:
                required: true
                content:
                  multipart/form-data:
                    schema:
                      $ref: '#/components/schemas/UploadForm'
              responses:
                '204':
                  description: Uploaded
        components:
          schemas:
            NoteForm:
              type: object
              required: [text]
              properties:
                text: { type: string }
            UploadForm:
              type: object
              properties:
                file: { type: string, format: binary }
        """;

    private const string HandlerSource = """"
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public sealed class CreateNoteHandlerImpl : ICreateNoteHandler
        {
            public System.Threading.Tasks.Task<CreateNoteResult> ExecuteAsync(CreateNoteParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(CreateNoteResult.NoContent());
        }

        public sealed class UploadFileHandlerImpl : IUploadFileHandler
        {
            public System.Threading.Tasks.Task<UploadFileResult> ExecuteAsync(UploadFileParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(UploadFileResult.NoContent());
        }

        public static class NoteRegistration
        {
            public static void Register(IServiceCollection services)
            {
                services.AddScoped<ICreateNoteHandler, CreateNoteHandlerImpl>();
                services.AddScoped<IUploadFileHandler, UploadFileHandlerImpl>();
            }
        }
        """";

    [Theory]
    [InlineData(false, 204)]
    [InlineData(true, 400)]
    public async Task AFormPostedWithoutAToken_IsAcceptedByDefault_AndRejectedWithUseAntiforgery(
        bool useAntiforgery,
        int expectedStatus)
    {
        var marker = $$"""
            {
              "useMinimalApiPackage": "Enabled",
              "useValidationFilter": "Enabled",
              "useGlobalErrorHandler": "Enabled",
              "useAntiforgery": {{(useAntiforgery ? "true" : "false")}}
            }
            """;
        await using var host = await XmlTestHost.StartAsync(
            Yaml,
            HandlerSource,
            "ErrorBodies.RuntimeTest.NoteRegistration",
            configure: app => app.UseAntiforgery(),
            configureServices: services => services.AddAntiforgery(),
            serverMarker: marker);

        var (status, _, body) = await host.SendRawAsync(HttpMethod.Post, "/notes", accept: "application/json", body: "text=hello", contentType: "application/x-www-form-urlencoded");

        Assert.True(status == expectedStatus, $"{status}: {body}");

        // A file upload (bound by the framework, not field by field) the same way
        using var client = new HttpClient { BaseAddress = host.BaseAddress };
        using var upload = new MultipartFormDataContent { { new ByteArrayContent([1, 2, 3]), "file", "a.bin" } };
        using var uploaded = await client.PostAsync(new Uri("/files", UriKind.Relative), upload, TestContext.Current.CancellationToken);
        Assert.True((int)uploaded.StatusCode == expectedStatus, $"{(int)uploaded.StatusCode}: {await uploaded.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)}");
    }
}