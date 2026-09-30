namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A raw file upload - a request body of any binary media type (PDF, PNG, Excel, octet-stream), not only
/// <c>application/octet-stream</c> - is sent and bound as the raw body with its media type on every side: the server
/// binds a <c>Stream</c> (<c>[FromBody]</c>, <c>.Accepts&lt;Stream&gt;(type)</c>), and both C# clients send the file's
/// bytes with its content type.
/// </summary>
public sealed class BodyUploadGenerationTests
{
    internal const string Yaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /uploads/pdf:
            post:
              operationId: uploadPdf
              requestBody:
                required: true
                content:
                  application/pdf:
                    schema:
                      type: string
                      format: binary
              responses:
                '204':
                  description: Stored
          /uploads/png:
            post:
              operationId: uploadPng
              requestBody:
                required: true
                content:
                  image/png:
                    schema:
                      type: string
                      format: binary
              responses:
                '204':
                  description: Stored
          /uploads/excel:
            post:
              operationId: uploadExcel
              requestBody:
                required: true
                content:
                  application/vnd.openxmlformats-officedocument.spreadsheetml.sheet:
                    schema:
                      type: string
                      format: binary
              responses:
                '204':
                  description: Stored
        """;

    /// <summary>
    /// Sends each upload through a generated client against a stub that records what reached the wire: the request
    /// Content-Type and the body bytes. The client and its operations are found by reflection (either mode).
    /// </summary>
    private const string UploadProbeSource = """"
        using System.Net;
        using Atc.Rest.Client;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public static class UploadProbe
        {
            private static readonly List<string> Wire = [];

            public static async Task<List<string>> RunAsync(bool typedClient)
            {
                var assembly = typeof(UploadProbe).Assembly;
                var uploads = new (string Operation, string ContentType, byte[] Bytes)[]
                {
                    ("UploadPdf", "application/pdf", [0x25, 0x50, 0x44, 0x46]),
                    ("UploadPng", "image/png", [0x89, 0x50, 0x4E, 0x47]),
                    ("UploadExcel", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", [0x50, 0x4B, 0x03, 0x04]),
                };

                foreach (var (operation, contentType, bytes) in uploads)
                {
                    var file = new ProbeFile(bytes, operation + ".bin", contentType);
                    if (typedClient)
                    {
                        var clientType = assembly.GetTypes().Single(t => t.IsClass && !t.IsAbstract && t.GetMethod(operation + "Async") is not null);
                        var client = Activator.CreateInstance(clientType, new HttpClient(new StubHandler()) { BaseAddress = new Uri("http://stub/") })!;
                        var method = clientType.GetMethod(operation + "Async")!;
                        var parameters = Activator.CreateInstance(method.GetParameters()[0].ParameterType, file);
                        await (Task)method.Invoke(client, [parameters, CancellationToken.None])!;
                    }
                    else
                    {
                        var httpClientName = (string)assembly.GetTypes().Single(t => t.Name == "Constants").GetField("HttpClientName")!.GetValue(null)!;
                        var services = new ServiceCollection();
                        services
                            .AddHttpClient(httpClientName, c => c.BaseAddress = new Uri("http://stub/"))
                            .ConfigurePrimaryHttpMessageHandler(() => new StubHandler());
                        assembly.GetTypes().SelectMany(t => t.GetMethods()).Single(m => m.Name == "AddErrorBodiesEndpoints").Invoke(null, [services]);
                        using var provider = services.BuildServiceProvider();
                        var endpointType = assembly.GetTypes().Single(t => t.Name == $"I{operation}Endpoint");
                        var parameters = Activator.CreateInstance(assembly.GetTypes().Single(t => t.Name == $"{operation}Parameters"), file);
                        var endpoint = provider.GetRequiredService(endpointType);
                        await (Task)endpointType.GetMethod("ExecuteAsync")!.Invoke(endpoint, [parameters, httpClientName, CancellationToken.None])!;
                    }
                }

                return Wire;
            }

            private sealed class ProbeFile(byte[] bytes, string fileName, string contentType) : IFileContent
            {
                public string FileName => fileName;

                public string? ContentType => contentType;

                public Stream OpenReadStream() => new MemoryStream(bytes);
            }

            private sealed class StubHandler : HttpMessageHandler
            {
                protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    var body = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken);
                    Wire.Add($"{request.RequestUri!.AbsolutePath} {request.Content?.Headers.ContentType?.MediaType} {Convert.ToHexString(body)}");
                    return new HttpResponseMessage(HttpStatusCode.NoContent);
                }
            }
        }
        """";

    private static readonly string[] ExpectedWire =
    [
        "/uploads/pdf application/pdf 25504446",
        "/uploads/png image/png 89504E47",
        "/uploads/excel application/vnd.openxmlformats-officedocument.spreadsheetml.sheet 504B0304",
    ];

    [Fact]
    public void Server_RawUploads_BindTheBodyAsAStream()
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", ErrorResponseBodyRuntimeTests.ServerMarker, includeMinimalApi: true, Yaml);
        var all = string.Join(Environment.NewLine, sources.Select(s => s.Source));

        foreach (var mediaType in (string[])["application/pdf", "image/png", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"])
        {
            Assert.Contains($".Accepts<Stream>(\"{mediaType}\")", all, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("IFormFile", all, StringComparison.Ordinal);
        Assert.Empty(CompilationVerificationHarness.CompileGeneratedSources(sources, includeMinimalApi: true));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Clients_RawUploads_SendTheFileWithItsMediaType(
        bool typedClient)
    {
        var marker = typedClient
            ? """{ "generationMode": "TypedClient" }"""
            : ErrorResponseBodyRuntimeTests.ClientOperationMarker("ProblemDetails");
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", marker, includeMinimalApi: false, Yaml);
        sources.Add(("UploadProbe.cs", UploadProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);
        var wire = await (Task<List<string>>)assembly.GetType("ErrorBodies.RuntimeTest.UploadProbe", throwOnError: true)!.GetMethod("RunAsync")!.Invoke(null, [typedClient])!;

        Assert.Equal(ExpectedWire, wire, StringComparer.Ordinal);
    }
}