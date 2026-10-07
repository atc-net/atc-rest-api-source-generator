namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// The typed client does not take whatever a server or a proxy sends: an error body (a 10 MB HTML page from a proxy)
/// is cut to a few kilobytes in the exception message, and a downloaded file's name is a file name - a
/// <c>Content-Disposition</c> of <c>../../evil.txt</c> or <c>..\..\evil.txt</c> gives <c>evil.txt</c>, never a path a
/// consumer could write outside its folder with.
/// </summary>
public sealed class TypedClientBoundsTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /files/{id}:
            get:
              operationId: getFile
              parameters:
                - name: id
                  in: path
                  required: true
                  schema: { type: string }
              responses:
                '200':
                  description: The file
                  content:
                    application/octet-stream:
                      schema: { type: string, format: binary }
        """;

    private const string ProbeSource = """"
        using System.Net;
        using System.Net.Http.Headers;

        namespace ErrorBodies.RuntimeTest;

        public static class BoundsProbe
        {
            public static async Task<string> RunAsync(int status, string? fileName, int bodyLength)
            {
                var assembly = typeof(BoundsProbe).Assembly;
                var parameters = Activator.CreateInstance(assembly.GetTypes().Single(t => t.Name == "GetFileParameters"), "1")!;
                var clientType = assembly.GetTypes().Single(t => t.IsClass && !t.IsAbstract && t.GetMethod("GetFileAsync") is not null);
                var client = Activator.CreateInstance(clientType, new HttpClient(new StubHandler(status, fileName, bodyLength)) { BaseAddress = new Uri("http://stub/") })!;
                try
                {
                    var task = (Task)clientType.GetMethod("GetFileAsync")!.Invoke(client, [parameters, CancellationToken.None])!;
                    await task;
                    var file = task.GetType().GetProperty("Result")!.GetValue(task)!;
                    return "file:" + file.GetType().GetProperty("FileName")!.GetValue(file);
                }
                catch (HttpRequestException ex)
                {
                    return "message:" + ex.Message.Length + (ex.Message.EndsWith('…') ? ":truncated" : string.Empty);
                }
            }

            private sealed class StubHandler(int status, string? fileName, int bodyLength) : HttpMessageHandler
            {
                protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    var content = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(new string('x', bodyLength)));
                    content.Headers.ContentType = new MediaTypeHeaderValue(status == 200 ? "application/octet-stream" : "text/html");
                    if (fileName is not null)
                    {
                        content.Headers.TryAddWithoutValidation("Content-Disposition", $"attachment; filename=\"{fileName}\"");
                    }

                    return Task.FromResult(new HttpResponseMessage((HttpStatusCode)status) { Content = content });
                }
            }
        }
        """";

    [Theory]
    [InlineData("report.pdf", "file:report.pdf")]
    [InlineData("../../evil.txt", "file:evil.txt")]
    [InlineData("..\\..\\evil.txt", "file:evil.txt")]
    [InlineData("/etc/passwd", "file:passwd")]
    [InlineData("..", "file:")]
    public async Task ADownloadedFilesName_IsAFileName_NeverAPath(
        string fileName,
        string expected)
        => Assert.Equal(expected, await RunAsync(200, fileName, 10));

    [Fact]
    public async Task AnErrorBody_IsCutInTheExceptionMessage()
    {
        var outcome = await RunAsync(502, null, 10 * 1024 * 1024);

        Assert.EndsWith(":truncated", outcome, StringComparison.Ordinal);
        var length = int.Parse(outcome.Split(':')[1], CultureInfo.InvariantCulture);
        Assert.InRange(length, 1, 5000);
    }

    private static async Task<string> RunAsync(
        int status,
        string? fileName,
        int bodyLength)
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", """{ "generationMode": "TypedClient" }""", includeMinimalApi: false, Yaml);
        sources.Add(("BoundsProbe.cs", ProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);

        return await (Task<string>)assembly.GetType("ErrorBodies.RuntimeTest.BoundsProbe", throwOnError: true)!.GetMethod("RunAsync")!.Invoke(null, [status, fileName, bodyLength])!;
    }
}