namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// What the typed client makes of a success without the declared body: a 204, an empty 200, a JSON <c>null</c>, and
/// (the Result style, which promises not to throw) a 200 whose body is no JSON, such as a proxy's HTML page.
/// </summary>
public sealed class TypedClientEmptyBodyTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /pets/{id}:
            get:
              operationId: getPet
              parameters:
                - name: id
                  in: path
                  required: true
                  schema: { type: integer }
              responses:
                '200':
                  description: The pet
                  content:
                    application/json:
                      schema: { $ref: '#/components/schemas/Pet' }
                '204':
                  description: No pet
                '404':
                  description: Not found
        components:
          schemas:
            Pet:
              type: object
              required: [name]
              properties:
                name: { type: string }
        """;

    /// <summary>
    /// Calls getPet against a stub answering with the given status, body and media type; returns the outcome:
    /// "value:{name}", "null", "succeeded:{status}" (Result style), or "throws:{exception type}".
    /// </summary>
    private const string ProbeSource = """"
        using System.Net;

        namespace ErrorBodies.RuntimeTest;

        public static class EmptyBodyProbe
        {
            public static async Task<string> RunAsync(int status, string? body, string? mediaType)
            {
                var assembly = typeof(EmptyBodyProbe).Assembly;
                var ctor = assembly.GetTypes().Single(t => t.Name == "GetPetParameters").GetConstructors().Single();
                var parameters = ctor.Invoke([Convert.ChangeType(1, ctor.GetParameters()[0].ParameterType, System.Globalization.CultureInfo.InvariantCulture)]);
                var clientType = assembly.GetTypes().Single(t => t.IsClass && !t.IsAbstract && t.GetMethod("GetPetAsync") is not null);
                var client = Activator.CreateInstance(clientType, new HttpClient(new StubHandler(status, body, mediaType)) { BaseAddress = new Uri("http://stub/") })!;
                try
                {
                    var task = (Task)clientType.GetMethod("GetPetAsync")!.Invoke(client, [parameters, CancellationToken.None])!;
                    await task;
                    var result = task.GetType().GetProperty("Result")!.GetValue(task);
                    if (result is null)
                    {
                        return "null";
                    }

                    if (result.GetType().GetProperty("IsSuccess") is { } isSuccess)
                    {
                        return $"succeeded:{isSuccess.GetValue(result)}";
                    }

                    return "value:" + result.GetType().GetProperty("Name")!.GetValue(result);
                }
                catch (Exception ex)
                {
                    return "throws:" + (ex is System.Reflection.TargetInvocationException { InnerException: { } inner } ? inner : ex).GetType().Name;
                }
            }

            private sealed class StubHandler(int status, string? body, string? mediaType) : HttpMessageHandler
            {
                protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    var response = new HttpResponseMessage((HttpStatusCode)status);
                    if (body is not null)
                    {
                        response.Content = new StringContent(body, System.Text.Encoding.UTF8, mediaType ?? "application/json");
                    }

                    return Task.FromResult(response);
                }
            }
        }
        """";

    [Theory]
    [InlineData(200, """{"name":"Rex"}""", "application/json", "value:Rex")]
    [InlineData(204, null, null, "null")]
    [InlineData(200, "", "application/json", "null")]
    [InlineData(200, "null", "application/json", "null")]
    [InlineData(200, "<html>proxy</html>", "text/html", "throws:JsonException")]
    public async Task ThrowStyle_ASuccessWithoutTheBody_IsNull(
        int status,
        string? body,
        string? mediaType,
        string expected)
        => Assert.Equal(expected, await RunAsync("Throw", status, body, mediaType));

    [Theory]
    [InlineData(200, """{"name":"Rex"}""", "application/json", "succeeded:True")]
    [InlineData(204, null, null, "succeeded:True")]
    [InlineData(200, "", "application/json", "succeeded:True")]
    [InlineData(200, "<html>proxy</html>", "text/html", "succeeded:False")]
    public async Task ResultStyle_NeverThrows(
        int status,
        string? body,
        string? mediaType,
        string expected)
        => Assert.Equal(expected, await RunAsync("Result", status, body, mediaType));

    /// <summary>
    /// An operation that also declares a success without a body (a 204 beside the 200 - Standard mode; Strict reports
    /// ATC_API_OPR018) says so in its type: the model is nullable.
    /// </summary>
    [Fact]
    public void ThrowStyle_AnOperationWithABodylessSuccess_ReturnsANullableModel()
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(
            new ApiClientGenerator(),
            ".atc-rest-api-client",
            """{ "generationMode": "TypedClient", "validateSpecificationStrategy": "Standard" }""",
            includeMinimalApi: false,
            Yaml);

        var client = Assert.Single(sources, s => s.Source.Contains("GetPetAsync(", StringComparison.Ordinal) && s.Source.Contains("class ", StringComparison.Ordinal)).Source;
        Assert.Contains("Task<Pet?> GetPetAsync(", client, StringComparison.Ordinal);
    }

    private static Task<string> RunAsync(
        string resultStyle,
        int status,
        string? body,
        string? mediaType)
    {
        var marker = $$"""{ "generationMode": "TypedClient", "typedClientResultStyle": "{{resultStyle}}", "validateSpecificationStrategy": "Standard" }""";
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", marker, includeMinimalApi: false, Yaml);
        sources.Add(("EmptyBodyProbe.cs", ProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);

        return (Task<string>)assembly.GetType("ErrorBodies.RuntimeTest.EmptyBodyProbe", throwOnError: true)!.GetMethod("RunAsync")!.Invoke(null, [status, body, mediaType])!;
    }
}