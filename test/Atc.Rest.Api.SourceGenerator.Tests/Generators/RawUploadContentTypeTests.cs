namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A raw upload that declares several media types (or a wildcard) tells the handler which one arrived: the generated
/// parameters carry the request's <c>ContentType</c> next to the <c>Stream File</c>, and the endpoint accepts every
/// declared type (an undeclared one is a 415). A single-type upload keeps its signature.
/// </summary>
public sealed class RawUploadContentTypeTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /documents:
            post:
              operationId: uploadDocument
              requestBody:
                required: true
                content:
                  application/pdf: { schema: { type: string, format: binary } }
                  image/png: { schema: { type: string, format: binary } }
              responses:
                '200':
                  description: Received
                  content:
                    text/plain:
                      schema:
                        type: string
          /images:
            post:
              operationId: uploadImage
              requestBody:
                required: true
                content:
                  image/*: { schema: { type: string, format: binary } }
              responses:
                '200':
                  description: Received
                  content:
                    text/plain:
                      schema:
                        type: string
          /pdfs:
            post:
              operationId: uploadPdf
              requestBody:
                required: true
                content:
                  application/pdf: { schema: { type: string, format: binary } }
              responses:
                '200':
                  description: Received
                  content:
                    text/plain:
                      schema:
                        type: string
        """;

    private const string HandlerSource = """"
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public sealed class UploadDocumentHandlerImpl : IUploadDocumentHandler
        {
            public async System.Threading.Tasks.Task<UploadDocumentResult> ExecuteAsync(UploadDocumentParameters parameters, System.Threading.CancellationToken cancellationToken = default)
            {
                using var buffer = new System.IO.MemoryStream();
                await parameters.File.CopyToAsync(buffer, cancellationToken);
                return UploadDocumentResult.Ok($"{parameters.ContentType} {buffer.Length}");
            }
        }

        public sealed class UploadImageHandlerImpl : IUploadImageHandler
        {
            public System.Threading.Tasks.Task<UploadImageResult> ExecuteAsync(UploadImageParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(UploadImageResult.Ok(parameters.ContentType));
        }

        public sealed class UploadPdfHandlerImpl : IUploadPdfHandler
        {
            public System.Threading.Tasks.Task<UploadPdfResult> ExecuteAsync(UploadPdfParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(UploadPdfResult.Ok("pdf"));
        }

        public static class UploadRegistration
        {
            public static void Register(IServiceCollection services)
            {
                services.AddScoped<IUploadDocumentHandler, UploadDocumentHandlerImpl>();
                services.AddScoped<IUploadImageHandler, UploadImageHandlerImpl>();
                services.AddScoped<IUploadPdfHandler, UploadPdfHandlerImpl>();
            }
        }
        """";

    [Fact]
    public void Server_Parameters_CarryTheContentType_OnlyForSeveralTypesOrAWildcard()
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", ErrorResponseBodyRuntimeTests.ServerMarker, includeMinimalApi: true, Yaml);
        var parameters = string.Join(Environment.NewLine, sources.Where(s => s.HintName.Contains("Parameters", StringComparison.Ordinal)).Select(s => s.Source));

        Assert.Matches(new Regex(@"record UploadDocumentParameters\(\s*\[property: FromBody, Required\] Stream File,\s*\[property: FromHeader\(Name = ""Content-Type""\), Required, global::System\.ComponentModel\.DataAnnotations\.Display\(Name = ""Content-Type""\)\] string ContentType\);", RegexOptions.None, TimeSpan.FromSeconds(1)), parameters);
        Assert.Matches(new Regex(@"record UploadImageParameters\(\s*\[property: FromBody, Required\] Stream File,\s*\[property: FromHeader\(Name = ""Content-Type""\), Required, global::System\.ComponentModel\.DataAnnotations\.Display\(Name = ""Content-Type""\)\] string ContentType\);", RegexOptions.None, TimeSpan.FromSeconds(1)), parameters);
        Assert.Matches(new Regex(@"record UploadPdfParameters\(\s*\[property: FromBody, Required\] Stream File\);", RegexOptions.None, TimeSpan.FromSeconds(1)), parameters);
    }

    [Fact]
    public async Task LiveHost_TheHandlerSeesTheTypeThatArrived_AndAnUndeclaredTypeIs415()
    {
        await using var host = await XmlTestHost.StartAsync(Yaml, HandlerSource, "ErrorBodies.RuntimeTest.UploadRegistration");

        var pdf = await host.SendRawAsync(HttpMethod.Post, "/documents", accept: null, contentType: "application/pdf", body: "%PDF-1.7");
        var png = await host.SendRawAsync(HttpMethod.Post, "/documents", accept: null, contentType: "image/png", body: "PNG");
        var jpeg = await host.SendRawAsync(HttpMethod.Post, "/documents", accept: null, contentType: "image/jpeg", body: "JPEG");
        var gif = await host.SendRawAsync(HttpMethod.Post, "/images", accept: null, contentType: "image/gif", body: "GIF");

        Assert.Equal((200, "application/pdf 8"), (pdf.Status, pdf.Body));
        Assert.Equal((200, "image/png 3"), (png.Status, png.Body));
        Assert.Equal(415, jpeg.Status);
        Assert.Equal((200, "image/gif"), (gif.Status, gif.Body));
    }
}